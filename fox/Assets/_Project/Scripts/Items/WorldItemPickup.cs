using UnityEngine;

[RequireComponent(typeof(KitchenObject))]
public class WorldItemPickup : MonoBehaviour
{
    private bool collected;
    private void Awake()
    {
        int layer = LayerMask.NameToLayer("WorldItem");
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
        // A stable interaction trigger is independent of imported model colliders and scale.
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        float scale = Mathf.Max(.001f, Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)));
        trigger.center = transform.InverseTransformPoint(transform.position + Vector3.up * .8f);
        trigger.radius = .55f / scale;
    }
    public bool TryCollect(PlayerItemController player)
    {
        if (collected || player == null || GetComponentInParent<KitchenObjectHolder>() != null)
            return false;
        if (!player.Collect(GetComponent<KitchenObject>().Instance))
            return false;
        collected = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
