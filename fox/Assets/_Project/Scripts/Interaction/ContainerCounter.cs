using UnityEngine;

public class ContainerCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO;

    public override void Interact(Player player) => TryGiveItem(player);

    protected bool TryGiveItem(Player player)
    {
        PlayerItemController inventory = player != null ? player.GetComponent<PlayerItemController>() : null;
        if (inventory != null)
            return kitchenObjectSO != null && kitchenObjectSO.Prefab != null && inventory.Collect(new ItemInstance(kitchenObjectSO));
        if (player == null || player.HasKitchenObject() || player.GetHoldPoint() == null) return false;
        if (!HasKitchenObject() && !CreateKitchenObject()) return false;
        return TryTransferKitchenObject(this, player);
    }

    private bool CreateKitchenObject()
    {
        if (kitchenObjectSO == null || kitchenObjectSO.Prefab == null || GetHoldPoint() == null)
        {
            Debug.LogError("物品来源缺少物品配置、预制体或挂点。", this);
            return false;
        }
        GameObject instance = Instantiate(kitchenObjectSO.Prefab, GetHoldPoint());
        if (!instance.TryGetComponent(out KitchenObject item) || !TryAddKitchenObject(item))
        {
            Debug.LogError("物品预制体缺少 KitchenObject 组件。", this);
            Destroy(instance);
            return false;
        }
        return true;
    }
}
