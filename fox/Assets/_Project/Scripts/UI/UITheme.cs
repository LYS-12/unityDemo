using TMPro;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Forest Party/UI Theme")]
public class UITheme : ScriptableObject
{
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;
    public Sprite roundedCard;
    public Sprite circle;
    public Sprite playerArrow;
    public Sprite titleArt;
    public Color cream = new Color(1f, .96f, .85f);
    public Color mint = new Color(.59f, .81f, .69f);
    public Color apricot = new Color(.98f, .74f, .52f);
    public Color ink = new Color(.22f, .34f, .29f);
    public static UITheme Current => Resources.Load<UITheme>("ForestParty/UITheme");

    public void Style(Image image, Color color)
    {
        image.sprite = roundedCard;
        image.type = Image.Type.Sliced;
        image.color = color;
    }
}

/// <summary>Small shared factory used by the HUD, dialogue and authored UI builder.</summary>
public static class PartyUI
{
    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.layer = 5;
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    public static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    public static Image Card(RectTransform rect, Color color, bool raycast = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        UITheme theme = UITheme.Current;
        if (theme != null)
            theme.Style(image, color);
        else
            image.color = color;
        image.raycastTarget = raycast;
        return image;
    }
    public static TextMeshProUGUI Text(string value, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
        float fontSize = 24, bool heading = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var text = Rect("Text", parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        UITheme theme = UITheme.Current;
        if (theme != null)
        {
            text.font = heading ? theme.headingFont : theme.bodyFont;
            text.color = theme.ink;
        }
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
    public static Button Button(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        UITheme theme = UITheme.Current;
        RectTransform rect = Rect(label, parent, anchor, position, size);
        Image image = Card(rect, theme != null ? theme.mint : Color.white, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.04f);
        colors.pressedColor = new Color(.88f, .94f, .86f);
        colors.fadeDuration = .12f;
        button.colors = colors;
        Text(label, rect, Vector2.one * .5f, Vector2.zero, size - new Vector2(24, 8), 24, true, TextAlignmentOptions.Center);
        if (action != null)
            button.onClick.AddListener(action);
        rect.gameObject.AddComponent<UIButtonFeedback>();
        return button;
    }
}
