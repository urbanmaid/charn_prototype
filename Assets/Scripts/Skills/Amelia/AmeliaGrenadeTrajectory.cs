using UnityEngine;

// Player-only aiming aid. Uses the projectile's launch calculation and custom gravity.
[DefaultExecutionOrder(610)]
public sealed class AmeliaGrenadeTrajectory : MonoBehaviour
{
    SkillBehavior owner;
    SkillSlot slot;
    float speed;
    LineRenderer line;
    Material material;
    Texture2D dashTexture;
    LineRenderer landing;
    readonly Vector3[] points = new Vector3[41];
    readonly RaycastHit[] hits = new RaycastHit[32];

    public void Configure(SkillSlot slot, float speed)
    {
        this.slot = slot; this.speed = speed;
        owner = GetComponent<SkillBehavior>();
        enabled = true;
    }
    void LateUpdate()
    {
        var motion = GetComponent<AmeliaGrenadeThrowMotion>();
        bool visible = CompareTag("Player") && GameplayCursor.AllowsGameplayInput && owner != null &&
            (owner.IsPreparingGrenade || (motion != null && motion.IsPreparingRelease));
        if (!visible) { Hide(); return; }
        if (!line)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader) return;
            material = new Material(shader);
            dashTexture = new Texture2D(16, 1, TextureFormat.RGBA32, false);
            dashTexture.wrapMode = TextureWrapMode.Repeat;
            dashTexture.filterMode = FilterMode.Point;
            for (int x = 0; x < 16; x++) dashTexture.SetPixel(x, 0, x < 10 ? Color.white : Color.clear);
            dashTexture.Apply();
            material.mainTexture = dashTexture;
            line = new GameObject("Grenade trajectory").AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.widthMultiplier = .045f;
            line.textureMode = LineTextureMode.Tile;
            line.startColor = new Color(1f, .45f, .08f, .95f); line.endColor = new Color(1f, .15f, .06f, .9f);
            landing = new GameObject("Grenade landing indicator").AddComponent<LineRenderer>();
            landing.sharedMaterial = material;
            landing.useWorldSpace = true;
            landing.loop = true;
            landing.positionCount = 48;
            landing.widthMultiplier = .055f;
            landing.textureMode = LineTextureMode.Tile;
            landing.startColor = landing.endColor = new Color(1f, .25f, .05f, .95f);
            landing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            landing.receiveShadows = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        }
        line.enabled = true;
        // Follow the held grenade and the same target captured by the throw callback.
        // Cooldown already starts on execution, so readiness must not gate this preview.
        bool holding = owner.IsPreparingGrenade;
        Vector3 origin = holding ? transform.position + Vector3.up * 1.4f + transform.forward * .65f : motion.PreviewOrigin;
        Vector3 velocity = AmeliaClusterGrenadeProjectile.LaunchVelocity(origin,
            holding ? AmeliaClusterGrenadeProjectile.AimPoint(gameObject) : motion.PreviewTarget, speed);
        points[0] = origin;
        int count = 1;
        bool landed = false;
        Vector3 normal = Vector3.up;
        for (int i = 1; i < points.Length; i++)
        {
            float t = i * .06f;
            Vector3 next = origin + velocity * t + Vector3.down * (.5f * AmeliaClusterGrenadeProjectile.Gravity * t * t);
            Vector3 segment = next - points[count - 1];
            float nearest = segment.magnitude;
            bool collision = false;
            int hitCount = Physics.SphereCastNonAlloc(points[count - 1], .06f, segment.normalized,
                hits, segment.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hitCount; h++)
            {
                var hit = hits[h];
                if (hit.transform.IsChildOf(transform) || hit.distance > nearest) continue;
                nearest = hit.distance; next = hit.point; normal = hit.normal; collision = true;
            }
            points[count++] = next;
            if (collision) { landed = true; break; }
        }
        line.positionCount = count;
        for (int i = 0; i < count; i++) line.SetPosition(i, points[i]);
        landing.enabled = landed;
        if (landed)
        {
            Vector3 center = points[count - 1] + normal * .025f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48;
                landing.SetPosition(i, center + rotation * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .45f);
            }
        }
    }
    void Hide() { if (line) line.enabled = false; if (landing) landing.enabled = false; }
    void OnDisable() { Hide(); }
    void OnDestroy()
    {
        if (line) Destroy(line.gameObject);
        if (landing) Destroy(landing.gameObject);
        if (material) Destroy(material);
        if (dashTexture) Destroy(dashTexture);
    }
}
