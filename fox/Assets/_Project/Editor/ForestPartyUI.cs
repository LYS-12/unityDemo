using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class ForestPartySetup
{
    private static GameObject Panel<T>(string name) where T : BasePanel
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.AddComponent<T>();
        return root;
    }
    private static void SavePanel(GameObject root)
    {
        PrefabUtility.SaveAsPrefabAsset(root, Root + "/Resources/UI/" + root.name + ".prefab");
        Object.DestroyImmediate(root);
    }
    private static RectTransform Modal(Transform root, Vector2 size, string title, string description)
    {
        PartyUI.Card(PartyUI.Stretch("SoftBackdrop", root), new Color(.18f, .31f, .26f, .70f), true);
        var window = PartyUI.Rect("Card", root, Vector2.one * .5f, Vector2.zero, size);
        PartyUI.Card(window, UITheme.Current.cream, true);
        PartyUI.Text(title, window, new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(size.x - 80, 66), 40, true, TextAlignmentOptions.Center);
        PartyUI.Text(description, window, new Vector2(.5f, 1), new Vector2(0, -112), new Vector2(size.x - 80, 64), 22, false, TextAlignmentOptions.Center);
        return window;
    }
    private static void CreatePanels(UITheme theme)
    {
        GameObject root = Panel<StartPanel>("StartPanel");
        var start = root.GetComponent<StartPanel>();
        Image hero = PartyUI.Stretch("ForestPartyArtwork", root.transform).gameObject.AddComponent<Image>();
        hero.sprite = theme.titleArt;
        hero.type = Image.Type.Simple;
        hero.preserveAspect = false;
        hero.raycastTarget = false;
        var card = PartyUI.Rect("WelcomeCard", root.transform, new Vector2(1, .5f), new Vector2(-90, 0), new Vector2(590, 840));
        PartyUI.Card(card, new Color(1, .96f, .85f, .96f));
        PartyUI.Text("一份迟到的邀请", card, new Vector2(.5f, 1), new Vector2(0, -44), new Vector2(470, 40), 23, false, TextAlignmentOptions.Center);
        var title = PartyUI.Text("幽灵的\n森林派对", card, new Vector2(.5f, 1), new Vector2(0, -104), new Vector2(520, 190), 70, true, TextAlignmentOptions.Center);
        title.lineSpacing = 8;
        PartyUI.Text("把勇气装进行囊，\n把朋友带回有灯光的地方。", card, new Vector2(.5f, 1), new Vector2(0, -310), new Vector2(470, 80), 25, false, TextAlignmentOptions.Center);
        Button begin = PartyUI.Button("开始我们的故事", card, new Vector2(.5f, 1), new Vector2(0, -430), new Vector2(430, 78), null);
        begin.GetComponent<Image>().color = theme.apricot;
        Button select = PartyUI.Button("关卡选择", card, new Vector2(.5f, 1), new Vector2(0, -530), new Vector2(430, 66), null);
        Button settings = PartyUI.Button("设置", card, new Vector2(.5f, 1), new Vector2(0, -616), new Vector2(430, 66), null);
        Button quit = PartyUI.Button("退出", card, new Vector2(.5f, 1), new Vector2(0, -702), new Vector2(430, 60), null);
        PartyUI.Text("FOREST PARTY  ·  A LITTLE HELLO", root.transform, new Vector2(0, 0), new Vector2(42, 30), new Vector2(700, 30), 18).color = theme.cream;
        Set(start, "startBtn", begin);
        Set(start, "settingBtn", settings);
        Set(start, "levelSelectBtn", select);
        Set(start, "quitBtn", quit);
        SavePanel(root);

        root = Panel<LevelSelectPanel>("LevelSelectPanel");
        var selection = root.GetComponent<LevelSelectPanel>();
        var window = Modal(root.transform, new Vector2(1100, 620), "下一站，去哪里？", "森林里的故事，还在等你继续。");
        string[] fields = { "btnCard1", "btnCard2", "btnCard3" };
        string[] labels = { "01\n森林派对\n寻找 · 道歉 · 护送", "02\n准备好心意\n食材摆放", "03\n最后的邀请\n完成心愿" };
        for (int i = 0; i < 3; i++)
            Set(selection, fields[i], PartyUI.Button(labels[i], window, new Vector2(.5f, .5f), new Vector2((i - 1) * 330, -28), new Vector2(296, 258), null));
        Set(selection, "backBtn", PartyUI.Button("返回", window, new Vector2(.5f, 0), new Vector2(0, 34), new Vector2(240, 58), null));
        SavePanel(root);

        root = Panel<SettingsPanel>("SettingsPanel");
        var panel = root.GetComponent<SettingsPanel>();
        window = Modal(root.transform, new Vector2(800, 610), "把声音调成喜欢的样子", "音乐轻一点，森林也会认真倾听。");
        Set(panel, "bgSlider", Slider(window, "森林音乐", -50));
        Set(panel, "bkSlider", Slider(window, "声音与对白", -150));
        Set(panel, "btnExit", PartyUI.Button("保存并返回", window, new Vector2(.5f, 0), new Vector2(0, 42), new Vector2(340, 64), null));
        SavePanel(root);

        root = Panel<CompletionPanel>("CompletionPanel");
        window = Modal(root.transform, new Vector2(790, 480), "谢谢你，让今晚完整了。", "心意已经送到，朋友一直都在。");
        Set(root.GetComponent<CompletionPanel>(), "btn", PartyUI.Button("回到标题页", window, new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(390, 72), null));
        SavePanel(root);
        for (int level = 1; level <= 3; level++)
        {
            root = level == 1 ? Panel<OrderPanel1>("OrderPanel1") : level == 2 ? Panel<OrderPanel2>("OrderPanel2") : Panel<OrderPanel3>("OrderPanel3");
            // The minimap is owned by the shared HUD. Old order prefabs keep their public type identity.
            if (level > 1)
            {
                var strip = PartyUI.Rect("RecipeReminder", root.transform, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(550, 70));
                PartyUI.Card(strip, theme.cream);
                PartyUI.Text(level == 2 ? "今日心意  ·  番茄 / 芝士 / 面包" : "最后的心愿  ·  送到指定台面", strip, Vector2.one * .5f, Vector2.zero, new Vector2(510, 54), 23, true, TextAlignmentOptions.Center);
            }
            SavePanel(root);
        }
        string hudPath = Root + "/Prefabs/Gameplay/GameplayHUD.prefab";
        GameObject hud = PrefabUtility.LoadPrefabContents(hudPath);
        hud.GetComponent<GameplayHUD>().font = theme.bodyFont;
        PrefabUtility.SaveAsPrefabAsset(hud, hudPath);
        PrefabUtility.UnloadPrefabContents(hud);
    }
    private static Slider Slider(Transform parent, string label, float y)
    {
        PartyUI.Text(label, parent, new Vector2(0, .5f), new Vector2(60, y + 17), new Vector2(210, 38), 25);
        var rect = PartyUI.Rect(label, parent, new Vector2(1, .5f), new Vector2(-62, y), new Vector2(410, 34));
        PartyUI.Card(PartyUI.Stretch("Track", rect), new Color(.81f, .85f, .75f));
        var fill = PartyUI.Stretch("Fill", rect);
        PartyUI.Card(fill, UITheme.Current.mint);
        var handle = PartyUI.Rect("Handle", rect, Vector2.one * .5f, Vector2.zero, new Vector2(38, 42));
        Image handleImage = PartyUI.Card(handle, UITheme.Current.apricot, true);
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.minValue = 0;
        slider.maxValue = 1;
        return slider;
    }
}
