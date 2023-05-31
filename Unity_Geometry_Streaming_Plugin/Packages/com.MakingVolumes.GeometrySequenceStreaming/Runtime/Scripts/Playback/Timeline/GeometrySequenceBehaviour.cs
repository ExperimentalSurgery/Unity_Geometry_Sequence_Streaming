using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace GeometrySequence.Streaming
{
    public class GeometrySequenceBehaviour : PlayableBehaviour
    {
        public string relativePath = "";
        public string absolutePath = "";
        public GeometrySequenceStream.PathType pathRelation;
        bool loadNewSequence = false;
        bool streamIsReady = false;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            GeometrySequenceStream stream = playerData as GeometrySequenceStream;

            if (!Application.isPlaying)
                return;

            //Load our clip if it hasn't been loaded yet
            if (loadNewSequence)
            {
                if (relativePath != string.Empty)
                {
                    streamIsReady = stream.ChangeSequence(absolutePath, 30);
                }

                loadNewSequence = false;
            }
            
            double currentTime = playable.GetTime<Playable>();

            if(streamIsReady)
                stream.UpdateFrame((float)currentTime * 1000);
        }

        //Playback start
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {

            //Set the correct absolute path depending on the files location
            if (pathRelation == GeometrySequenceStream.PathType.RelativeToDataPath)
                absolutePath = Path.Combine(Application.dataPath, relativePath);

            if (pathRelation == GeometrySequenceStream.PathType.RelativeToStreamingAssets)
                absolutePath = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (pathRelation == GeometrySequenceStream.PathType.AbsolutePath)
                absolutePath = relativePath;

            loadNewSequence = true;

            base.OnBehaviourPlay(playable, info);
        }
    }
}

