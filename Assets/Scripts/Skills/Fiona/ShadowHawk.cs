using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Hawk's Ambush" charge: one huge hawk flies in from far behind the shooter toward the front, high overhead,
/// like a warship arriving in a movie. Its giant shadow sweeps in from behind and gets denser,
/// while the sun dims and the screen darkens. Release() flashes it and sends it away; Leave() just sends it away.
/// </summary>
public class ShadowHawk : MonoBehaviour
{
    private const float Wingspan = 16f;
    private const float StartBack = 30f, StartHeight = 20f;
    private const float OverheadTime = 2.2f;           // Reaches over the shooter around max charge
    private const float OverheadAhead = 7f, OverheadHeight = 15f;
    private const float DriftSpeed = 3f;               // Keeps drifting forward after passing overhead
    private const float MaxDarken = 0.4f, SunDim = 0.45f;
    private static readonly Color HawkBody = new Color(0.3f, 0.26f, 0.23f, 0.95f);   // Dark brown, feather shading still reads

    private Vector3 forward, origin;   // Path stays fixed in the world where the charge began
    private float time, exitTime = -1f, darkness;
    private bool released;
    private Vector3 exitStart, exitDir;

    private Transform body;
    private MeshRenderer bodyRenderer;
    private MaterialPropertyBlock bodyBlock;
    private HawkShadow shadow;
    private ParticleSystem wisps;

    // Screen darkening (player only) and sun dimming
    private GameObject overlayCanvas;
    private Image overlay;
    private readonly Dictionary<Light, float> suns = new();

    public Vector3 Position => transform.position;

    public static ShadowHawk Begin(Transform shooter, Vector3 forward, bool darkenScreen)
    {
        GameObject go = new GameObject("VFX_ShadowHawk");
        ShadowHawk hawk = go.AddComponent<ShadowHawk>();
        hawk.origin = shooter.position;
        forward.y = 0f;
        hawk.forward = forward.sqrMagnitude > 0.01f ? forward.normalized : shooter.forward;
        hawk.transform.position = hawk.PathPosition(0f);
        hawk.Build(darkenScreen);
        return hawk;
    }

    /// <summary> Flash and send the hawks down: the huge hawk flies off ahead. </summary>
    public void Release()
    {
        if (exitTime >= 0f) return;
        released = true;
        FionaVfx.SpawnHawkGlint(transform.position, 2f);
        StartExit();
    }

    /// <summary> Ambush ended without striking: just fly away. </summary>
    public void Leave()
    {
        if (exitTime >= 0f) return;
        StartExit();
    }

    private void StartExit()
    {
        exitTime = 0f;
        exitStart = transform.position;
        exitDir = forward * 30f + Vector3.up * 12f;
        if (wisps != null) wisps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // Path: far behind and high -> just ahead of the shooter overhead -> keeps drifting forward
    private Vector3 PathPosition(float t)
    {
        Vector3 start = origin - forward * StartBack + Vector3.up * StartHeight;
        Vector3 over = origin + forward * OverheadAhead + Vector3.up * OverheadHeight;
        if (t <= OverheadTime)
        {
            float u = t / OverheadTime;
            float eased = 1f - (1f - u) * (1f - u) * (1f - u);   // Fast approach, slows down overhead
            return Vector3.Lerp(start, over, eased);
        }
        return over + forward * DriftSpeed * (t - OverheadTime);
    }

    private void Build(bool darkenScreen)
    {
        bodyRenderer = HawkShape.CreateQuad("Hawk", transform, HawkShape.Dark);
        body = bodyRenderer.transform;
        bodyBlock = new MaterialPropertyBlock();

        shadow = new HawkShadow("VFX_ShadowHawkShadow", 32);

        // Dark wisps trailing from the wings
        wisps = SkillVfxFactory.CreateParticles(transform, "Wisps", Vector3.zero, SkillVfxFactory.Kenney("smoke_08"));
        wisps.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney("smoke_08"));
        var main = wisps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.04f, 0.03f, 0.06f, 0.35f);
        var shape = wisps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Wingspan * 0.8f, 0.5f, 1f);
        var emission = wisps.emission;
        emission.rateOverTime = 12f;
        SkillVfxFactory.SetFadeOut(wisps);
        wisps.Play();

        if (darkenScreen)
        {
            overlayCanvas = new GameObject("ShadowHawkDarken");
            Canvas canvas = overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            overlay = new GameObject("Dark", typeof(RectTransform)).AddComponent<Image>();
            overlay.transform.SetParent(overlayCanvas.transform, false);
            overlay.raycastTarget = false;
            RectTransform r = overlay.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            overlay.color = Color.clear;
        }

        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light.type == LightType.Directional && !suns.ContainsKey(light))
                suns[light] = light.intensity;
        }
    }

    private void Update()
    {
        Vector3 previous = transform.position;
        float fade = 1f;

        if (exitTime < 0f)
        {
            time += Time.deltaTime;
            transform.position = PathPosition(time);
            // Shadow approaching from behind darkens the scene; fully dark once overhead
            darkness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, OverheadTime, time));
            fade = Mathf.Clamp01(time / 0.6f);
        }
        else
        {
            exitTime += Time.deltaTime;
            float u = Mathf.Clamp01(exitTime / 1.4f);
            transform.position = exitStart + exitDir * (u * u);
            darkness = Mathf.MoveTowards(darkness, 0f, Time.deltaTime / 0.5f);
            fade = 1f - u;
            if (u >= 1f && darkness <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        Animate(transform.position - previous, fade);
        ApplyDarkness();
    }

    private void Animate(Vector3 velocity, float fade)
    {
        // Seen from below, slightly tilted toward the camera; head points along the flight
        Camera cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - body.position).normalized : Vector3.down;
        Vector3 normal = Vector3.Slerp(Vector3.up, toCamera, 0.25f).normalized;
        Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity : forward;
        Vector3 up = Vector3.ProjectOnPlane(new Vector3(heading.x, 0f, heading.z), normal);
        if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(forward, normal);
        body.rotation = Quaternion.LookRotation(normal, up.normalized);

        // Slow, heavy wingbeats
        float span = 0.86f + 0.14f * Mathf.Sin(Time.time * 2.4f);
        body.localScale = new Vector3(Wingspan * span, Wingspan, 1f);

        // Dark silhouette; flashes gold at release
        Color color = released && exitTime < 0.3f
            ? Color.Lerp(new Color(1f, 0.85f, 0.5f, 1f), HawkBody, exitTime / 0.3f)
            : HawkBody;
        color.a *= fade;
        bodyBlock.SetColor("_BaseColor", color);
        bodyRenderer.SetPropertyBlock(bodyBlock);

        // Giant shadow draped over the ground and characters, denser as it arrives
        float alpha = Mathf.Lerp(0.25f, 0.75f, darkness) * fade;
        shadow.Place(transform.position, heading, Wingspan * span * 1.15f, Wingspan * 1.15f, alpha);
    }

    private void ApplyDarkness()
    {
        if (overlay != null)
            overlay.color = new Color(0.02f, 0.02f, 0.04f, MaxDarken * darkness);
        foreach (var pair in suns)
        {
            if (pair.Key != null)
                pair.Key.intensity = pair.Value * Mathf.Lerp(1f, SunDim, darkness);
        }
    }

    private void OnDestroy()
    {
        // Always give the sun back
        foreach (var pair in suns)
        {
            if (pair.Key != null) pair.Key.intensity = pair.Value;
        }
        if (overlayCanvas != null) Destroy(overlayCanvas);
        shadow?.Destroy();
    }
}
