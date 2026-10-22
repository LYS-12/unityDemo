using UnityEngine;
using UnityEngine.UI;

public class MinimapSymbols : MaskableGraphic
{
    public CircularMinimap map;
    public Image PlayerMarker
    {
        get; private set;
    }
    public int FrustumLineCount => lines == null ? 0 : lines.Length;
    private Image[] lines;
    private Image[] foxMarkers, enemyMarkers;
    private Image partyMarker;
    private EnemySpawner spawner;
    protected override void Start()
    {
        base.Start();
        UITheme theme = UITheme.Current;
        lines = new Image[4];
        for (int i = 0; i < 4; i++)
            lines[i] = Marker("CameraFootprint_" + i, theme.cream, Vector2.one * 2);
        partyMarker = Marker("PartyDiamond", new Color(1, .78f, .3f), Vector2.one * 13);
        partyMarker.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        foxMarkers = new Image[3];
        enemyMarkers = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            foxMarkers[i] = Marker("FoxMark_" + i, new Color(1, .65f, .32f), Vector2.one * 9);
            enemyMarkers[i] = Marker("EnemyMark_" + i, new Color(.9f, .3f, .35f), Vector2.one * 8);
        }
        PlayerMarker = Marker("PlayerArrow", Color.white, new Vector2(24, 28));
        PlayerMarker.sprite = theme.playerArrow;
        spawner = Object.FindObjectOfType<EnemySpawner>();
    }
    private Image Marker(string name, Color tint, Vector2 size)
    {
        var image = PartyUI.Rect(name, transform, Vector2.one * .5f, Vector2.zero, size).gameObject.AddComponent<Image>();
        image.sprite = UITheme.Current.circle;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }
    private void LateUpdate()
    {
        if (PlayerMarker == null || map == null || map.MapCamera == null || Player.Instance == null)
            return;
        PlayerMarker.rectTransform.anchoredPosition = Vector2.zero;
        PlayerMarker.rectTransform.localEulerAngles = new Vector3(0, 0, -Player.Instance.transform.eulerAngles.y);
        Camera camera = Camera.main;
        var plane = new Plane(Vector3.up, Player.Instance.transform.position);
        Vector2[] points = new Vector2[4];
        Vector2[] corners = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        bool valid = camera != null;
        for (int i = 0; i < 4 && valid; i++)
        {
            Ray ray = camera.ViewportPointToRay(corners[i]);
            if (plane.Raycast(ray, out float distance) && distance < 300)
                points[i] = map.ToMap(ray.GetPoint(distance));
            else
                valid = false;
        }
        for (int i = 0; i < 4; i++)
        {
            lines[i].gameObject.SetActive(valid);
            if (!valid)
                continue;
            Vector2 delta = points[(i + 1) % 4] - points[i];
            var rect = lines[i].rectTransform;
            rect.anchoredPosition = (points[i] + points[(i + 1) % 4]) * .5f;
            rect.sizeDelta = new Vector2(delta.magnitude, 2);
            rect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        FoxEscortQuest quest = FoxEscortQuest.Current;
        partyMarker.gameObject.SetActive(quest != null);
        if (quest != null)
            partyMarker.rectTransform.anchoredPosition = map.ToMap(quest.partyPoint.position);
        for (int i = 0; i < foxMarkers.Length; i++)
        {
            bool visible = quest != null && i < quest.foxes.Length && quest.foxes[i] != null;
            foxMarkers[i].gameObject.SetActive(visible);
            if (visible)
            {
                foxMarkers[i].rectTransform.anchoredPosition = map.ToMap(quest.foxes[i].transform.position);
                foxMarkers[i].color = quest.foxes[i].State == FoxEscortState.Downed ? new Color(.95f, .3f, .3f) : new Color(1, .68f, .33f);
            }
        }
        int index = 0;
        if (spawner != null)
            foreach (var enemy in spawner.ActiveEnemies)
            {
                if (index >= enemyMarkers.Length)
                    break;
                enemyMarkers[index].gameObject.SetActive(true);
                enemyMarkers[index++].rectTransform.anchoredPosition = map.ToMap(enemy.transform.position);
            }
        for (; index < enemyMarkers.Length; index++)
            enemyMarkers[index].gameObject.SetActive(false);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); // Symbols are native Image children so the circle's stencil is shared consistently.
    }
}
