using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana-like ally target marker. While the skill of the slot is ready, finds the ally nearest to the crosshair
/// and draws a square bracket frame around it on screen. Added at runtime by skill logic (OnEquip).
/// </summary>
public class SkillTargetIndicator : MonoBehaviour
{
    [SerializeField] private SkillSlot slot;
    [SerializeField] private float range = 40f;
    [SerializeField] private float maxAngle = 10f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private Color color = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private string label = "BOOST";

    public SkillSlot Slot => slot;
    public HealthManager CurrentTarget { get; private set; }

    private SkillBehavior owner;
    private Transform view;
    private Camera viewCamera;
    private readonly List<HealthManager> candidates = new();
    private float nextCandidateRefresh;

    // UI
    private GameObject canvasObject;
    private RectTransform frame;
    private Image fill;
    private Text labelText;
    private readonly List<Image> bracketImages = new();
    private RectTransform[] corners;
    private Vector2 currentCenter, currentSize;
    private HealthManager shownTarget;
    private readonly List<Renderer> targetRenderers = new();

    private const float Thickness = 3f;

    public void Configure(SkillSlot slot, float range, float maxAngle, bool requireLineOfSight, Color color, string label)
    {
        this.slot = slot;
        this.range = range;
        this.maxAngle = maxAngle;
        this.requireLineOfSight = requireLineOfSight;
        this.color = color;
        this.label = label;
        ApplyColor();
    }

    public static SkillTargetIndicator Find(GameObject owner, SkillSlot slot)
    {
        foreach (SkillTargetIndicator indicator in owner.GetComponents<SkillTargetIndicator>())
        {
            if (indicator.slot == slot)
                return indicator;
        }
        return null;
    }

    private void Awake()
    {
        owner = GetComponent<SkillBehavior>();
        BuildUI();
    }

    private void OnDisable()
    {
        CurrentTarget = null;
        if (canvasObject) canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvasObject) Destroy(canvasObject);
    }

    private void Update()
    {
        ResolveView();

        if (Time.time >= nextCandidateRefresh)
        {
            nextCandidateRefresh = Time.time + 0.5f;
            AllyTargeting.CollectAllies(candidates);
        }

        CurrentTarget = view != null
            ? AllyTargeting.FindAllyInSight(transform.root, view, candidates, range, maxAngle, requireLineOfSight)
            : null;

        bool show = CurrentTarget != null && owner != null && owner.IsSkillReady(slot);
        if (!show || !TryGetScreenRect(CurrentTarget, out Vector2 center, out Vector2 size))
        {
            canvasObject.SetActive(false);
            shownTarget = null;
            return;
        }

        canvasObject.SetActive(true);

        // Snap on new target, then follow smoothly
        if (shownTarget != CurrentTarget)
        {
            currentCenter = center;
            currentSize = size * 1.4f;
            shownTarget = CurrentTarget;
        }
        float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
        currentCenter = Vector2.Lerp(currentCenter, center, k);
        currentSize = Vector2.Lerp(currentSize, size, k);

        float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 6f);
        LayoutFrame(currentCenter, currentSize * pulse);
    }

    /// <summary>Refresh the crosshair target synchronously when the skill button is pressed.</summary>
    public HealthManager SelectTargetNow()
    {
        ResolveView();
        if (view == null)
        {
            CurrentTarget = null;
            return null;
        }

        AllyTargeting.CollectAllies(candidates);
        CurrentTarget = AllyTargeting.FindAllyInSight(transform.root, view, candidates, range, maxAngle, requireLineOfSight);
        return CurrentTarget;
    }

    private void ResolveView()
    {
        if (viewCamera != null && viewCamera.isActiveAndEnabled)
            return;

        BasicBehaviour basic = GetComponent<BasicBehaviour>();
        view = basic != null ? basic.playerCamera : null;
        viewCamera = view != null ? view.GetComponent<Camera>() : null;

        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            view = viewCamera != null ? viewCamera.transform : null;
        }
    }

    private bool TryGetScreenRect(HealthManager target, out Vector2 center, out Vector2 size)
    {
        center = size = Vector2.zero;
        if (viewCamera == null) return false;

        if (shownTarget != target)
        {
            targetRenderers.Clear();
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                    targetRenderers.Add(r);
            }
        }

        Bounds bounds = new Bounds(target.transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        bool first = true;
        foreach (Renderer r in targetRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        Vector3 c = bounds.center, e = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 sp = viewCamera.WorldToScreenPoint(corner);
            if (sp.z <= 0f) return false;
            min = Vector2.Min(min, sp);
            max = Vector2.Max(max, sp);
        }

        const float padding = 10f;
        size = Vector2.Max(max - min + Vector2.one * padding * 2f, new Vector2(36f, 36f));
        center = (min + max) * 0.5f;
        return true;
    }

    #region UI
    private void BuildUI()
    {
        canvasObject = new GameObject("SkillTargetIndicatorCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        frame = NewRect("Frame", canvasObject.transform);
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = new Vector2(0.5f, 0.5f);

        fill = NewImage("Fill", frame);
        Stretch(fill.rectTransform);

        // 4 corner brackets, each made of horizontal + vertical bar
        corners = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform corner = NewRect("Corner" + i, frame);
            Vector2 anchor = new Vector2(i % 2, i / 2);
            corner.anchorMin = corner.anchorMax = corner.pivot = anchor;
            corner.anchoredPosition = Vector2.zero;
            corners[i] = corner;

            Image h = NewImage("H", corner);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = h.rectTransform.pivot = anchor;
            Image v = NewImage("V", corner);
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = anchor;
            bracketImages.Add(h);
            bracketImages.Add(v);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            labelText = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            labelText.transform.SetParent(frame, false);
            labelText.font = font;
            labelText.fontSize = 14;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.LowerCenter;
            labelText.raycastTarget = false;
            RectTransform lr = labelText.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.anchoredPosition = new Vector2(0f, 6f);
            lr.sizeDelta = new Vector2(80f, 20f);
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        ApplyColor();
        canvasObject.SetActive(false);
    }

    private void LayoutFrame(Vector2 center, Vector2 size)
    {
        frame.anchoredPosition = center;
        frame.sizeDelta = size;

        float length = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.25f, 10f, 30f);
        for (int i = 0; i < 4; i++)
        {
            corners[i].sizeDelta = new Vector2(length, length);
            bracketImages[i * 2].rectTransform.sizeDelta = new Vector2(length, Thickness);
            bracketImages[i * 2 + 1].rectTransform.sizeDelta = new Vector2(Thickness, length);
        }
    }

    private void ApplyColor()
    {
        foreach (Image img in bracketImages)
            img.color = color;
        if (fill) fill.color = new Color(color.r, color.g, color.b, 0.08f);
        if (labelText)
        {
            labelText.color = color;
            labelText.text = label;
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image NewImage(string name, Transform parent)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    #endregion
}
