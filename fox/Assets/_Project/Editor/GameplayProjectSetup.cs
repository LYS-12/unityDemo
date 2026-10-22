using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>显式执行的功能资源与场景配置工具，不在导入时自动改动工程。</summary>
public static class GameplayProjectSetup
{
    private const string Root = "Assets/_Project";
    private const string CatalogPath = Root + "/Resources/Gameplay/GameplayCatalog.asset";

    [MenuItem("Tools/Fox/Configure Inventory And Combat")]
    public static void Configure()
    {
        if (AssetDatabase.LoadAssetAtPath<UITheme>(Root + "/Resources/ForestParty/UITheme.asset") != null)
        {
            ForestPartySetup.Configure();
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EnsureFolders();
        ConfigureLayers();
        ConfigureItems();
        GameplayCatalog catalog = AssetDatabase.LoadAssetAtPath<GameplayCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<GameplayCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.startingItems = new[] { Item("Butterfly"), Item("Tomato"), Item("Cheese"), Item("Cabbage") };
        catalog.hudPrefab = CreateHUD();
        catalog.explosionMaterial = CreateExplosionMaterial();
        EditorUtility.SetDirty(catalog);
        EditorSceneManager.OpenScene(Root + "/Scenes/GameScene1.unity", OpenSceneMode.Single);
        RockEnemy enemy = CreateEnemy();
        for (int level = 1; level <= 3; level++)
            ConfigureScene(level, catalog, enemy);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Root + "/Scenes/StartScene.unity", OpenSceneMode.Single);
        File.WriteAllText("Docs/gameplay-setup-result.txt", "Configured 6 item definitions, HUD, independent rock enemy assets, 3 gameplay scenes and first-level navigation.\n");
        Debug.Log("Inventory/combat project setup complete.");
    }

