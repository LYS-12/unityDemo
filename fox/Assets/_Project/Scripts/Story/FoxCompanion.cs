using System;
using UnityEngine;
using UnityEngine.AI;

public enum FoxEscortState
{
    WaitingToFind, Dialogue, Following, Waiting, Arrived, Downed
}

[RequireComponent(typeof(NavMeshAgent))]
public class FoxCompanion : MonoBehaviour, ICombatTarget, IHealthSource
{
    public FoxDialogue dialogue;
    public Transform arrivalPoint;
    public int formationIndex;
    [Min(1)] public int maxHealth = 5;
    [Min(.1f)] public float normalSpeed = 5;
    [Min(.1f)] public float catchUpSpeed = 8;
    [Min(1)] public float loseDistance = 12;
    [Min(1)] public float resumeDistance = 8;
    [Min(.1f)] public float arrivalRadius = 2.5f;
    [Min(0)] public float protectionDuration = .75f;
    public FoxEscortState State { get; private set; } = FoxEscortState.WaitingToFind;
    public FoxEscortState StateBeforeDowned
    {
        get; private set;
    }
    public int CurrentHealth
    {
        get; private set;
    }
    public int MaximumHealth => maxHealth;
    public string DisplayName => dialogue != null ? dialogue.foxName : name;
    public CombatTeam Team => CombatTeam.Player;
    public Transform TargetTransform => transform;
    public bool CanBeTargeted => isActiveAndEnabled && CurrentHealth > 0 && (State == FoxEscortState.Following || State == FoxEscortState.Waiting);
    public event Action HealthChanged;
    public event Action StateChanged;
    private NavMeshAgent agent;
    private Animator animator;
    private float protection, pathTimer;
    private bool manuallyWaiting;
    public bool IsManuallyWaiting => manuallyWaiting;
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        CurrentHealth = maxHealth;
    }
    private void OnEnable() => CombatTargets.Register(this);
    private void OnDisable() => CombatTargets.Unregister(this);
    private void SetState(FoxEscortState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke();
        HealthChanged?.Invoke();
    }
    public bool BeginApology()
    {
        if (State != FoxEscortState.WaitingToFind)
            return false;
        SetState(FoxEscortState.Dialogue);
        return true;
    }
    public void CompleteApology()
    {
        if (State != FoxEscortState.Dialogue)
            return;
        manuallyWaiting = false;
        SetState(FoxEscortState.Following);
        PartyAudio.Play(PartySound.Complete, transform.position);
    }
    public void ToggleFollow()
    {
        if (State != FoxEscortState.Following && State != FoxEscortState.Waiting)
            return;
        manuallyWaiting = !manuallyWaiting;
        SetState(manuallyWaiting ? FoxEscortState.Waiting : FoxEscortState.Following);
    }
    public bool TryApplyDamage(DamageInfo damage)
    {
        if (!CanBeTargeted || damage.SourceTeam == Team || damage.Amount <= 0 || protection > 0 || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return false;
        protection = protectionDuration;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage.Amount);
        if (CurrentHealth == 0)
        {
            StateBeforeDowned = State;
            SetState(FoxEscortState.Downed);
        }
        HealthChanged?.Invoke();
        PartyAudio.Play(PartySound.Hurt, transform.position);
        return true;
    }
    public bool Rescue()
    {
        if (State != FoxEscortState.Downed)
            return false;
        CurrentHealth = maxHealth;
        protection = Mathf.Max(2, protectionDuration);
        SetState(StateBeforeDowned == FoxEscortState.Waiting ? FoxEscortState.Waiting : FoxEscortState.Following);
        PartyAudio.Play(PartySound.Rescue, transform.position);
        return true;
    }
    public bool TryArrive()
    {
        if (CurrentHealth <= 0 || State != FoxEscortState.Following && State != FoxEscortState.Waiting || arrivalPoint == null || Vector3.Distance(transform.position, arrivalPoint.position) > arrivalRadius)
            return false;
        CurrentHealth = maxHealth;
        SetState(FoxEscortState.Arrived);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
        PartyAudio.Play(PartySound.Complete, transform.position);
        return true;
    }
    private void Update()
    {
        if (agent == null)
            return;
        if (!agent.enabled)
        {
            if (StoryCinematic.IsPlaying || !NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2, NavMesh.AllAreas))
                return;
            agent.enabled = true;
            agent.Warp(start.position);
        }
        if (!agent.isOnNavMesh)
            return;
        bool frozen = GameplayPause.IsPaused || SceneLoader.IsLoading || RunSession.Current == null || RunSession.Current.IsDead;
        if (frozen)
        {
            agent.isStopped = true;
            return;
        }
        protection = Mathf.Max(0, protection - Time.deltaTime);
        bool follow = State == FoxEscortState.Following;
        Player player = Player.Instance;
        if (player != null && (follow || State == FoxEscortState.Waiting))
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (follow && distance > loseDistance)
            {
                SetState(FoxEscortState.Waiting);
                follow = false;
            }
            else if (State == FoxEscortState.Waiting && !manuallyWaiting && distance < resumeDistance)
            {
                SetState(FoxEscortState.Following);
                follow = true;
            }
            if (follow)
            {
                pathTimer -= Time.deltaTime;
                Vector3 offset = -player.transform.forward * (2 + formationIndex * .8f) + player.transform.right * ((formationIndex % 2 == 0 ? -1 : 1) * .9f);
                Vector3 destination = player.transform.position + offset;
                agent.speed = distance > 6 ? catchUpSpeed : normalSpeed;
                agent.isStopped = distance < 2;
                if (pathTimer <= 0 && !agent.pathPending)
                {
                    pathTimer = .35f;
                    var path = new NavMeshPath();
                    if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2, NavMesh.AllAreas) && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                        agent.SetPath(path);
                    else
                    {
                        SetState(FoxEscortState.Waiting);
                        agent.isStopped = true;
                    }
                }
            }
            TryArrive();
        }
        if (State != FoxEscortState.Following)
            agent.isStopped = true;
        if (animator != null)
            animator.SetBool("isWalking", State == FoxEscortState.Following && agent.velocity.sqrMagnitude > .02f);
        if (State == FoxEscortState.Downed && transform.childCount > 0)
            transform.GetChild(0).localRotation = Quaternion.Slerp(transform.GetChild(0).localRotation, Quaternion.Euler(0, 0, 75), Time.deltaTime * 6);
        else if (transform.childCount > 0)
            transform.GetChild(0).localRotation = Quaternion.Slerp(transform.GetChild(0).localRotation, Quaternion.identity, Time.deltaTime * 6);
    }
    public void ResetToSearch(Vector3 position)
    {
        CurrentHealth = maxHealth;
        protection = 0;
        manuallyWaiting = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.Warp(position);
        }
        else
            transform.position = position;
        SetState(FoxEscortState.WaitingToFind);
    }
}
