using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueView : MonoBehaviour
{
    public static DialogueView Current
    {
        get; private set;
    }
    private DialogueLine[] lines;
    private Action completed;
    private int index;
    private float characters;
    private TextMeshProUGUI text, nameplate, prompt;
    private Canvas canvas;
    private bool conversation;
    private bool closing, fading;
    private CanvasGroup opacity;
    private float fadeTime, fadeFrom, fadeTo;
    private Action faded;
    private int openedFrame;
    public bool IsOpen => canvas != null && canvas.enabled;
    public static DialogueView Ensure()
    {
        if (Current != null)
            return Current;
        Current = new GameObject("ForestDialogue").AddComponent<DialogueView>();
        return Current;
    }
    private void Awake()
    {
        Current = this;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        opacity = gameObject.AddComponent<CanvasGroup>();
        opacity.alpha = 0;
        var scale = gameObject.AddComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1920, 1080);
        scale.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var box = PartyUI.Rect("DialogueBubble", transform, new Vector2(.5f, 0), new Vector2(0, 142), new Vector2(1110, 196));
        PartyUI.Card(box, UITheme.Current.cream, true);
        var tag = PartyUI.Rect("NameTag", box, new Vector2(0, 1), new Vector2(32, 16), new Vector2(180, 48));
        PartyUI.Card(tag, UITheme.Current.apricot);
        nameplate = PartyUI.Text("", tag, Vector2.one * .5f, Vector2.zero, new Vector2(160, 44), 27, true, TextAlignmentOptions.Center);
        text = PartyUI.Text("", box, new Vector2(0, 1), new Vector2(42, -45), new Vector2(1020, 106), 30);
        prompt = PartyUI.Text("", box, new Vector2(1, 0), new Vector2(-30, 12), new Vector2(410, 30), 18, false, TextAlignmentOptions.Right);
        canvas.enabled = false;
    }
    public void Open(DialogueLine[] content, Action onCompleted)
    {
        if (conversation || closing || StoryCinematic.IsPlaying)
            return;
        lines = content;
        completed = onCompleted;
        index = 0;
        conversation = true;
        openedFrame = Time.frameCount;
        GameplayPause.Set(this, true);
        PartyAudio.SetDialogue(true);
        if (lines == null || lines.Length == 0)
        {
            Close(true);
            return;
        }
        ShowLine(lines[0], false);
    }
    public void ShowLine(DialogueLine line, bool cinematic)
    {
        Fade(1);
        nameplate.text = line.speaker;
        text.text = line.text;
        text.maxVisibleCharacters = 0;
        characters = 0;
        prompt.text = cinematic ? "长按 Esc 跳过演出" : "E 继续  ·  把心意慢慢说清楚";
    }
    public void Advance()
    {
        if (!conversation)
            return;
        if (text.maxVisibleCharacters < text.text.Length)
        {
            characters = text.text.Length;
            text.maxVisibleCharacters = text.text.Length;
            return;
        }
        if (++index >= lines.Length)
        {
            Close(true);
            return;
        }
        ShowLine(lines[index], false);
    }
    public void HideSubtitle()
    {
        if (!conversation && !closing)
            Fade(0);
    }
    private void Fade(float target, Action callback = null)
    {
        if (!canvas.enabled)
            opacity.alpha = 0;
        canvas.enabled = true;
        fadeFrom = opacity.alpha;
        fadeTo = target;
        fadeTime = 0;
        fading = true;
        faded = callback;
        opacity.blocksRaycasts = true;
    }
    private void Update()
    {
        if (!IsOpen)
            return;
        if (fading)
        {
            fadeTime += Time.unscaledDeltaTime;
            opacity.alpha = Mathf.Lerp(fadeFrom, fadeTo, Mathf.SmoothStep(0, 1, fadeTime / .25f));
            if (fadeTime >= .25f)
            {
                fading = false;
                if (fadeTo == 0)
                {
                    canvas.enabled = false;
                    opacity.blocksRaycasts = false;
                }
                Action callback = faded;
                faded = null;
                callback?.Invoke();
            }
        }
        int previous = Mathf.FloorToInt(characters);
        characters += Time.unscaledDeltaTime * 22;
        text.maxVisibleCharacters = Mathf.FloorToInt(characters);
        if (nameplate.text != "森林手记" && text.maxVisibleCharacters > previous && previous < text.text.Length && previous % 3 == 0)
            PartyAudio.Play(nameplate.text == "小幽" ? PartySound.TalkGhost : PartySound.TalkFox);
        if (conversation && Time.frameCount > openedFrame && Player.Instance != null && Player.Instance.Input.DialogueAdvancePressed)
            Advance();
    }
    public void Close(bool submit)
    {
        if (closing)
            return;
        Action callback = completed;
        completed = null;
        conversation = false;
        closing = true;
        Fade(0, () => { closing = false; GameplayPause.Set(this, false); PartyAudio.SetDialogue(false); if (submit) callback?.Invoke(); });
    }
    private void OnDestroy()
    {
        GameplayPause.Set(this, false);
        if (Current == this)
            Current = null;
        if (conversation)
            PartyAudio.SetDialogue(false);
    }
}
