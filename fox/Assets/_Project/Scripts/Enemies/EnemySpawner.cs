using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using UnityEngine.Serialization;

public class EnemySpawner : MonoBehaviour
{
    public RockEnemy enemyPrefab;
    public NavMeshData navigation;
    public Transform[] spawnAnchors;
    [FormerlySerializedAs("Items")] public KitchenObjectSO[] items;
    [Min(1)] public int totalLimit = 10;
    [Min(1)] public int aliveLimit = 3;
    [Min(0.1f)] public float spawnInterval = 3f;
    [Min(0.1f)] public float spawnRadius = 2f;
    public int SpawnedCount
    {
        get; private set;
    }
    public int AliveCount => active.Count;
    public IReadOnlyCollection<RockEnemy> ActiveEnemies => active;
    private readonly HashSet<RockEnemy> active = new HashSet<RockEnemy>();
    private ObjectPool<RockEnemy> pool;
    private NavMeshDataInstance navigationInstance;
    private float timer;
    private bool disposing;

    private void Awake()
    {
        if (navigation != null)
            navigationInstance = NavMesh.AddNavMeshData(navigation);
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner 缺少怪物预制体。", this);
            enabled = false;
            return;
        }
        pool = new ObjectPool<RockEnemy>(() => Instantiate(enemyPrefab, transform), null,
            enemy => enemy.gameObject.SetActive(false), enemy => Destroy(enemy.gameObject), true, aliveLimit, aliveLimit);
        var warm = new List<RockEnemy>();
        for (int index = 0; index < aliveLimit; index++)
            warm.Add(pool.Get());
        foreach (RockEnemy enemy in warm)
            pool.Release(enemy);
    }

    private void Update()
    {
        if (GameplayPause.IsPaused || SceneLoader.IsLoading || RunSession.Current == null || RunSession.Current.IsDead ||
            SpawnedCount >= totalLimit || AliveCount >= aliveLimit)
            return;
        timer += Time.deltaTime;
        if (timer < spawnInterval)
            return;
        // 无合法位置时延后重试，不计入累计数量。
        timer = TrySpawn() ? 0f : Mathf.Max(0f, spawnInterval - 0.5f);
    }

    public bool TrySpawn()
    {
        if (disposing || pool == null || GameplayPause.IsPaused || SceneLoader.IsLoading || Player.Instance == null ||
            RunSession.Current == null || RunSession.Current.IsDead ||
            SpawnedCount >= totalLimit || AliveCount >= aliveLimit || spawnAnchors == null || spawnAnchors.Length == 0)
            return false;
        Transform anchor = spawnAnchors[SpawnedCount % spawnAnchors.Length];
        if (anchor == null || !NavMesh.SamplePosition(Player.Instance.transform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
            return false;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = anchor.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - anchor.position.y) > 2f)
                continue;
            int actors = LayerMask.GetMask("PlayerCombat", "Enemy", "Companion");
            if (Physics.CheckCapsule(hit.position + Vector3.up * 0.65f, hit.position + Vector3.up * 1.4f, 0.65f, actors, QueryTriggerInteraction.Ignore))
                continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(hit.position, playerHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            RockEnemy enemy = pool.Get();
            active.Add(enemy);
            enemy.Spawn(this, hit.position, Player.Instance);
            SpawnedCount++;
            return true;
        }
        return false;
    }

    public void Return(RockEnemy enemy)
    {
        if (!active.Remove(enemy) || disposing)
            return;
        pool.Release(enemy);
    }

    private void OnDestroy()
    {
        disposing = true;
        active.Clear();
        pool?.Dispose();
        if (navigationInstance.valid)
            navigationInstance.Remove();
    }
}
