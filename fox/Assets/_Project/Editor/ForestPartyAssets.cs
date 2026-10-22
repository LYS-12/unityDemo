using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    private static T Save<T>(T value, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(value, path);
            return value;
        }
        EditorUtility.CopySerialized(value, existing);
        Object.DestroyImmediate(value);
        EditorUtility.SetDirty(existing);
        return existing;
    }
    private static void Folders()
    {
        foreach (string path in new[] { "Resources/ForestParty", "Art/Sprites/ForestParty", "Art/Materials/Ghost", "Art/Materials/ForestParty", "Art/Models/Ghost", "Art/Models/ForestParty", "Art/Animations/Ghost", "Art/Animations/FoxNPC", "Prefabs/Characters/Ghost", "Prefabs/Characters/FoxNPC", "Data/Story", "Data/Audio", "Timelines", "Fonts/ForestParty" })
            Directory.CreateDirectory(Root + "/" + path);
        AssetDatabase.Refresh();
    }
    private static UITheme CreateTheme()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(Root + "/Resources/ForestParty/UITheme.asset");
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, Root + "/Resources/ForestParty/UITheme.asset");
        }
        theme.bodyFont = FontAsset("NotoSansSC", "Assets/ThirdParty/ForestPartyFonts/NotoSansSC/NotoSansSC-Regular.ttf");
        theme.headingFont = FontAsset("ZCOOLKuaiLe", "Assets/ThirdParty/ForestPartyFonts/ZCOOLKuaiLe/ZCOOLKuaiLe-Regular.ttf");
        if (!theme.headingFont.fallbackFontAssetTable.Contains(theme.bodyFont))
            theme.headingFont.fallbackFontAssetTable.Add(theme.bodyFont);
        theme.roundedCard = ShapeSprite("RoundedCard", false);
        theme.circle = ShapeSprite("Circle", true);
        theme.playerArrow = ArrowSprite();
        string art = Root + "/Art/Textures/ForestParty/ForestPartyTitle.png";
        var importer = AssetImporter.GetAtPath(art) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        theme.titleArt = AssetDatabase.LoadAssetAtPath<Sprite>(art);
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return theme;
    }
    private static TMP_FontAsset FontAsset(string name, string source)
    {
        string path = Root + "/Fonts/ForestParty/" + name + ".asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null && font.material != null)
            return font;
        TMP_FontAsset existing = font;
        Font file = AssetDatabase.LoadAssetAtPath<Font>(source);
        if (file == null)
            throw new Exception("Missing licensed font: " + source);
        font = TMP_FontAsset.CreateFontAsset(file, 42, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = name;
        font.isMultiAtlasTexturesEnabled = true;
        Material material = font.material;
        Texture2D[] atlases = font.atlasTextures;
        if (existing == null)
            AssetDatabase.CreateAsset(font, path);
        else
        {
            EditorUtility.CopySerialized(font, existing);
            Object.DestroyImmediate(font);
            font = existing;
        }
        AssetDatabase.AddObjectToAsset(material, font);
        foreach (var atlas in atlases)
            AssetDatabase.AddObjectToAsset(atlas, font);
        if (font.fallbackFontAssetTable == null)
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (TMP_Settings.defaultFontAsset != null)
            font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        string characters = string.Concat(Directory.GetFiles(Root + "/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        characters = new string(characters.Where(c => c >= '\u3000' || c >= 32 && c <= 126).Distinct().ToArray());
        font.TryAddCharacters(characters, out string missing);
        File.AppendAllText("Docs/forest-party-setup.txt", name + " prewarmed characters=" + characters.Length + ", missing=" + missing + "\n");
        EditorUtility.SetDirty(font);
        return font;
    }
    private static Sprite ShapeSprite(string name, bool circle)
    {
        string path = Root + "/Art/Sprites/ForestParty/" + name + ".png";
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                Vector2 p = new Vector2(x - 63.5f, y - 63.5f);
                float distance = circle ? p.magnitude - 62 : new Vector2(Mathf.Max(Mathf.Abs(p.x) - 39, 0), Mathf.Max(Mathf.Abs(p.y) - 39, 0)).magnitude - 24;
                texture.SetPixel(x, y, new Color(1, 1, 1, 1 - Mathf.Clamp01(distance + .5f)));
            }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteBorder = circle ? Vector4.zero : Vector4.one * 32;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite ArrowSprite()
    {
        string path = Root + "/Art/Sprites/ForestParty/PlayerArrow.png";
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                texture.SetPixel(x, y, new Color(1, 1, 1, y >= 8 && y <= 58 && Mathf.Abs(x - 31.5f) <= (58 - y) * .44f ? 1 : 0));
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Material Mat(string name, Color color, bool transparent = false)
    {
        string path = Root + "/Art/Materials/ForestParty/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(transparent ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        if (transparent)
            material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (transparent && (name.Contains("Sparkle") || name.Contains("Firefly")))
            material.SetTexture("_BaseMap", UITheme.Current.circle.texture);
        material.SetColor("_BaseColor", color);
        if (transparent)
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", 5);
            material.SetFloat("_DstBlend", 10);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        material.SetFloat("_Cull", 0);
        EditorUtility.SetDirty(material);
        return material;
    }
    private static void Set(Object target, string name, Object reference)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(name);
        if (property == null)
            throw new Exception(target.GetType() + " missing " + name);
        property.objectReferenceValue = reference;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Mesh LeafMesh()
    {
        var mesh = new Mesh { name = "CurvedLeaf" };
        mesh.vertices = new[] { new Vector3(0, 0, -.6f), new Vector3(-.35f, .05f, -.12f), new Vector3(-.24f, .08f, .26f), new Vector3(0, .12f, .62f), new Vector3(.24f, .08f, .26f), new Vector3(.35f, .05f, -.12f), new Vector3(0, .15f, 0) };
        mesh.triangles = new[] { 6, 1, 0, 6, 2, 1, 6, 3, 2, 6, 4, 3, 6, 5, 4, 6, 0, 5 };
        mesh.RecalculateNormals();
        return Save(mesh, Root + "/Art/Models/ForestParty/CurvedLeaf.asset");
    }
    private static Mesh SmokeMesh()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        // Flat tapering curled ribbon, inspired by the supplied hand-drawn smoke shape.
        for (int i = 0; i <= 30; i++)
        {
            float t = i / 30f;
            float angle = t * Mathf.PI * 1.8f;
            float radius = .48f * (1 - t * .7f);
            Vector3 point = new Vector3(Mathf.Cos(angle) * radius, .01f, Mathf.Sin(angle) * radius + .3f * (1 - t));
            Vector3 side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .15f * Mathf.Sin(t * Mathf.PI);
            vertices.Add(point - side);
            vertices.Add(point + side);
            if (i > 0)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a - 2, a - 1, a, a - 1, a + 1, a });
            }
        }
        var mesh = new Mesh { name = "CurlingSmokeRibbon" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return Save(mesh, Root + "/Art/Models/ForestParty/CurlingSmoke.asset");
    }
    private static GameObject CreateGhost()
    {
        string modelPath = Root + "/Art/Models/Ghost/ForestGhost.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var settings = importer.defaultClipAnimations;
        foreach (var setting in settings)
        {
            setting.loopTime = setting.name.Contains("IdleHover") || setting.name.Contains("MoveFloat");
            setting.lockRootPositionXZ = true;
            setting.lockRootHeightY = true;
        }
        importer.clipAnimations = settings;
        importer.SaveAndReimport();
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
        string controllerPath = Root + "/Art/Animations/Ghost/ForestGhost.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "isWalking", type = AnimatorControllerParameterType.Bool } };
        var machine = controller.layers[0].stateMachine;
        foreach (var old in machine.states)
            machine.RemoveState(old.state);
        var states = new Dictionary<string, AnimatorState>();
        foreach (string name in new[] { "IdleHover", "MoveFloat", "Wave", "Startle", "Bow" })
        {
            AnimationClip clip = clips.FirstOrDefault(value => value.name.EndsWith(name));
            if (clip == null)
                throw new Exception("Ghost FBX missing baked action: " + name + "; imported: " + string.Join(",", clips.Select(c => c.name)));
            var state = machine.AddState(name);
            state.motion = clip;
            states.Add(name, state);
        }
        machine.defaultState = states["IdleHover"];
        var walk = states["IdleHover"].AddTransition(states["MoveFloat"]);
        walk.hasExitTime = false;
        walk.duration = .2f;
        walk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        var idle = states["MoveFloat"].AddTransition(states["IdleHover"]);
        idle.hasExitTime = false;
        idle.duration = .2f;
        idle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        foreach (string name in new[] { "Wave", "Startle", "Bow" })
        {
            var back = states[name].AddTransition(states["IdleHover"]);
            back.hasExitTime = true;
            back.exitTime = 1;
            back.duration = .2f;
        }
        EditorUtility.SetDirty(controller);
        Material cloth = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/Ghost/GhostPearl.mat");
        if (cloth == null)
        {
            cloth = new Material(Shader.Find("ForestParty/GhostPearl"));
            AssetDatabase.CreateAsset(cloth, Root + "/Art/Materials/Ghost/GhostPearl.mat");
        }
        var root = new GameObject("GhostVisual");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
        model.name = "GhostRig";
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        model.AddComponent<PlayerAnimator>();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            Material material = renderer.name.Contains("Cloth") ? cloth : renderer.name.Contains("Hem") ? Mat("GhostRoseHem", new Color(.87f, .48f, .76f)) : renderer.name.Contains("Cheek") ? Mat("GhostBlush", new Color(.98f, .52f, .67f)) : renderer.name.Contains("Glint") ? Mat("GhostGlint", new Color(1, .95f, .9f)) : Mat("GhostFace", new Color(.12f, .035f, .16f));
            renderer.sharedMaterial = material;
        }
        GhostMotionEffects fx = root.AddComponent<GhostMotionEffects>();
        fx.sparkleMaterial = Mat("GhostSparkle", new Color(.81f, .7f, 1, .6f), true);
        fx.smokeMaterial = Mat("GhostSmoke", new Color(.89f, .85f, 1, 1), true);
        fx.smokeMesh = SmokeMesh();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Characters/Ghost/GhostVisual.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
