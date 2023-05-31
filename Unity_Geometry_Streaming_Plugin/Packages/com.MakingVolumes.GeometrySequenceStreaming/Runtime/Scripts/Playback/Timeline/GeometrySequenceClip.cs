using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace GeometrySequence.Streaming
{    public class GeometrySequenceClip : PlayableAsset
    {
        public string relativePath;
        public GeometrySequenceStream.PathType pathRelation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<GeometrySequenceBehaviour> playable = ScriptPlayable<GeometrySequenceBehaviour>.Create(graph);

            GeometrySequenceBehaviour geoSequenceBehaviour = playable.GetBehaviour();
            geoSequenceBehaviour.relativePath = relativePath;
            geoSequenceBehaviour.pathRelation = pathRelation;

            return playable;
        }
    }
}

