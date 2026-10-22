using UnityEngine;
using UnityEngine.Serialization;

public class FoxPlacementGoal : MonoBehaviour
{
    [FormerlySerializedAs("k1"), SerializeField] private ClearCounter firstCounter;
    [FormerlySerializedAs("k2"), SerializeField] private ClearCounter secondCounter;
    [FormerlySerializedAs("k3"), SerializeField] private ClearCounter thirdCounter;
    [FormerlySerializedAs("FoxSO"), SerializeField] private KitchenObjectSO foxDefinition;

    private void Start()
    {
        if (firstCounter != null && secondCounter != null && thirdCounter != null && foxDefinition != null) return;
        Debug.LogError("狐狸摆放目标配置不完整。", this);
        enabled = false;
    }

    private void Update()
    {
        if (foxDefinition == null || firstCounter == null || secondCounter == null || thirdCounter == null) return;
        if (firstCounter.GetCurrentObjectSO() != foxDefinition || secondCounter.GetCurrentObjectSO() != foxDefinition ||
            thirdCounter.GetCurrentObjectSO() != foxDefinition) return;
        if (UIManager.Instance.ShowPanel<LevelSelectPanel>() != null) enabled = false;
    }
}
