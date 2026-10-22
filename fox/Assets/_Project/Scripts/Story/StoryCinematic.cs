using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.UI;

public class StoryCinematic : MonoBehaviour
{
    public static bool IsPlaying
    {
        get; private set;
    }
    public PlayableDirector intro;
    public PlayableDirector reunion;
    public FoxCompanion[] foxes;
    public Vector3[] searchPositions;
    public Transform[] partySlots;
    public Transform ghostPartySpot;
    [Min(.1f)] public float skipHoldDuration = 1;
    private PlayableDirector active;
    private readonly List<NavMeshAgent> agents = new List<NavMeshAgent>();
    private readonly List<Animator> animators = new List<Animator>();
    private readonly List<AnimatorUpdateMode> animationModes = new List<AnimatorUpdateMode>();
    private Canvas letterbox;
    private RectTransform top, bottom, left, right;
    private TMPro.TextMeshProUGUI skipLabel;
    private bool ending, submitted;
    private float skipTime;
    private Vector3 playerPosition;
    private Quaternion playerRotation;
    private CinemachineBrain brain;
    private bool oldIgnoreTimeScale;
    public bool PlayingReunion => active == reunion;
    public void PlayIntro() => StartCinematic(intro);
    public void PlayReunion() => StartCinematic(reunion);
    private void StartCinematic(PlayableDirector director)
    {
        if (IsPlaying || director == null || Player.Instance == null)
            return;
        active = director;
        ending = false;
        submitted = false;
        skipTime = 0;
        IsPlaying = true;
        GameplayHUD.Current?.CloseInventory();
        GameplayPause.Set(this, true);
        PartyAudio.SetDialogue(true);
        playerPosition = Player.Instance.transform.position;
        playerRotation = Player.Instance.transform.rotation;
        agents.Clear();
        animators.Clear();
        animationModes.Clear();
        foreach (NavMeshAgent agent in FindObjectsOfType<NavMeshAgent>())
            if (agent.enabled)
            {
                agents.Add(agent);
                agent.enabled = false;
            }
        foreach (Animator animator in Player.Instance.GetComponentsInChildren<Animator>(true))
            CacheAnimator(animator);
        foreach (var fox in foxes)
            foreach (var animator in fox.GetComponentsInChildren<Animator>(true))
                CacheAnimator(animator);
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            oldIgnoreTimeScale = brain.m_IgnoreTimeScale;
            brain.m_IgnoreTimeScale = true;
        }
        CreateLetterbox();
        active.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        active.extrapolationMode = DirectorWrapMode.None;
        active.stopped += OnStopped;
        active.time = 0;
        active.Play();
    }
    private void CacheAnimator(Animator animator)
    {
        animators.Add(animator);
        animationModes.Add(animator.updateMode);
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }
    private void CreateLetterbox()
    {
        var root = new GameObject("CinemaSafeFrame");
        root.transform.SetParent(transform);
        letterbox = root.AddComponent<Canvas>();
        letterbox.renderMode = RenderMode.ScreenSpaceOverlay;
        letterbox.sortingOrder = 280;
        root.AddComponent<GraphicRaycaster>();
        top = PartyUI.Rect("Top", root.transform, new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
        bottom = PartyUI.Rect("Bottom", root.transform, new Vector2(.5f, 0), Vector2.zero, Vector2.zero);
        left = PartyUI.Rect("Left", root.transform, new Vector2(0, .5f), Vector2.zero, Vector2.zero);
        right = PartyUI.Rect("Right", root.transform, new Vector2(1, .5f), Vector2.zero, Vector2.zero);
        foreach (var bar in new[] { top, bottom, left, right })
        {
            var image = bar.gameObject.AddComponent<Image>();
            image.color = new Color(.035f, .045f, .05f);
            image.raycastTarget = true;
        }
        skipLabel = PartyUI.Text("长按 Esc 跳过", root.transform, new Vector2(1, 1), new Vector2(-34, -22), new Vector2(350, 34), 20, false, TMPro.TextAlignmentOptions.Right);
        skipLabel.color = UITheme.Current.cream;
    }
    private void Update()
    {
        if (active == null || !IsPlaying || ending)
            return;
        float height = Mathf.Max(0, (Screen.height - Screen.width / 2.35f) * .5f);
        top.sizeDelta = bottom.sizeDelta = new Vector2(Screen.width, height);
        float width = Mathf.Max(0, (Screen.width - Screen.height * 2.35f) * .5f);
        left.sizeDelta = right.sizeDelta = new Vector2(width, Screen.height);
        bool held = Player.Instance != null && Player.Instance.Input.SkipHeld;
        skipTime = held ? skipTime + Time.unscaledDeltaTime : 0;
        skipLabel.text = held ? "跳过演出 " + Mathf.RoundToInt(Mathf.Clamp01(skipTime / skipHoldDuration) * 100) + "%" : "长按 Esc 跳过";
        if (skipTime >= skipHoldDuration)
            Finish();
    }
    public void Skip() => Finish();
    private void OnStopped(PlayableDirector director)
    {
        if (director == active && !ending && !SceneLoader.IsLoading)
            Finish();
    }
    private void Finish()
    {
        if (active == null || submitted || ending)
            return;
        ending = true;
        ScreenTransition.Cover(SubmitEnding);
    }
    private void SubmitEnding()
    {
        if (active == null || submitted || SceneLoader.IsLoading)
            return;
        submitted = true;
        bool wasIntro = active == intro;
        active.stopped -= OnStopped;
        active.Stop();
        if (wasIntro)
        {
            for (int i = 0; i < foxes.Length; i++)
                foxes[i].ResetToSearch(searchPositions[i]);
            if (RunSession.Current != null)
                RunSession.Current.IntroSeen = true;
            Player.Instance.transform.SetPositionAndRotation(playerPosition, playerRotation);
            Rigidbody body = Player.Instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = playerPosition;
                body.rotation = playerRotation;
            }
        }
        else
        {
            for (int i = 0; i < foxes.Length; i++)
                foxes[i].transform.position = partySlots[i].position;
            if (ghostPartySpot != null)
            {
                Player.Instance.transform.position = ghostPartySpot.position;
                Player.Instance.GetComponent<Rigidbody>().position = ghostPartySpot.position;
            }
        }
        Restore();
        active = null;
        ScreenTransition.Reveal();
        if (!wasIntro)
            FoxEscortQuest.ShowCompletion();
    }
    private void Restore(bool resumeActors = true)
    {
        if (resumeActors)
        {
            foreach (var agent in agents)
                if (agent != null)
                {
                    agent.enabled = true;
                    if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 2, NavMesh.AllAreas))
                        agent.Warp(hit.position);
                }
            for (int i = 0; i < animators.Count; i++)
                if (animators[i] != null)
                {
                    animators[i].updateMode = animationModes[i];
                    animators[i].Rebind();
                }
            if (brain != null)
                brain.m_IgnoreTimeScale = oldIgnoreTimeScale;
        }
        if (letterbox != null)
            Destroy(letterbox.gameObject);
        DialogueView.Current?.HideSubtitle();
        IsPlaying = false;
        GameplayPause.Set(this, false);
        PartyAudio.SetDialogue(false);
    }
    private void OnDestroy()
    {
        if (active != null)
        {
            active.stopped -= OnStopped;
            active.Stop();
            Restore(false);
        }
        GameplayPause.Set(this, false);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => IsPlaying = false;
}
