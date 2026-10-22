using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>场景级视图；会话数据与暂停所有权不保存在 UI 对象里。</summary>
public class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Current
    {
        get; private set;
    }
    public TMP_FontAsset font;
    public bool InventoryOpen
    {
        get; private set;
    }
    private Player player;
    private PlayerItemController items;
    private RunSession session;
    private GameplayCatalog catalog;
    private readonly object inventoryPause = new object();
    private readonly object deathPause = new object();
    private readonly List<Image> hearts = new List<Image>();
    private GameObject inventoryPanel, deathPanel;
    private RectTransform grid;
    private TextMeshProUGUI healthText, equippedText, countText, effectText, toast, waveText;
    private Image equippedIcon;
    private Image noticeCard;
    private float toastTime;
    private EnemySpawner spawner;
    private readonly Color cream = new Color(.22f, .34f, .29f);
    private readonly Color gold = new Color(.44f, .55f, .37f);
    private TextMeshProUGUI questText;
    private CanvasGroup visibility;

    public void Bind(Player owner, GameplayCatalog settings)
    {
        Current = this;
        player = owner;
        catalog = settings;
        session = RunSession.Current;
        items = player.GetComponent<PlayerItemController>();
        spawner = FindObjectOfType<EnemySpawner>();
        Canvas sceneCanvas = transform.parent.GetComponentInParent<Canvas>();
        Canvas hudCanvas = GetComponent<Canvas>();
        hudCanvas.renderMode = sceneCanvas.renderMode;
        hudCanvas.worldCamera = sceneCanvas.worldCamera;
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 50;
        Build();
        gameObject.AddComponent<CircularMinimap>();
        visibility = gameObject.AddComponent<CanvasGroup>();
        PartyAudio.Ensure();
        session.Inventory.Changed += Refresh;
        session.Changed += Refresh;
        session.Died += ShowDeath;
        items.Notice += ShowNotice;
        player.Input.InventoryRequested += ToggleInventory;
        player.Input.CancelRequested += CloseInventory;
        Refresh();
        if (session.IsDead)
            ShowDeath();
    }

    private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Image Image(RectTransform rect, Color color, Sprite sprite = null, bool raycast = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (sprite == null && UITheme.Current != null)
            UITheme.Current.Style(image, new Color(.99f, .96f, .87f, color.a));
        if (sprite != null)
            image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = raycast;
        return image;
    }

    private TextMeshProUGUI Text(string value, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
        int fontSize = 22, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect("Text", parent, anchor, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = fontSize >= 30 ? UITheme.Current.headingFont : font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = cream;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private Button Button(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect(label, parent, anchor, position, size);
        Image background = Image(rect, new Color(0.17f, 0.23f, 0.15f, 0.98f), null, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.3f, 1.1f);
        colors.pressedColor = new Color(0.75f, 0.85f, 0.65f);
        button.colors = colors;
        TextMeshProUGUI text = Text(label, rect, Vector2.one * 0.5f, Vector2.zero, size - Vector2.one * 8f, 20, TextAlignmentOptions.Center);
        text.color = cream;
        text.font = UITheme.Current.headingFont;
        button.onClick.AddListener(action);
        background.color = UITheme.Current.mint;
        rect.gameObject.AddComponent<UIButtonFeedback>();
        return button;
    }

    private void Build()
    {
        RectTransform root = (RectTransform)transform;
        gameObject.layer = LayerMask.NameToLayer("UI");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        RectTransform health = Rect("QuestCard", root, new Vector2(0, 1), new Vector2(28, -24), new Vector2(420, 146));
        Image(health, new Color(0.04f, 0.07f, 0.04f, 0.78f));
        healthText = Text("森林手记", health, new Vector2(0, 1), new Vector2(20, -12), new Vector2(350, 32), 27);
        healthText.font = UITheme.Current.headingFont;
        questText = Text("", health, new Vector2(0, 1), new Vector2(20, -52), new Vector2(380, 82), 20);
        Image(Rect("StatusPill", root, new Vector2(0, 1), new Vector2(28, -182), new Vector2(420, 44)), Color.white);
        effectText = Text("", root, new Vector2(0, 1), new Vector2(44, -188), new Vector2(390, 32), 20);
        RectTransform held = Rect("HeldItem", root, Vector2.zero, new Vector2(28, 28), new Vector2(520, 116));
        Image(held, new Color(0.04f, 0.07f, 0.04f, 0.88f));
        equippedIcon = Image(Rect("Icon", held, Vector2.zero, new Vector2(12, 14), new Vector2(80, 80)), Color.white);
        equippedText = Text("", held, Vector2.zero, new Vector2(108, 32), new Vector2(400, 72), 22);
        Text("E 拾取 / 摆放    左键 使用    右键 投掷    R 收起", held, Vector2.zero,
            new Vector2(12, 5), new Vector2(500, 25), 17);
        Button bagButton = Button("", root, new Vector2(1, 1), new Vector2(-28, -26), new Vector2(192, 68), ToggleInventory);
        countText = bagButton.GetComponentInChildren<TextMeshProUGUI>();
        if (spawner != null)
            Image(Rect("WavePill", root, new Vector2(1, 1), new Vector2(-28, -106), new Vector2(310, 44)), Color.white);
        waveText = Text("", root, new Vector2(1, 1), new Vector2(-42, -112), new Vector2(286, 32), 20, TextAlignmentOptions.Right);
        noticeCard = Image(Rect("NoticePill", root, new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(620, 58)), Color.white);
        noticeCard.enabled = false;
        toast = Text("", root, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(600, 46), 24, TextAlignmentOptions.Center);
        inventoryPanel = Overlay("Inventory", root, 110);
        RectTransform window = Rect("Window", inventoryPanel.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(760, 660));
        Image(window, new Color(0.065f, 0.10f, 0.065f, 0.99f), null, true);
        Text("行 囊", window, new Vector2(0, 1), new Vector2(30, -22), new Vector2(240, 58), 36);
        Text("选择一件拿在手中 · 每件道具独立保存耐久", window, new Vector2(0, 1), new Vector2(30, -82), new Vector2(690, 32), 20);
        Button("关闭 [Tab]", window, new Vector2(1, 1), new Vector2(-26, -24), new Vector2(154, 46), CloseInventory);
        RectTransform viewport = Rect("Viewport", window, new Vector2(0, 1), new Vector2(26, -132), new Vector2(708, 424));
        Image(viewport, new Color(0.09f, 0.13f, 0.08f));
        viewport.gameObject.AddComponent<RectMask2D>();
        grid = Rect("Items", viewport, new Vector2(0, 1), Vector2.zero, new Vector2(708, 424));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(162, 128);
        layout.spacing = new Vector2(12, 12);
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        ContentSizeFitter fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = grid;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        Button("收起手持 [R]", window, new Vector2(0, 0), new Vector2(28, 30), new Vector2(210, 52), () => items.Stow());
        Text("Tab / Esc 返回游戏", window, new Vector2(1, 0), new Vector2(-28, 34), new Vector2(300, 40), 20, TextAlignmentOptions.Right);
        inventoryPanel.SetActive(false);
        deathPanel = Overlay("Defeat", root, 160);
        RectTransform deathWindow = Rect("Window", deathPanel.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(560, 330));
        Image(deathWindow, new Color(0.10f, 0.12f, 0.08f, 0.99f), null, true);
        Text("旅程暂歇", deathWindow, new Vector2(0.5f, 1), new Vector2(0, -35), new Vector2(470, 76), 38, TextAlignmentOptions.Center);
        Text("重试将恢复初始道具与 5 / 5 生命", deathWindow, new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(490, 40), 21, TextAlignmentOptions.Center);
        Button("重试本关", deathWindow, new Vector2(0, 0), new Vector2(35, 45), new Vector2(220, 64), Retry);
        Button("返回菜单", deathWindow, new Vector2(1, 0), new Vector2(-35, 45), new Vector2(220, 64), ReturnToMenu);
        deathPanel.SetActive(false);
    }

    private GameObject Overlay(string name, Transform parent, int order)
    {
        RectTransform root = Stretch(name, parent);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        root.gameObject.AddComponent<GraphicRaycaster>();
        Image overlay = Image(root, new Color(0.01f, 0.025f, 0.02f, 0.80f), null, true);
        overlay.sprite = null;
        overlay.color = new Color(.17f, .28f, .24f, .68f);
        root.gameObject.AddComponent<PanelTween>();
        return root.gameObject;
    }

    public void ToggleInventory()
    {
        if (session == null || session.IsDead || SceneLoader.IsLoading || (GameplayPause.IsPaused && !InventoryOpen))
            return;
        SetInventory(!InventoryOpen);
    }

    public void CloseInventory() => SetInventory(false);
    private void SetInventory(bool open)
    {
        if (inventoryPanel == null)
            return;
        if (InventoryOpen == open)
            return;
        InventoryOpen = open;
        PartyAudio.Play(open ? PartySound.Confirm : PartySound.Back);
        inventoryPanel.GetComponent<PanelTween>().SetVisible(open, () => { if (!InventoryOpen) GameplayPause.Set(inventoryPause, false); });
        if (open)
            GameplayPause.Set(inventoryPause, true);
        if (open)
            Refresh();
    }

    private void Refresh()
    {
        if (session == null || healthText == null)
            return;
        for (int index = 0; index < hearts.Count; index++)
        {
            hearts[index].gameObject.SetActive(index < session.MaxHealth);
            hearts[index].color = index < session.CurrentHealth ? Color.white : new Color(0.28f, 0.28f, 0.24f, 0.7f);
        }
        countText.text = "背包 [Tab]  " + session.Inventory.Items.Count;
        ItemInstance equipped = session.Inventory.Equipped;
        equippedIcon.enabled = equipped != null;
        if (equipped != null)
            equippedIcon.sprite = equipped.definition.Sprite;
        equippedText.text = equipped != null ? equipped.definition.ObjectName + "  " + equipped.remainingDurability + " / " + equipped.definition.maxDurability +
            (equipped.definition.effect == ItemEffectKind.None ? "\n任务物品 · E 摆放" : "\n左键使用 · 右键整件投掷") : "空手\n打开背包选择道具";
        if (!InventoryOpen)
            return;
        foreach (Transform child in grid)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        foreach (ItemInstance item in session.Inventory.Items)
        {
            string id = item.id;
            Button slot = Button("", grid, new Vector2(0, 1), Vector2.zero, new Vector2(162, 128), () => items.Equip(id));
            if (item == equipped)
                slot.GetComponent<Image>().color = UITheme.Current.apricot;
            Image(Rect("Icon", slot.transform, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(64, 64)), Color.white, item.definition.Sprite);
            Text(item.definition.ObjectName + (item == equipped ? " · 手持" : ""), slot.transform,
                new Vector2(0.5f, 0), new Vector2(0, 27), new Vector2(154, 27), 19, TextAlignmentOptions.Center);
            Text("耐久 " + item.remainingDurability + " / " + item.definition.maxDurability, slot.transform,
                new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(154, 25), 17, TextAlignmentOptions.Center).color = gold;
        }
    }

    public void ShowNotice(string message)
    {
        toast.text = message;
        toastTime = 2.5f;
    }

    private void Update()
    {
        if (session == null)
            return;
        visibility.alpha = StoryCinematic.IsPlaying ? 0 : 1;
        visibility.blocksRaycasts = !StoryCinematic.IsPlaying;
        if (FoxEscortQuest.Current != null)
        {
            FoxEscortQuest quest = FoxEscortQuest.Current;
            healthText.text = "朋友重聚  " + quest.ArrivedCount + " / 3";
            questText.text = quest.StatusText();
        }
        else
            questText.text = SceneManager.GetActiveScene().name == "GameScene2" ? "把番茄、芝士和面包放到对应的台面上。" : "把最后的心意带到台面上。";
        effectText.text = "护盾 " + session.ShieldCharges + " / 3" + (session.BoostRemaining > 0f ? "   加速 " + Mathf.CeilToInt(session.BoostRemaining) + " 秒" : "");
        toastTime -= Time.unscaledDeltaTime;
        if (toastTime <= 0f)
            toast.text = "";
        noticeCard.enabled = toastTime > 0 && !string.IsNullOrEmpty(toast.text);
        waveText.text = spawner != null ? "怪物 " + spawner.SpawnedCount + " / " + spawner.totalLimit + "   存活 " + spawner.AliveCount : "";
    }

    private void ShowDeath()
    {
        CloseInventory();
        deathPanel.GetComponent<PanelTween>().SetVisible(true);
        GameplayPause.Set(deathPause, true);
    }

    public void Retry()
    {
        if (SceneLoader.IsLoading)
            return;
        RunSession.BeginNew(catalog);
        RunSession.Current.IntroSeen = true;
        SceneLoader.Load(CurrentLevel(), ShowOrder);
    }

    public void ReturnToMenu() => SceneLoader.Load(SceneLoader.Scene.StartScene, () => UIManager.Instance.ShowPanel<StartPanel>());

    private static SceneLoader.Scene CurrentLevel()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case "GameScene2":
                return SceneLoader.Scene.GameScene2;
            case "GameScene3":
                return SceneLoader.Scene.GameScene3;
            default:
                return SceneLoader.Scene.GameScene1;
        }
    }

    private static void ShowOrder()
    {
        switch (CurrentLevel())
        {
            case SceneLoader.Scene.GameScene1:
                break;
            case SceneLoader.Scene.GameScene2:
                UIManager.Instance.ShowPanel<OrderPanel2>();
                break;
            case SceneLoader.Scene.GameScene3:
                UIManager.Instance.ShowPanel<OrderPanel3>();
                break;
        }
    }

    private void OnDestroy()
    {
        GameplayPause.Set(inventoryPause, false);
        GameplayPause.Set(deathPause, false);
        if (Current == this)
            Current = null;
        if (session != null)
        {
            session.Inventory.Changed -= Refresh;
            session.Changed -= Refresh;
            session.Died -= ShowDeath;
        }
        if (items != null)
            items.Notice -= ShowNotice;
        if (player != null)
        {
            player.Input.InventoryRequested -= ToggleInventory;
            player.Input.CancelRequested -= CloseInventory;
        }
    }
}
