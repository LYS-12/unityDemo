using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider), typeof(Rigidbody))]
public class RockEnemy : MonoBehaviour, IDamageable, IHealthSource
{
    [Min(1)] public int maxHealth = 3;
    [Min(0)] public int contactDamage = 1;
    [Min(0.1f)] public float moveSpeed = 3.5f;
    [Min(1f)] public float detectionRange = 20f;
    [Min(1f)] public float attackRange = 10f;
    [Min(0.1f)] public float attackInterval = 2.5f;
    private NavMeshAgent agent;
    private Animator animator;
    private CapsuleCollider bodyCollider;
    private EnemySpawner spawner;
    private ICombatTarget target;
    public ICombatTarget CurrentTarget => target;
    private float attackTimer;
    private float pathTimer;
    private bool leased;
    public int CurrentHealth
    {
        get; private set;
    }
    public int MaximumHealth => maxHealth;
    public string DisplayName => "岩岩怪";
    public event System.Action HealthChanged;
    public CombatTeam Team => CombatTeam.Enemy;
    private static readonly int WalkingParameter = Animator.StringToHash("isWalking");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<CapsuleCollider>();
    }

    public void Spawn(EnemySpawner owner, Vector3 position, Player player)
    {
        spawner = owner;
        target = player != null ? player.GetComponent<PlayerHealth>() : null;
        CurrentHealth = Mathf.Max(1, maxHealth);
        attackTimer = pathTimer = 0f;
        leased = true;
        transform.position = position;
        gameObject.SetActive(true);
        agent.enabled = true;
        agent.Warp(position);
        agent.speed = moveSpeed;
        agent.isStopped = false;
        bodyCollider.enabled = true;
        HealthChanged?.Invoke();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void Update()
    {
        if (!leased || !agent.enabled || !agent.isOnNavMesh)
            return;
        if (GameplayPause.IsPaused || SceneLoader.IsLoading || RunSession.Current == null || RunSession.Current.IsDead)
        {
            agent.isStopped = true;
            return;
        }
        target = CombatTargets.Nearest(transform.position, detectionRange);
        if (target == null)
        {
            agent.isStopped = true;
            if (animator != null)
                animator.SetBool(WalkingParameter, false);
            return;
        }
        float distance = Vector3.Distance(transform.position, target.TargetTransform.position);
        if (distance > detectionRange)
        {
            agent.isStopped = true;
            if (animator != null)
                animator.SetBool(WalkingParameter, false);
            return;
        }
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 toTarget = target.TargetTransform.position + Vector3.up - origin;
        bool blocked = Physics.Raycast(origin, toTarget.normalized, out RaycastHit obstacle, toTarget.magnitude,
            ~LayerMask.GetMask("Enemy", "WorldItem", "ItemProjectile"), QueryTriggerInteraction.Ignore) &&
            obstacle.collider.GetComponentInParent<ICombatTarget>() != target;
        bool inRange = distance <= attackRange && !blocked;
        agent.isStopped = inRange;
        pathTimer -= Time.deltaTime;
        if (!inRange && pathTimer <= 0f && !agent.pathPending)
        {
            pathTimer = 0.25f;
            agent.SetDestination(target.TargetTransform.position);
        }
        if (animator != null)
            animator.SetBool(WalkingParameter, !inRange && agent.velocity.sqrMagnitude > 0.02f);
        if (!inRange)
            return;
        Vector3 flat = toTarget;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 8f);
        attackTimer += Time.deltaTime;
        if (attackTimer < attackInterval || ItemProjectilePool.Current == null)
            return;
        KitchenObjectSO[] items = spawner.items;
        if (items == null || items.Length == 0)
            return;
        KitchenObjectSO definition = items[Random.Range(0, items.Length)];
        if (ItemProjectilePool.Current.Launch(definition, origin, target.TargetTransform.position, Team, gameObject, false))
        {
            attackTimer = 0f;
            PartyAudio.Play(PartySound.Throw, origin);
        }
    }

    public bool TryApplyDamage(DamageInfo damage)
    {
        if (!leased || damage.SourceTeam == Team || damage.Amount <= 0 || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return false;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage.Amount);
        HealthChanged?.Invoke();
        PartyAudio.Play(PartySound.Hit, transform.position);
        if (CurrentHealth == 0)
        {
            leased = false;
            bodyCollider.enabled = false;
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
            spawner.Return(this);
        }
        return true;
    }

    private void OnCollisionEnter(Collision collision) => ApplyContact(collision);
    private void OnCollisionStay(Collision collision) => ApplyContact(collision);
    private void ApplyContact(Collision collision)
    {
        if (!leased)
            return;
        IDamageable health = collision.collider.GetComponentInParent<IDamageable>();
        if (health != null && health.Team != Team)
            health.TryApplyDamage(new DamageInfo(contactDamage, Team, gameObject));
    }

    private void OnDisable()
    {
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
        leased = false;
        target = null;
        attackTimer = pathTimer = 0f;
        CurrentHealth = 0;
        if (bodyCollider != null)
            bodyCollider.enabled = false;
    }
}
