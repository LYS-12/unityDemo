public class ClearCounter : BaseCounter
{
    public override void Interact(Player player)
    {
        if (player == null) return;
        PlayerItemController inventory = player.GetComponent<PlayerItemController>();
        if (inventory != null)
        {
            if (player.HasKitchenObject() && !HasKitchenObject()) inventory.TryPlace(this);
            else if (!player.HasKitchenObject() && HasKitchenObject()) inventory.TryCollect(this);
            return;
        }
        if (player.HasKitchenObject() && !HasKitchenObject())
            TryTransferKitchenObject(player, this);
        else if (!player.HasKitchenObject() && HasKitchenObject())
            TryTransferKitchenObject(this, player);
    }

    public KitchenObjectSO GetCurrentObjectSO()
    {
        KitchenObject item = GetKitchenObject();
        return item != null ? item.GetKitchenObjectSO() : null;
    }
}
