using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public enum Scene
    {
        StartScene, GameScene1, GameScene2, GameScene3, LoadingScene
    }
    private static Scene targetScene;
    private static Action loadedCallback;
    public static bool IsLoading
    {
        get; private set;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        IsLoading = false;
        targetScene = Scene.StartScene;
        loadedCallback = null;
    }

    public static string GetSceneName(Scene scene)
    {
        switch (scene)
        {
            case Scene.StartScene:
                return "StartScene";
            case Scene.GameScene1:
                return "GameScene1";
            case Scene.GameScene2:
                return "GameScene2";
            case Scene.GameScene3:
                return "GameScene3";
            case Scene.LoadingScene:
                return "LoadScene";
            default:
                throw new ArgumentOutOfRangeException(nameof(scene));
        }
    }

    public static bool Load(Scene target, Action onLoaded = null)
    {
        if (IsLoading || target == Scene.LoadingScene)
            return false;
        string name = GetSceneName(target);
        if (!Application.CanStreamedLevelBeLoaded(name) || !Application.CanStreamedLevelBeLoaded("LoadScene"))
        {
            Debug.LogError($"目标场景 {name} 或 LoadScene 未加入 Build Settings。 ");
            return false;
        }
        targetScene = target;
        GameplayPause.ClearScene();
        if (target == Scene.StartScene)
            RunSession.End();
        loadedCallback = onLoaded;
        IsLoading = true;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        try
        {
            ScreenTransition.Cover(() => SceneManager.LoadSceneAsync("LoadScene"));
            return true;
        }
        catch (Exception exception)
        {
            CancelLoad(exception);
            return false;
        }
    }

    public static AsyncOperation LoadTargetScene()
    {
        // 从编辑器单独运行加载场景时，默认回到主菜单。
        if (!IsLoading)
        {
            targetScene = Scene.StartScene;
            IsLoading = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
        try
        {
            return SceneManager.LoadSceneAsync(GetSceneName(targetScene));
        }
        catch (Exception exception)
        {
            CancelLoad(exception);
            return null;
        }
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        if (!IsLoading || scene.name != GetSceneName(targetScene))
            return;
        Action callback = loadedCallback;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        loadedCallback = null;
        ScreenTransition.Reveal(() => { IsLoading = false; callback?.Invoke(); });
    }

    private static void CancelLoad(Exception exception)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        loadedCallback = null;
        IsLoading = false;
        Debug.LogException(exception);
    }
}
