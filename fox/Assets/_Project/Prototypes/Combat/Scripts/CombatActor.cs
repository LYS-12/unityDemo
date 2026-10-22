using UnityEngine;
using UnityEngine.Serialization;

public abstract class CombatActor : MonoBehaviour
{
    [FormerlySerializedAs("atk"), SerializeField] private int attack;
    [FormerlySerializedAs("def"), SerializeField] private int defense;
    [SerializeField] protected int maxHp;
    [SerializeField] protected int hp;
    [FormerlySerializedAs("tankHead"), SerializeField] protected Transform head;
    [SerializeField] protected float moveSpeed = 10f;
    [FormerlySerializedAs("deadEff"), SerializeField] private GameObject deathEffect;
    private bool isDead;

    public abstract void Fire();

    public virtual void Wound(CombatActor attacker)
    {
        if (attacker == null || isDead) return;
        int damage = attacker.attack - defense;
        if (damage <= 0) return;
        hp = Mathf.Max(0, hp - damage);
        if (hp == 0) Dead();
    }

    public virtual void Dead()
    {
        if (isDead) return;
        isDead = true;
        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, transform.rotation);
            if (effect.TryGetComponent(out AudioSource audioSource)) audioSource.Play();
        }
        Destroy(gameObject);
    }
}
