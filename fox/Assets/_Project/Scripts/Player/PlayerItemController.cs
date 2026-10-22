using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Player))]
public class PlayerItemController : MonoBehaviour
{
    private Player player;
    private RunSession session;
    private KitchenObject equippedView;
    private ItemInstance presentedItem;
    private float cooldown;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly RaycastHit[] groundHits = new RaycastHit[64];
    public InventoryModel Inventory => session?.Inventory;
    public event Action<string> Notice;

    private void Awake()
    {
        player = GetComponent<Player>();
        session = RunSession.Current;
    }

    private void OnEnable()
    {
        if (session == null)
            return;
        session.Inventory.Changed += SyncHeldView;
        player.Input.UseRequested += HandleUse;
        player.Input.ThrowRequested += HandleThrow;
        player.Input.StowRequested += Stow;
        SyncHeldView();
    }

    private void OnDisable()
    {
        if (session == null)
            return;
        session.Inventory.Changed -= SyncHeldView;
        player.Input.UseRequested -= HandleUse;
        player.Input.ThrowRequested -= HandleThrow;
        player.Input.StowRequested -= Stow;
    }

    private void Update() => cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);

    public bool Collect(ItemInstance item)
    {
        if (session == null || session.IsDead || !session.Inventory.Add(item))
            return false;
        Notice?.Invoke("拾取：" + item.definition.ObjectName);
        PartyAudio.Play(PartySound.Pickup);
        return true;
    }

    public bool Equip(string id)
    {
        if (session == null || session.IsDead || !session.Inventory.Equip(id))
            return false;
        PartyAudio.Play(PartySound.Equip);
        return true;
    }

    public void Stow()
    {
        if (session == null || session.IsDead || SceneLoader.IsLoading ||
            (GameplayPause.IsPaused && GameplayHUD.Current != null && !GameplayHUD.Current.InventoryOpen))
            return;
        if (session.Inventory.Equipped != null)
            PartyAudio.Play(PartySound.Equip);
        session.Inventory.Stow();
    }

    public bool TryPlace(ClearCounter counter)
    {
        if (session == null || counter == null || counter.HasKitchenObject() || equippedView == null)
            return false;
        ItemInstance item = session.Inventory.Equipped;
        if (!KitchenObjectHolder.TryTransferKitchenObject(player, counter))
            return false;
        equippedView.GetComponent<ItemView>().SetHeld(false);
        equippedView = null;
        presentedItem = null;
        session.Inventory.Remove(item);
        return true;
    }

    public bool TryCollect(ClearCounter counter)
    {
        KitchenObject view = counter != null ? counter.GetKitchenObject() : null;
        if (view == null || !Collect(view.Instance))
            return false;
        counter.ClearKitchenObject();
        Destroy(view.gameObject);
        return true;
    }

    private void SyncHeldView()
    {
        ItemInstance item = session.Inventory.Equipped;
        if (presentedItem == item && equippedView != null)
            return;
        if (equippedView != null)
        {
            player.ClearKitchenObject();
            Destroy(equippedView.gameObject);
        }
        equippedView = null;
        presentedItem = item;
        if (item == null || item.definition.Prefab == null)
            return;
        GameObject visual = ItemView.CreateVisual(item.definition, player.GetHoldPoint());
        equippedView = visual.GetComponent<KitchenObject>();
        if (equippedView == null)
        {
            Destroy(visual);
            return;
        }
        equippedView.Bind(item);
        player.TryAddKitchenObject(equippedView);
    }

    private bool CanAct => session != null && !session.IsDead && !GameplayPause.IsPaused && !SceneLoader.IsLoading && cooldown <= 0f;

    private bool PointerOverUI()
    {
        if (EventSystem.current == null)
            return false;
        var pointer = new PointerEventData(EventSystem.current) { position = player.Input.GetPointerPosition() };
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        return uiHits.Exists(hit => hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null);
    }

    private void HandleUse()
    {
        if (!PointerOverUI())
            TryUse(GetAimPoint());
    }
    private void HandleThrow()
    {
        if (!PointerOverUI())
            TryThrow(GetAimPoint());
    }

    public Vector3 GetAimPoint()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Ray ray = camera.ScreenPointToRay(player.Input.GetPointerPosition());
            int excluded = LayerMask.GetMask("PlayerCombat", "Enemy", "ItemProjectile", "WorldItem");
            int count = Physics.RaycastNonAlloc(ray, groundHits, 250f, ~excluded, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            // 当前三关使用水平地面；穿过树冠寻找地面，弹体本身仍会被树干阻挡。
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = groundHits[index];
                if (hit.normal.y < 0.6f || Mathf.Abs(hit.point.y - transform.position.y) > 0.5f || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                point = hit.point;
            }
            if (!float.IsPositiveInfinity(nearest))
                return point;
        }
        return transform.position + transform.forward * 8f;
    }

    public bool TryUse(Vector3 target)
    {
        if (!CanAct)
            return false;
        ItemInstance item = session.Inventory.Equipped;
        if (item == null)
            return false;
        KitchenObjectSO definition = item.definition;
        bool success;
        switch (definition.effect)
        {
            case ItemEffectKind.SpeedBoost:
                success = session.UseSpeed(definition);
                break;
            case ItemEffectKind.Health:
                success = session.UseHealth(definition.healthIncrease);
                break;
            case ItemEffectKind.Shield:
                success = session.UseShield(definition);
                break;
            case ItemEffectKind.Explosion:
                success = ItemProjectilePool.Current != null && ItemProjectilePool.Current.Launch(definition,
                    transform.position + Vector3.up * 1.2f, target, CombatTeam.Player, gameObject, true);
                break;
            default:
                Notice?.Invoke("这是任务物品，可以放到台面上。");
                return false;
        }
        if (!success)
        {
            Notice?.Invoke(definition.effect == ItemEffectKind.Health ? "血量已满" : "护盾已满");
            return false;
        }
        cooldown = definition.actionCooldown;
        session.Inventory.ConsumeUse(item);
        PartyAudio.Play(PartySound.Use, transform.position);
        return true;
    }

    public bool TryThrow(Vector3 target)
    {
        if (!CanAct)
            return false;
        ItemInstance item = session.Inventory.Equipped;
        if (item == null)
            return false;
        if (!item.definition.canThrow)
        {
            Notice?.Invoke("任务物品不能投掷。");
            return false;
        }
        if (ItemProjectilePool.Current == null || !ItemProjectilePool.Current.Launch(item.definition,
                transform.position + Vector3.up * 1.2f, target, CombatTeam.Player, gameObject, false))
            return false;
        cooldown = item.definition.actionCooldown;
        session.Inventory.Remove(item);
        PartyAudio.Play(PartySound.Throw, transform.position);
        return true;
    }
}