    private static void EnsureFolders()
    {
        foreach (string path in new[] { "Resources/Gameplay", "Prefabs/Gameplay", "Prefabs/Enemies", "Art/Models/Enemies",
                     "Art/Materials/Enemies", "Art/Materials/Gameplay", "Art/Textures/Enemies", "Art/Animations/Enemies", "Art/Sprites/Items", "Fonts/Gameplay", "Data/Navigation" })
            EnsureFolder(Root + "/" + path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void ConfigureLayers()
    {
        var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = manager.FindProperty("layers");
        foreach (string name in new[] { "PlayerCombat", "Enemy", "WorldItem", "ItemProjectile" })
        {
            if (LayerMask.NameToLayer(name) >= 0)
                continue;
            int slot = -1;
            for (int index = 8; index < layers.arraySize; index++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
                {
                    slot = index;
                    break;
                }
            if (slot < 0)
                throw new Exception("No free physics layer for " + name);
            layers.GetArrayElementAtIndex(slot).stringValue = name;
            manager.ApplyModifiedPropertiesWithoutUndo();
        }
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("ItemProjectile"), LayerMask.NameToLayer("ItemProjectile"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("ItemProjectile"), LayerMask.NameToLayer("WorldItem"), true);
    }

    private static KitchenObjectSO Item(string name) => AssetDatabase.LoadAssetAtPath<KitchenObjectSO>(Root + "/Data/Items/" + name + ".asset");

    private static void ConfigureItems()
    {
        KitchenObjectSO cabbage = Item("Cabbage");
        if (cabbage == null)
        {
            cabbage = ScriptableObject.CreateInstance<KitchenObjectSO>();
            AssetDatabase.CreateAsset(cabbage, Root + "/Data/Items/Cabbage.asset");
            var item = new GameObject("Cabbage");
            item.layer = LayerMask.NameToLayer("WorldItem");
            KitchenObject component = item.AddComponent<KitchenObject>();
            SetReference(component, "kitchenObjectSO", cabbage);
            SphereCollider collider = item.AddComponent<SphereCollider>();
            collider.radius = 0.35f;
            item.AddComponent<WorldItemPickup>();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Visuals/KitchenAssets/KitchenObjectsVisuals/Cabbage_Visual.prefab");
            GameObject visual = Object.Instantiate(source, item.transform, false);
            visual.name = "CabbageVisual";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(item, Root + "/Prefabs/Items/Cabbage.prefab");
            Object.DestroyImmediate(item);
            SetReference(cabbage, "prefab", prefab);
            SetReference(cabbage, "sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Textures/KitchenAssets/Icons/Cabbage.png"));
            SetString(cabbage, "objectName", "白菜");
        }
        string[] names = { "Butterfly", "Tomato", "Cheese", "Cabbage", "Fox", "Bread" };
        ItemEffectKind[] effects = { ItemEffectKind.SpeedBoost, ItemEffectKind.Health, ItemEffectKind.Shield,
            ItemEffectKind.Explosion, ItemEffectKind.None, ItemEffectKind.None };
        for (int index = 0; index < names.Length; index++)
        {
            KitchenObjectSO definition = Item(names[index]);
            if (definition == null || definition.Prefab == null)
                throw new Exception("Invalid item " + names[index]);
            definition.effect = effects[index];
            definition.canThrow = index < 4;
            definition.maxDurability = 3;
            if (definition.Sprite == null)
                SetReference(definition, "sprite", CreateThumbnail(definition));
            EditorUtility.SetDirty(definition);
            string path = AssetDatabase.GetAssetPath(definition.Prefab);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            if (prefab.GetComponent<WorldItemPickup>() == null)
                prefab.AddComponent<WorldItemPickup>();
            prefab.layer = LayerMask.NameToLayer("WorldItem");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    [MenuItem("Tools/Fox/Refresh Item Icons")]
    public static void RefreshIcons()
    {
        foreach (string name in new[] { "Butterfly", "Fox" })
        {
            KitchenObjectSO definition = Item(name);
            SetReference(definition, "sprite", CreateThumbnail(definition, true));
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets();
    }

    private static Sprite CreateThumbnail(KitchenObjectSO definition, bool force = false)
    {
        string path = Root + "/Art/Sprites/Items/" + definition.name + ".png";
        if (force || !File.Exists(path))
        {
            var preview = new PreviewRenderUtility();
            GameObject instance = Object.Instantiate(definition.Prefab);
            instance.transform.position = Vector3.zero;
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
                light.enabled = false;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>().Where(renderer => !(renderer is ParticleSystemRenderer)).ToArray();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            instance.transform.position -= bounds.center;
            preview.AddSingleGO(instance);
            preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.clear;
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = Mathf.Max(bounds.extents.magnitude, 0.4f) * 1.12f;
            preview.camera.aspect = 1f;
            preview.camera.transform.position = new Vector3(2f, 1.4f, -3f).normalized * 8f * Mathf.Max(1f, bounds.extents.magnitude);
            preview.camera.transform.LookAt(Vector3.zero);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 1000f;
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
            preview.lights[1].intensity = 0.6f;
            preview.Render(true, false);
            Texture result = preview.EndPreview();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = result as RenderTexture;
            var output = new Texture2D(result.width, result.height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0, 0, result.width, result.height), 0, 0);
            output.Apply();
            Color32[] pixels = output.GetPixels32();
            int left = output.width, bottom = output.height, right = 0, top = 0;
            for (int y = 0; y < output.height; y++)
                for (int x = 0; x < output.width; x++)
                    if (pixels[y * output.width + x].a > 32)
                    {
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
            int size = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(right - left, top - bottom) * 1.2f), 32, Mathf.Min(output.width, output.height));
            left = Mathf.Clamp((left + right - size) / 2, 0, output.width - size);
            bottom = Mathf.Clamp((bottom + top - size) / 2, 0, output.height - size);
            var framed = new Texture2D(size, size, TextureFormat.RGBA32, false);
            framed.ReadPixels(new Rect(left, bottom, size, size), 0, 0);
            framed.Apply();
            File.WriteAllBytes(path, framed.EncodeToPNG());
            Object.DestroyImmediate(framed);
            Object.DestroyImmediate(output);
            RenderTexture.active = previous;
            preview.Cleanup();
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TMP_FontAsset CreateFont()
    {
        string path = Root + "/Fonts/Gameplay/GameplayChinese.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
        {
            if (TMP_Settings.defaultFontAsset != null && !existing.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))
                existing.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        Font source = AssetDatabase.FindAssets("t:Font", new[] { Root + "/Fonts" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Font>)
            .OrderByDescending(font => font.name.Contains("heiti")).FirstOrDefault();
        if (source == null)
            throw new Exception("No source font for gameplay UI.");
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(source, 36, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fontAsset.name = "GameplayChinese";
        if (TMP_Settings.defaultFontAsset != null)
            fontAsset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        AssetDatabase.CreateAsset(fontAsset, path);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (Texture2D texture in fontAsset.atlasTextures)
            AssetDatabase.AddObjectToAsset(texture, fontAsset);
        fontAsset.TryAddCharacters("生命背包行囊选择一件拿在手中每道具独立保存耐久关闭收起随身物品空打开左右键使用投掷任务可以放到台面上拾取护盾加速秒怪存活旅程暂歇重试将恢复初始与返回游戏本关菜单血量已满这是不能 /：·[]0123456789ERTabEsc蝴蝶番茄芝士白菜狐狸面", out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning("Gameplay font missing characters: " + missing);
        EditorUtility.SetDirty(fontAsset);
        return fontAsset;
    }

    private static GameObject CreateHUD()
    {
        var root = new GameObject("GameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(GameplayHUD));
        root.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        root.GetComponent<GameplayHUD>().font = CreateFont();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Gameplay/GameplayHUD.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material CreateExplosionMaterial()
    {
        string path = Root + "/Art/Materials/Gameplay/CabbageExplosion.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = new Color(0.72f, 0.91f, 0.25f);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static RockEnemy CreateEnemy()
    {
        // Rock enemies own their assets. Reconfiguring after a player art replacement
        // must never recopy the ghost or overwrite the independent rock controller.
        RockEnemy authored = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Enemies/RockEnemy.prefab")?.GetComponent<RockEnemy>();
        if (authored != null)
            return authored;
        string materialPath = Root + "/Art/Materials/Enemies/RockBody.mat";
        if (!File.Exists(materialPath))
            AssetDatabase.CopyAsset("Assets/ThirdParty/Quibli/Demos/Nature/Materials/NatureScene_Rock.mat", materialPath);
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        foreach (string property in stone.GetTexturePropertyNames())
        {
            Texture texture = stone.GetTexture(property);
            if (texture == null)
                continue;
            string original = AssetDatabase.GetAssetPath(texture);
            if (!original.StartsWith("Assets/ThirdParty/"))
                continue;
            string copied = Root + "/Art/Textures/Enemies/" + Path.GetFileName(original);
            if (!File.Exists(copied))
                AssetDatabase.CopyAsset(original, copied);
            stone.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture>(copied));
        }
        stone.SetColor("_BaseColor", new Color(0.43f, 0.46f, 0.42f));
        if (stone.HasProperty("_Smoothness"))
            stone.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(stone);
        var root = new GameObject("RockEnemy");
        root.SetActive(false);
        root.layer = LayerMask.NameToLayer("Enemy");
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.65f;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.height = 2f;
        agent.radius = 0.65f;
        agent.speed = 3.5f;
        agent.angularSpeed = 480f;
        agent.enabled = false;
        root.AddComponent<RockEnemy>();
        // 使用场景实际运行的视觉和控制器，包含对基础预制体的动画覆盖。
        Animator playerAnimator = Object.FindObjectOfType<Player>().GetComponentInChildren<Animator>(true);
        GameObject playerVisual = playerAnimator.gameObject;
        GameObject visual = Object.Instantiate(playerVisual, root.transform, false);
        visual.name = "RockVisual";
        if (PrefabUtility.IsPartOfPrefabInstance(visual))
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (MonoBehaviour script in visual.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(script);
        var meshes = new Dictionary<Mesh, Mesh>();
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            if (!meshes.TryGetValue(filter.sharedMesh, out Mesh copied))
            {
                string path = Root + "/Art/Models/Enemies/Rock_" + filter.sharedMesh.name.Replace('/', '_') + ".asset";
                copied = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (copied == null)
                {
                    copied = Object.Instantiate(filter.sharedMesh);
                    AssetDatabase.CreateAsset(copied, path);
                }
                meshes.Add(filter.sharedMesh, copied);
            }
            filter.sharedMesh = copied;
        }
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            if (!renderer.name.StartsWith("Eye"))
                renderer.sharedMaterial = stone;
        Animator animator = visual.GetComponent<Animator>();
        string controllerPath = Root + "/Art/Animations/Enemies/RockEnemy.controller";
        string sourceController = AssetDatabase.GetAssetPath(playerAnimator.runtimeAnimatorController);
        if (!File.Exists(controllerPath))
            AssetDatabase.CopyAsset(sourceController, controllerPath);
        else
        {
            File.Copy(sourceController, controllerPath, true);
            AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceUpdate);
        }
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        controller.name = "RockEnemy";
        foreach (AnimatorControllerLayer layer in controller.layers)
            foreach (ChildAnimatorState state in layer.stateMachine.states)
            {
                AnimationClip clip = state.state.motion as AnimationClip;
                if (clip == null)
                    continue;
                string clipPath = Root + "/Art/Animations/Enemies/" + clip.name + ".anim";
                if (!File.Exists(clipPath))
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(clip), clipPath);
                state.state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            }
        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(controller);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Enemies/RockEnemy.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<RockEnemy>();
    }

    private static void ConfigureScene(int level, GameplayCatalog catalog, RockEnemy enemy)
    {
        string scenePath = Root + "/Scenes/GameScene" + level + ".unity";
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Player player = Object.FindObjectOfType<Player>();
        SceneUIRoot canvas = Object.FindObjectOfType<SceneUIRoot>();
        if (player == null || canvas == null)
            throw new Exception("Missing gameplay scene dependencies: " + scenePath);
        player.gameObject.layer = LayerMask.NameToLayer("PlayerCombat");
        player.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
        if (player.GetComponent<PlayerItemController>() == null)
            player.gameObject.AddComponent<PlayerItemController>();
        if (player.GetComponent<PlayerHealth>() == null)
            player.gameObject.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerEffects>() == null)
            player.gameObject.AddComponent<PlayerEffects>();
        GameplayBootstrap bootstrap = Object.FindObjectOfType<GameplayBootstrap>();
        if (bootstrap == null)
            bootstrap = new GameObject("GameplaySystems").AddComponent<GameplayBootstrap>();
        bootstrap.catalog = catalog;
        bootstrap.player = player;
        bootstrap.canvasRoot = canvas.transform;
        if (bootstrap.GetComponent<ItemProjectilePool>() == null)
            bootstrap.gameObject.AddComponent<ItemProjectilePool>();
        if (level == 1)
        {
            EnemySpawner spawner = bootstrap.GetComponent<EnemySpawner>();
            if (spawner == null)
                spawner = bootstrap.gameObject.AddComponent<EnemySpawner>();
            spawner.enemyPrefab = enemy;
            spawner.items = catalog.startingItems;
            spawner.spawnAnchors = Object.FindObjectsOfType<OneShotContainerCounter>()
                .Where(counter => new SerializedObject(counter).FindProperty("kitchenObjectSO").objectReferenceValue == Item("Fox"))
                .OrderBy(counter => counter.transform.position.z).Select(counter => counter.transform).ToArray();
            if (spawner.spawnAnchors.Length != 3)
                throw new Exception("Expected three fox spawn anchors.");
            spawner.navigation = BuildNavigation(player, spawner.spawnAnchors);
        }
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
    }

    public static NavMeshData BuildNavigation(Player player, Transform[] anchors)
    {
        Bounds bounds = new Bounds(player.transform.position, new Vector3(30, 20, 30));
        foreach (Transform anchor in anchors)
            bounds.Encapsulate(new Bounds(anchor.position, new Vector3(30, 20, 30)));
        var sources = new List<NavMeshBuildSource>();
        int excluded = LayerMask.GetMask("PlayerCombat", "Enemy", "WorldItem", "ItemProjectile", "Companion", "DynamicFX", "UI");
        NavMeshBuilder.CollectSources(bounds, ~excluded, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(source => source.component is Collider collider && collider.isTrigger);
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.65f;
        settings.agentHeight = 2f;
        settings.agentClimb = 0.4f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.16f;
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null)
            throw new Exception("Failed to bake navigation.");
        string path = Root + "/Data/Navigation/GameScene1Combat.asset";
        NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(data, existing);
            Object.DestroyImmediate(data);
            data = existing;
        }
        else
            AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);
        NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
        try
        {
            if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                throw new Exception("Player is outside baked navigation.");
            File.WriteAllText("Docs/navigation-build-result.txt", "Sources: " + sources.Count + "\nBounds: " + bounds + "\nPlayer navigation position: " + hit.position + "\n");
        }
        finally { instance.Remove(); }
        return data;
    }

    private static void SetReference(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string name, string value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
