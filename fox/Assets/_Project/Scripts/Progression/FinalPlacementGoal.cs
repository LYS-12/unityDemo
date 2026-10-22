using UnityEngine;
using UnityEngine.Serialization;

public class FinalPlacementGoal : MonoBehaviour
{
    [FormerlySerializedAs("k1"), SerializeField] private ClearCounter counter;
    [FormerlySerializedAs("Cheese"), SerializeField] private KitchenObjectSO requiredItem;

    private void Start()
    {
        if (counter != null && requiredItem != null) return;
        Debug.LogError("最终摆放目标配置不完整。", this);
        enabled = false;
    }

    private void Update()
    {
        if (counter == null || requiredItem == null || counter.GetCurrentObjectSO() != requiredItem) return;
        if (UIManager.Instance.ShowPanel<CompletionPanel>() != null) enabled = false;
    }
}
