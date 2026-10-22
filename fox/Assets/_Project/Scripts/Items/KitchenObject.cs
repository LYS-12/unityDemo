using UnityEngine;

public class KitchenObject : MonoBehaviour
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO;
    private ItemInstance instance;
    public KitchenObjectSO GetKitchenObjectSO() => kitchenObjectSO;

    public ItemInstance Instance => instance ?? (kitchenObjectSO != null ? instance = new ItemInstance(kitchenObjectSO) : null);
    public void Bind(ItemInstance item)
    {
        instance = item;
        kitchenObjectSO = item.definition;
    }
}
