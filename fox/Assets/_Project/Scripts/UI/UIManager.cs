using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UIManager
{
    public static UIManager Instance { get; } = new UIManager();
    private readonly Dictionary<Type, BasePanel> panels = new Dictionary<Type, BasePanel>();
    private Transform canvasRoot;
    private UIManager()
    {
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Instance.panels.Clear();
        Instance.canvasRoot = null;
    }

    public void RegisterCanvas(Transform root)
    {
        if (root == canvasRoot)
            return;
        ClearPanels();
        canvasRoot = root;
    }

    public void UnregisterCanvas(Transform root)
    {
        if (canvasRoot != root)
            return;
        panels.Clear();
        canvasRoot = null;
    }

    public void ClearPanels()
    {
        foreach (BasePanel panel in panels.Values)
            if (panel != null)
                UnityEngine.Object.Destroy(panel.gameObject);
        panels.Clear();
    }

    public T ShowPanel<T>() where T : BasePanel
    {
        Type type = typeof(T);
        if (panels.TryGetValue(type, out BasePanel existing) && existing != null)
        {
            if (!existing.IsShowing)
                existing.ShowMe();
            return existing as T;
        }
        panels.Remove(type);
        if (canvasRoot == null)
        {
            Debug.LogError("场景 Canvas 尚未注册，请检查 SceneUIRoot。 ");
            return null;
        }
        string path = "UI/" + type.Name;
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"找不到 UI 预制体：Resources/{path}。 ");
            return null;
        }
        GameObject instance = UnityEngine.Object.Instantiate(prefab, canvasRoot, false);
        T panel = instance.GetComponent<T>();
        if (panel == null || !panel.enabled)
        {
            Debug.LogError($"UI 预制体 {path} 缺少有效的 {type.Name} 组件。 ");
            UnityEngine.Object.Destroy(instance);
            return null;
        }
        panels[type] = panel;
        panel.ShowMe();
        return panel;
    }

    public void HidePanel<T>(bool isFade = true) where T : BasePanel
    {
        Type type = typeof(T);
        if (!panels.TryGetValue(type, out BasePanel panel))
            return;
        PartyAudio.Play(PartySound.Back);
        if (panel == null)
        {
            panels.Remove(type);
            return;
        }
        void RemovePanel()
        {
            if (!panels.TryGetValue(type, out BasePanel current) || current != panel)
                return;
            panels.Remove(type);
            if (panel != null)
                UnityEngine.Object.Destroy(panel.gameObject);
        }
        if (isFade)
            panel.HideMe(RemovePanel);
        else
            RemovePanel();
    }

    public T GetPanel<T>() where T : BasePanel
    {
        return panels.TryGetValue(typeof(T), out BasePanel panel) && panel != null ? panel as T : null;
    }
}
