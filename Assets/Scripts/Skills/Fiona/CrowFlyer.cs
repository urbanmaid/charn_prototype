using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One flapping crow for "Crow's Wingbeat": either flies off along a velocity (flock burst, scatter)
/// or circles the caster as part of the shield. Scatter() sends an orbiting crow away.
/// </summary>
public class CrowFlyer : MonoBehaviour
{
    // Black with a cool sheen: dark enough to read as a crow, light enough for the feather shading to show
    private static readonly Color Tint = new Color(0.2f, 0.22f, 0.28f, 1f);
    private static readonly Color Sheen = new Color(0.72f, 0.82f, 1f, 1f);

    private bool orbiting;
    private Transform center;
    private float radius, angle, angularSpeed, height, bobPhase;
    private Vector3 velocity;
    private float time, flyTime, flyLife, size, flapSpeed, flapPhase;

    private Transform body;
    private MeshRenderer bodyRenderer;
    private MaterialPropertyBlock block;
    private TrailRenderer trail;
    private Vector3 lastPosition;

    /// <summary> Crow flying off from a point; climbs a little and fades out at the end of its life. </summary>
    public static CrowFlyer Fly(Vector3 from, Vector3 velocity, float size, float lifetime)
    {
        CrowFlyer crow = Create(from, size);
        crow.velocity = velocity;
        crow.flyLife = lifetime;
        crow.flapSpeed = Random.Range(20f, 26f);
        return crow;
    }

    /// <summary> Crow circling center; it spirals out from the chest to its ring first. </summary>
    public static CrowFlyer Orbit(Transform center, float radius, float startAngle, float height, float size, float angularSpeed)
    {
        CrowFlyer crow = Create(center.position + Vector3.up * 1.1f, size);
        crow.orbiting = true;
        crow.center = center;
        crow.radius = radius;
        crow.angle = startAngle;
        crow.height = height;
        crow.angularSpeed = angularSpeed;
        crow.bobPhase = Random.Range(0f, Mathf.PI * 2f);
        crow.flapSpeed = Random.Range(13f, 16f);
        return crow;
    }

    /// <summary> Leave the orbit: fly outward and up, then fade. </summary>
    public void Scatter(float speed)
    {
        if (!orbiting) return;
        orbiting = false;
        Vector3 outward = center != null ? transform.position - center.position : Random.insideUnitSphere;
        outward.y = 0f;
        if (outward.sqrMagnitude < 1e-4f) outward = Random.insideUnitCircle.normalized;
        velocity = outward.normalized * speed + Vector3.up * speed * 0.6f;
        flyTime = 0f;
        flyLife = Random.Range(0.9f, 1.2f);
        flapSpeed *= 1.6f;
    }

    private static CrowFlyer Create(Vector3 position, float size)
    {
        GameObject go = new GameObject("VFX_Crow");
        go.transform.position = position;
        CrowFlyer crow = go.AddComponent<CrowFlyer>();
        crow.size = size;
        crow.flapPhase = Random.Range(0f, Mathf.PI * 2f);
        crow.lastPosition = position;
        crow.Build();
        return crow;
    }

    private void Build()
    {
        bodyRenderer = HawkShape.CreateQuad("Crow", transform, HawkShape.Crow);
        body = bodyRenderer.transform;
        block = new MaterialPropertyBlock();

        // Faint silver streak behind the wings
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, size * 0.25f), new Keyframe(1f, 0f));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Sheen, 0f), new GradientColorKey(Sheen, 1f) },
            new[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.sharedMaterial = SkillVfxFactory.GetAdditiveMaterial(SkillVfxFactory.Kenney("trace_07"));
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        time += dt;
        float alpha = Mathf.Clamp01(time / 0.1f);

        if (orbiting)
        {
            if (center == null) { Scatter(4f); return; }
            // Spiral out from the chest onto the ring, then keep circling with a gentle bob
            float join = Mathf.SmoothStep(0f, 1f, time / 0.45f);
            angle += angularSpeed * dt;
            float r = Mathf.Lerp(0.2f, radius, join);
            float h = Mathf.Lerp(1.1f, height + 0.2f * Mathf.Sin(time * 1.7f + bobPhase), join);
            transform.position = center.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r + Vector3.up * h;
        }
        else
        {
            flyTime += dt;
            velocity += Vector3.up * 1.5f * dt;   // Climbs as it flies away
            velocity *= 1f - 0.4f * dt;
            transform.position += velocity * dt;
            alpha *= 1f - Mathf.Clamp01((flyTime - (flyLife - 0.4f)) / 0.4f);
            if (flyTime >= flyLife)
            {
                Destroy(gameObject);
                return;
            }
        }

        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        Animate(moved, alpha);
    }

    private void Animate(Vector3 moved, float alpha)
    {
        // Billboard toward the camera, head along the flight direction
        Camera cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - body.position).normalized : Vector3.back;
        Vector3 up = Vector3.ProjectOnPlane(moved.sqrMagnitude > 1e-8f ? moved : Vector3.up, toCamera);
        if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
        body.rotation = Quaternion.LookRotation(-toCamera, up.normalized);

        // Wingbeat: wings sweep in and out
        float span = 0.55f + 0.45f * Mathf.Sin(time * flapSpeed + flapPhase);
        body.localScale = new Vector3(size * span, size, 1f);

        block.SetColor("_BaseColor", new Color(Tint.r, Tint.g, Tint.b, alpha));
        bodyRenderer.SetPropertyBlock(block);
    }
}
