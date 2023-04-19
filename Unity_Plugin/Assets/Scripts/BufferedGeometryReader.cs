using System;
using System.IO;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using Unity.Jobs;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;


namespace GeometrySequence.Streaming
{

    public struct Frame
    {
        public HeaderPLY plyHeaderInfo;
        public Mesh.MeshDataArray meshArray;
        public ReadGeometryJob geoJob;
        public JobHandle geoJobHandle;

        public HeaderDDS ddsHeaderInfo;
        public NativeArray<byte> textureBufferRaw;
        public ReadTextureJob textureJob;
        public JobHandle textureJobHandle;
        public TextureMode textureMode;

        public int playbackIndex;
        public bool isDisposed;
    }

    public struct HeaderPLY
    {
        public MeshTopology meshType;
        public int vertexCount;
        public int indexCount;
        public int byteCount;
        public bool error;
    }

    public struct HeaderDDS
    {
        public int width;
        public int height;
        public int size;
        public bool error;
    }

    public enum TextureMode { None, Single, PerFrame };

    public class BufferedGeometryReader
    {
        public string folder;
        public string[] plyFilePaths;
        public string[] texturesFilePath;
        public int bufferSize = 4;
        public int totalFrames = 0;

        public Frame[] frameBuffer;

        bool buffering = true;

        /// <summary>
        /// Create a new buffered reader. You must include a path to a valid folder
        /// </summary>
        /// <param name="folder">A path to a folder containing .ply geometry files and optionally .dds texture files</param>
        public BufferedGeometryReader(string folder, int frameBufferSize)
        {
            SetupReader(folder, frameBufferSize);
        }

        ~BufferedGeometryReader()
        {
            //When the reader is destroyed, we need to ensure that all the NativeArrays will also be manually deleted/disposed
            DisposeAllFrames(true, true, true);
        }

        /// <summary>
        /// Use this function to setup a new buffered Reader. Don't forget to also setup the texture Reader, even if you don't use textures!
        /// </summary>
        /// <param name="folder">A path to a folder containing .ply geometry files and optionally .dds texture files</param>
        /// <returns>Returns true on success, false when any errors have occured during setup</returns>
        public bool SetupReader(string folder, int frameBufferSize)
        {
            this.folder = folder;

            try
            {
                //Add a temporary padding to the file list, as otherwise the file order will be messed up
                plyFilePaths = new List<string>(Directory.GetFiles(folder, "*.ply")).OrderBy(file =>
                Regex.Replace(file, @"\d+", match => match.Value.PadLeft(9, '0'))).ToArray<string>();
            }

            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Sequence path is not valid or has restricted access!");
                return false;
            }

            if (plyFilePaths.Length == 0)
            {
                UnityEngine.Debug.LogError("Could not find any .ply files in the directory!");
                return false;
            }

            bufferSize = frameBufferSize;
            totalFrames = plyFilePaths.Length;
            frameBuffer = new Frame[bufferSize];

            for (int i = 0; i < frameBuffer.Length; i++)
                frameBuffer[i].isDisposed = true;

            return true;
        }

        /// <summary>
        /// Setup the texture reader. You need to set up a texture reader even when you don't use textures!
        /// </summary>
        /// <param name="textureMode">Select between no textures, a single texture for the whole sequence or one texture per frame</param>
        /// <param name="headerDDS">Include the header data from the first texture in the folder, you can read it with ReadDDSHeader() </param>
        /// <returns>Returns true on success, false when any error has occured</returns>
        public bool SetupTextureReader(TextureMode textureMode, HeaderDDS headerDDS)
        {
            if (textureMode != TextureMode.None)
            {
                try
                {
                    texturesFilePath = new List<string>(Directory.GetFiles(folder, "*.dds")).OrderBy(file =>
                    Regex.Replace(file, @"\d+", match => match.Value.PadLeft(9, '0'))).ToArray<string>();
                }

                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Texture could not be found !");
                    return false;
                }

                if (plyFilePaths.Length == 0)
                {
                    UnityEngine.Debug.LogError("Could not find any .ply files in the directory!");
                    return false;
                }
            }

            for (int i = 0; i < frameBuffer.Length; i++)
            {
                frameBuffer[i].textureMode = textureMode;

                if (textureMode == TextureMode.PerFrame)
                    frameBuffer[i].textureBufferRaw = new NativeArray<byte>(headerDDS.size, Allocator.Persistent);
                else
                    frameBuffer[i].textureBufferRaw = new NativeArray<byte>(0, Allocator.Persistent);

            }

