using System;
using Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    private static void CreateTimelines(StoryCinematic story, Player player, Transform party, PartyAudioLibrary audio)
    {
        var navigation = NavMesh.AddNavMeshData(Object.FindObjectOfType<EnemySpawner>().navigation);
        try
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();
            if (brain == null)
                brain = Camera.main.gameObject.AddComponent<CinemachineBrain>();
            var cameras = new CinemachineVirtualCamera[5];
            Vector3[] offsets = { new Vector3(8, 10, -11), new Vector3(-3, 3.5f, -3.8f), new Vector3(0, 5, -6), new Vector3(-7, 10, 3), new Vector3(8, 10, -11) };
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = new GameObject("StoryCamera_" + i).AddComponent<CinemachineVirtualCamera>();
                camera.transform.SetParent(story.transform);
                Vector3 focus = party.position + Vector3.up * 1.2f + (i == 2 ? Vector3.left * 2 + Vector3.back * 2 : Vector3.zero);
                Vector3 position = party.position + offsets[i];
                for (int step = 0; step < 16; step++)
                {
                    bool blocked = false;
                    foreach (var tree in Trees())
                        foreach (var renderer in tree.GetComponentsInChildren<Renderer>())
                            if (renderer.bounds.Contains(position))
                                blocked = true;
                    if (!blocked)
                        break;
                    position = Vector3.Lerp(position, party.position + Vector3.up * 3, .2f);
                }
                camera.transform.position = position;
                camera.transform.LookAt(focus);
                camera.m_Lens.FieldOfView = i == 0 || i == 4 ? 42 : i == 2 ? 65 : 55;
                camera.m_Lens.NearClipPlane = .1f;
                camera.Priority = -100;
                cameras[i] = camera;
            }
            foreach (bool isIntro in new[] { true, false })
            {
                string name = isIntro ? "ForestPartyIntro" : "ForestPartyReunion";
                string path = Root + "/Timelines/" + name + ".playable";
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (timeline == null)
                {
                    timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    AssetDatabase.CreateAsset(timeline, path);
                }
                foreach (var track in timeline.GetRootTracks())
                    timeline.DeleteTrack(track);
                timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
                timeline.fixedDuration = isIntro ? 40 : 24;
                var node = new GameObject(name);
                node.transform.SetParent(story.transform);
                var director = node.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timeline;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.extrapolationMode = DirectorWrapMode.None;
                if (isIntro)
                    story.intro = director;
                else
                    story.reunion = director;
                var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "镜头 / Cinemachine");
                director.SetGenericBinding(cameraTrack, brain);
                double[] cuts = isIntro ? new double[] { 0, 8, 15, 23, 30, 40 } : new double[] { 0, 6, 12, 18, 24 };
                for (int i = 0; i < cuts.Length - 1; i++)
                {
                    TimelineClip clip = cameraTrack.CreateClip<CinemachineShot>();
                    clip.start = cuts[i];
                    clip.duration = cuts[i + 1] - cuts[i];
                    clip.displayName = "镜头 " + (i + 1);
                    clip.blendInDuration = i == 0 ? 0 : .65;
                    var shot = (CinemachineShot)clip.asset;
                    shot.VirtualCamera.exposedName = GUID.Generate().ToString();
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, cameras[isIntro ? i : (i == 3 ? 4 : i)]);
                }
                var subtitles = timeline.CreateTrack<StorySubtitleTrack>(null, "对白 / 可编辑字幕");
                if (isIntro)
                {
                    Subtitle(subtitles, 0, 8, "森林手记", "今晚，森林里有一场只属于朋友的小聚会。");
                    Subtitle(subtitles, 8, 7, "栗栗", "等月亮升起来，我们就一起开动！");
                    Subtitle(subtitles, 15, 8, "小幽", "那个……我也可以加入吗？");
                    Subtitle(subtitles, 23, 7, "松松", "呜哇——幽灵！");
                    Subtitle(subtitles, 30, 10, "小幽", "我只是想说声你好……我得找到他们，好好道个歉。");
                }
                else
                {
                    Subtitle(subtitles, 0, 6, "栗栗", "原来，你只是想一起玩呀。");
                    Subtitle(subtitles, 6, 6, "小幽", "对不起，吓到大家了。谢谢你们愿意听我说。 ");
                    Subtitle(subtitles, 12, 6, "松松", "那就留下来吧！朋友的位置，刚好还空着一个。");
                    Subtitle(subtitles, 18, 6, "森林手记", "今晚的派对，终于到齐了。");
                }
                for (int i = 0; i < 3; i++)
                {
                    Transform fox = story.foxes[i].transform;
                    var track = timeline.CreateTrack<StoryActorTrack>(null, story.foxes[i].DisplayName + " / 走位");
                    director.SetGenericBinding(track, fox);
                    Vector3 atParty = story.partySlots[i].position;
                    if (isIntro)
                    {
                        Motion(track, 0, 23, atParty, atParty, false, party.position - atParty);
                        Motion(track, 23, 7, atParty, story.searchPositions[i], true, story.searchPositions[i] - atParty);
                        Motion(track, 30, 10, story.searchPositions[i], story.searchPositions[i], false, party.position - story.searchPositions[i]);
                    }
                    else
                        Motion(track, 0, 24, atParty, atParty, false, story.ghostPartySpot.position - atParty);
                    var actorAnimation = timeline.CreateTrack<AnimationTrack>(null, story.foxes[i].DisplayName + " / 动画");
                    var animator = fox.GetComponentInChildren<Animator>();
                    director.SetGenericBinding(actorAnimation, animator);
                    var clip = actorAnimation.CreateClip<AnimationPlayableAsset>();
                    var playable = (AnimationPlayableAsset)clip.asset;
                    playable.clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Art/Animations/FoxNPC/FoxIdle.anim");
                    clip.start = 0;
                    clip.duration = isIntro ? 23 : 24;
                    if (isIntro)
                    {
                        var run = actorAnimation.CreateClip<AnimationPlayableAsset>();
                        ((AnimationPlayableAsset)run.asset).clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Art/Animations/FoxNPC/FoxRun.anim");
                        run.start = 23;
                        run.duration = 7;
                    }
                }
                var ghostMotion = timeline.CreateTrack<StoryActorTrack>(null, "小幽 / 走位");
                director.SetGenericBinding(ghostMotion, player.transform);
                Vector3 partyGhost = story.ghostPartySpot.position;
                if (isIntro)
                {
                    Motion(ghostMotion, 0, 15, partyGhost + Vector3.left * 4, partyGhost + Vector3.left * 4, false, Vector3.back);
                    Motion(ghostMotion, 15, 8, partyGhost + Vector3.left * 4, partyGhost, true, Vector3.back);
                    Motion(ghostMotion, 23, 17, partyGhost, partyGhost, false, Vector3.back);
                }
                else
                    Motion(ghostMotion, 0, 24, partyGhost, partyGhost, false, Vector3.back);
                var ghostAnimation = timeline.CreateTrack<AnimationTrack>(null, "小幽 / 表情动作");
                var ghostAnimator = player.GetComponentInChildren<Animator>();
                director.SetGenericBinding(ghostAnimation, ghostAnimator);
                GhostClip(ghostAnimation, ghostAnimator, isIntro ? "IdleHover" : "Bow", 0, isIntro ? 15 : 6);
                if (isIntro)
                {
                    GhostClip(ghostAnimation, ghostAnimator, "Wave", 15, 8);
                    GhostClip(ghostAnimation, ghostAnimator, "Startle", 23, 7);
                    GhostClip(ghostAnimation, ghostAnimator, "Bow", 30, 10);
                }
                else
                    GhostClip(ghostAnimation, ghostAnimator, "Wave", 6, 18);
                if (isIntro)
                {
                    var entrance = timeline.CreateTrack<ActivationTrack>(null, "小幽登场 / 显示");
                    var reveal = entrance.CreateDefaultClip();
                    reveal.start = 15;
                    reveal.duration = 25;
                    director.SetGenericBinding(entrance, player.GetComponentInChildren<GhostMotionEffects>().gameObject);
                    entrance.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
                }
                var audioTrack = timeline.CreateTrack<AudioTrack>(null, "森林音乐 / Audio");
                var audioClip = audioTrack.CreateClip(audio.forestMusic);
                audioClip.start = 0;
                audioClip.duration = isIntro ? 40 : 24;
                audioClip.easeInDuration = 1;
                audioClip.easeOutDuration = 1;
                var source = node.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.ignoreListenerPause = true;
                source.outputAudioMixerGroup = audio.musicGroup;
                source.volume = .23f;
                director.SetGenericBinding(audioTrack, source);
                var soundTrack = timeline.CreateTrack<AudioTrack>(null, "受惊与重聚 / 音效");
                AudioClip cue = audio.sounds[(int)(isIntro ? PartySound.FoxStartle : PartySound.Complete)];
                var cueClip = soundTrack.CreateClip(cue);
                cueClip.start = isIntro ? 23 : 18;
                cueClip.duration = cue.length;
                var cueNode = new GameObject("StorySoundCue");
                cueNode.transform.SetParent(node.transform, false);
                var cueSource = cueNode.AddComponent<AudioSource>();
                cueSource.playOnAwake = false;
                cueSource.ignoreListenerPause = true;
                cueSource.outputAudioMixerGroup = audio.effectsGroup;
                cueSource.volume = .65f;
                director.SetGenericBinding(soundTrack, cueSource);
                var effects = timeline.CreateTrack<ActivationTrack>(null, "派对灯光与落叶 / FX");
                GameObject fx = new GameObject(name + "_Fireflies");
                fx.transform.SetParent(node.transform, false);
                fx.transform.position = party.position + Vector3.up * 2;
                var particles = fx.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.startLifetime = 4;
                main.startSize = .07f;
                main.startSpeed = .25f;
                main.startColor = new Color(1, .84f, .43f, .6f);
                main.useUnscaledTime = true;
                main.maxParticles = 35;
                var emission = particles.emission;
                emission.rateOverTime = 5;
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(7, 3, 5);
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("FireflyGlow", new Color(1, .86f, .45f, .7f), true);
                var activation = effects.CreateDefaultClip();
                activation.start = 0;
                activation.duration = isIntro ? 40 : 24;
                director.SetGenericBinding(effects, fx);
                effects.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
                fx.SetActive(false);
                EditorUtility.SetDirty(timeline);
                EditorUtility.SetDirty(director);
            }
        }
        finally { navigation.Remove(); }
    }
    private static void Subtitle(StorySubtitleTrack track, double start, double duration, string speaker, string text)
    {
        var clip = track.CreateClip<StorySubtitleClip>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = speaker + "：" + text;
        var asset = (StorySubtitleClip)clip.asset;
        asset.speaker = speaker;
        asset.subtitle = text;
    }
    private static void Motion(StoryActorTrack track, double start, double duration, Vector3 from, Vector3 to, bool move, Vector3 facing)
    {
        var clip = track.CreateClip<StoryActorClip>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = move ? "走向朋友" : "停留";
        var asset = (StoryActorClip)clip.asset;
        asset.from = from;
        asset.to = to;
        asset.move = move;
        facing.y = 0;
        asset.facing = facing;
        if (move)
        {
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(from, out NavMeshHit a, 3, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out NavMeshHit b, 3, NavMesh.AllAreas) && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                asset.pathPoints = path.corners;
            else
                throw new Exception("Story movement has no complete navigation route: " + from + " -> " + to);
        }
    }
    private static void GhostClip(AnimationTrack track, Animator animator, string name, double start, double duration)
    {
        var clip = track.CreateClip<AnimationPlayableAsset>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = name;
        clip.blendInDuration = .2;
        ((AnimationPlayableAsset)clip.asset).clip = Array.Find(animator.runtimeAnimatorController.animationClips, value => value.name.EndsWith(name));
    }
}
