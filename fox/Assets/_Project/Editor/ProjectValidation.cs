using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只通过菜单或命令行显式执行；不会在导入时修改或保存场景。</summary>
public static class ProjectValidation
{
    [Serializable]
    private class KnownMissingAsset
    {
        public string path;
        public string guid;
        public string originalPath;
        public string reason;
    }

    [Serializable]
    private class KnownMissingRegistry
    {
        public List<KnownMissingAsset> assets = new List<KnownMissingAsset>();
    }

    [Serializable]
    private class AuditResult
    {
        public string unityVersion;
        public int scenes;
        public int prefabs;
        public int itemDefinitions;
        public int models;
        public int presentationAssets;
        public List<string> errors = new List<string>();
        public List<string> notes = new List<string>();
    }

    [MenuItem("Tools/Fox/Validate Project References")]
    public static void ValidateReferences()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var result = new AuditResult { unityVersion = Application.unityVersion };
        string knownPath = "Docs/known-missing-assets.json";
        KnownMissingRegistry known = File.Exists(knownPath)
            ? JsonUtility.FromJson<KnownMissingRegistry>(File.ReadAllText(knownPath)) : new KnownMissingRegistry();
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(entry => entry.enabled))
            {
                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                result.scenes++;
                foreach (GameObject root in scene.GetRootGameObjects())
                    CheckHierarchy(root, entry.path, result);
                SceneUIRoot[] uiRoots = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SceneUIRoot>(true)).ToArray();
                if (uiRoots.Length != 1)
                    result.errors.Add(entry.path + ": SceneUIRoot count=" + uiRoots.Length);
                foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                        result.notes.Add(entry.path + ": existing ScreenSpaceCamera Canvas has no assigned camera.");
                foreach (MonoBehaviour behaviour in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
                    if (behaviour != null)
                        CheckRequiredConfiguration(behaviour, entry.path, result);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                bool hasMissingSource = false;
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})"))
                {
                    string sourceGuid = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(sourceGuid)))
                        continue;
                    if (known.assets.Any(asset => asset.path == path && asset.guid == sourceGuid))
                        result.notes.Add(path + ": pre-existing missing nested prefab GUID " + sourceGuid + "; original asset preserved.");
                    else
                        result.errors.Add(path + ": missing nested prefab GUID " + sourceGuid);
                    hasMissingSource = true;
                }
                if (hasMissingSource)
                {
                    result.prefabs++;
                    continue;
                }
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CheckHierarchy(root, path, result);
                    result.prefabs++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:KitchenObjectSO", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KitchenObjectSO definition = AssetDatabase.LoadAssetAtPath<KitchenObjectSO>(path);
                result.itemDefinitions++;
                CheckSerializedReferences(definition, path, result);
                if (definition.Prefab == null || definition.Prefab.GetComponent<KitchenObject>() == null)
                    result.errors.Add(path + ": invalid item prefab.");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                result.models++;
                if (model == null)
                    result.errors.Add(path + ": failed model import.");
                else
                    CheckHierarchy(model, path, result);
            }
            foreach (Type type in new[] { typeof(StartPanel), typeof(SettingsPanel), typeof(LevelSelectPanel),
                         typeof(CompletionPanel), typeof(OrderPanel1), typeof(OrderPanel2), typeof(OrderPanel3) })
            {
                GameObject prefab = Resources.Load<GameObject>("UI/" + type.Name);
                if (prefab == null || prefab.GetComponent(type) == null)
                    result.errors.Add(type.Name + ": Resources prefab/type mismatch.");
            }
            foreach (string path in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Resources/ForestParty", "Assets/_Project/Data/Story", "Assets/_Project/Timelines" }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is ScriptableObject)
                    {
                        CheckSerializedReferences(asset, path + "/" + asset.name, result);
                        result.presentationAssets++;
                    }
        }
        finally
        {
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/unity-reference-validation.json", JsonUtility.ToJson(result, true));
            if (setup.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Debug.Log($"Fox reference audit: {result.scenes} scenes, {result.prefabs} prefabs, {result.itemDefinitions} items, {result.models} models, {result.errors.Count} errors.");
        if (result.errors.Count != 0)
            throw new Exception(string.Join("\n", result.errors));
    }

    private static void CheckHierarchy(GameObject root, string path, AuditResult result)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            if (missing > 0)
                result.errors.Add(path + "/" + child.name + ": missing scripts=" + missing);
            foreach (Component component in child.GetComponents<Component>())
                if (component != null)
                    CheckSerializedReferences(component, path + "/" + child.name, result);
        }
    }

    private static void CheckSerializedReferences(UnityEngine.Object target, string path, AuditResult result)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                property.objectReferenceInstanceIDValue != 0)
                result.errors.Add(path + ": broken reference " + property.propertyPath);
    }

    private static void CheckRequiredConfiguration(MonoBehaviour target, string path, AuditResult result)
    {
        string[] fields = target is FoxPlacementGoal ? new[] { "firstCounter", "secondCounter", "thirdCounter", "foxDefinition" } :
            target is IngredientPlacementGoal ? new[] { "tomatoCounter", "cheeseCounter", "bread", "tomatoDefinition", "cheeseDefinition", "breadDefinition" } :
            target is FinalPlacementGoal ? new[] { "counter", "requiredItem" } :
            target is Player ? new[] { "gameInput", "holdPoint" } :
            target is FoxCompanion ? new[] { "dialogue", "arrivalPoint" } :
            target is FoxEscortQuest ? new[] { "partyPoint", "cinematic" } :
            target is StoryCinematic ? new[] { "intro", "reunion", "ghostPartySpot" } :
            target is ContainerCounter ? new[] { "kitchenObjectSO", "holdPoint" } : Array.Empty<string>();
        var serialized = new SerializedObject(target);
        foreach (string field in fields)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue == null)
                result.errors.Add(path + "/" + target.name + ": required field missing: " + field);
        }
    }

    [MenuItem("Tools/Fox/Build Windows Development Player")]
    public static void BuildDevelopment()
    {
        Directory.CreateDirectory("Builds/WindowsDevelopment");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = "Builds/WindowsDevelopment/Fox.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        string summary = $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n";
        File.WriteAllText("Docs/windows-build-result.txt", summary);
        File.WriteAllLines("Docs/windows-build-warnings.txt", report.steps.SelectMany(step => step.messages)
            .Where(message => message.type == LogType.Warning || message.type == LogType.Error)
            .Select(message => message.type + ": " + message.content).Distinct());
        Debug.Log(summary);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows development build failed.");
    }

    public static void ValidateAndBuild()
    {
        ValidateReferences();
        BuildDevelopment();
    }
}
