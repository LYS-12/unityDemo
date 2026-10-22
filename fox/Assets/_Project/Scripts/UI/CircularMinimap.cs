using UnityEngine;
using UnityEngine.UI;

public class CircularMinimap : MonoBehaviour
{
    [Min(80)] public float diameter = 240;
    [Min(5)] public float halfRange = 20;
    public Camera MapCamera
    {
        get; private set;
    }
    private RenderTexture texture;
    private GameObject frame;
    private void Start()
    {
        UITheme theme = UITheme.Current;
        var root = PartyUI.Rect("ForestCompass", transform, new Vector2(1, 0), new Vector2(-32, 32), Vector2.one * (diameter + 18));
        frame = root.gameObject;
        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.circle;
        border.color = theme.cream;
        border.raycastTarget = false;
        var maskRect = PartyUI.Rect("CircleMask", root, Vector2.one * .5f, Vector2.zero, Vector2.one * diameter);
        Image mask = maskRect.gameObject.AddComponent<Image>();
        mask.sprite = theme.circle;
        maskRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RawImage image = PartyUI.Stretch("Terrain", maskRect).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "ForestMinimapRT" };
        texture.Create();
        image.texture = texture;
        MapCamera = new GameObject("ForestMinimapCamera").AddComponent<Camera>();
        MapCamera.transform.SetParent(transform);
        MapCamera.orthographic = true;
        MapCamera.orthographicSize = halfRange;
        MapCamera.nearClipPlane = .1f;
        MapCamera.farClipPlane = 100;
        MapCamera.cullingMask = ~LayerMask.GetMask("UI", "PlayerCombat", "Enemy", "Companion", "WorldItem", "ItemProjectile", "DynamicFX");
        MapCamera.targetTexture = texture;
        MapCamera.clearFlags = CameraClearFlags.SolidColor;
        MapCamera.backgroundColor = new Color(.39f, .55f, .41f);
        MapCamera.enabled = true;
        var marks = PartyUI.Stretch("MapSymbols", maskRect).gameObject.AddComponent<MinimapSymbols>();
        marks.map = this;
        marks.raycastTarget = false;
        marks.rectTransform.pivot = Vector2.one * .5f;
        marks.rectTransform.anchoredPosition = Vector2.zero;
        PartyUI.Text("N", root, new Vector2(.5f, 1), new Vector2(0, 14), new Vector2(32, 28), 20, true, TMPro.TextAlignmentOptions.Center);
        PartyUI.Card(PartyUI.Rect("MapCaption", root, new Vector2(.5f, 0), new Vector2(0, -28), new Vector2(270, 30)), theme.cream);
        PartyUI.Text("森林小径  ·  ◇ 聚会", root, new Vector2(.5f, 0), new Vector2(0, -28), new Vector2(270, 30), 17, false, TMPro.TextAlignmentOptions.Center);
        for (int i = 0; i < 2; i++)
        {
            var leaf = PartyUI.Rect("LeafOrnament", root, new Vector2(0, 1), new Vector2(18 + i * 14, -10 - i * 8), new Vector2(15, 30));
            var ornament = leaf.gameObject.AddComponent<Image>();
            ornament.sprite = theme.circle;
            ornament.color = theme.mint;
            ornament.raycastTarget = false;
            leaf.localEulerAngles = new Vector3(0, 0, 35 + i * 55);
        }
    }
    private void LateUpdate()
    {
        if (MapCamera == null || Player.Instance == null)
            return;
        MapCamera.transform.SetPositionAndRotation(Player.Instance.transform.position + Vector3.up * 45, Quaternion.Euler(90, 0, 0));
        frame.SetActive(!StoryCinematic.IsPlaying);
    }
    public Vector2 ToMap(Vector3 world)
    {
        Vector3 local = world - MapCamera.transform.position;
        return new Vector2(local.x, local.z) / halfRange * (diameter * .5f);
    }
    private void OnDestroy()
    {
        if (MapCamera != null)
            Destroy(MapCamera.gameObject);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}

