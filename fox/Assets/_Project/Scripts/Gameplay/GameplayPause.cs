using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameplayPause
{
    private static readonly HashSet<object> owners = new HashSet<object>();
    public static bool IsPaused => owners.Count > 0;
    public static event Action Changed;

    public static void Set(object owner, bool paused)
    {
        if (owner == null) return;
        bool previous = IsPaused;
        if (paused) owners.Add(owner);
        else owners.Remove(owner);
        Time.timeScale = IsPaused ? 0f : 1f;
        if (previous != IsPaused) Changed?.Invoke();
    }

    public static void ClearScene()
    {
        owners.Clear();
        Time.timeScale = 1f;
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        owners.Clear();
        Changed = null;
        Time.timeScale = 1f;
    }
}
