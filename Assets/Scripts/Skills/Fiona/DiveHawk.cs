using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Golden hawk sent down from the sky by the huge hawk: one per target.
/// Swoops out, curves over its target and dives onto it with folded wings; onHit is called at impact.
/// </summary>
public class DiveHawk : MonoBehaviour
{
    private const float Wingspan = 2.7f;
    private static float lastAftershock = -10f;

    private Vector3 start, targetPoint, targetOffset;
    private Transform target;
    private float delay, duration, time, sideSign;
    private Action<Vector3> onHit;

    private Transform body;
    private MeshRenderer bodyRenderer;
    private MaterialPropertyBlock bodyBlock;
    private HawkShadow shadow;
    private TrailRenderer trail;
    private ParticleSystem feathers, halo;
    private Light glow;
    private Vector3 lastPosition;
    private bool visible;

    public static DiveHawk Launch(Vector3 from, Transform target, Vector3 offset, float delay, Action<Vector3> onHit)
    {
        GameObject go = new GameObject("VFX_DiveHawk");
        go.transform.position = from;
        DiveHawk hawk = go.AddComponent<DiveHawk>();
        hawk.start = from;
        hawk.target = target;
        hawk.targetOffset = offset;
        hawk.targetPoint = target != null ? target.position + offset : from;
        hawk.delay = delay;
        hawk.onHit = onHit;
        hawk.sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        hawk.duration = Mathf.Clamp(0.55f + Vector3.Distance(from, hawk.targetPoint) / 60f, 0.6f, 1.1f);
        hawk.lastPosition = from;
        hawk.Build();
        hawk.SetVisible(false);
        return hawk;
    }

    private void Build()
    {
        bodyRenderer = HawkShape.CreateQuad("Hawk", transform, HawkShape.Gold);
        body = bodyRenderer.transform;
        bodyBlock = new MaterialPropertyBlock();
        shadow = new HawkShadow("VFX_DiveHawkShadow", 12);

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.5f;
        trail.minVertexDistance = 0.1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 0f));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(FionaVfx.HawkAmber, 0.3f), new GradientColorKey(FionaVfx.HawkAmber, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.sharedMaterial = SkillVfxFactory.GetAdditiveMaterial(SkillVfxFactory.Kenney("trace_07"));
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;

        feathers = SkillVfxFactory.CreateParticles(transform, "Feathers", Vector3.zero, SkillVfxFactory.Kenney("flame_05"));
        var main = feathers.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(FionaVfx.HawkAmber, Color.white);
        main.gravityModifier = 0.2f;
        var shape = feathers.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        var emission = feathers.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 3f;
        var rotation = feathers.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(2f, 5f);
        SkillVfxFactory.SetFadeOut(feathers);

        halo = SkillVfxFactory.CreateParticles(transform, "Halo", Vector3.zero, SkillVfxFactory.Kenney("circle_05"));
        var haloMain = halo.main;
        haloMain.loop = true;
        haloMain.duration = 1f;
        haloMain.startLifetime = 0.25f;
        haloMain.startSpeed = 0f;
        haloMain.startSize = Wingspan * 1.3f;
        haloMain.startColor = new Color(1f, 0.85f, 0.55f, 0.4f);
        haloMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var haloEmission = halo.emission;
        haloEmission.rateOverTime = 20f;
        SkillVfxFactory.SetFadeOut(halo);

        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.type = LightType.Point;
        glow.color = FionaVfx.HawkAmber;
        glow.range = 5f;
        glow.intensity = 4f;
        glow.shadows = LightShadows.None;
    }

    private void SetVisible(bool on)
    {
        visible = on;
        body.gameObject.SetActive(on);
        shadow.SetActive(on);
        glow.enabled = on;
        trail.emitting = on;
        if (on) { feathers.Play(); halo.Play(); }
        else { feathers.Stop(); halo.Stop(); }
    }

    private void Update()
    {
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            if (delay > 0f) return;
        }
        if (!visible) SetVisible(true);

        if (target != null) targetPoint = target.position + targetOffset;
        time += Time.deltaTime;
        float u = Mathf.Clamp01(time / duration);
        float eased = Mathf.Pow(u, 1.8f);   // Accelerates into the dive

        // Swoop out toward the target side, curve above it, then drop straight onto it
        Vector3 flat = targetPoint - start;
        flat.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * sideSign;
        Vector3 control = targetPoint + Vector3.up * 7f - flat * 0.25f + side * 2f;
        transform.position = Bezier(start, control, targetPoint, eased);
        // Wing-driven lift and banking fade to zero before contact; the hit point stays exact.
        float flight = Mathf.Sin(u * Mathf.PI) * (1f - Mathf.SmoothStep(.65f, 1f, u));
        transform.position += side * (Mathf.Sin(time * 12f) * .65f * flight) +
            Vector3.up * (Mathf.Sin(time * 24f) * .24f * flight);

        Vector3 velocity = transform.position - lastPosition;
        lastPosition = transform.position;
        Animate(velocity, u);

        if (u >= 1f) Arrive();
    }

    private void Animate(Vector3 velocity, float progress)
    {
        Camera cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - body.position).normalized : Vector3.back;
        Vector3 up = Vector3.ProjectOnPlane(velocity.sqrMagnitude > 1e-6f ? velocity : Vector3.down, toCamera);
        if (up.sqrMagnitude < 1e-6f) up = Vector3.down;
        body.rotation = Quaternion.LookRotation(-toCamera, up.normalized);
        body.rotation *= Quaternion.Euler(0f, 0f, sideSign * Mathf.Sin(progress * Mathf.PI) * 24f);

        float diving = Mathf.InverseLerp(0.45f, 0.8f, progress);
        float span = Mathf.Lerp(0.75f + 0.25f * Mathf.Sin(time * 26f), 0.42f, diving);
        float length = Mathf.Lerp(1f, 1.3f, diving);
        body.localScale = new Vector3(Wingspan * span, Wingspan * length, 1f);
        bodyBlock.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f, 1f) * 1.3f);
        bodyRenderer.SetPropertyBlock(bodyBlock);

        shadow.Place(transform.position, velocity, Wingspan * span, Wingspan * length, 0.45f);
    }

    private void Arrive()
    {
        onHit?.Invoke(targetPoint);
        onHit = null;
        if (Time.time - lastAftershock > .8f)
        {
            lastAftershock = Time.time;
            StartCoroutine(Aftershocks(targetPoint));
        }
        body.gameObject.SetActive(false);
        shadow?.Destroy();
        glow.enabled = false;
        trail.emitting = false;
        feathers.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        halo.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        enabled = false;
        Destroy(gameObject, 1.4f);
    }

    private System.Collections.IEnumerator Aftershocks(Vector3 point)
    {
        // Visual/audio echoes only: damage remains a single impact.
        for (int i = 0; i < 2; i++)
        {
            yield return new WaitForSeconds(i == 0 ? .18f : .24f);
            CombatCameraFeedback.GroundSlam(point, 24f, 1.5f - i * .35f, 1f, FionaVfx.HawkAmber);
            CombatSkillAccent.Burst(point, FionaVfx.HawkAmber, 4.5f + i);
            SkillSfx.Play("hawk_impact", point, .45f - i * .12f, .78f + i * .06f);
        }
    }

    private void OnDestroy()
    {
        shadow?.Destroy();
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
