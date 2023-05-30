using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometrySequence.Streaming
{

    public class GeometrySequencePlayer : MonoBehaviour
    {
        GeometrySequenceStream stream;

        [SerializeField]
        string relativePath = "";
        [SerializeField]
        string absolutePath = "";
        [SerializeField]
        PathType pathRelation;

        [SerializeField]
        bool playAtStart = true;
        [SerializeField]
        bool loopPlay = true;
        [SerializeField]
        float playbackFPS = 30;

        float playbackTimeMs = 0;
        bool play = false;

        public enum PathType { AbsolutePath, RelativeToDataPath, RelativeToStreamingAssets };

        // Start is called before the first frame update
        void Start()
        {
            SetupGeometryStream();
            LoadSequence(relativePath, pathRelation, playbackFPS, playAtStart);
        }

        public void SetupGeometryStream()
        {
            //Add a Geometry Sequence Stream if there is non already existing on this gameobject
            if (stream == null)
            {
                stream = gameObject.GetComponent<GeometrySequenceStream>();
                if (stream == null)
                    stream = gameObject.AddComponent<GeometrySequenceStream>();
            }
        }

        private void Update()
        {
            if (play)
            {
                playbackTimeMs += Time.deltaTime * 1000;

                if (GetCurrentTime() >= GetTotalTime())
                {
                    GoToTime(0);

                    if (!loopPlay)
                        Pause();
                }

                stream.UpdateFrame(playbackTimeMs);
            }
            
        }

        //+++++++++++++++++++++ PLAYBACK API ++++++++++++++++++++++++

        /// <summary>
        /// Load a .ply sequence (and optionally textures) from the path, and start playback if autoplay is enabled.
        /// Returns false when sequence could not be loaded, see Unity Console output for details in this case
        /// </summary>
        /// <param name="path"></param>
        public bool LoadSequence(string path, PathType relativeTo, float playbackFPS , bool autoplay = false)
        {
            if (path.Length < 1)
                return false;

            relativePath = path;
            this.playbackFPS = playbackFPS;
            play = false;

            //Set the correct absolute path depending on the files location
            if (pathRelation == PathType.RelativeToDataPath)
                absolutePath = Path.Combine(Application.dataPath, relativePath);

            if (pathRelation == PathType.RelativeToStreamingAssets)
                absolutePath = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (pathRelation == PathType.AbsolutePath)
                absolutePath = relativePath;

            bool sucess = stream.ChangeSequence(absolutePath, playbackFPS);

            if (autoplay && sucess)
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
        /// Activate or deactivate looped playback
        /// </summary>
        /// <param name="enabled"></param>
        public void SetLoopPlay(bool enabled)
        {
            loopPlay = enabled;
        }

        /// <summary>
        /// Activate or deactivate automatic playback (when the scene starts)
        /// </summary>
        /// <param name="enabled"></param>
        public void SetAutoStart(bool enabled)
        {
            playAtStart = false;
        }

        /// <summary>
        /// Seeks to the start of the sequence and then starts playback
        /// </summary>
        public bool PlayFromStart()
        {
            if (GoToFrame(0))
            {
                play = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Goes to a specific frame. Use GetTotalFrames() to check how many frames the clip contains
        /// </summary>
        /// <param name="frame"></param>
        public bool GoToFrame(int frame)
        {
            if(stream != null)
            {
                playbackTimeMs = frame * stream.targetFrameTimeMs;
                if (!play)
                    stream.UpdateFrame(playbackTimeMs);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Goes to a specific time in  a clip. The time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <param name="timeInSeconds"></param>
        /// <returns></returns>
        public void GoToTime(float timeInSeconds)
        {
            playbackTimeMs = timeInSeconds * 1000;
            if (!play)
                stream.UpdateFrame(playbackTimeMs);
        }


        /// <summary>
        /// Gets the absolute path to the folder containing the sequence
        /// </summary>
        /// <returns></returns>
        public string GetSequencePath()
        {
            return stream.pathToSequence;
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
            if (stream != null)
                return stream.currentFrameIndex;
            return -1;
        }

        /// <summary>
        /// At which time is the playback currently in seconds?
        /// Note that the time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <returns></returns>
        public float GetCurrentTime()
        {
            return playbackTimeMs / 1000;
        }

        /// <summary>
        /// How many frames are there in total in the whole sequence?
        /// </summary>
        /// <returns></returns>
        public int GetTotalFrames()
        {
            if(stream != null)
                if (stream.bufferedReader != null)
                    return stream.bufferedReader.totalFrames;
            return -1;
        }

        /// <summary>
        /// How long is the sequence in total?
        /// Note that the time is dependent on the framerate e.g. the same clip at 30 FPS is twice as long as at 60 FPS.
        /// </summary>
        /// <returns></returns>
        public float GetTotalTime()
        {
            return GetTotalFrames() / GetTargetFPS();
        }

        /// <summary>
        /// The target fps is the framerate we _want_ to achieve in playback. However, this is not guranteed, if system resources
        /// are too low. Use GetActualFPS() to see if you actually achieve this framerate
        /// </summary>
        /// <returns></returns>
        public float GetTargetFPS()
        {
            if(stream != null)
                return 1000 / stream.targetFrameTimeMs;
            return -1;
        }

        /// <summary>
        /// What is the actual current playback framerate? If the framerate is much lower than the target framerate,
        /// consider reducing the complexity of your sequence, and don't forget to disable any V-Sync (VSync, FreeSync, GSync) methods!
        /// </summary>
        /// <returns></returns>
        public float GetActualFPS()
        {
            if(stream != null)
                return stream.smoothedFPS;
            return -1;
        }

        /// <summary>
        /// Check if there have been framedrops since you last checked this function
        /// Too many framedrops mean the system can't keep up with the playback
        /// and you should reduce your Geometric complexity or framerate
        /// </summary>
        /// <returns></returns>
        public bool GetFrameDropped()
        {
            if(stream != null)
            {
                bool dropped = stream.frameDropped;
                stream.frameDropped = false;
                return dropped;
            }

            return false;
        }
    }

}
