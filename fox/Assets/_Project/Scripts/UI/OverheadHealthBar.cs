using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>World anchor projected into one screen-space canvas, including pooled actors.</summary>
public class OverheadHealthBar : MonoBehaviour
{
    public string actorName;
    [Min(.1f)] public float height = 2.8f;
    public Color fillColor = new Color(.58f, .80f, .65f);
    private IHealthSource source;
    private RectTransform view;
    private Image fill;
    private TextMeshProUGUI label;
    private float shown = 1;
    private Canvas canvas;
    private void OnEnable()
    {
        source = GetComponent<IHealthSource>();
        if (source != null)
            source.HealthChanged += Refresh;
        shown = 1;
    }
    private void Create()
    {
        if (GameplayHUD.Current == null)
            return;
        canvas = GameplayHUD.Current.GetComponent<Canvas>();
        view = PartyUI.Rect("Health_" + name, canvas.transform, Vector2.one * .5f, Vector2.zero, new Vector2(150, 46));
        PartyUI.Card(view, new Color(1, .96f, .85f, .94f));
        label = PartyUI.Text("", view, new Vector2(.5f, 1), new Vector2(0, -3), new Vector2(140, 22), 15, false, TextAlignmentOptions.Center);
        RectTransform track = PartyUI.Rect("Track", view, new Vector2(.5f, 0), new Vector2(0, 7), new Vector2(128, 9));
        PartyUI.Card(track, new Color(.76f, .79f, .70f));
        fill = PartyUI.Card(PartyUI.Stretch("Fill", track), fillColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        Refresh();
    }
    private void Refresh()
    {
        if (source != null && label != null)
            label.text = (string.IsNullOrEmpty(actorName) ? source.DisplayName : actorName) + "  " + source.CurrentHealth + "/" + source.MaximumHealth;
    }
    private void LateUpdate()
    {
        if (source == null)
            return;
        if (view == null)
            Create();
        if (view == null)
            return;
        Camera camera = Camera.main;
        Vector3 screen = camera != null ? camera.WorldToScreenPoint(transform.position + Vector3.up * height) : Vector3.back;
        bool visible = screen.z > 0 && !StoryCinematic.IsPlaying && (source.CurrentHealth > 0 || source is FoxCompanion) && !SceneLoader.IsLoading;
        view.gameObject.SetActive(visible);
        if (!visible)
            return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 position);
        view.anchoredPosition = position;
        shown = Mathf.MoveTowards(shown, (float)source.CurrentHealth / Mathf.Max(1, source.MaximumHealth), Time.unscaledDeltaTime * 1.8f);
        fill.fillAmount = shown;
    }
    private void OnDisable()
    {
        if (source != null)
            source.HealthChanged -= Refresh;
        if (view != null)
            Destroy(view.gameObject);
        view = null;
    }
}
