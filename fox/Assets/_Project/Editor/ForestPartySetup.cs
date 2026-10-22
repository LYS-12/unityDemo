using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static partial class ForestPartySetup
{
    private const string Root = "Assets/_Project";
    public static void InspectAudioApi()
    {
        Type mixer = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        if (mixer == null)
            mixer = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.Audio.AudioMixerController")).FirstOrDefault(type => type != null);
        File.WriteAllText("Logs/audio-editor-api.txt", mixer == null ? "NOT FOUND" : string.Join("\n", mixer.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(method => method.Name.Contains("Group") || method.Name.Contains("Expos") || method.Name.Contains("Create")).Select(method => method.ToString())));
        Debug.Log("ForestParty: scripts compiled, audio API inspected.");
    }
}
