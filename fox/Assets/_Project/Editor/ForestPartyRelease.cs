using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class ForestPartyRelease
{
    public static void ValidateAndBuild()
    {
        PlayerSettings.runInBackground = false;
        Material smoke = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/ForestParty/GhostSmoke.mat");
        if (smoke != null)
        {
            Color tint = smoke.GetColor("_BaseColor");
            tint.a = 1;
            smoke.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(smoke);
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/StartScene.unity");
        AssetDatabase.SaveAssets();
        GameplayValidation.Validate();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity");
        FoxEscortQuest quest = UnityEngine.Object.FindObjectOfType<FoxEscortQuest>();
        foreach (PlayableDirector director in new[] { quest.cinematic.intro, quest.cinematic.reunion })
        {
            var timeline = (TimelineAsset)director.playableAsset;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is StorySubtitleTrack)
                {
                    foreach (TimelineClip clip in track.GetClips())
                    {
                        var subtitle = (StorySubtitleClip)clip.asset;
                        if (clip.duration < subtitle.subtitle.Length / 22f + 2)
                            throw new Exception("Subtitle retention shorter than two seconds: " + subtitle.subtitle);
                    }
                }
                else if (director.GetGenericBinding(track) == null)
                    throw new Exception("Unbound Timeline track: " + director.name + " / " + track.name);
            }
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/StartScene.unity");
        ProjectValidation.ValidateAndBuild();
        File.WriteAllText("Docs/forest-release-validation.txt", "PASS: gameplay references, connected escort positions, Timeline bindings, subtitle retention, full reference audit and Windows development build.\n");
    }
}
