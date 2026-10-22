using System;
using UnityEngine;

/// <summary>仅在本局中存在。场景视图销毁后，道具实例和玩家状态仍保留。</summary>
public sealed class RunSession
{
    public static RunSession Current
    {
        get; private set;
    }
    public InventoryModel Inventory { get; } = new InventoryModel();
    public GameplayCatalog Catalog
    {
        get;
    }
    public int CurrentHealth
    {
        get; private set;
    }
    public int MaxHealth
    {
        get; private set;
    }
    public int ShieldCharges
    {
        get; private set;
    }
    public float BoostRemaining
    {
        get; private set;
    }
    public float BoostMultiplier { get; private set; } = 1f;
    public float ProtectionRemaining
    {
        get; private set;
    }
    public KitchenObjectSO ShieldDefinition
    {
        get; private set;
    }
    public bool IsDead => CurrentHealth <= 0;
    public bool IntroSeen
    {
        get; set;
    }
    public event Action Changed;
    public event Action Died;

    private RunSession(GameplayCatalog catalog)
    {
        Catalog = catalog;
        MaxHealth = Mathf.Clamp(catalog.initialHealth, 1, Mathf.Max(1, catalog.healthCap));
        CurrentHealth = MaxHealth;
        if (catalog.startingItems == null)
            return;
        foreach (KitchenObjectSO definition in catalog.startingItems)
            if (definition != null)
                for (int index = 0; index < catalog.startingCount; index++)
                    Inventory.Add(new ItemInstance(definition));
    }

    public static RunSession Ensure(GameplayCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        return Current ?? (Current = new RunSession(catalog));
    }

    public static RunSession BeginNew(GameplayCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        return Current = new RunSession(catalog);
    }

    public static void End() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => End();

    public bool UseHealth(int amount)
    {
        if (IsDead || amount <= 0 || (MaxHealth >= Catalog.healthCap && CurrentHealth >= MaxHealth))
            return false;
        MaxHealth = Mathf.Min(Catalog.healthCap, MaxHealth + amount);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        Changed?.Invoke();
        return true;
    }

    public bool UseShield(KitchenObjectSO definition)
    {
        if (IsDead || definition == null || ShieldCharges >= Mathf.Min(3, definition.shieldLimit))
            return false;
        ShieldCharges++;
        ShieldDefinition = definition;
        Changed?.Invoke();
        return true;
    }

    public bool UseSpeed(KitchenObjectSO definition)
    {
        if (IsDead || definition == null)
            return false;
        BoostRemaining = Mathf.Max(0.1f, definition.speedDuration);
        BoostMultiplier = Mathf.Max(1f, definition.speedMultiplier);
        Changed?.Invoke();
        return true;
    }

    public bool TryTakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || ProtectionRemaining > 0f)
            return false;
        ProtectionRemaining = Mathf.Max(0f, Catalog.damageProtection);
        if (ShieldCharges > 0)
            ShieldCharges--;
        else
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        Changed?.Invoke();
        if (IsDead)
            Died?.Invoke();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (IsDead || deltaTime <= 0f)
            return;
        bool wasBoosted = BoostRemaining > 0f;
        BoostRemaining = Mathf.Max(0f, BoostRemaining - deltaTime);
        ProtectionRemaining = Mathf.Max(0f, ProtectionRemaining - deltaTime);
        if (wasBoosted && BoostRemaining == 0f)
        {
            BoostMultiplier = 1f;
            Changed?.Invoke();
        }
    }
}
