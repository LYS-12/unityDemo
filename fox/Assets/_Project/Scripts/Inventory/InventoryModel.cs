using System;
using System.Collections.Generic;

public sealed class InventoryModel
{
    private readonly List<ItemInstance> items = new List<ItemInstance>();
    public IReadOnlyList<ItemInstance> Items => items;
    public ItemInstance Equipped { get; private set; }
    public event Action Changed;

    public bool Add(ItemInstance item)
    {
        if (item == null || item.definition == null || item.remainingDurability <= 0 || Find(item.id) != null) return false;
        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public ItemInstance Find(string id) => items.Find(item => item.id == id);

    public bool Equip(string id)
    {
        ItemInstance item = Find(id);
        if (item == null || item.remainingDurability <= 0) return false;
        Equipped = item;
        Changed?.Invoke();
        return true;
    }

    public void Stow()
    {
        if (Equipped == null) return;
        Equipped = null;
        Changed?.Invoke();
    }

    public bool Remove(ItemInstance item)
    {
        if (item == null || !items.Remove(item)) return false;
        if (Equipped == item) Equipped = null;
        Changed?.Invoke();
        return true;
    }

    public bool ConsumeUse(ItemInstance item)
    {
        if (item == null || !items.Contains(item) || item.remainingDurability <= 0) return false;
        item.remainingDurability--;
        if (item.remainingDurability == 0) return Remove(item);
        Changed?.Invoke();
        return true;
    }
}
