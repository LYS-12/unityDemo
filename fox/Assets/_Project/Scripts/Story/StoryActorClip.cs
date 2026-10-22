using UnityEngine;
using UnityEngine.Playables;

public class StoryActorClip : PlayableAsset
{
    public Vector3 from;
    public Vector3 to;
    public Vector3 facing = Vector3.forward;
    public bool move;
    public Vector3[] pathPoints;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<StoryActorBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.from = from;
        behaviour.to = to;
        behaviour.facing = facing;
        behaviour.move = move;
        behaviour.pathPoints = pathPoints;
        return playable;
    }
}
public class StoryActorBehaviour : PlayableBehaviour
{
    public Vector3 from, to, facing;
    public bool move;
    public Vector3[] pathPoints;
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Transform actor = playerData as Transform;
        if (actor == null)
            return;
        float t = move ? Mathf.SmoothStep(0, 1, (float)(playable.GetTime() / playable.GetDuration())) : 0;
        Vector3 direction = facing;
        if (move && pathPoints != null && pathPoints.Length > 1)
        {
            float length = 0;
            for (int i = 1; i < pathPoints.Length; i++)
                length += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
            float remaining = t * length;
            actor.position = pathPoints[pathPoints.Length - 1];
            for (int i = 1; i < pathPoints.Length; i++)
            {
                float segment = Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                if (remaining <= segment)
                {
                    actor.position = Vector3.Lerp(pathPoints[i - 1], pathPoints[i], remaining / Mathf.Max(.001f, segment));
                    direction = pathPoints[i] - pathPoints[i - 1];
                    break;
                }
                remaining -= segment;
            }
        }
        else
            actor.position = Vector3.Lerp(from, to, t);
        direction.y = 0;
        if (direction.sqrMagnitude > .01f)
            actor.rotation = Quaternion.LookRotation(direction);
    }
}
