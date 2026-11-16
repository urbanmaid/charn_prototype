using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a revive icon over dead allies that can still be revived (Overwatch Mercy style):
/// gold cross in a ring whose fill is the time left. Bright when in revive range, dim when out of range.
/// Added to the player at runtime by a revive skill (OnEquip).
/// </summary>
public class ReviveIndicator : MonoBehaviour
{
    [SerializeField] private float reviveWindow = Charm.Mechanism.Missions.CombatTelemetry.CommonReviveSeconds;
    [SerializeField] private float reviveRadius = 20f;
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.38f, 1f);

    private Camera viewCamera;
    private GameObject canvasObject;
    private readonly List<Icon> icons = new();
    private static Sprite discSprite, ringSprite;

    private class Icon
    {
        public RectTransform root;
        public Image back, fill, ring;
        public Image[] cross;
        public Text timeText;
    }

    public void Configure(float window, float radius, Color iconColor)
    {
        reviveWindow = ReviveWindow.Duration;
        reviveRadius = radius;
        color = iconColor;
    }

    private void Awake()
    {
        ReviveWindow.EnsureTracker();
        canvasObject = new GameObject("ReviveIndicatorCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49;
    }

    private void OnDestroy()
    {
        if (canvasObject) Destroy(canvasObject);
    }

    private void OnDisable()
    {
        if (canvasObject) canvasObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (canvasObject) canvasObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            BasicBehaviour basic = GetComponent<BasicBehaviour>();
            viewCamera = basic != null && basic.playerCamera != null ? basic.playerCamera.GetComponent<Camera>() : Camera.main;
        }

        int used = 0;
        if (viewCamera != null)
        {
            foreach (HealthManager dead in ReviveWindow.DeadAllies)
            {
                if (dead == null || dead.transform.root == transform.root) continue;

                float left = ReviveWindow.TimeLeft(dead, reviveWindow);
                if (left <= 0f) continue;

                Vector3 world = GetBodyCenter(dead) + Vector3.up * 0.9f;
                Vector3 screen = viewCamera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                bool inRange = (dead.transform.position - transform.position).sqrMagnitude <= reviveRadius * reviveRadius;
                Icon icon = GetIcon(used++);
                icon.root.gameObject.SetActive(true);
                icon.root.anchoredPosition = screen;

                float pulse = inRange ? 1f + 0.06f * Mathf.Sin(Time.time * 7f) : 1f;
                icon.root.localScale = Vector3.one * pulse;

                float alpha = inRange ? 1f : 0.45f;
                icon.fill.fillAmount = Mathf.Clamp01(left / reviveWindow);
                icon.fill.color = new Color(color.r, color.g, color.b, 0.9f * alpha);
                icon.ring.color = new Color(color.r, color.g, color.b, alpha);
                icon.back.color = new Color(0f, 0f, 0f, 0.45f * alpha);
                foreach (Image c in icon.cross) c.color = new Color(1f, 1f, 1f, alpha);
                if (icon.timeText) icon.timeText.text = Mathf.CeilToInt(left).ToString();
            }
        }

        for (int i = used; i < icons.Count; i++)
            icons[i].root.gameObject.SetActive(false);
    }

    // Ragdoll moves bones away from root, so use visible renderer bounds
    private static Vector3 GetBodyCenter(HealthManager health)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (SkinnedMeshRenderer r in health.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found ? bounds.center : health.transform.position + Vector3.up * 0.5f;
    }

    private Icon GetIcon(int index)
    {
        while (icons.Count <= index)
            icons.Add(CreateIcon());
        return icons[index];
    }

    private Icon CreateIcon()
    {
        const float size = 46f;
        Icon icon = new Icon();
        icon.root = new GameObject("ReviveIcon", typeof(RectTransform)).GetComponent<RectTransform>();
        icon.root.SetParent(canvasObject.transform, false);
        icon.root.anchorMin = icon.root.anchorMax = Vector2.zero;
        icon.root.sizeDelta = new Vector2(size, size);

        icon.back = NewImage(icon.root, Disc, size);
        icon.fill = NewImage(icon.root, Disc, size);
        icon.fill.type = Image.Type.Filled;
        icon.fill.fillMethod = Image.FillMethod.Radial360;
        icon.fill.fillOrigin = (int)Image.Origin360.Top;
        icon.fill.fillClockwise = false;
        // Inner dark disc so fill looks like a ring gauge
        Image inner = NewImage(icon.root, Disc, size * 0.72f);
        inner.color = new Color(0.12f, 0.1f, 0.05f, 0.85f);
        icon.ring = NewImage(icon.root, Ring, size);

        // Cross (plus)
        Image v = NewImage(icon.root, null, 0f);
        v.rectTransform.sizeDelta = new Vector2(6f, 20f);
        Image h = NewImage(icon.root, null, 0f);
        h.rectTransform.sizeDelta = new Vector2(20f, 6f);
        icon.cross = new[] { v, h };

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            icon.timeText = new GameObject("Time", typeof(RectTransform)).AddComponent<Text>();
            icon.timeText.transform.SetParent(icon.root, false);
            icon.timeText.font = font;
            icon.timeText.fontSize = 13;
            icon.timeText.fontStyle = FontStyle.Bold;
            icon.timeText.alignment = TextAnchor.UpperCenter;
            icon.timeText.color = Color.white;
            icon.timeText.raycastTarget = false;
            RectTransform tr = icon.timeText.rectTransform;
            tr.anchoredPosition = new Vector2(0f, -size * 0.5f - 10f);
            tr.sizeDelta = new Vector2(40f, 18f);
        }

        return icon;
    }

    private static Image NewImage(RectTransform parent, Sprite sprite, float size)
    {
        Image image = new GameObject("Img", typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        return image;
    }

    private static Sprite Disc => discSprite != null ? discSprite : discSprite = MakeSprite(128, r => Mathf.Clamp01((1f - r) * 40f));
    private static Sprite Ring => ringSprite != null ? ringSprite : ringSprite = MakeSprite(128, r => Mathf.Clamp01((1f - r) * 40f) * Mathf.Clamp01((r - 0.9f) * 40f));

    private static Sprite MakeSprite(int size, System.Func<float, float> alphaByRadius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaByRadius(r))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
