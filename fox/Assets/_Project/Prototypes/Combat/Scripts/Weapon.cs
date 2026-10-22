using UnityEngine;
using UnityEngine.Serialization;

public class Weapon : MonoBehaviour
{
    [SerializeField] private GameObject bullet;
    [FormerlySerializedAs("shootPos"), SerializeField] private Transform[] firingPoints;
    [FormerlySerializedAs("fatherObj"), SerializeField] private CombatActor owner;

    public void SetOwner(CombatActor actor) => owner = actor;

    public void Fire()
    {
        if (bullet == null || owner == null || firingPoints == null) return;
        foreach (Transform point in firingPoints)
        {
            if (point == null) continue;
            GameObject instance = Instantiate(bullet, point.position, point.rotation);
            if (instance.TryGetComponent(out Projectile projectile)) projectile.SetOwner(owner);
            else
            {
                Debug.LogError("子弹预制体缺少 Projectile 组件。", this);
                Destroy(instance);
            }
        }
    }
}
