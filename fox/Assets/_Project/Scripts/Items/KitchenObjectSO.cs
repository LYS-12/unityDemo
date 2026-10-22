using UnityEngine;

[CreateAssetMenu(menuName = "Fox/Item Definition")]
public class KitchenObjectSO : ScriptableObject
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Sprite sprite;
    [SerializeField] private string objectName;

    [Header("道具配置（每件道具的耐久独立保存）")]
    public ItemEffectKind effect;
    [Min(1)] public int maxDurability = 3;
    public bool canThrow;
    [Min(0.05f)] public float actionCooldown = 0.25f;
    [Min(0.1f)] public float throwSpeed = 12f;
    [Min(1f)] public float throwRange = 12f;
    [Min(0.1f)] public float projectileLifetime = 5f;
    [Min(0)] public int throwDamage = 1;
    [Min(1f)] public float speedMultiplier = 1.5f;
    [Min(0.1f)] public float speedDuration = 10f;
    [Min(1)] public int healthIncrease = 1;
    [Range(1, 3)] public int shieldLimit = 3;
    [Min(0.1f)] public float shieldRadius = 1.4f;
    [Min(0f)] public float shieldOrbitSpeed = 80f;
    [Min(0.1f)] public float explosionSpeed = 10f;
    [Min(1f)] public float explosionRange = 8f;
    [Min(0.1f)] public float explosionRadius = 3f;
    [Min(0)] public int explosionDamage = 1;
    [Min(0.1f)] public float explosionFuse = 2f;

    public GameObject Prefab => prefab;
    public Sprite Sprite => sprite;
    public string ObjectName => objectName;
}
