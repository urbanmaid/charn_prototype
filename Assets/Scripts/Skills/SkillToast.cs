using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short on-screen message for skill feedback (ex: "NO ALLY TO REVIVE"). Fades out automatically.
/// </summary>
public class SkillToast : MonoBehaviour
{
    private static SkillToast instance;
    private Text text;
    private float shownAt = -99f;
    private const float Duration = 1.6f;

    public static void Show(string message, Color color)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("SkillToast");
            instance = go.AddComponent<SkillToast>();
            instance.Build();
        }

        if (instance.text == null) return;
        instance.text.text = message;
        instance.text.color = color;
        instance.shownAt = Time.unscaledTime;
        instance.gameObject.SetActive(true);
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) return;

        text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(transform, false);
        text.font = font;
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.38f);
        rect.sizeDelta = new Vector2(600f, 40f);
    }

    private void Update()
    {
        if (text == null) return;

        float t = Time.unscaledTime - shownAt;
        Color c = text.color;
        c.a = t < Duration - 0.4f ? 1f : Mathf.Clamp01((Duration - t) / 0.4f);
        text.color = c;
        if (t > Duration) gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
