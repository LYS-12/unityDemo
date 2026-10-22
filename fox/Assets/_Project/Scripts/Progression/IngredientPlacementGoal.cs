using UnityEngine;
using UnityEngine.Serialization;

public class IngredientPlacementGoal : MonoBehaviour
{
    [FormerlySerializedAs("tomo"), SerializeField] private ClearCounter tomatoCounter;
    [FormerlySerializedAs("chee"), SerializeField] private ClearCounter cheeseCounter;
    [SerializeField] private ClearCounter bread;
    [FormerlySerializedAs("tomoSO"), SerializeField] private KitchenObjectSO tomatoDefinition;
    [FormerlySerializedAs("cheeSO"), SerializeField] private KitchenObjectSO cheeseDefinition;
    [FormerlySerializedAs("breadSO"), SerializeField] private KitchenObjectSO breadDefinition;

    private void Start()
    {
        if (tomatoCounter != null && cheeseCounter != null && bread != null && tomatoDefinition != null &&
            cheeseDefinition != null && breadDefinition != null) return;
        Debug.LogError("食材摆放目标配置不完整。", this);
        enabled = false;
    }

    private void Update()
    {
        if (tomatoCounter == null || cheeseCounter == null || bread == null || tomatoDefinition == null ||
            cheeseDefinition == null || breadDefinition == null) return;
        if (tomatoCounter.GetCurrentObjectSO() != tomatoDefinition || cheeseCounter.GetCurrentObjectSO() != cheeseDefinition ||
            bread.GetCurrentObjectSO() != breadDefinition) return;
        if (UIManager.Instance.ShowPanel<LevelSelectPanel>() != null) enabled = false;
    }
}
