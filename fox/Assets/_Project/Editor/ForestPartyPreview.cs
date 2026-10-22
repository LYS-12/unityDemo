using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Ordinary graphical Editor capture of the actual Game View, never batch Camera.Render.</summary>
[InitializeOnLoad]
public static class ForestPartyPreview
{
    private const string Key = "ForestParty.Preview";
    private static double deadline;
    private static int frames;
    static ForestPartyPreview()
    {
        if (SessionState.GetInt(Key, 0) > 0)
            EditorApplication.update += Update;
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/StartScene.unity");
        SetSize(1920, 1080);
        SessionState.SetInt(Key, 1);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.isPlaying = true;
    }
    public static void RunAndRecord()
    {
        SessionState.SetBool(Key + ".RecordNext", true);
        Run();
    }
    internal static EditorWindow View() => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
    internal static void SetSize(int width, int height)
    {
        var assembly = typeof(EditorWindow).Assembly;
        Type sizes = assembly.GetType("UnityEditor.GameViewSizes"), singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizes);
        object instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        object group = sizes.GetMethod("GetGroup").Invoke(instance, new[] { Enum.ToObject(groupType, 0) });
        Type sizeType = assembly.GetType("UnityEditor.GameViewSize"), kind = assembly.GetType("UnityEditor.GameViewSizeType");
        object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { Enum.ToObject(kind, 1), width, height, "Forest QA " + width + "x" + height }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        EditorWindow view = View();
        view.Show();
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, total - 1);
        view.Repaint();
    }
    private static void Capture(string name) => GameplayVisualPreview.Capture(View(), "Docs/Images/" + name + ".png");
    private static void Update()
    {
        try
        {
            int phase = SessionState.GetInt(Key, 0);
            if (phase == 10 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.update -= Update;
                if (SessionState.GetBool(Key + ".RecordNext", false))
                {
                    SessionState.SetBool(Key + ".RecordNext", false);
                    ForestPartyRecording.Run();
                }
                else
                    EditorApplication.Exit(0);
                return;
            }
            if (!EditorApplication.isPlaying)
                return;
            View().Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
            if (++frames < 80)
                return;
            if (phase == 1)
            {
                if (UIManager.Instance.GetPanel<StartPanel>() == null)
                    return;
                Capture("forest-title-1080");
                SceneLoader.Load(SceneLoader.Scene.GameScene1);
                SessionState.SetInt(Key, 2);
                deadline = EditorApplication.timeSinceStartup + 6;
            }
            else if (phase == 2 && !SceneLoader.IsLoading && GameplayHUD.Current != null && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-intro-wide");
                var story = Object.FindObjectOfType<StoryCinematic>();
                story.intro.time = 17;
                story.intro.Evaluate();
                SessionState.SetInt(Key, 3);
                deadline = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-intro-ghost");
                Object.FindObjectOfType<StoryCinematic>().Skip();
                Object.FindObjectOfType<EnemySpawner>().spawnInterval = 100;
                SessionState.SetInt(Key, 4);
                deadline = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 4 && EditorApplication.timeSinceStartup > deadline)
            {
                var symbols = Object.FindObjectOfType<MinimapSymbols>();
                File.WriteAllText("Docs/minimap-render-diagnostic.txt", "rect=" + symbols.rectTransform.rect + ", anchored=" + symbols.rectTransform.anchoredPosition + ", player icon=" + symbols.PlayerMarker.rectTransform.anchoredPosition + ", footprint lines=" + symbols.FrustumLineCount + "\n");
                Capture("forest-gameplay-1080");
                var spawner = Object.FindObjectOfType<EnemySpawner>();
                spawner.TrySpawn();
                foreach (var enemy in spawner.ActiveEnemies)
                {
                    enemy.attackInterval = 1000;
                    enemy.detectionRange = 0;
                    if (UnityEngine.AI.NavMesh.SamplePosition(Player.Instance.transform.position + Vector3.right * 3, out UnityEngine.AI.NavMeshHit spot, 2, UnityEngine.AI.NavMesh.AllAreas))
                        enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(spot.position);
                }
                var fox = FoxEscortQuest.Current.foxes[0];
                if (UnityEngine.AI.NavMesh.SamplePosition(Player.Instance.transform.position + Vector3.left * 3, out UnityEngine.AI.NavMeshHit friend, 2, UnityEngine.AI.NavMesh.AllAreas))
                    fox.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(friend.position);
                Player.Instance.transform.rotation = Quaternion.Euler(0, 180, 0);
                var session = RunSession.Current;
                session.UseShield(session.Catalog.startingItems.First(item => item.effect == ItemEffectKind.Shield));
                session.Inventory.Equip(session.Inventory.Items.First(item => item.definition.effect == ItemEffectKind.Health).id);
                SessionState.SetInt(Key, 40);
                deadline = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 40 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-combat-bars");
                GameplayHUD.Current.ToggleInventory();
                SessionState.SetInt(Key, 5);
                deadline = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 5 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-inventory-1080");
                SetSize(1280, 720);
                SessionState.SetInt(Key, 6);
                deadline = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 6 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-inventory-720");
                GameplayHUD.Current.CloseInventory();
                SetSize(2560, 1080);
                SessionState.SetInt(Key, 7);
                deadline = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 7 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-gameplay-ultrawide");
                SetSize(1920, 1080);
                FoxEscortQuest quest = FoxEscortQuest.Current;
                foreach (var fox in quest.foxes)
                {
                    fox.BeginApology();
                    fox.CompleteApology();
                    fox.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(fox.arrivalPoint.position);
                    fox.TryArrive();
                }
                SessionState.SetInt(Key, 8);
                deadline = EditorApplication.timeSinceStartup + 5;
            }
            else if (phase == 8 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-reunion");
                Object.FindObjectOfType<StoryCinematic>().Skip();
                SessionState.SetInt(Key, 9);
                deadline = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 9 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture("forest-level-select");
                File.WriteAllText("Docs/forest-visual-result.txt", "Native graphical Game View captures: title, intro, ghost, shared HUD/map, inventory at 1080/720, ultrawide and reunion.\nGPU: " + SystemInfo.graphicsDeviceName + "\n");
                GameplayPause.ClearScene();
                SessionState.SetInt(Key, 10);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); SessionState.SetInt(Key, 0); EditorApplication.update -= Update; EditorApplication.Exit(1); }
    }
}
