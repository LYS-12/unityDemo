using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Forest Party/Audio Library")]
public class PartyAudioLibrary : ScriptableObject
{
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup effectsGroup;
    public AudioClip forestMusic;
    public AudioClip[] sounds;
}
