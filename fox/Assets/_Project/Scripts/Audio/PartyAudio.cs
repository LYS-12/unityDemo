using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum PartySound
{
    Hover, Confirm, Back, Pickup, Equip, Use, Throw, Hit, Hurt, Shield, Explosion, FoxStartle, Rescue, Complete, TalkGhost, TalkFox
}

/// <summary>Persistent music pair and a bounded, reusable spatial/UI source bank.</summary>
public class PartyAudio : MonoBehaviour
{
    private static PartyAudio instance;
    private PartyAudioLibrary library;
    private readonly List<AudioSource> voices = new List<AudioSource>();
    private AudioSource[] music;
    private int currentMusic;
    private float duck = 1, targetDuck = 1;
    private float musicBlend = 1;
    private const int VoiceLimit = 24;
    public static PartyAudio Ensure()
    {
        if (instance != null)
            return instance;
        instance = new GameObject("ForestPartyAudio").AddComponent<PartyAudio>();
        DontDestroyOnLoad(instance.gameObject);
        return instance;
    }
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        library = Resources.Load<PartyAudioLibrary>("ForestParty/AudioLibrary");
        music = new[] { NewSource(true), NewSource(true) };
        foreach (AudioSource source in music)
        {
            source.loop = true;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = library != null ? library.musicGroup : null;
        }
        if (library != null && library.forestMusic != null)
        {
            music[0].clip = library.forestMusic;
            music[0].Play();
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        RouteSceneAudio();
        ApplyVolumes();
    }
    private AudioSource NewSource(bool isMusic)
    {
        var child = new GameObject(isMusic ? "Music" : "Voice");
        child.transform.SetParent(transform);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.dopplerLevel = 0;
        return source;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RouteSceneAudio();
        targetDuck = scene.name == "StartScene" ? .85f : 1;
        if (library != null && library.forestMusic != null)
            CrossFade(library.forestMusic);
    }
    private void RouteSceneAudio()
    {
        foreach (AudioSource source in FindObjectsOfType<AudioSource>())
        {
            if (source.transform.IsChildOf(transform) || !source.loop || !source.playOnAwake)
                continue;
            if (source.clip != null && (source.clip == library?.forestMusic || source.spatialBlend < .01f))
                source.Stop();
            else if (library != null)
                source.outputAudioMixerGroup = library.effectsGroup;
        }
    }
    public void CrossFade(AudioClip clip)
    {
        if (clip == null)
            return;
        int next = 1 - currentMusic;
        music[next].clip = clip;
        music[next].volume = 0;
        music[next].time = 0;
        music[next].Play();
        currentMusic = next;
        musicBlend = 0;
    }
    public static void SetDialogue(bool active)
    {
        if (active)
            Ensure().targetDuck = .36f;
        else if (instance != null)
            instance.targetDuck = 1;
    }
    public static void Play(PartySound sound, Vector3? position = null)
    {
        if (!Application.isPlaying)
            return;
        PartyAudio audio = Ensure();
        if (audio.library == null || audio.library.sounds == null || (int)sound >= audio.library.sounds.Length)
            return;
        AudioClip clip = audio.library.sounds[(int)sound];
        if (clip == null)
            return;
        AudioSource voice = audio.voices.Find(source => !source.isPlaying && (!GameplayPause.IsPaused || source.ignoreListenerPause || source.clip == null));
        if (voice == null)
        {
            if (audio.voices.Count >= VoiceLimit)
                return;
            voice = audio.NewSource(false);
            audio.voices.Add(voice);
        }
        voice.outputAudioMixerGroup = audio.library.effectsGroup;
        voice.transform.position = position ?? Vector3.zero;
        voice.spatialBlend = position.HasValue ? .75f : 0;
        voice.minDistance = 3;
        voice.maxDistance = 26;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.ignoreListenerPause = !position.HasValue;
        voice.pitch = Random.Range(.96f, 1.04f);
        voice.clip = clip;
        voice.volume = audio.library.effectsGroup != null ? .7f : AudioManager.Instance.GetBkVolume() * .7f;
        voice.Play();
    }
    public void ApplyVolumes()
    {
        if (library != null && library.mixer != null)
        {
            library.mixer.SetFloat("MusicVolume", Decibels(AudioManager.Instance.GetBgVolume()));
            library.mixer.SetFloat("EffectsVolume", Decibels(AudioManager.Instance.GetBkVolume()));
        }
    }
    private static float Decibels(float value) => value <= .0001f ? -80 : Mathf.Log10(value) * 20;
    private void Update()
    {
        AudioListener.pause = GameplayPause.IsPaused;
        duck = Mathf.MoveTowards(duck, targetDuck, Time.unscaledDeltaTime * 1.6f);
        musicBlend = Mathf.Min(1, musicBlend + Time.unscaledDeltaTime / 1.2f);
        float volume = (library != null && library.musicGroup != null ? 1 : AudioManager.Instance.GetBgVolume()) * duck * .55f;
        music[currentMusic].volume = volume * musicBlend;
        music[1 - currentMusic].volume = volume * (1 - musicBlend);
        if (musicBlend >= 1 && music[1 - currentMusic].isPlaying)
            music[1 - currentMusic].Stop();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
            AudioListener.pause = false;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => instance = null;
}
