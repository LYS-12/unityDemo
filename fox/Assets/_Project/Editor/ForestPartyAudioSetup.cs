using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    private static PartyAudioLibrary CreateAudio()
    {
        string path = Root + "/Data/Audio/ForestParty.mixer";
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        if (mixer == null || mixer.FindMatchingGroups("Music").Length == 0 || mixer.FindMatchingGroups("Effects").Length == 0)
        {
            WriteMixer(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        }
        var library = AssetDatabase.LoadAssetAtPath<PartyAudioLibrary>(Root + "/Resources/ForestParty/AudioLibrary.asset");
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PartyAudioLibrary>();
            AssetDatabase.CreateAsset(library, Root + "/Resources/ForestParty/AudioLibrary.asset");
        }
        library.mixer = mixer;
        library.musicGroup = mixer.FindMatchingGroups("Music").FirstOrDefault();
        library.effectsGroup = mixer.FindMatchingGroups("Effects").FirstOrDefault();
        if (library.musicGroup == null || library.effectsGroup == null)
            throw new Exception("Mixer groups were not authored.");
        library.forestMusic = AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio/Music" }).Select(AssetDatabase.GUIDToAssetPath).Where(file => file.Contains("治愈")).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).First();
        library.sounds = Enum.GetNames(typeof(PartySound)).Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/SFX/ForestParty/" + name + ".wav")).ToArray();
        EditorUtility.SetDirty(library);
        return library;
    }
    private static void WriteMixer(string path)
    {
        // Unity 2022 native mixer serialization, avoiding unsupported internal editor APIs.
        string[] names = { "Master", "Music", "Effects" };
        string[] group = new string[3], volume = new string[3], pitch = new string[3], mix = new string[3];
        for (int i = 0; i < 3; i++)
        {
            group[i] = GUID.Generate().ToString();
            volume[i] = GUID.Generate().ToString();
            pitch[i] = GUID.Generate().ToString();
            mix[i] = GUID.Generate().ToString();
        }
        var text = new StringBuilder("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
        const string fields = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n";
        text.Append("--- !u!241 &24100000\nAudioMixerController:\n" + fields + "  m_Name: ForestParty\n  m_OutputGroup: {fileID: 0}\n  m_MasterGroup: {fileID: 24300002}\n  m_Snapshots:\n  - {fileID: 24500006}\n  m_StartSnapshot: {fileID: 24500006}\n  m_SuspendThreshold: -80\n  m_EnableSuspend: 1\n  m_UpdateMode: 0\n  m_ExposedParameters:\n  - guid: " + volume[1] + "\n    name: MusicVolume\n  - guid: " + volume[2] + "\n    name: EffectsVolume\n  m_AudioMixerGroupViews:\n  - guids:\n    - " + group[0] + "\n    - " + group[1] + "\n    - " + group[2] + "\n    name: Main\n  m_CurrentViewIndex: 0\n  m_TargetSnapshot: {fileID: 24500006}\n");
        for (int i = 0; i < 3; i++)
        {
            int id = 24300002 + i * 10, effect = 24400004 + i * 10;
            text.Append("--- !u!243 &" + id + "\nAudioMixerGroupController:\n" + fields + "  m_Name: " + names[i] + "\n  m_AudioMixer: {fileID: 24100000}\n  m_GroupID: " + group[i] + "\n");
            text.Append(i == 0 ? "  m_Children:\n  - {fileID: 24300012}\n  - {fileID: 24300022}\n" : "  m_Children: []\n");
            text.Append("  m_Volume: " + volume[i] + "\n  m_Pitch: " + pitch[i] + "\n  m_Send: 00000000000000000000000000000000\n  m_Effects:\n  - {fileID: " + effect + "}\n  m_UserColorIndex: 0\n  m_Mute: 0\n  m_Solo: 0\n  m_BypassEffects: 0\n");
            text.Append("--- !u!244 &" + effect + "\nAudioMixerEffectController:\n" + fields + "  m_Name: \n  m_EffectID: " + GUID.Generate() + "\n  m_EffectName: Attenuation\n  m_MixLevel: " + mix[i] + "\n  m_Parameters: []\n  m_SendTarget: {fileID: 0}\n  m_EnableWetMix: 0\n  m_Bypass: 0\n");
        }
        text.Append("--- !u!245 &24500006\nAudioMixerSnapshotController:\n" + fields + "  m_Name: ForestDefault\n  m_AudioMixer: {fileID: 24100000}\n  m_SnapshotID: " + GUID.Generate() + "\n  m_FloatValues:\n");
        for (int i = 0; i < 3; i++)
            text.Append("    " + volume[i] + ": 0\n    " + pitch[i] + ": 1\n    " + mix[i] + ": 0\n");
        text.Append("  m_TransitionOverrides: {}\n");
        File.WriteAllText(path, text.ToString());
    }
}
