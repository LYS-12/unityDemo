using UnityEngine;

public class KitchenObjectHolder : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private KitchenObject kitchenObject;

    public KitchenObject GetKitchenObject() => kitchenObject;
    public bool HasKitchenObject() => kitchenObject != null;
    public Transform GetHoldPoint() => holdPoint;

    public bool TryAddKitchenObject(KitchenObject item)
    {
        if (item == null || holdPoint == null || (kitchenObject != null && kitchenObject != item))
            return false;
        // 保留世界缩放，避免不同缩放的台面与玩家改变物品尺寸。
        item.transform.SetParent(holdPoint, true);
        item.transform.localPosition = Vector3.zero;
        kitchenObject = item;
        return true;
    }

    /// <summary>转移成功后才清空源容器；调用者不必是源或目标。</summary>
    public static bool TryTransferKitchenObject(KitchenObjectHolder source, KitchenObjectHolder target)
    {
        if (source == null || target == null || source == target || target.HasKitchenObject())
            return false;
        KitchenObject item = source.GetKitchenObject();
        if (item == null || !target.TryAddKitchenObject(item)) return false;
        Vector3 forward = target.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            item.transform.rotation = Quaternion.LookRotation(forward.normalized);
        source.ClearKitchenObject();
        return true;
    }

    public void ClearKitchenObject() => kitchenObject = null;
}
