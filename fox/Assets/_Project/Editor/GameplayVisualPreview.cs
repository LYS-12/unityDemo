using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>显式启动图形编辑器的 Game View 截图，避免批处理跳过屏幕 UI 渲染。</summary>
[InitializeOnLoad]
public static class GameplayVisualPreview
{
    private const string Key = "Fox.GameplayVisualPreview";
    private static double deadline;
    private static int frames;
    static GameplayVisualPreview()
    {
        if (SessionState.GetInt(Key, 0) > 0) EditorApplication.update += Update;
    }

    public static void Run()
    {
        GameplayProjectSetup.RefreshIcons();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity", OpenSceneMode.Single);
        SessionState.SetInt(Key, 1);
        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        EditorWindow.GetWindow(gameViewType).Show();
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        try
        {
            int phase = SessionState.GetInt(Key, 0);
            if (phase == 4 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.update -= Update;
                File.WriteAllText("Docs/visual-preview-result.txt", "Captured native Game View inventory, combat and explosion frames.\nGraphics: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + "\n");
                EditorApplication.Exit(0);
                return;
            }
            if (!EditorApplication.isPlaying || GameplayHUD.Current == null) return;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(gameViewType);
            view.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
            if (++frames < 45) return;
            if (phase == 1)
            {
                EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
                spawner.spawnInterval = 100f;
                GameplayHUD.Current.ToggleInventory();
                phase = 2;
                SessionState.SetInt(Key, phase);
                deadline = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 2 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture(view, "Docs/Images/inventory.png");
                GameplayHUD.Current.CloseInventory();
                RunSession session = RunSession.Current;
                KitchenObjectSO cheese = session.Catalog.startingItems.First(item => item.effect == ItemEffectKind.Shield);
                session.UseShield(cheese); session.UseShield(cheese); session.UseShield(cheese);
                session.Inventory.Equip(session.Inventory.Items.First(item => item.definition.effect == ItemEffectKind.Explosion).id);
                EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
                spawner.TrySpawn();
                foreach (RockEnemy enemy in spawner.ActiveEnemies)
                {
                    enemy.attackInterval = 100f;
                    if (NavMesh.SamplePosition(Player.Instance.transform.position + Vector3.forward * 4, out NavMeshHit hit, 2, NavMesh.AllAreas))
                        enemy.GetComponent<NavMeshAgent>().Warp(hit.position);
                }
                SessionState.SetInt(Key, 3);
                deadline = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture(view, "Docs/Images/combat.png");
                ExplosionVisual.Play(Player.Instance.transform.position + Vector3.forward * 2f, 3f);
                SessionState.SetInt(Key, 5);
                deadline = EditorApplication.timeSinceStartup + 0.14;
            }
            else if (phase == 5 && EditorApplication.timeSinceStartup > deadline)
            {
                Capture(view, "Docs/Images/explosion.png");
                GameplayPause.ClearScene();
                SessionState.SetInt(Key, 4);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(Key, 0);
            EditorApplication.update -= Update;
            EditorApplication.Exit(1);
        }
    }

    internal static void Capture(EditorWindow view, string path)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        RenderTexture texture = null;
        for (Type type = view.GetType(); type != null && texture == null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                if (typeof(RenderTexture).IsAssignableFrom(field.FieldType)) texture = field.GetValue(view) as RenderTexture ?? texture;
            foreach (PropertyInfo property in type.GetProperties(flags | BindingFlags.DeclaredOnly))
                if (property.GetIndexParameters().Length == 0 && typeof(RenderTexture).IsAssignableFrom(property.PropertyType))
                    texture = property.GetValue(view) as RenderTexture ?? texture;
        }
        if (texture == null) throw new Exception("Game View has no rendered target. Use a graphical Editor session.");
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0);
        Graphics.Blit(texture, target, new Vector2(1, -1), new Vector2(0, 1));
        RenderTexture.active = target;
        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Debug.Log("Captured Game View: " + path + " " + texture.width + "x" + texture.height);
    }
}
