using UnityEngine;

[CreateAssetMenu(menuName = "Fox/Gameplay Catalog")]
public class GameplayCatalog : ScriptableObject
{
    public KitchenObjectSO[] startingItems;
    [Min(0)] public int startingCount = 3;
    [Min(1)] public int initialHealth = 5;
    [Range(1, 10)] public int healthCap = 10;
    [Min(0f)] public float damageProtection = 0.75f;
    public GameObject hudPrefab;
    public Material explosionMaterial;
}
