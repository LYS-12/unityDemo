using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(.55f, .76f, .70f), TrackClipType(typeof(StoryActorClip)), TrackBindingType(typeof(Transform))]
public class StoryActorTrack : TrackAsset
{
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
        Transform actor = director.GetGenericBinding(this) as Transform;
        if (actor == null)
            return;
        // Timeline restores these properties when leaving editor preview.
        // Runtime choreography still submits its explicit final state once.
        foreach (string property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                     "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" })
            driver.AddFromName<Transform>(actor.gameObject, property);
    }
}
