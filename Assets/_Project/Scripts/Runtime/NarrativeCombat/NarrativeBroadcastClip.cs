using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DropletPrototype
{
    [System.Serializable]
    public sealed class NarrativeBroadcastClip : PlayableAsset, ITimelineClipAsset
    {
        public int lineIndex;
        public bool enableAutopilot = true;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<NarrativeBroadcastBehaviour>.Create(graph);
            playable.GetBehaviour().lineIndex = lineIndex;
            playable.GetBehaviour().enableAutopilot = enableAutopilot;
            return playable;
        }
    }
    public sealed class NarrativeBroadcastBehaviour : PlayableBehaviour { public int lineIndex; public bool enableAutopilot; }
    public sealed class NarrativeBroadcastMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is NarrativeApproachController controller) || !controller.IsActive) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0) continue;
                var input = (ScriptPlayable<NarrativeBroadcastBehaviour>)playable.GetInput(i);
                var command = input.GetBehaviour();
                controller.ApplyCue(command.lineIndex, command.enableAutopilot);
            }
        }
    }
}
