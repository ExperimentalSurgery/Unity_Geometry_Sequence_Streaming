using UnityEngine;
using System.IO;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using System.Diagnostics;
using UnityEngine.UI;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using System.Data;
using UnityEditor;

namespace GeometrySequence.Streaming
{
    public class GeometrySequenceStream : MonoBehaviour
    {
        public string pathToSequence { get; private set; }

        public int bufferSize = 12;
        public bool useAllThreads = true;
        public int threadCount = 4;

        public Material pointcloudMaterial;
        public Material meshMaterial;

        bool readerIsReady = false;
        [HideInInspector]
        public bool frameDropped = false;

        [HideInInspector]
        public int currentFrameIndex = 0;
        [HideInInspector] 
        public float targetFrameTimeMs = 0;
        [HideInInspector] 
        float elapsedMsSinceLastFrame = 0;
        [HideInInspector] 
        public float smoothedFPS = 0f;

        MeshTopology meshType = MeshTopology.Points;
        TextureMode textureMode = TextureMode.None;

        public BufferedGeometryReader bufferedReader;
        GameObject meshObject;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        [HideInInspector]
        public Texture2D texture;

        public enum PathType { AbsolutePath, RelativeToDataPath, RelativeToStreamingAssets };

        private void Start()
        {
            if (!useAllThreads)
                JobsUtility.JobWorkerCount = threadCount;
        }

        /// <summary>
        /// Cleans up the current sequence and prepares the playback of the sequence in the given folder. Doesn't start playback!
        /// </summary>
        /// <param name="absolutePathToSequence">The absolute path to the folder containing a sequence of .ply geometry files and optionally .dds texture files</param>
        public bool ChangeSequence(string absolutePathToSequence, float playbackFPS)
        {
            CleanupSequence();
            CleanupMeshAndTexture();
            currentFrameIndex = 0;            

            this.pathToSequence = absolutePathToSequence;

            bufferedReader = new BufferedGeometryReader(pathToSequence, bufferSize);

            bool meshRes = SetupMesh();
            bool textureRes = SetupTexture();
            readerIsReady = meshRes && textureRes;

            if (!readerIsReady)
            {
                UnityEngine.Debug.LogError("Reader could not be set up correctly, stopping playback!");
                return false;
            }

            targetFrameTimeMs = 1000f / (float)playbackFPS;

            return true;
        }

        public void UpdateFrame(float playbackTimeInMs)
        {
            if (!readerIsReady)
                return;

            //Fill the buffer with new data from the disk, and delete unused frames (In case of lag/skip)
            bufferedReader.BufferFrames(currentFrameIndex);

            elapsedMsSinceLastFrame += Time.deltaTime * 1000;

            int targetFrameIndex = Mathf.RoundToInt(playbackTimeInMs / targetFrameTimeMs);

            if (targetFrameIndex != currentFrameIndex && targetFrameIndex < bufferedReader.totalFrames)
            {
                //Check if our desired frame is inside the frame buffer and loaded, so that we can use it
                int frameBufferIndex = bufferedReader.GetBufferIndexForLoadedPlaybackIndex(targetFrameIndex);

                //Is the frame inside the buffer and fully loaded?
                if (frameBufferIndex > -1)
                {
                    //The frame has been loaded and we'll show the model (& texture)
                    ShowFrameData(bufferedReader.frameBuffer[frameBufferIndex]);
                    bufferedReader.frameBuffer[frameBufferIndex].isDisposed = true;

                    float decay = 0.95f;
                    if (elapsedMsSinceLastFrame > 0)
                        smoothedFPS = decay * smoothedFPS + (1.0f - decay) * (1000f / elapsedMsSinceLastFrame);

                    elapsedMsSinceLastFrame = 0;
                }

                if (Mathf.Abs(targetFrameIndex - currentFrameIndex) > 1 && targetFrameIndex > 0)
                    frameDropped = true;

                currentFrameIndex = targetFrameIndex;
            }

            //TODO: Buffering callback

        }

        bool SetupMesh()
        {
            meshObject = new GameObject("StreamedMesh");
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;

            string[] paths;

            try { paths = Directory.GetFiles(pathToSequence, "*.ply"); }

            catch(System.Exception e)
            {
                UnityEngine.Debug.LogError("Error getting sequence files, folder is probably empty!" + pathToSequence);
                return false;
            }

            if (paths.Length == 0)
            {
                UnityEngine.Debug.LogError("Couldn't find .ply files in sequence directory: " + pathToSequence);
                return false;
            }

            if (pointcloudMaterial == null || meshMaterial == null)
            {

            }

            BinaryReader headerReader = new BinaryReader(new FileStream(paths[0], FileMode.Open));

            string line = "";
            bool mesh = false;

            while (!line.Contains("end_header"))
            {
                line = bufferedReader.ReadPLYHeaderLine(headerReader);

                if (line.Contains("face"))
                    mesh = true;
            }

            headerReader.Dispose();

            if (mesh)
                meshType = MeshTopology.Triangles;
            else
                meshType = MeshTopology.Points;

            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.mesh = new Mesh();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();

            if (mesh)
            {
                if(meshMaterial == null)
                {
                    UnityEngine.Debug.LogError("Mesh material not assigned in GeometrySequenceStream Component, please assign a material!");
                    return false;
                }

                meshRenderer.material = meshMaterial;
                meshRenderer.material.SetTexture("_MainTex", texture);
            }

            else
            {
                if (meshMaterial == null)
                {
                    UnityEngine.Debug.LogError("Pointcloud material not assigned in GeometrySequenceStream Component, please assign a material!");
                    return false;
                }

                meshRenderer.material = pointcloudMaterial;
            }

            return true;
        }

