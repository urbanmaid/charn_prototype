using UnityEngine;
using UnityEngine.UI;

// A transient tint on an existing graphic; never changes layout or alpha.
public sealed class HUDValueFlash : MonoBehaviour
{
    private Graphic graphic;
    private Color original, accent;
    private float elapsed;
    private bool running;

    public static void Play(Graphic target, Color color)
    {
        if (!target || !target.isActiveAndEnabled) return;
        var flash = target.GetComponent<HUDValueFlash>();
        if (!flash) flash = target.gameObject.AddComponent<HUDValueFlash>();
        // Continuous regeneration must not hold the HUD in a permanent flash.
        if (flash.running) return;
        flash.graphic = target;
        flash.original = target.color;
        color.a = flash.original.a;
        flash.accent = color;
        flash.elapsed = 0f;
        flash.running = true;
    }

    private void Update()
    {
        if (!running || !graphic) return;
        elapsed += Time.deltaTime;
        graphic.color = Color.Lerp(original, accent,
            Mathf.Sin(Mathf.Clamp01(elapsed / 0.35f) * Mathf.PI) * 0.65f);
        if (elapsed >= 0.35f) Restore();
    }

    private void Restore()
    {
        if (running && graphic) graphic.color = original;
        running = false;
    }
    private void OnDisable() => Restore();

    public static void Cancel(Graphic target)
    {
        if (target && target.TryGetComponent<HUDValueFlash>(out var flash)) flash.Restore();
    }
}
