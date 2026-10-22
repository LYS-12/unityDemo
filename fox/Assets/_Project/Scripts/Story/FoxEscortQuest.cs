using System;
using UnityEngine;

public class FoxEscortQuest : MonoBehaviour
{
    public static FoxEscortQuest Current
    {
        get; private set;
    }
    public FoxCompanion[] foxes;
    public Transform partyPoint;
    public StoryCinematic cinematic;
    [Min(.1f)] public float interactionRange = 2.5f;
    [Min(.1f)] public float rescueDuration = 2;
    public bool IsComplete
    {
        get; private set;
    }
    public int ArrivedCount
    {
        get
        {
            int count = 0;
            if (foxes != null)
                foreach (var fox in foxes)
                    if (fox != null && fox.State == FoxEscortState.Arrived)
                        count++;
            return count;
        }
    }
    public int BefriendedCount
    {
        get
        {
            int count = 0;
            if (foxes != null)
                foreach (var fox in foxes)
                    if (fox != null && fox.State != FoxEscortState.WaitingToFind && fox.State != FoxEscortState.Dialogue)
                        count++;
            return count;
        }
    }
    public float RescueProgress
    {
        get; private set;
    }
    public FoxCompanion Nearby
    {
        get; private set;
    }
    private FoxCompanion rescueTarget;
    public event Action Changed;
    private void Awake() => Current = this;
    private void Start()
    {
        foreach (FoxCompanion fox in foxes)
            fox.StateChanged += OnFoxChanged;
        if (RunSession.Current != null && !RunSession.Current.IntroSeen && cinematic != null)
            cinematic.PlayIntro();
    }
    private void OnFoxChanged()
    {
        Changed?.Invoke();
        if (!IsComplete && ArrivedCount == foxes.Length)
        {
            IsComplete = true;
            if (cinematic != null)
                cinematic.PlayReunion();
            else
                ShowCompletion();
        }
    }
    public static void ShowCompletion() => UIManager.Instance.ShowPanel<LevelSelectPanel>();
    public bool TryInteract()
    {
        FindNearby();
        if (Nearby == null)
            return false;
        if (Nearby.State == FoxEscortState.WaitingToFind)
        {
            FoxCompanion fox = Nearby;
            if (fox.BeginApology())
                DialogueView.Ensure().Open(fox.dialogue.apology, fox.CompleteApology);
        }
        else if (Nearby.State == FoxEscortState.Following || Nearby.State == FoxEscortState.Waiting)
            Nearby.ToggleFollow();
        return true;
    }
    private void FindNearby()
    {
        Nearby = null;
        if (Player.Instance == null || StoryCinematic.IsPlaying)
            return;
        float closest = interactionRange;
        foreach (FoxCompanion fox in foxes)
        {
            if (fox == null || fox.State == FoxEscortState.Arrived)
                continue;
            float distance = Vector3.Distance(Player.Instance.transform.position, fox.transform.position);
            if (distance > closest)
                continue;
            Vector3 from = Player.Instance.transform.position + Vector3.up;
            Vector3 to = fox.transform.position + Vector3.up;
            if (Physics.Linecast(from, to, out RaycastHit hit, ~LayerMask.GetMask("PlayerCombat", "WorldItem", "Enemy", "ItemProjectile", "Companion"), QueryTriggerInteraction.Ignore))
                continue;
            closest = distance;
            Nearby = fox;
        }
    }
    private void Update()
    {
        if (GameplayPause.IsPaused || SceneLoader.IsLoading || Player.Instance == null || RunSession.Current == null || RunSession.Current.IsDead)
        {
            CancelRescue();
            return;
        }
        FindNearby();
        bool held = Player.Instance.Input.InteractHeld;
        if (Nearby == null || Nearby.State != FoxEscortState.Downed || !held)
        {
            CancelRescue();
            return;
        }
        if (rescueTarget != Nearby)
        {
            rescueTarget = Nearby;
            RescueProgress = 0;
        }
        RescueProgress += Time.deltaTime / rescueDuration;
        if (RescueProgress >= 1)
        {
            rescueTarget.Rescue();
            CancelRescue();
        }
    }
    public void CancelRescue()
    {
        rescueTarget = null;
        RescueProgress = 0;
    }
    public string StatusText()
    {
        if (IsComplete)
            return "朋友到齐了！一起享受森林派对吧。";
        if (Nearby != null)
        {
            switch (Nearby.State)
            {
                case FoxEscortState.Downed:
                    return "长按 E 救援 " + Nearby.DisplayName + "  " + Mathf.RoundToInt(RescueProgress * 100) + "%";
                case FoxEscortState.WaitingToFind:
                    return "E 和 " + Nearby.DisplayName + " 说声对不起";
                case FoxEscortState.Waiting:
                    return "E 请 " + Nearby.DisplayName + " 继续跟随";
                case FoxEscortState.Following:
                    return "E 请 " + Nearby.DisplayName + " 在这里等一等";
            }
        }
        foreach (var fox in foxes)
            if (fox.State == FoxEscortState.Downed)
                return fox.DisplayName + " 倒下了！靠近并长按 E 救援。";
        foreach (var fox in foxes)
            if (fox.State == FoxEscortState.Waiting)
                return fox.DisplayName + " 正在等你。靠近后一起回家。";
        return "找到三位狐狸朋友，好好道歉，再护送回聚会空地。";
    }
    private void OnDestroy()
    {
        if (foxes != null)
            foreach (var fox in foxes)
                if (fox != null)
                    fox.StateChanged -= OnFoxChanged;
        if (Current == this)
            Current = null;
    }
}
