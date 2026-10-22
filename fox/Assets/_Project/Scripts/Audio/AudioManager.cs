using UnityEngine;

public sealed class AudioManager
{
    public static AudioManager Instance { get; } = new AudioManager();
    private readonly AudioData audioData = new AudioData();
    private AudioManager()
    {
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Instance.audioData.bg = PlayerPrefs.GetFloat("ForestParty.Music", .6f);
        Instance.audioData.bk = PlayerPrefs.GetFloat("ForestParty.Effects", .8f);
    }

    public float GetBgVolume() => audioData.bg;
    public float SaveBgVolume(float value)
    {
        audioData.bg = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("ForestParty.Music", audioData.bg);
        PlayerPrefs.Save();
        PartyAudio.Ensure().ApplyVolumes();
        return audioData.bg;
    }
    public float GetBkVolume() => audioData.bk;
    public float SaveBkVolume(float value)
    {
        audioData.bk = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("ForestParty.Effects", audioData.bk);
        PlayerPrefs.Save();
        PartyAudio.Ensure().ApplyVolumes();
        return audioData.bk;
    }
}
