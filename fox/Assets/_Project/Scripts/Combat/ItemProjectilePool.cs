using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ItemProjectilePool : MonoBehaviour
{
    public static ItemProjectilePool Current { get; private set; }
    private readonly Dictionary<KitchenObjectSO, ObjectPool<ItemProjectile>> pools = new Dictionary<KitchenObjectSO, ObjectPool<ItemProjectile>>();
    private readonly HashSet<ItemProjectile> active = new HashSet<ItemProjectile>();
    private bool disposing;
    internal EnemySpawner Enemies { get; private set; }
    public int ActiveCount => active.Count;
    private void Awake()
    {
        Current = this;
        Enemies = GetComponent<EnemySpawner>();
    }

    public bool Launch(KitchenObjectSO definition, Vector3 origin, Vector3 target, CombatTeam team, GameObject owner, bool useEffect)
    {
        if (disposing || definition == null || definition.Prefab == null) return false;
        if (!pools.TryGetValue(definition, out ObjectPool<ItemProjectile> pool))
        {
            pool = new ObjectPool<ItemProjectile>(() => Create(definition), null, projectile => projectile.gameObject.SetActive(false),
                projectile => Destroy(projectile.gameObject), true, 4, 32);
            pools.Add(definition, pool);
        }
        ItemProjectile shot = pool.Get();
        active.Add(shot);
        shot.Fire(this, definition, origin, target, team, owner, useEffect);
        return true;
    }

    private ItemProjectile Create(KitchenObjectSO definition)
    {
        var root = new GameObject("Projectile_" + definition.name);
        root.SetActive(false);
        root.transform.SetParent(transform);
        root.layer = LayerMask.NameToLayer("ItemProjectile");
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.radius = 0.22f;
        ItemView.CreateVisual(definition, root.transform);
        return root.AddComponent<ItemProjectile>();
    }

    public void Release(ItemProjectile shot, KitchenObjectSO definition)
    {
        if (!active.Remove(shot)) return;
        if (!disposing && pools.TryGetValue(definition, out ObjectPool<ItemProjectile> pool)) pool.Release(shot);
    }

    private void OnDestroy()
    {
        disposing = true;
        if (Current == this) Current = null;
        active.Clear();
        foreach (ObjectPool<ItemProjectile> pool in pools.Values) pool.Dispose();
        pools.Clear();
    }
}
