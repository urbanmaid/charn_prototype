using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen feedback for skills: floating damage numbers over world positions and full screen color flash.
/// </summary>
public class SkillScreenFx : MonoBehaviour
{
    private static SkillScreenFx instance;
    private Canvas canvas;
    private Font font;
    private Image flash;
    private float flashTime, flashDuration;
    private Color flashColor;
    private readonly List<Image> skillEdges = new();
    private Color edgeColor;
    private float edgeAge = 10f, edgeDuration = .5f, edgeOpacity, edgeFrequency;

    public static void SkillEdges(Color color, float duration, float opacity, float frequency)
    {
        var fx = Instance;
        fx.edgeColor = color;
        fx.edgeAge = 0f;
        fx.edgeDuration = duration;
        fx.edgeOpacity = opacity;
        fx.edgeFrequency = frequency;
    }

    private class Popup
    {
        public Text text;
        public Vector3 world;
        public float born;
        public Color color;
    }
    private readonly List<Popup> popups = new();
    private const float PopupLife = 0.9f;

    private static SkillScreenFx Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("SkillScreenFx").AddComponent<SkillScreenFx>();
                instance.Build();
            }
            return instance;
        }
    }

    public static void DamageNumber(Vector3 world, float damage, Color color)
    {
        SkillScreenFx fx = Instance;
        if (fx.font == null) return;

        Text text = new GameObject("Damage", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(fx.canvas.transform, false);
        text.font = fx.font;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = Mathf.RoundToInt(damage).ToString();
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(160f, 40f);

        fx.popups.Add(new Popup { text = text, world = world, born = Time.time, color = color });
    }

    public static void Flash(Color color, float duration)
    {
        SkillScreenFx fx = Instance;
        fx.flashColor = color;
        fx.flashDuration = Mathf.Max(0.01f, duration);
        fx.flashTime = 0f;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 65;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        flash = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
        flash.transform.SetParent(transform, false);
        flash.raycastTarget = false;
        RectTransform r = flash.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        flash.color = Color.clear;
        flashTime = flashDuration = 1f;
        // Six soft bands per side; the central aiming area stays transparent.
        for (int side = 0; side < 4; side++)
        for (int band = 0; band < 6; band++)
        {
            Image edge = new GameObject("SkillEdge", typeof(RectTransform)).AddComponent<Image>();
            edge.transform.SetParent(transform, false);
            edge.raycastTarget = false;
            edge.color = Color.clear;
            RectTransform rect = edge.rectTransform;
            float a = band * .012f, b = (band + 1) * .012f;
            rect.anchorMin = side == 0 ? new Vector2(a, 0) :
                side == 1 ? new Vector2(1-b, 0) :
                side == 2 ? new Vector2(.072f, a) : new Vector2(.072f, 1-b);
            rect.anchorMax = side == 0 ? new Vector2(b, 1) :
                side == 1 ? new Vector2(1-a, 1) :
                side == 2 ? new Vector2(.928f, b) : new Vector2(.928f, 1-a);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            skillEdges.Add(edge);
        }
    }

    private void LateUpdate()
    {
        edgeAge += Time.deltaTime;
        float envelope = Mathf.Pow(Mathf.Clamp01(1f - edgeAge / edgeDuration), 2f);
        float pulse = .8f + .2f * Mathf.Cos(edgeAge * edgeFrequency);
        for (int i = 0; i < skillEdges.Count; i++)
        {
            Color c = edgeColor;
            c.a = GameplayCursor.AllowsGameplayInput && Time.timeScale > 0f
                ? edgeOpacity * envelope * pulse * Mathf.Pow(1f - (i % 6) / 6f, 2f) : 0f;
            skillEdges[i].color = c;
        }
        // Flash fade
        if (flashTime < flashDuration)
        {
            flashTime += Time.deltaTime;
            Color c = flashColor;
            c.a *= 1f - Mathf.Clamp01(flashTime / flashDuration);
            flash.color = c;
        }
        else if (flash.color.a > 0f)
        {
            flash.color = Color.clear;
        }

        // Damage numbers rise and fade
        Camera cam = Camera.main;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup p = popups[i];
            float t = (Time.time - p.born) / PopupLife;
            if (t >= 1f || p.text == null || cam == null)
            {
                if (p.text != null) Destroy(p.text.gameObject);
                popups.RemoveAt(i);
                continue;
            }

            Vector3 screen = cam.WorldToScreenPoint(p.world + Vector3.up * (0.4f + t * 0.8f));
            p.text.enabled = screen.z > 0f;
            p.text.rectTransform.anchoredPosition = screen;
            float scale = t < 0.15f ? Mathf.Lerp(1.8f, 1f, t / 0.15f) : 1f;
            p.text.rectTransform.localScale = Vector3.one * scale;
            Color c = p.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            p.text.color = c;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
