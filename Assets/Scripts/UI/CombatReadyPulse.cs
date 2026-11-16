using UnityEngine;
using UnityEngine.UI;

public sealed class CombatReadyPulse : MonoBehaviour
{
    private Image icon;
    private Color original, accent;
    private Vector3 scale;
    private float elapsed;
    private bool running;

    public static void Play(Image image, Color color)
    {
        if (!image || !image.isActiveAndEnabled) return;
        var pulse = image.GetComponent<CombatReadyPulse>();
        if (!pulse) pulse = image.gameObject.AddComponent<CombatReadyPulse>();
        if (!pulse.running)
        {
            pulse.icon = image;
            pulse.original = image.color;
            pulse.scale = image.rectTransform.localScale;
        }
        color.a = pulse.original.a;
        pulse.accent = color;
        pulse.elapsed = 0f;
        pulse.running = true;
    }
    private void Update()
    {
        if (!running || !icon) return;
        elapsed += Time.deltaTime;
        float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / 0.4f) * Mathf.PI);
        icon.color = Color.Lerp(original, accent, pulse);
        icon.rectTransform.localScale = scale * (1f + 0.12f * pulse);
        if (elapsed >= 0.4f) Restore();
    }
    private void Restore()
    {
        if (running && icon) { icon.color = original; icon.rectTransform.localScale = scale; }
        running = false;
    }
    private void OnDisable() => Restore();

    public static void Cancel(Image image)
    {
        if (image && image.TryGetComponent<CombatReadyPulse>(out var pulse)) pulse.Restore();
    }
}