            return true;
        }


        /// <summary>
        /// Loads new frames in the buffer if there are free slots. Call this every frame
        /// </summary>
        public void BufferFrames(int currentPlaybackFrame)
        {
            if (!buffering)
                return;

            //Delete frames from buffer that are outside our current buffer range
            //which keeps our buffer clean in case of skips or lags
            DeleteFramesOutsideOfBufferRange(currentPlaybackFrame);

            //Find out which frames we need to buffer, meaning which frames from the
            //current playback index to the max needed buffered frames are not already in the buffer 
            int minPlaybackIndex = currentPlaybackFrame;
            int maxPlaybackIndex = currentPlaybackFrame + bufferSize;
            List<int> framesToBuffer = new List<int>();

            for (int i = minPlaybackIndex; i <= maxPlaybackIndex; i++)
            {
                if (GetBufferIndexForPlaybackIndex(i) == -1)
                    framesToBuffer.Add(i);
            }

            for (int i = 0; i < frameBuffer.Length; i++)
            {
                //Check if the buffer is ready to load the next frame 
                if (frameBuffer[i].isDisposed && framesToBuffer.Count > 0)
                {
                    int newPlaybackIndex = framesToBuffer[0];

                    if (newPlaybackIndex < totalFrames)
                    {
                        Frame newFrame = frameBuffer[i];

                        newFrame.meshArray = Mesh.AllocateWritableMeshData(1);

                        newFrame.playbackIndex = newPlaybackIndex;
                        newFrame = ScheduleGeometryReadJob(newFrame, plyFilePaths[newPlaybackIndex]);

                        if (newFrame.textureMode == TextureMode.PerFrame && !newFrame.plyHeaderInfo.error)
                            newFrame = ScheduleTextureJob(newFrame, texturesFilePath[newPlaybackIndex]);

                        if (newFrame.plyHeaderInfo.error || newFrame.ddsHeaderInfo.error)
                        {
                            newFrame.meshArray.Dispose();
                            //If the mesh isn't in the right format, we simply skip it
                            framesToBuffer.Remove(newPlaybackIndex);
                            continue;
                        }

                        newFrame.isDisposed = false;
                        frameBuffer[i] = newFrame;
                        framesToBuffer.Remove(newPlaybackIndex);
                    }
                }
            }

            JobHandle.ScheduleBatchedJobs();
        }


        /// <summary>
        /// Deletes frames that are either in the past of current Frame index,
        /// or too far in the future (the whole buffer size away from the current Frame Index)
        /// Should be regularily called to keep the buffer clean
        /// </summary>
        /// <param name="currentFrameIndex">The currently shown/played back frame</param>
        public void DeleteFramesOutsideOfBufferRange(int currentFrameIndex)
        {
            for (int i = 0; i < frameBuffer.Length; i++)
            {
                if (frameBuffer[i].playbackIndex < currentFrameIndex || frameBuffer[i].playbackIndex > currentFrameIndex + bufferSize)
                {
                    if (!frameBuffer[i].isDisposed)
                    {
                        DisposeFrame(i, false, false);
                    }

                }
            }
        }

        /// <summary>
        /// Check if the desired input frame of the sequence has already been buffered.
        /// </summary>
        /// <param name="playbackIndex">The desired frame number from the whole sequence</param>
        /// <returns>If the frame could be found and has been loaded, you get the index of the frame in the buffer. Returns -1 if frame could not be found or has not been loaded yet</returns>
        public int GetBufferIndexForLoadedPlaybackIndex(int playbackIndex)
        {
            for (int i = 0; i < frameBuffer.Length; i++)
            {
                if (frameBuffer[i].playbackIndex == playbackIndex)
                {
                    if (IsFrameBuffered(frameBuffer[i]))
                    {
                        return i;
                    }
                    else
                        return -1;
                }
            }

            return -1;
        }

        public int GetBufferIndexForPlaybackIndex(int playbackIndex)
        {
            for (int i = 0; i < frameBuffer.Length; i++)
            {
                if (frameBuffer[i].playbackIndex == playbackIndex)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Get the total amount of frames that are fully stored in buffer
        /// After skipping or loading in a new sequence, it's useful to wait 
        /// until the buffer has stored at least a few frames
        /// </summary>
        /// <returns></returns>
        public int GetBufferedFrames()
        {
            int loadedFrames = 0;

            for (int i = 0; i < frameBuffer.Length; i++)
            {
                if (IsFrameBuffered(frameBuffer[i]))
                    loadedFrames++;
            }

            return loadedFrames;
        }

        /// <summary>
        /// Has the data loading finished for this frame?
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public bool IsFrameBuffered(Frame frame)
        {
            if (frame.geoJobHandle.IsCompleted && !frame.isDisposed)
            {
                if (frame.textureMode == TextureMode.PerFrame)
                {
                    if (frame.textureJobHandle.IsCompleted)
                        return true;
                }

                else
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Schedules a Job that reads a .ply Pointcloud or mesh file from disk
        /// and loads it into memory.
        /// </summary>
        /// <param name="frame">The frame into which to load the data. The meshdataarray needs to be initialized already</param>
        /// <param name="plyPath">The absolute path to the .ply file </param>
        /// <returns></returns>
        public Frame ScheduleGeometryReadJob(Frame frame, string plyPath)
        {
            frame.geoJob = new ReadGeometryJob();

            frame.plyHeaderInfo = ReadPLYHeader(plyPath);

            if (frame.plyHeaderInfo.error)
                return frame;

            VertexAttributeDescriptor[] layout = new VertexAttributeDescriptor[0];

            if (frame.plyHeaderInfo.meshType == MeshTopology.Points)
            {
                layout = new[] {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4) };
            }

            else if (frame.plyHeaderInfo.meshType == MeshTopology.Triangles)
            {
                layout = new[] {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2) };
            }

            frame.meshArray[0].SetVertexBufferParams(frame.plyHeaderInfo.vertexCount, layout);
            frame.meshArray[0].SetIndexBufferParams(frame.plyHeaderInfo.indexCount, IndexFormat.UInt32);
            frame.geoJob.pathCharArray = new NativeArray<byte>(Encoding.UTF8.GetBytes(plyPath), Allocator.TempJob);
            frame.geoJob.headerInfo = frame.plyHeaderInfo;
            frame.geoJob.mesh = frame.meshArray[0];

            frame.geoJobHandle = frame.geoJob.Schedule(frame.geoJobHandle);

            return frame;

        }

        /// <summary>
        /// Schedules a job which loads a .dds DXT1 file from disk into memory
        /// </summary>
        /// <param name="frame">The frame data into which the texture will be loaded. The textureBufferRaw needs to be intialized already </param>
        /// <param name="texturePath"></param>
        /// <returns></returns>
        public Frame ScheduleTextureJob(Frame frame, string texturePath)
        {
            frame.textureJob = new ReadTextureJob();
            frame.ddsHeaderInfo = ReadDDSHeader(texturePath);

            if (!frame.ddsHeaderInfo.error && frame.ddsHeaderInfo.size > 0)
            {
                if (frame.textureBufferRaw.Length != frame.ddsHeaderInfo.size)
                {
                    frame.textureBufferRaw.Dispose();
                    frame.textureBufferRaw = new NativeArray<byte>(frame.ddsHeaderInfo.size, Allocator.Persistent);
                }

                frame.textureJob.textureRawData = frame.textureBufferRaw;
                frame.textureJob.texturePathCharArray = new NativeArray<byte>(Encoding.UTF8.GetBytes(texturePath), Allocator.TempJob);

                frame.textureJobHandle = frame.textureJob.Schedule(frame.textureJobHandle);
            }

            return frame;
        }


        /// <summary>
        /// Reads the header of a .ply file on disk into the HeaderPLY struct. Also checks if the file is in the correct format
        /// </summary>
        /// <param name="path">Absolute path to the .ply file on disk</param>
        /// <returns>Returns the header information. Check the error attribute to see if the file has been loaded correctly</returns>
        private HeaderPLY ReadPLYHeader(string path)
        {
            HeaderPLY info = new HeaderPLY();

            BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open));

            string line = "";

            while (!line.Contains("format"))
                line = ReadPLYHeaderLine(reader);

            if (!line.Contains("binary"))
            {
                Debug.LogError("PLY File contains ascíi data, which is not supported. " +
                    "Please use the included Converter Tool to convert your files into the right format");
                info.error = true;
                return info;
            }

            if (!line.Contains("little_endian"))
            {
                Debug.LogError("PLY File contains data in big-endian format, which is not supported. " +
                    "Please use the included Converter Tool to convert your files into the right format");
                info.error = true;
                return info;
            }

            while (!line.Contains("element vertex"))
                line = ReadPLYHeaderLine(reader);

            string[] lineElements = line.Split(' ');

            info.vertexCount = Int32.Parse(lineElements[2]);
            info.indexCount = info.vertexCount;

            line = ReadPLYHeaderLine(reader);

            if (line.Contains("double"))
            {
                Debug.LogError("PLY File contains 64-bit floats (Double) numbers, which is not supported. " +
                    "Please use the included Converter Tool to convert your files into the right format");
                info.error = true;
                return info;
            }

            while (!line.Contains("float z"))
                line = ReadPLYHeaderLine(reader);

            line = ReadPLYHeaderLine(reader);


            if (line.Contains("uchar red"))
            {
                info.meshType = MeshTopology.Points;

                while (!line.Contains("blue"))
                    line = ReadPLYHeaderLine(reader);

                line = ReadPLYHeaderLine(reader);

                if (!line.Contains("alpha"))
                {
                    Debug.LogError("PLY File doesn't contain an alpha channel, which is required. " +
                        "Please use the included Converter Tool to convert your files into the right format");
                    info.error = true;
                    return info;
                }
            }

            else
            {
                string u = line;
                string v = ReadPLYHeaderLine(reader);
                string elementFace = ReadPLYHeaderLine(reader);

                if (u.Contains("property float s") && v.Contains("property float t") && elementFace.Contains("element face"))
                {
                    info.meshType = MeshTopology.Triangles;
                    string[] elementFaceSplit = elementFace.Split(' ');
                    info.indexCount = Int32.Parse(elementFaceSplit[2]) * 3;
                }

                else
                {
                    Debug.LogError("PLY File has invalid format. " +
                        "Please use the included Converter Tool to convert your files into the right format");
                    info.error = true;
                    return info;
                }
            }

            while (!line.Contains("end_header"))
            {
                line = ReadPLYHeaderLine(reader);
            }

            info.byteCount = (int)reader.BaseStream.Position;

            reader.Close();
            reader.Dispose();

            return info;
        }

        public string ReadPLYHeaderLine(BinaryReader reader)
        {
            char currentChar = 'a';
            string s = "";

            while (currentChar != '\r' && currentChar != '\n')
            {
                currentChar = reader.ReadChar();
                s += currentChar;
            }

            return s;
        }

        /// <summary>
        /// Reads the header of a .dds file on disk. Also checks if the file has the correct format
        /// </summary>
        /// <param name="path">The absolute path to the .dds file on disk</param>
        /// <returns>Returns header information. Check the error attribute to see if the file is in the correct format</returns>
        public HeaderDDS ReadDDSHeader(string path)
        {
            HeaderDDS headerDDS = new HeaderDDS();

            BinaryReader ddsReader = new BinaryReader(new FileStream(path, FileMode.Open));

            byte[] headerBytes = ddsReader.ReadBytes(128);

            byte ddsSizeCheck = headerBytes[4];
            if (ddsSizeCheck != 124)
            {
                Debug.LogError("Invalid DDS DXTn texture. Unable to read");
                headerDDS.error = true;
                ddsReader.Dispose();
                return headerDDS;
            }

            headerDDS.height = headerBytes[13] * 256 + headerBytes[12];
            headerDDS.width = headerBytes[17] * 256 + headerBytes[16];
            headerDDS.size = (int)ddsReader.BaseStream.Length - 128;

            ddsReader.Dispose();
            return headerDDS;
        }

        /// <summary>
        /// This function ensures that all memory resources are unlocated
        /// and all jobs are finished, so that no memory leaks occur.
        /// </summary>
        public void DisposeAllFrames(bool stopBuffering, bool forceWorkerCompletion, bool disposeTexture)
        {
            buffering = !stopBuffering;

            for (int i = 0; i < frameBuffer.Length; i++)
            {
                DisposeFrame(i, forceWorkerCompletion, disposeTexture);
            }
        }

        public bool DisposeFrame(int frameBufferIndex, bool forceWorkerCompletion, bool disposeTexture)
        {
            if (frameBuffer != null)
            {
                if (forceWorkerCompletion)
                {
                    frameBuffer[frameBufferIndex].geoJobHandle.Complete();
                    frameBuffer[frameBufferIndex].textureJobHandle.Complete();
                }

                if (frameBuffer[frameBufferIndex].geoJobHandle.IsCompleted && frameBuffer[frameBufferIndex].textureJobHandle.IsCompleted)
                {
                    if (!frameBuffer[frameBufferIndex].isDisposed)
                    {
                        if (frameBuffer[frameBufferIndex].meshArray.Length > 0)
                            frameBuffer[frameBufferIndex].meshArray.Dispose();

                        frameBuffer[frameBufferIndex].isDisposed = true;
                    }

                    if (disposeTexture)
                    {
                        if (frameBuffer[frameBufferIndex].textureBufferRaw.Length > 0)
                            frameBuffer[frameBufferIndex].textureBufferRaw.Dispose();
                    }

                    return true;
                }

                return false;
            }

            return false;
        }
    }

    public struct ReadGeometryJob : IJob
    {
        public Mesh.MeshData mesh;
        public bool readFinished;
        public HeaderPLY headerInfo;

        [DeallocateOnJobCompletion]
        public NativeArray<byte> pathCharArray;

        public void Execute()
        {
            readFinished = false;

            //We can't give Lists/strings to a job directly, so we need this workaround 
            byte[] pathCharBuffer = new byte[pathCharArray.Length];
            pathCharArray.CopyTo(pathCharBuffer);
            string path = Encoding.UTF8.GetString(pathCharBuffer);

            //We read all bytes into a buffer at once, much quicker than doing it in many shorter reads.
            //This buffer only contains the raw mesh data without the header
            BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open));
            reader.BaseStream.Position = headerInfo.byteCount;
            byte[] byteBuffer = reader.ReadBytes((int)(reader.BaseStream.Length - headerInfo.byteCount));
            reader.Close();
            reader.Dispose();

            if (headerInfo.meshType == MeshTopology.Points)
            {
                int[] indices = new int[headerInfo.vertexCount];

                for (int i = 0; i < headerInfo.vertexCount; i++)
                {
                    indices[i] = i; //If the pointcloud "mesh" doesn't have indices, Unity won't display it, so we make them up here
                }

                mesh.GetVertexData<byte>().CopyFrom(byteBuffer);
                mesh.GetIndexData<int>().CopyFrom(indices);

                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Points));
            }

            else if (headerInfo.meshType == MeshTopology.Triangles)
            {
                float[] positionsAndUVs = new float[headerInfo.vertexCount * 5];
                int[] indicesRaw = new int[headerInfo.indexCount];

                Buffer.BlockCopy(byteBuffer, 0, positionsAndUVs, 0, headerInfo.vertexCount * 5 * sizeof(float));

                int facePositionInBuffer = headerInfo.vertexCount * 5 * sizeof(float);
                int sizeOfIndexLine = sizeof(byte) + sizeof(int) * 3;

                //Reading the index is a bit tricky because each index line contains the number of indices in that line, which we dont want to include
                for (int i = 0; i < headerInfo.indexCount / 3; i++)
                {
                    Buffer.BlockCopy(byteBuffer, facePositionInBuffer + sizeOfIndexLine * i + sizeof(byte), indicesRaw, i * 3 * sizeof(int), 3 * sizeof(int));
                }

                NativeArray<float> verts = new NativeArray<float>(positionsAndUVs, Allocator.Temp);
                NativeArray<int> indices = new NativeArray<int>(indicesRaw, Allocator.Temp);

                mesh.GetVertexData<float>().CopyFrom(verts);
                mesh.GetIndexData<int>().CopyFrom(indices);

                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles));
            }

            readFinished = true;

        }
    }

    public struct ReadTextureJob : IJob
    {
        public NativeArray<byte> textureRawData;
        public bool readFinished;

        [DeallocateOnJobCompletion]
        public NativeArray<byte> texturePathCharArray;

        public void Execute()
        {
            readFinished = false;
            string texturePath = "";

            byte[] texturePathCharBuffer = new byte[texturePathCharArray.Length];
            texturePathCharArray.CopyTo(texturePathCharBuffer);
            texturePath = Encoding.UTF8.GetString(texturePathCharBuffer);

            int DDS_HEADER_SIZE = 128;

            BinaryReader textureReader = new BinaryReader(new FileStream(texturePath, FileMode.Open));
            byte[] textureBuffer = textureReader.ReadBytes((int)textureReader.BaseStream.Length - DDS_HEADER_SIZE);
            textureReader.Close();

            //As GPUs can access .DDS data directly, we can simply take the binary blob and upload it to the GPU
            textureRawData.CopyFrom(textureBuffer);

            readFinished = true;
        }
    }
}
