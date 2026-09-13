using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DropletPrototype
{
    [TrackColor(.15f, .63f, .78f)]
    [TrackClipType(typeof(NarrativeBroadcastClip))]
    [TrackBindingType(typeof(NarrativeApproachController))]
    public sealed class NarrativeBroadcastTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<NarrativeBroadcastMixer>.Create(graph, inputCount);
    }
}