        bool SetupTexture()
        {
            string[] textureFiles = Directory.GetFiles(pathToSequence + "/", "*.dds");

            HeaderDDS headerDDS = new HeaderDDS();

            if (textureFiles.Length > 0)
            {
                headerDDS = bufferedReader.ReadDDSHeader(textureFiles[0]);

                if (headerDDS.error)
                    return false;

                texture = new Texture2D(headerDDS.width, headerDDS.height, TextureFormat.DXT1, false);

                //Case: A single texture for the whole geometry sequence
                if (textureFiles.Length == 1)
                {
                    textureMode = TextureMode.Single;

                    //In this case we simply pre-load the texture at the start
                    Frame textureLoad = new Frame();
                    textureLoad.textureBufferRaw = new NativeArray<byte>(headerDDS.size, Allocator.Persistent);
                    textureLoad = bufferedReader.ScheduleTextureJob(textureLoad, textureFiles[0]);
                    ShowTextureData(textureLoad);
                    textureLoad.textureBufferRaw.Dispose();
                }

                //Case: Each frame has its own texture
                if (textureFiles.Length > 1)
                    textureMode = TextureMode.PerFrame;

            }

            else
                textureMode = TextureMode.None;

            if (!bufferedReader.SetupTextureReader(textureMode, headerDDS))
                    return false;

            return true;
        }

        

        /// <summary>
        /// Display mesh and texture data from a frame buffer
        /// </summary>
        /// <param name="frame"></param>
        void ShowFrameData(Frame frame)
        {
            ShowGeometryData(frame);

            if (textureMode == TextureMode.PerFrame)
            {
                ShowTextureData(frame);
            }
        }


        /// <summary>
        /// Reads mesh data from a native array buffer and disposes of it right after 
        /// </summary>
        /// <param name="frame"></param>
        void ShowGeometryData(Frame frame)
        {
            if (frame.plyHeaderInfo.error)
                return;

            frame.geoJobHandle.Complete();

            Mesh.ApplyAndDisposeWritableMeshData(frame.meshArray, meshFilter.mesh);
            meshFilter.mesh.RecalculateBounds();

            if (meshType == MeshTopology.Triangles)
                meshFilter.mesh.RecalculateNormals();
        }

        /// <summary>
        /// Reads texture data from a frame buffer. Doesn't dispose of the data, you need to do that manually!
        /// </summary>
        /// <param name="frame"></param>
        void ShowTextureData(Frame frame)
        {
            if (frame.ddsHeaderInfo.error)
                return;

            frame.textureJobHandle.Complete();

            NativeArray<byte> textureRaw = texture.GetRawTextureData<byte>();
            HeaderDDS textureHeader = frame.ddsHeaderInfo;

            if (textureRaw.Length != frame.textureBufferRaw.Length)
            {
                texture = new Texture2D(textureHeader.width, textureHeader.height, TextureFormat.DXT1, false);
                textureRaw = texture.GetRawTextureData<byte>();
            }

            textureRaw.CopyFrom(frame.textureBufferRaw);
            texture.Apply();

            if (meshRenderer.material.GetTexture("_MainTex") != texture)
                meshRenderer.material.SetTexture("_MainTex", texture);
        }


        void CleanupSequence()
        {
            if (bufferedReader != null)
            {
                bufferedReader.DisposeAllFrames(true, true, true);
            }

        }

        void CleanupMeshAndTexture()
        {
            if (meshObject != null)
                Destroy(meshObject);

            if (texture != null)
                Destroy(texture);
        }

        private void OnEnable()
        {
            //Fill up material slots with default materials
            if (pointcloudMaterial == null)
            {
                string[] Guids = AssetDatabase.FindAssets("GS_PointcloudMaterial t:material");
                if (Guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(Guids[0]);
                    Material mat = (Material)AssetDatabase.LoadAssetAtPath(path, typeof(Material));
                    pointcloudMaterial = mat;
                }
            }

            if (meshMaterial == null)
            {
                string[] Guids = AssetDatabase.FindAssets("GS_MeshMaterial t:material");
                if (Guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(Guids[0]);
                    Material mat = (Material)AssetDatabase.LoadAssetAtPath(path, typeof(Material));
                    meshMaterial = mat;
                }
            }
        }


        void OnDestroy()
        {
            CleanupSequence();
        }

    }

}
