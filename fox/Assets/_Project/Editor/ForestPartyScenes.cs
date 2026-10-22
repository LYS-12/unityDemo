using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    [MenuItem("Tools/Forest Party/Configure All Assets And Scenes")]
    public static void Configure()
    {
        File.WriteAllText("Docs/forest-party-setup.txt", "Forest Party authoring, Unity " + Application.unityVersion + "\n");
        Folders();
        Layers();
        UITheme theme = CreateTheme();
        PartyAudioLibrary audio = CreateAudio();
        GameObject ghost = CreateGhost();
        GameObject fox = CreateFoxPrefab();
        CreatePanels(theme);
        ConfigureInput();
        for (int level = 1; level <= 3; level++)
            ConfigureForest(level, ghost, fox, audio);
        EditorSceneManager.OpenScene(Root + "/Scenes/StartScene.unity");
        foreach (VideoPlayer video in Object.FindObjectsOfType<VideoPlayer>(true))
        {
            RawImage background = video.GetComponent<RawImage>();
            if (background != null)
                Object.DestroyImmediate(background);
            Object.DestroyImmediate(video);
        }
        foreach (AudioSource source in Object.FindObjectsOfType<AudioSource>(true))
            source.playOnAwake = false;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
        File.AppendAllText("Docs/forest-party-setup.txt", "Three ghost players; title video bindings removed; independent rock enemies retained.\nAuthoring completed.\n");
        Debug.Log("FOREST_PARTY_SETUP_COMPLETE");
    }
    private static void Layers()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = settings.FindProperty("layers");
        layers.GetArrayElementAtIndex(12).stringValue = "Companion";
        layers.GetArrayElementAtIndex(13).stringValue = "DynamicFX";
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Layer(Transform root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }
    private static void ConfigureForest(int level, GameObject ghost, GameObject foxPrefab, PartyAudioLibrary audio)
    {
        EditorSceneManager.OpenScene(Root + "/Scenes/GameScene" + level + ".unity");
        Player player = Object.FindObjectOfType<Player>();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.63f, .71f, .65f);
        foreach (Light light in Object.FindObjectsOfType<Light>())
            if (light.type == LightType.Directional)
                light.shadowStrength = .62f;
        foreach (Volume volume in Object.FindObjectsOfType<Volume>())
        {
            if (volume.sharedProfile == null)
                continue;
            string profilePath = Root + "/Resources/ForestParty/Scene" + level + "Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile), profilePath);
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            }
            if (profile.TryGet(out DepthOfField depth))
                depth.active = false;
            if (profile.TryGet(out ShadowsMidtonesHighlights grading))
                grading.active = false;
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.overrideState = true;
                color.postExposure.value = .35f;
            }
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
        }
        foreach (PlayerAnimator old in player.GetComponentsInChildren<PlayerAnimator>(true))
        {
            Transform visual = old.transform;
            // Visual roots have no gameplay children. The hold point belongs to the physical player.
            if (visual != player.transform)
                Object.DestroyImmediate(visual.gameObject);
        }
        foreach (Animator old in player.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(old.gameObject);
        GameObject visualGhost = (GameObject)PrefabUtility.InstantiatePrefab(ghost, player.transform);
        visualGhost.transform.localPosition = Vector3.zero;
        visualGhost.transform.localRotation = Quaternion.identity;
        visualGhost.transform.localScale = Vector3.one * .8f;
        Layer(visualGhost.transform, LayerMask.NameToLayer("PlayerCombat"));
        Set(visualGhost.GetComponentInChildren<PlayerAnimator>(), "player", player);
        Transform hold = player.GetHoldPoint();
        if (hold != null)
            hold.localPosition = new Vector3(0, 1.1f, 1.05f);
        OverheadHealthBar bar = player.GetComponent<OverheadHealthBar>();
        if (bar == null)
            bar = player.gameObject.AddComponent<OverheadHealthBar>();
        bar.actorName = "小幽";
        bar.height = 2.55f;
        foreach (CameraMove follow in Object.FindObjectsOfType<CameraMove>())
            follow.GetComponent<Camera>().enabled = false;
        GameObject oldLeaves = GameObject.Find("ForestAtmosphere");
        if (oldLeaves != null)
            Object.DestroyImmediate(oldLeaves);
        if (level == 1)
            ConfigureQuest(player, foxPrefab, audio);
        CreateLeaves(level);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        // Add the bar to the independent enemy prefab, preserving its GUID and model/controller.
        string enemyPath = Root + "/Prefabs/Enemies/RockEnemy.prefab";
        GameObject enemy = PrefabUtility.LoadPrefabContents(enemyPath);
        var enemyBar = enemy.GetComponent<OverheadHealthBar>();
        if (enemyBar == null)
            enemyBar = enemy.AddComponent<OverheadHealthBar>();
        enemyBar.height = 2.7f;
        enemyBar.fillColor = new Color(.89f, .52f, .43f);
        Layer(enemy.transform, LayerMask.NameToLayer("Enemy"));
        PrefabUtility.SaveAsPrefabAsset(enemy, enemyPath);
        PrefabUtility.UnloadPrefabContents(enemy);
    }
    private static GameObject CreateFoxPrefab()
    {
        var root = new GameObject("ForestFox");
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Characters/Variants/Objects/fox.prefab");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(original, root.transform);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = "FoxVisual";
        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        float scale = 1.55f / Mathf.Max(.01f, bounds.size.y);
        visual.transform.localScale *= scale;
        visual.transform.localPosition = new Vector3(0, -bounds.min.y * scale, 0);
        Animator animator = visual.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        string controllerPath = Root + "/Art/Animations/FoxNPC/FoxCompanion.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
            AnimationClip idle = FoxMotion("FoxIdle", .035f, 1.8f), walk = FoxMotion("FoxRun", .13f, .45f);
            var a = controller.AddMotion(idle);
            var b = controller.AddMotion(walk);
            controller.layers[0].stateMachine.defaultState = a;
            var ab = a.AddTransition(b);
            ab.hasExitTime = false;
            ab.duration = .2f;
            ab.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
            var ba = b.AddTransition(a);
            ba.hasExitTime = false;
            ba.duration = .2f;
            ba.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = .45f;
        agent.height = 1.6f;
        agent.baseOffset = 0;
        agent.stoppingDistance = 1.1f;
        agent.angularSpeed = 540;
        agent.acceleration = 18;
        agent.enabled = false;
        var colliderBody = root.AddComponent<CapsuleCollider>();
        colliderBody.center = Vector3.up * .7f;
        colliderBody.radius = .42f;
        colliderBody.height = 1.4f;
        var rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        root.AddComponent<FoxCompanion>();
        var bar = root.AddComponent<OverheadHealthBar>();
        bar.height = 1.9f;
        bar.fillColor = new Color(.96f, .68f, .39f);
        Layer(root.transform, LayerMask.NameToLayer("Companion"));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Characters/FoxNPC/ForestFox.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
    private static AnimationClip FoxMotion(string name, float bounce, float duration)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.EaseInOut(0, 0, duration * .5f, bounce));
        clip.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(new Keyframe(0, 0), new Keyframe(duration * .5f, bounce), new Keyframe(duration, 0)));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return Save(clip, Root + "/Art/Animations/FoxNPC/" + name + ".anim");
    }
    private static void ConfigureQuest(Player player, GameObject foxPrefab, PartyAudioLibrary audio)
    {
        FoxEscortQuest old = Object.FindObjectOfType<FoxEscortQuest>();
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Vector3[] search = Object.FindObjectsOfType<OneShotContainerCounter>(true)
            .Where(counter => new SerializedObject(counter).FindProperty("kitchenObjectSO").objectReferenceValue == AssetDatabase.LoadAssetAtPath<KitchenObjectSO>(Root + "/Data/Items/Fox.asset"))
            .OrderBy(counter => counter.transform.position.z).Select(counter => counter.transform.position).ToArray();
        if (search.Length != 3)
        {
            // The disabled source objects are intentionally retained as the migration record.
            search = new[] { new Vector3(4.91f, 0, -10.14f), new Vector3(7.54f, 0, 5.51f), new Vector3(.48f, 0, 13.95f) };
        }
        foreach (FoxPlacementGoal goal in Object.FindObjectsOfType<FoxPlacementGoal>(true))
            Object.DestroyImmediate(goal);
        foreach (OneShotContainerCounter source in Object.FindObjectsOfType<OneShotContainerCounter>(true))
        {
            var item = (KitchenObjectSO)new SerializedObject(source).FindProperty("kitchenObjectSO").objectReferenceValue;
            if (item != null && item.name == "Fox")
            {
                source.gameObject.name = "Archived_FoxPickup_" + source.gameObject.name;
                source.gameObject.SetActive(false);
            }
        }
        var root = new GameObject("ForestPartyQuest");
        var quest = root.AddComponent<FoxEscortQuest>();
        var party = new GameObject("PartyClearing").transform;
        party.SetParent(root.transform);
        party.position = new Vector3(13, 0, -17);
        quest.partyPoint = party;
        CreateParty(party);
        quest.foxes = new FoxCompanion[3];
        var anchors = new Transform[3];
        var slots = new Transform[3];
        string[] names = { "栗栗", "松松", "团团" };
        string[][] dialogue = { new[] { "你追过来……是想吓我吗？", "不是的，对不起。我只是想和你们一起玩。", "那……我们慢慢走，一起回去吧。" }, new[] { "刚才那声‘呜——’，我还以为……", "是我太紧张了。下次我会先说‘你好’。", "好呀。回去的路上，你可以陪着我吗？" }, new[] { "我跑得太快，连点心都没拿。", "我们一起回去吧，点心应该还在。", "那就说好了！我们一起回派对。" } };
        for (int i = 0; i < 3; i++)
        {
            var anchor = new GameObject("SearchPoint_" + names[i]).transform;
            anchor.SetParent(root.transform);
            anchor.position = search[i];
            anchors[i] = anchor;
            var slot = new GameObject("Arrival_" + names[i]).transform;
            slot.SetParent(party);
            slot.localPosition = new Vector3((i - 1) * 1.7f, 0, i == 1 ? 1.7f : -.8f);
            slots[i] = slot;
            var fox = (GameObject)PrefabUtility.InstantiatePrefab(foxPrefab, root.transform);
            fox.name = names[i];
            fox.transform.position = search[i];
            var companion = fox.GetComponent<FoxCompanion>();
            companion.formationIndex = i;
            companion.arrivalPoint = slot;
            quest.foxes[i] = companion;
            var config = ScriptableObject.CreateInstance<FoxDialogue>();
            config.foxName = names[i];
            config.apology = new[] { new DialogueLine(names[i], dialogue[i][0]), new DialogueLine("小幽", dialogue[i][1]), new DialogueLine(names[i], dialogue[i][2]) };
            companion.dialogue = Save(config, Root + "/Data/Story/Fox" + i + "Dialogue.asset");
        }
        var spawner = Object.FindObjectOfType<EnemySpawner>();
        spawner.spawnAnchors = anchors;
        Physics.SyncTransforms();
        spawner.navigation = GameplayProjectSetup.BuildNavigation(player, anchors.Concat(new[] { party }).ToArray());
        NavMeshDataInstance navigation = NavMesh.AddNavMeshData(spawner.navigation);
        try
        {
            if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerPoint, 3, NavMesh.AllAreas))
                throw new Exception("Player start outside navigation.");
            for (int i = 0; i < 3; i++)
            {
                search[i] = ConnectedPoint(search[i], playerPoint.position);
                anchors[i].position = search[i];
                quest.foxes[i].transform.position = search[i];
                slots[i].position = ConnectedPoint(slots[i].position, playerPoint.position);
            }
        }
        finally { navigation.Remove(); }
        var story = root.AddComponent<StoryCinematic>();
        quest.cinematic = story;
        story.foxes = quest.foxes;
        story.searchPositions = search;
        story.partySlots = slots;
        var ghostSpot = new GameObject("GhostPartySpot").transform;
        ghostSpot.SetParent(party);
        ghostSpot.localPosition = new Vector3(0, 0, -2.4f);
        story.ghostPartySpot = ghostSpot;
        CreateTimelines(story, player, party, audio);
        File.AppendAllText("Docs/forest-party-setup.txt", "Level 1: removed old FoxPlacementGoal; disabled 3 old Fox pickups; authored 3 NPCs, party and two Timelines.\n");
    }
    private static Vector3 ConnectedPoint(Vector3 desired, Vector3 reference)
    {
        float best = float.PositiveInfinity;
        Vector3 result = desired;
        for (int x = -10; x <= 10; x++)
            for (int z = -10; z <= 10; z++)
            {
                Vector3 candidate = desired + new Vector3(x * .5f, 0, z * .5f);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, .45f, NavMesh.AllAreas))
                    continue;
                float cost = (hit.position - desired).sqrMagnitude;
                if (cost >= best)
                    continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(hit.position, reference, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                best = cost;
                result = hit.position;
            }
        if (float.IsPositiveInfinity(best))
            throw new Exception("No connected escort position within five metres: " + desired);
        File.AppendAllText("Docs/forest-party-setup.txt", "Connected escort point: " + desired + " -> " + result + "\n");
        return result;
    }
}
