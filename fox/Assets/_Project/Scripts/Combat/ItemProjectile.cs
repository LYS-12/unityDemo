using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class ItemProjectile : MonoBehaviour
{
    private ItemProjectilePool pool;
    private KitchenObjectSO definition;
    private CombatTeam team;
    private GameObject owner;
    private Rigidbody body;
    private Collider ownCollider;
    private readonly List<Collider> ignored = new List<Collider>();
    private float remaining;
    private bool resolved;
    private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
    public CombatTeam Team => team;

    public void Fire(ItemProjectilePool source, KitchenObjectSO item, Vector3 origin, Vector3 target,
        CombatTeam sourceTeam, GameObject sourceOwner, bool useEffect)
    {
        pool = source;
        definition = item;
        owner = sourceOwner;
        team = sourceTeam;
        body = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        resolved = false;
        damaged.Clear();
        remaining = item.effect == ItemEffectKind.Explosion ? item.explosionFuse : item.projectileLifetime;
        transform.SetPositionAndRotation(origin, Quaternion.identity);
        gameObject.SetActive(true);
        ownCollider.enabled = true;
        body.isKinematic = false;
        body.useGravity = true;
        body.velocity = Ballistics.LaunchVelocity(origin, target, owner != null ? owner.transform.forward : Vector3.forward,
            useEffect ? item.explosionSpeed : item.throwSpeed, useEffect ? item.explosionRange : item.throwRange);
        body.angularVelocity = Vector3.one * 3f;
        if (owner != null)
            foreach (Collider collider in owner.GetComponentsInChildren<Collider>())
                Ignore(collider);
        if (team == CombatTeam.Player)
            foreach (ICombatTarget friend in CombatTargets.Registered)
                if (friend is Object actor && actor != null && friend.Team == team)
                    foreach (Collider collider in friend.TargetTransform.GetComponentsInChildren<Collider>())
                        Ignore(collider);
        if (team == CombatTeam.Enemy && source.Enemies != null)
            foreach (RockEnemy friend in source.Enemies.ActiveEnemies)
                foreach (Collider collider in friend.GetComponentsInChildren<Collider>())
                    Ignore(collider);
    }

    private void Update()
    {
        if (resolved || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
            Resolve(null);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (resolved || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return;
        IDamageable victim = collision.collider.GetComponentInParent<IDamageable>();
        if (victim != null && victim.Team == team)
        {
            Ignore(collision.collider);
            return;
        }
        if (collision.collider.GetComponentInParent<ItemProjectile>() != null)
            return;
        Resolve(victim);
    }

    private void Resolve(IDamageable directHit)
    {
        if (resolved)
            return;
        resolved = true;
        if (definition.effect == ItemEffectKind.Explosion)
        {
            foreach (Collider hit in Physics.OverlapSphere(transform.position, definition.explosionRadius, ~0, QueryTriggerInteraction.Collide))
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target != null && target.Team != team && damaged.Add(target))
                    target.TryApplyDamage(new DamageInfo(definition.explosionDamage, team, owner));
            }
            ExplosionVisual.Play(transform.position, definition.explosionRadius);
            PartyAudio.Play(PartySound.Explosion, transform.position);
        }
        else if (directHit != null && directHit.Team != team)
            directHit.TryApplyDamage(new DamageInfo(definition.throwDamage, team, owner));
        ResetPhysics();
        pool.Release(this, definition);
    }

    private void ResetPhysics()
    {
        foreach (Collider collider in ignored)
            if (collider != null && ownCollider != null)
                Physics.IgnoreCollision(ownCollider, collider, false);
        ignored.Clear();
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        ownCollider.enabled = false;
    }

    private void Ignore(Collider collider)
    {
        if (collider == null || collider == ownCollider || ignored.Contains(collider))
            return;
        Physics.IgnoreCollision(ownCollider, collider, true);
        ignored.Add(collider);
    }

    private void OnDisable()
    {
        if (body != null)
            ResetPhysics();
        owner = null;
        damaged.Clear();
    }
}
