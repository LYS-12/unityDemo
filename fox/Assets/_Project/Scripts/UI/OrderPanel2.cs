using UnityEngine;
using UnityEngine.Serialization;

/// <summary>保留第 2 关已有订单展示；动态配方列表尚未实现。</summary>
public class OrderPanel2 : BasePanel
{
    [FormerlySerializedAs("RecipeList"), SerializeField] private Transform recipeList;
    [SerializeField] private RecipeUI recipeUI;
    public override void Init() { }
}
