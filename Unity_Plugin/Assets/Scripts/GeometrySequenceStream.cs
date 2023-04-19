using UnityEngine;
using System.IO;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using System.Diagnostics;
using UnityEngine.UI;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace GeometrySequence.Streaming
{

    public class GeometrySequenceStream : MonoBehaviour
    {
        public string pathToSequence;

        public bool autoplay = true;
        public bool loopPlay = true;
        public int playbackFPS = 30;

        public int bufferSize = 8;
        public bool useAllThreads = true;
        public int threadCount = 4;

        public Material pointCloudMaterial;
        public Material meshMaterial;
        public Material meshMaterialTextured;

        bool play = false;
        bool playAfterBufferingComplete = false;
        bool readerIsReady = false;
        bool frameDropped = false;

        int currentFrameIndex = 0;
        float targetFrameTimeMs = 0;
        float elapsedMs = 0;
        float elapsedMsSinceLastFrame = 0;
        float smoothedFPS = 0f;

        MeshTopology meshType = MeshTopology.Points;
        TextureMode textureMode = TextureMode.None;

        BufferedGeometryReader bufferedReader;
        GameObject meshObject;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        public Texture2D texture;


        private void Start()
        {
            if (pathToSequence != null)
            {
                if (pathToSequence.Length > 0)
                    ChangeSequence(pathToSequence);
            }

            if (!useAllThreads)
                JobsUtility.JobWorkerCount = threadCount;

            if (autoplay)
                PlayFromStart();

            smoothedFPS = playbackFPS;

        }

        /// <summary>
        /// Cleans up the current sequence and prepares the playback of the sequence in the given folder. Doesn't start playback!
        /// </summary>
        /// <param name="sequenceDirPath">The absolute path to the folder containing a sequence of .ply geometry files and optionally .dds texture files</param>
        bool ChangeSequence(string sequenceDirPath)
        {
            CleanupSequence();
            CleanupMeshAndTexture();
            currentFrameIndex = 0;

            bufferedReader = new BufferedGeometryReader(sequenceDirPath, bufferSize);

            bool meshRes = SetupMesh();
            bool textureRes = SetupTexture();
            readerIsReady = meshRes && textureRes;

            if (!readerIsReady)
            {
                UnityEngine.Debug.Log("Error, reader could not be set up correctly, stopping playback!");
                return false;
            }

            targetFrameTimeMs = 1000f / (float)playbackFPS;

            return true;
        }

        bool SetupMesh()
        {
            meshObject = new GameObject("StreamedMesh");
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;

            string[] paths = Directory.GetFiles(pathToSequence, "*.ply");

            if (paths.Length == 0)
            {
                UnityEngine.Debug.LogError("Couldn't find .ply files in sequence directory!");
                return false;
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
                meshRenderer.sharedMaterial = meshMaterial;
            else
                meshRenderer.sharedMaterial = pointCloudMaterial;

            meshRenderer.sharedMaterial.SetTexture("_MainTex", texture);

            return true;
        }

        bool SetupTexture()
        {
            string[] textureFiles = Directory.GetFiles(pathToSequence + "/", "*.dds");

            HeaderDDS headerDDS = new HeaderDDS();

            if (textureFiles.Length > 0)
            {
                meshRenderer.sharedMaterial = meshMaterialTextured;

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

                if (!bufferedReader.SetupTextureReader(textureMode, headerDDS))
                    return false;
            }

            return true;
        }

        void CleanupMeshAndTexture()
        {
            if (meshObject != null)
                Destroy(meshObject);

            if (texture != null)
                Destroy(texture);
        }


        void Update()
        {
            if (!readerIsReady)
                return;            

            if (play)
            {
                elapsedMs += Time.deltaTime * 1000;
                elapsedMsSinceLastFrame += Time.deltaTime * 1000;

                //The Unity update does not really have precise frame timings, so each frame we
                //take the time that has elapsed since the playback start and look which 
                //frame is closest to it. For example if playback at 30 FPS, these are our frame times (in ms):
                //
                //Frame:     1    2    3    4 
                //Frametime: 0   33   66   99 ...
                //
                //If our elapsed time in this update loop is at 54, Frame #3 with frametime 66ms is closest,
                //and we show it.
                //After that we discard any frame in the buffer which are in the past and load in new ones.
                //This is needed when a lag/freeze occurs and all frames in the buffer are skipped

                int targetFrameIndex = Mathf.RoundToInt(elapsedMs / targetFrameTimeMs);

                if (targetFrameIndex != currentFrameIndex)
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

                    if (Mathf.Abs(targetFrameIndex - currentFrameIndex) > 1)
                        frameDropped = true;

                    currentFrameIndex = targetFrameIndex;
                }

                if (loopPlay)
                {
                    if (currentFrameIndex >= bufferedReader.totalFrames)
                    {
                        PlayFromStart();
                    }
                }
            }

            if (playAfterBufferingComplete)
            {
                if(bufferedReader.GetBufferedFrames() == bufferedReader.bufferSize)
                {
                    playAfterBufferingComplete = false;
                    play = true;
                }
            }

            //Fill the buffer with new data from the disk
            bufferedReader.BufferFrames(currentFrameIndex);
        }

        /// <summary>
        /// Display mesh and texture data from a frame buffer
        /// </summary>
        /// <param name="frame"></param>
        void ShowFrameData(Frame frame)
        {
            ShowGeometryData(frame);

            if (textureMode == TextureMode.PerFrame)
                ShowTextureData(frame);
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

            if (meshRenderer.sharedMaterial.GetTexture("_MainTex") != texture)
                meshRenderer.sharedMaterial.SetTexture("_MainTex", texture);
        }

        void OnDestroy()
        {
            CleanupSequence();
        }

        void CleanupSequence()
        {
            if (bufferedReader != null)
                bufferedReader.DisposeAllFrames(true, true, true);
        }



        //+++++++++++++++++++++ PUBLIC PLAYBACK CONTROLS ++++++++++++++++++++++++

        /// <summary>
        /// Load a .ply sequence (and optionally textures) from the path, and start playback if autoplay is enabled.
        /// Returns false when sequence could not be loaded, see Unity Console output for details in this case
        /// </summary>
        /// <param name="path"></param>
        public bool LoadSequence(string path)
        {
            bool sucess = ChangeSequence(path);
            
            if(autoplay && sucess)
                PlayFromStart();

            return sucess;
        }

        /// <summary>
        /// Start Playback from the current location
        /// </summary>
        public void Play()
        {
            play = true;
        }

        /// <summary>
        /// Pause current playback
        /// </summary>
        public void Pause()
        {
            play = false;
        }

        /// <summary>
        /// Activate or deactivate auto playback when a new sequence has been loaded
        /// </summary>
        /// <param name="enabled"></param>
        public void SetAutoPlay(bool enabled)
        {
            autoplay = enabled;
        }


        /// <summary>
        /// Activate or deactivate looped playback
        /// </summary>
        /// <param name="enabled"></param>
        public void SetLoopPlay(bool enabled)
        {
            loopPlay = enabled;
        }

        /// <summary>
        /// Seeks to the start of the sequence and then starts playback
        /// </summary>
        public void PlayFromStart()
        {
            SkipToFrame(0);
            playAfterBufferingComplete = true;
            play = false;
        }

        /// <summary>
        /// Goes to a specific frame. Use GetTotalFrames() to check how many frames the clip contains
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public bool SkipToFrame(int frame)
        {
            if (bufferedReader != null)
            {
                if (frame >= 0 && frame < bufferedReader.totalFrames)
                {
                    bufferedReader.DisposeAllFrames(false, true, false);
                    elapsedMs = frame * targetFrameTimeMs;
                    currentFrameIndex = frame;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Goes to a specific time in  a clip. The time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <param name="timeInSeconds"></param>
        /// <returns></returns>
        public bool SkipToTime(float timeInSeconds)
        {
            if (bufferedReader != null)
                return SkipToFrame(Mathf.RoundToInt(timeInSeconds * playbackFPS));

            return false;
        }

        /// <summary>
        /// Set the target framerate at which the playback should occur. This is only a target, actual framerate might be lower if your system
        /// resources are not fast enough for the desired FPS!
        /// </summary>
        /// <param name="fps"></param>
        public void SetTargetFramerate(int fps)
        {
            playbackFPS = fps;
        }

        /// <summary>
        /// Is the current clip playing?
        /// </summary>
        /// <returns></returns>
        public bool IsPlaying()
        {
            return play;
        }

        /// <summary>
        /// Is auto playback enabled?
        /// </summary>
        /// <returns></returns>
        public bool GetAutoplayEnabled()
        {
            return autoplay;
        }

        /// <summary>
        /// Is looped playback enabled?
        /// </summary>
        /// <returns></returns>
        public bool GetLoopingEnabled()
        {
            return loopPlay;
        }

        /// <summary>
        /// At which frame is the playback currently?
        /// </summary>
        /// <returns></returns>
        public int GetCurrentFrameIndex()
        {
            return currentFrameIndex;
        }

        /// <summary>
        /// At which time is the playback currently?
        /// Note that the time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <returns></returns>
        public float GetCurrentTime()
        {
            return currentFrameIndex / playbackFPS;
        }

        /// <summary>
        /// How many frames are there in total in the whole sequence?
        /// </summary>
        /// <returns></returns>
        public int GetTotalFrames()
        {
            if (bufferedReader != null)
                return bufferedReader.totalFrames;
            return 0;
        }

        /// <summary>
        /// How long is the sequence in total?
        /// Note that the time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <returns></returns>
        public float GetTotalTime()
        {
            if (bufferedReader != null)
            {
                return (float)bufferedReader.totalFrames / (float)playbackFPS;
            }

            return 0;
        }

        /// <summary>
        /// What is the actual current playback framerate? If the framerate is much lower than the target framerate,
        /// consider reducing the complexity of your sequence, and don't forget to disable any V-Sync (VSync, FreeSync, GSync) methods!
        /// </summary>
        /// <returns></returns>
        public float GetCurrentFPS()
        {
            return smoothedFPS;
        }

        /// <summary>
        /// Check if there have been framedrops since you last checked this function
        /// Too many framedrops mean the system can't keep up with the playback
        /// and you should reduce your Geometric complexity or framerate
        /// </summary>
        /// <returns></returns>
        public bool GetFrameDropped()
        {
            bool dropped = frameDropped;
            frameDropped = false;
            return dropped;
        }
    }

}
