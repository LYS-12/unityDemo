using UnityEngine;
using UnityEngine.Playables;

public class StorySubtitleClip : PlayableAsset
{
    public string speaker;
    [TextArea(2, 5)] public string subtitle;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<StorySubtitleBehaviour>.Create(graph);
        playable.GetBehaviour().line = new DialogueLine(speaker, subtitle);
        return playable;
    }
}

public class StorySubtitleBehaviour : PlayableBehaviour
{
    public DialogueLine line;
    private bool shown;
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || !StoryCinematic.IsPlaying || shown)
            return;
        shown = true;
        DialogueView.Ensure().ShowLine(line, true);
    }
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (shown && Application.isPlaying && DialogueView.Current != null)
            DialogueView.Current.HideSubtitle();
        shown = false;
    }
}
