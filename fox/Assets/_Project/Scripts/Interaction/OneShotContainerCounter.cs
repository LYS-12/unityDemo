using UnityEngine;

/// <summary>保留原行为：成功给予物品后移除此交互组件，保留场景物体。</summary>
public class OneShotContainerCounter : ContainerCounter
{
    private bool consumed;

    public override void Interact(Player player)
    {
        if (consumed || !TryGiveItem(player)) return;
        consumed = true;
        CancelSelect();
        player.SetSelectedCounter(null);
        enabled = false;
        Destroy(this);
    }
}
