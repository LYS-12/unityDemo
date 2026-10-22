using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>Real-time, native Game View RGB recording piped to a local ffmpeg process.</summary>
[InitializeOnLoad]
public static class ForestPartyRecording
{
    private const string Key = "ForestParty.Recording";
    private const int Fps = 15;
    private static Process encoder;
    private static Texture2D pixels;
    private static RenderTexture target;
    private static PartyAudioCapture audio;
    private static string movie;
    private static int count;
    private static float started;
    private static double deadline;
    private static readonly List<float> frameTimes = new List<float>();
    private static int sampledFrame;
    private static double settle;
    private static readonly List<Renderer> demoHiddenTrees = new List<Renderer>();
    private static GameObject demoFloor;
    static ForestPartyRecording()
    {
        if (SessionState.GetInt(Key, 0) > 0)
            EditorApplication.update += Update;
    }
    public static void Run()
    {
        Directory.CreateDirectory("Docs/Videos");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameScene1.unity");
        ForestPartyPreview.SetSize(1280, 720);
        SessionState.SetInt(Key, 1);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.isPlaying = true;
    }
    public static void RunGhostOnly()
    {
        SessionState.SetBool(Key + ".GhostOnly", true);
        Run();
    }
    private static void StartGhostDemo()
    {
        if (Keyboard.current == null)
            InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(Keyboard.current);
        demoHiddenTrees.Clear();
        foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer.GetComponentInParent<Player>() != null || renderer.transform.IsChildOf(FoxEscortQuest.Current.partyPoint))
                continue;
            demoHiddenTrees.Add(renderer);
            renderer.enabled = false;
        }
        demoFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        demoFloor.name = "GhostPreviewFloor";
        demoFloor.transform.position = FoxEscortQuest.Current.partyPoint.position + Vector3.down * .03f;
        demoFloor.transform.localScale = Vector3.one * 20;
        demoFloor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/ForestParty/PartySage.mat");
        Object.Destroy(demoFloor.GetComponent<Collider>());
        Vector3 demo = FoxEscortQuest.Current.partyPoint.position + Vector3.back * 3.5f;
        Player.Instance.transform.position = demo;
        Player.Instance.GetComponent<Rigidbody>().position = demo;
        Player.Instance.transform.rotation = Quaternion.Euler(0, 180, 0);
        Player.Instance.GetComponent<Rigidbody>().rotation = Quaternion.Euler(0, 180, 0);
        Begin("ghost-animation");
        SessionState.SetInt(Key, 3);
    }
    private static string Encoder => Directory.GetFiles("Tools/.media/imageio_ffmpeg/binaries", "ffmpeg*.exe").Select(Path.GetFullPath).First();
    private static string Quote(string value) => "\"" + Path.GetFullPath(value) + "\"";
    private static void Begin(string name)
    {
        movie = "Docs/Videos/" + name;
        encoder = new Process { StartInfo = new ProcessStartInfo(Encoder, "-y -f rawvideo -pixel_format rgb24 -video_size 1280x720 -framerate " + Fps + " -i pipe:0 -an -vf vflip -c:v libx264 -preset fast -crf 21 -pix_fmt yuv420p " + Quote(movie + "-silent.mp4")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true } };
        encoder.ErrorDataReceived += (sender, args) => { };
        encoder.Start();
        encoder.BeginErrorReadLine();
        target = new RenderTexture(1280, 720, 0);
        target.Create();
        pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        audio = Camera.main.gameObject.AddComponent<PartyAudioCapture>();
        audio.Begin();
        count = 0;
        started = Time.realtimeSinceStartup;
        frameTimes.Clear();
    }
    private static RenderTexture GameTexture()
    {
        var view = ForestPartyPreview.View();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type type = view.GetType(); type != null; type = type.BaseType)
        {
            foreach (var property in type.GetProperties(flags | BindingFlags.DeclaredOnly))
                if (property.GetIndexParameters().Length == 0 && typeof(RenderTexture).IsAssignableFrom(property.PropertyType) && property.GetValue(view) is RenderTexture texture)
                    return texture;
            foreach (var field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                if (typeof(RenderTexture).IsAssignableFrom(field.FieldType) && field.GetValue(view) is RenderTexture texture)
                    return texture;
        }
        return null;
    }
    private static void Frame()
    {
        if (encoder == null)
            return;
        float elapsed = Time.realtimeSinceStartup - started;
        if (count > elapsed * Fps)
            return;
        RenderTexture source = GameTexture();
        if (source == null)
            return;
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target, new Vector2(1, -1), new Vector2(0, 1));
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        byte[] bytes = pixels.GetRawTextureData();
        while (count <= elapsed * Fps)
        {
            encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            count++;
        }
        frameTimes.Add(Time.unscaledDeltaTime);
    }
    private static void End()
    {
        Frame();
        encoder.StandardInput.Close();
        encoder.WaitForExit(10000);
        encoder.Dispose();
        encoder = null;
        audio.End(movie + ".wav");
        Object.Destroy(audio);
        audio = null;
        var mux = new Process { StartInfo = new ProcessStartInfo(Encoder, "-y -i " + Quote(movie + "-silent.mp4") + " -i " + Quote(movie + ".wav") + " -c:v copy -c:a aac -b:a 160k -shortest " + Quote(movie + ".mp4")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } };
        mux.ErrorDataReceived += (sender, args) => { };
        mux.Start();
        mux.BeginErrorReadLine();
        mux.WaitForExit(10000);
        mux.Dispose();
        File.AppendAllText("Docs/forest-recording-result.txt", movie + ".mp4: " + count + " frames / " + Fps + " fps, " + (count / (float)Fps).ToString("F2") + " seconds; audio samples=" + PartyAudioCapture.LastSampleCount + "; mean game frame=" + (frameTimes.Count > 0 ? frameTimes.Average() * 1000 : 0).ToString("F2") + " ms (includes recording overhead).\n");
        Object.Destroy(pixels);
        target.Release();
        Object.Destroy(target);
    }
    private static void Update()
    {
        try
        {
            int phase = SessionState.GetInt(Key, 0);
            if (phase == 8 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.update -= Update;
                EditorApplication.Exit(0);
                return;
            }
            if (!EditorApplication.isPlaying || GameplayHUD.Current == null)
                return;
            Application.runInBackground = true;
            ForestPartyPreview.View().Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
            if (phase == 1)
            {
                if (!StoryCinematic.IsPlaying)
                    return;
                Object.FindObjectOfType<EnemySpawner>().spawnInterval = 1000;
                var story = FoxEscortQuest.Current.cinematic;
                if (SessionState.GetBool(Key + ".GhostOnly", false))
                {
                    story.Skip();
                    SessionState.SetInt(Key, 20);
                    deadline = EditorApplication.timeSinceStartup + 1.2;
                    return;
                }
                story.intro.time = 0;
                story.intro.Evaluate();
                File.WriteAllText("Docs/forest-recording-result.txt", "Native graphical Game View + AudioListener capture. GPU: " + SystemInfo.graphicsDeviceName + "\n");
                File.WriteAllText("Docs/forest-performance-result.txt", "GPU: " + SystemInfo.graphicsDeviceName + "; CPU: " + SystemInfo.processorType + "\n");
                Begin("forest-intro");
                SessionState.SetInt(Key, 2);
            }
            else if (phase == 2)
            {
                Frame();
                if (StoryCinematic.IsPlaying || Time.realtimeSinceStartup - started < 41)
                    return;
                End();
                StartGhostDemo();
            }
            else if (phase == 20 && !StoryCinematic.IsPlaying && EditorApplication.timeSinceStartup > deadline)
            {
                StartGhostDemo();
            }
            else if (phase == 3)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                UnityEngine.InputSystem.Key? key = elapsed > 2 && elapsed < 2.4f ? UnityEngine.InputSystem.Key.A : elapsed > 3 && elapsed < 3.4f ? UnityEngine.InputSystem.Key.D : (UnityEngine.InputSystem.Key?)null;
                InputSystem.QueueStateEvent(Keyboard.current, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState());
                if (elapsed > 4 && elapsed < 4.15f)
                {
                    Player.Instance.transform.rotation = Quaternion.Euler(0, 180, 0);
                    Player.Instance.GetComponentInChildren<Animator>().Play("Wave", 0, 0);
                }
                Frame();
                if (elapsed < 9)
                    return;
                End();
                foreach (Renderer renderer in demoHiddenTrees)
                    if (renderer != null)
                        renderer.enabled = true;
                demoHiddenTrees.Clear();
                if (demoFloor != null)
                    Object.Destroy(demoFloor);
                if (SessionState.GetBool(Key + ".GhostOnly", false))
                {
                    SessionState.SetBool(Key + ".GhostOnly", false);
                    SessionState.SetInt(Key, 8);
                    EditorApplication.isPlaying = false;
                    return;
                }
                foreach (var fox in FoxEscortQuest.Current.foxes)
                {
                    fox.BeginApology();
                    fox.CompleteApology();
                    fox.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(fox.arrivalPoint.position);
                    fox.TryArrive();
                }
                FoxEscortQuest.Current.cinematic.reunion.time = 0;
                FoxEscortQuest.Current.cinematic.reunion.Evaluate();
                Begin("forest-reunion");
                SessionState.SetInt(Key, 4);
            }
            else if (phase == 4)
            {
                Frame();
                if (StoryCinematic.IsPlaying || Time.realtimeSinceStartup - started < 25)
                    return;
                End();
                UIManager.Instance.HidePanel<LevelSelectPanel>(false);
                GameplayPause.ClearScene();
                SessionState.SetInt(Key, 5);
                deadline = EditorApplication.timeSinceStartup + 3;
                settle = EditorApplication.timeSinceStartup + 1;
                frameTimes.Clear();
            }
            else if (phase == 5 || phase == 6)
            {
                if (EditorApplication.timeSinceStartup > settle && sampledFrame != Time.frameCount)
                {
                    sampledFrame = Time.frameCount;
                    frameTimes.Add(Time.unscaledDeltaTime * 1000);
                }
                if (EditorApplication.timeSinceStartup < deadline)
                    return;
                var values = frameTimes.OrderBy(value => value).ToArray();
                if (values.Length > 0)
                    File.AppendAllText("Docs/forest-performance-result.txt", (phase == 5 ? "Presentation FX on" : "Presentation FX off") + ": frames=" + values.Length + ", mean=" + values.Average().ToString("F2") + " ms, median=" + values[values.Length / 2].ToString("F2") + " ms, p95=" + values[Mathf.Min(values.Length - 1, Mathf.FloorToInt(values.Length * .95f))].ToString("F2") + " ms. Native Editor Game View 1280x720; no movie encoding during sampling.\n");
                if (phase == 5)
                {
                    GameObject.Find("ForestAtmosphere")?.SetActive(false);
                    foreach (var fx in Object.FindObjectsOfType<GhostMotionEffects>())
                        foreach (var particles in fx.GetComponentsInChildren<ParticleSystem>())
                            particles.gameObject.SetActive(false);
                    frameTimes.Clear();
                    settle = EditorApplication.timeSinceStartup + 1;
                    deadline = EditorApplication.timeSinceStartup + 3;
                    SessionState.SetInt(Key, 6);
                }
                else
                {
                    SessionState.SetInt(Key, 8);
                    EditorApplication.isPlaying = false;
                }
            }
        }
        catch (Exception exception) { Debug.LogException(exception); if (encoder != null) { encoder.StandardInput.Close(); encoder.Dispose(); } SessionState.SetInt(Key, 0); EditorApplication.update -= Update; EditorApplication.Exit(1); }
    }
}

public class PartyAudioCapture : MonoBehaviour
{
    private readonly object gate = new object();
    private readonly List<short> samples = new List<short>();
    private bool recording;
    private int channels = 2, rate;
    public static int LastSampleCount;
    public void Begin()
    {
        rate = AudioSettings.outputSampleRate;
        recording = true;
    }
    private void OnAudioFilterRead(float[] data, int channelCount)
    {
        if (!recording)
            return;
        lock (gate)
        {
            channels = channelCount;
            foreach (float value in data)
                samples.Add((short)(Math.Max(-1, Math.Min(1, value)) * 32767));
        }
    }
    public void End(string path)
    {
        recording = false;
        lock (gate)
        {
            LastSampleCount = samples.Count;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(rate);
                writer.Write(rate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Count * 2);
                foreach (short sample in samples)
                    writer.Write(sample);
            }
        }
    }
}
