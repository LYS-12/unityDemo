using System;
using UnityEngine;

public enum ItemEffectKind { None, SpeedBoost, Health, Shield, Explosion }

/// <summary>一件物品的身份和耐久；定义资产中不保存运行时状态。</summary>
[Serializable]
public sealed class ItemInstance
{
    public string id;
    public KitchenObjectSO definition;
    public int remainingDurability;

    public ItemInstance(KitchenObjectSO definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        id = Guid.NewGuid().ToString("N");
        this.definition = definition;
        remainingDurability = Mathf.Max(1, definition.maxDurability);
    }
}
