using UnityEngine;
using UnityEngine.Serialization;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 50f;
    [FormerlySerializedAs("fatherObj"), SerializeField] private CombatActor owner;
    [FormerlySerializedAs("effObj"), SerializeField] private GameObject impactEffect;
    private bool hasHit;

    private void Update() => transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        bool hitEnvironment = other.CompareTag("Cube");
        bool hitOpponent = owner != null &&
            ((other.CompareTag("Player") && owner.CompareTag("Monster")) ||
             (other.CompareTag("Monster") && owner.CompareTag("Player")));
        if (!hitEnvironment && !hitOpponent) return;
        hasHit = true;
        if (owner != null && other.TryGetComponent(out CombatActor actor)) actor.Wound(owner);
        if (impactEffect != null) Instantiate(impactEffect, transform.position, transform.rotation);
        Destroy(gameObject);
    }

    public void SetOwner(CombatActor actor) => owner = actor;
}
