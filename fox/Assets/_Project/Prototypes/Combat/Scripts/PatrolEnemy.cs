using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PatrolEnemy : CombatActor
{
    [FormerlySerializedAs("randomPos"), SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform lookAtTarget;
    [FormerlySerializedAs("fireDis"), SerializeField] private float firingDistance = 5f;
    [FormerlySerializedAs("fireOffsetTime"), SerializeField] private float firingInterval = 1f;
    [FormerlySerializedAs("shootPos"), SerializeField] private Transform[] firingPoints;
    [FormerlySerializedAs("bulletObj"), SerializeField] private GameObject projectilePrefab;
    [FormerlySerializedAs("maxHpBK"), SerializeField] private Texture healthBackground;
    [FormerlySerializedAs("hpBK"), SerializeField] private Texture healthFill;
    private Transform targetPoint;
    private float firingTimer;
    private float healthDisplayTime;

    private void Start() => SelectPatrolPoint();

    private void Update()
    {
        healthDisplayTime = Mathf.Max(0f, healthDisplayTime - Time.deltaTime);
        if (targetPoint != null)
        {
            Vector3 direction = targetPoint.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f) transform.forward = direction.normalized;
            transform.position = Vector3.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f) SelectPatrolPoint();
        }
        if (lookAtTarget == null) return;
        if (head != null) head.LookAt(lookAtTarget);
        if (Vector3.Distance(transform.position, lookAtTarget.position) > firingDistance) return;
        firingTimer += Time.deltaTime;
        if (firingTimer < firingInterval) return;
        firingTimer = 0f;
        Fire();
    }

    private void SelectPatrolPoint()
    {
        var validPoints = new List<Transform>();
        if (patrolPoints != null)
            foreach (Transform point in patrolPoints)
                if (point != null) validPoints.Add(point);
        targetPoint = validPoints.Count > 0 ? validPoints[Random.Range(0, validPoints.Count)] : null;
    }

    public override void Fire()
    {
        if (projectilePrefab == null || firingPoints == null) return;
        foreach (Transform point in firingPoints)
        {
            if (point == null) continue;
            GameObject instance = Instantiate(projectilePrefab, point.position, point.rotation);
            if (instance.TryGetComponent(out Projectile projectile)) projectile.SetOwner(this);
            else Destroy(instance);
        }
    }

    public override void Wound(CombatActor attacker)
    {
        base.Wound(attacker);
        if (attacker != null) healthDisplayTime = 3f;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (healthDisplayTime <= 0f || camera == null || maxHp <= 0 || healthBackground == null || healthFill == null) return;
        Vector3 position = camera.WorldToScreenPoint(transform.position);
        if (position.z <= 0f) return;
        float x = position.x - 40f;
        float y = Screen.height - position.y - 60f;
        GUI.DrawTexture(new Rect(x, y, 100f, 15f), healthBackground);
        GUI.DrawTexture(new Rect(x, y, Mathf.Clamp01((float)hp / maxHp) * 100f, 15f), healthFill);
    }
}
