using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Amelia's game changer "Quartermastering": a supply flag is thrown to the aimed spot and planted there
/// (like Jarvan's standard). While it stands, every ally inside its radius fires without using ammo;
/// stepping out ends it at once, stepping in (even later) starts it.
/// Look: a sacred standard. It is flung up into the sky, then slams straight down onto the spot with a shockwave;
/// a column of golden light stays on it, a halo turns over the pole,
/// the banner (gold trim, infinity sign) waves, a knee-high hologram wall with infinity marks rises on the
/// zone edge, light motes drift up inside, and a gauge arc on the ground runs down with the time left.
/// </summary>
public class QuartermasterFlag : MonoBehaviour
{
    private const float LaunchTime = 0.22f, HangTime = 0.3f, SlamTime = 0.13f, FadeTime = 0.6f;
    private const float SkyHeight = 16f, ImpactDip = 0.35f, ImpactRecover = 0.18f;
    private const float FenceHeight = 1.1f, FenceRise = 0.35f;
    private const float CrossY = 2.3f, CoreY = 2.74f, TopY = 3.24f;           // Standard model (Blender): crossbar, halo core, spear tip
    private const float BannerW = 0.9f, BannerH = 1.55f;

    private static readonly Color Holy = new Color(1f, 0.9f, 0.7f, 1f);            // Warm gold-white light

    private Vector3 from, to;
    private float radius, activeTime, time, plantTime, endTime = -1f;
    private bool planted;

    private Transform flag;
    private Mesh bannerMesh;
    private Vector3[] bannerRest, bannerVerts;
    private MeshRenderer reticle, ring, fence, halo, sign, beam, core;
    private LineRenderer gauge;
    private TrailRenderer trail;
    private Light holyLight;
    private SkillVfxRunner motes;
    private MaterialPropertyBlock block;

    private readonly Dictionary<HealthManager, GameObject> inside = new();
    private readonly List<HealthManager> allies = new();
    private float nextScan;

    private static Material poleMaterial, bannerMaterial;
    private static Texture2D bannerTexture;
    private static Texture2D fenceTex, signTex;
    private static Texture2D FenceTexture => fenceTex != null ? fenceTex : fenceTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_fence");
    public static Texture2D SignTexture => signTex != null ? signTex : signTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_emblem_sign");

    public static QuartermasterFlag Throw(Transform caster, Vector3 target, float radius, float duration)
    {
        GameObject go = new GameObject("VFX_QuartermasterFlag");
        QuartermasterFlag q = go.AddComponent<QuartermasterFlag>();
        q.from = caster.position + Vector3.up * 1.5f + caster.forward * 0.3f;
        q.to = GroundPoint(target, target.y);
        q.radius = radius;
        q.activeTime = duration;
        q.block = new MaterialPropertyBlock();
        go.transform.position = q.from;
        Vector3 back = Vector3.ProjectOnPlane(q.from - q.to, Vector3.up);
        if (back.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(back.normalized, Vector3.up);   // Banner faces the thrower
        q.BuildFlag();
        q.BuildReticle();
        SkillSfx.PlayAttached("amelia_flag_throw", go.transform, 0.7f);   // Rushes up and back down with the flag, ends on the impact
        return q;
    }

    /// <summary> Ground below p, ignoring characters (so the flag never lands on someone's head). </summary>
    public static Vector3 GroundPoint(Vector3 p, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<HealthManager>() != null) continue;
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
            return hit.point;
        }
        return new Vector3(p.x, fallbackY, p.z);
    }

    #region Build
    private void BuildFlag()
    {
        flag = new GameObject("Flag").transform;
        flag.SetParent(transform, false);

        // Ornate standard: gold fittings, gunmetal shaft, khaki grip, glowing core in the halo ring
        Mesh standard = StandardMesh;
        if (standard != null)
        {
            GameObject pole = new GameObject("Standard");
            pole.transform.SetParent(flag, false);
            pole.AddComponent<MeshFilter>().sharedMesh = standard;
            MeshRenderer pr = pole.AddComponent<MeshRenderer>();
            pr.sharedMaterials = new[] { GoldMaterial, LitMaterial("VFX_FlagGunmetal", new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.55f),
                                         LitMaterial("VFX_FlagGrip", new Color(0.3f, 0.34f, 0.24f), 0f, 0.3f), CoreMaterial };
            core = pr;
        }

        // Gonfalon hanging from the crossbar, waving
        GameObject bannerGo = new GameObject("Banner");
        bannerGo.transform.SetParent(flag, false);
        bannerGo.transform.localPosition = new Vector3(-BannerW * 0.5f, CrossY - 0.01f, 0.045f);
        bannerMesh = BuildBannerMesh(BannerW, BannerH, 8, 16);
        bannerGo.AddComponent<MeshFilter>().sharedMesh = bannerMesh;
        MeshRenderer br = bannerGo.AddComponent<MeshRenderer>();
        br.sharedMaterial = BannerMaterial;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Golden streak while it flies
        trail = flag.gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Holy, 0f), new GradientColorKey(AmeliaVfx.GlossOrange, 0.5f), new GradientColorKey(AmeliaVfx.GlossPink, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.sharedMaterial = AmeliaVfx.Additive(AmeliaVfx.LineTexture);
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private Mesh BuildBannerMesh(float w, float h, int nx, int ny)
    {
        Mesh m = new Mesh { name = "VFX_Banner" };
        m.MarkDynamic();
        bannerRest = new Vector3[(nx + 1) * (ny + 1)];
        Vector2[] uv = new Vector2[bannerRest.Length];
        Color[] colors = new Color[bannerRest.Length];
        int[] tris = new int[nx * ny * 6];
        for (int y = 0, t = 0; y <= ny; y++)
        {
            for (int x = 0; x <= nx; x++)
            {
                int i = y * (nx + 1) + x;
                bannerRest[i] = new Vector3(x / (float)nx * w, -y / (float)ny * h, 0f);        // Row 0 at the crossbar
                uv[i] = new Vector2(x / (float)nx, 1f - y / (float)ny);
                colors[i] = Color.white;
                if (x < nx && y < ny)
                {
                    tris[t++] = i; tris[t++] = i + nx + 1; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + nx + 1; tris[t++] = i + nx + 2;
                }
            }
        }
        bannerVerts = (Vector3[])bannerRest.Clone();
        m.vertices = bannerVerts;
        m.uv = uv;
        m.colors = colors;
        m.triangles = tris;
        m.bounds = new Bounds(new Vector3(w * 0.5f, -h * 0.5f, 0f), new Vector3(w + 1f, h + 1f, 1f));
        return m;
    }

    // Landing marker at the target while the flag flies
    private void BuildReticle()
    {
        reticle = HawkShape.CreateQuad("Reticle", null, AmeliaVfx.Additive(AmeliaSupplyScan.RingTexture));
        reticle.transform.SetParent(transform, true);
        reticle.transform.position = to + Vector3.up * 0.05f;
        reticle.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void BuildZone()
    {
        Vector3 ground = to + Vector3.up * 0.04f;

        ring = HawkShape.CreateQuad("Ring", null, AmeliaVfx.Additive(AmeliaSupplyScan.RingTexture));
        ring.transform.SetParent(transform, true);
        ring.transform.position = ground;
        float d = radius * 2f / 0.975f;
        ring.transform.localScale = new Vector3(d, d, 1f);

        // Hologram wall on the zone edge
        GameObject fenceGo = new GameObject("Fence");
        fenceGo.transform.SetParent(transform, false);
        fenceGo.transform.position = ground;
        fenceGo.AddComponent<MeshFilter>().sharedMesh = BuildFenceMesh(radius, 1f, 96, Mathf.Round(2f * Mathf.PI * radius / 4f));
        fence = fenceGo.AddComponent<MeshRenderer>();
        fence.sharedMaterial = AmeliaVfx.Additive(FenceTexture);
        fence.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Time-left gauge arc just inside the edge
        GameObject gaugeGo = new GameObject("Gauge");
        gaugeGo.transform.SetParent(transform, false);
        gaugeGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        gauge = gaugeGo.AddComponent<LineRenderer>();
        gauge.useWorldSpace = true;
        gauge.alignment = LineAlignment.TransformZ;
        gauge.sharedMaterial = AmeliaVfx.AdditiveBright(AmeliaVfx.LineTexture, 2f);
        gauge.widthMultiplier = 0.28f;
        gauge.numCapVertices = 4;
        gauge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Halo turning above the pole, big emblem over it
        halo = HawkShape.CreateQuad("Halo", null, AmeliaVfx.AdditiveBright(AmeliaSupplyScan.RingTexture, 1.8f));   // Holo ring as a halo
        halo.transform.SetParent(flag, false);
        halo.transform.localPosition = Vector3.up * CoreY;
        halo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        sign = HawkShape.CreateQuad("Sign", null, AmeliaVfx.Additive(SignTexture));
        sign.transform.SetParent(transform, false);
        sign.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        // Column of light coming down onto the flag
        beam = HawkShape.CreateQuad("Beam", null, AmeliaVfx.AdditiveBright(AmeliaVfx.LineTexture, 2.5f));
        beam.transform.SetParent(transform, false);

        holyLight = new GameObject("HolyLight").AddComponent<Light>();
        holyLight.transform.SetParent(transform, false);
        holyLight.transform.position = to + Vector3.up * 2.5f;
        holyLight.type = LightType.Point;
        holyLight.color = Holy;
        holyLight.range = radius * 0.9f;
        holyLight.shadows = LightShadows.None;

        motes = AmeliaVfx.SpawnHolyMotes(to, radius);
    }

    // Open cylinder, UV u tiled around, v bottom -> top (height scaled by transform)
    private static Mesh BuildFenceMesh(float r, float height, int segments, float tiles)
    {
        Mesh m = new Mesh { name = "VFX_Fence" };
        Vector3[] v = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[v.Length];
        Color[] c = new Color[v.Length];
        int[] t = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            v[i * 2] = p;
            v[i * 2 + 1] = p + Vector3.up * height;
            uv[i * 2] = new Vector2(i / (float)segments * tiles, 0f);
            uv[i * 2 + 1] = new Vector2(i / (float)segments * tiles, 1f);
            c[i * 2] = c[i * 2 + 1] = Color.white;
            if (i < segments)
            {
                int k = i * 6, b = i * 2;
                t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2;
                t[k + 3] = b + 2; t[k + 4] = b + 1; t[k + 5] = b + 3;
            }
        }
        m.vertices = v;
        m.uv = uv;
        m.colors = c;
        m.triangles = t;
        m.RecalculateBounds();
        return m;
    }
    #endregion

    private void Update()
    {
        time += Time.deltaTime;
        WaveBanner();

        if (!planted)
        {
            Fly();
            return;
        }

        float alive = time - plantTime;
        if (endTime < 0f && alive >= activeTime) BeginEnd();
        float end01 = endTime >= 0f ? Mathf.Clamp01((time - endTime) / FadeTime) : 0f;
        float fade = 1f - end01;

        // Driven in by the slam and springs back; sways; sinks into the ground at the end
        float dip = alive < ImpactRecover ? ImpactDip * (1f - Mathf.SmoothStep(0f, 1f, alive / ImpactRecover)) : 0f;
        float shudder = Mathf.Exp(-alive * 9f) * Mathf.Sin(alive * 55f) * 6f;
        flag.localRotation = Quaternion.Euler(shudder, Mathf.Sin(time * 1.7f) * 4f, shudder * 0.5f);
        flag.localPosition = Vector3.down * (end01 * 1.2f + dip);

        // Wall rises, scrolls its marks, then settles into the ground
        float rise = Mathf.SmoothStep(0f, 1f, alive / FenceRise) * fade;
        fence.transform.localScale = new Vector3(1f, Mathf.Max(0.001f, FenceHeight * rise), 1f);
        block.Clear();
        block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, time * 0.15f, 0f));
        Color fc = Color.Lerp(Holy, AmeliaVfx.GlossPink, 0.25f + 0.1f * Mathf.Sin(time * 3f)) * (0.9f * fade);
        fc.a = fade;
        block.SetColor("_BaseColor", fc);
        fence.SetPropertyBlock(block);

        // Ground ring turns slowly
        ring.transform.rotation = Quaternion.Euler(90f, time * 8f, 0f);
        Tint(ring, Holy, 0.7f * fade * Mathf.Clamp01(alive / 0.2f));

        // Gauge: remaining time, running down clockwise
        float left = Mathf.Clamp01(1f - alive / activeTime) * (endTime >= 0f ? 0f : 1f);
        int points = Mathf.Max(2, Mathf.CeilToInt(96 * left));
        gauge.positionCount = left > 0.001f ? points : 0;
        for (int i = 0; i < gauge.positionCount; i++)
        {
            float a = Mathf.PI * 0.5f - i / (float)(points - 1) * left * Mathf.PI * 2f;
            gauge.SetPosition(i, to + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius - 0.35f) + Vector3.up * 0.06f);
        }
        gauge.startColor = AmeliaVfx.GlossOrange;
        gauge.endColor = Color.Lerp(AmeliaVfx.GlossPink, AmeliaVfx.GlossOrange, 1f - left);

        // Halo and emblem over the pole
        halo.transform.localRotation = Quaternion.Euler(90f, time * 60f, 0f);
        Tint(halo, Holy, (0.8f + 0.2f * Mathf.Sin(time * 4f)) * fade);
        if (core != null)
        {
            block.Clear();
            float glow = (2.5f + 0.8f * Mathf.Sin(time * 4f)) * fade;
            block.SetColor("_BaseColor", new Color(Holy.r * glow, Holy.g * glow, Holy.b * glow, 1f));
            core.SetPropertyBlock(block, 3);
        }
        Camera cam = Camera.main;
        Vector3 signPos = to + Vector3.up * (TopY + 0.45f + Mathf.Sin(time * 2f) * 0.06f);
        sign.transform.position = signPos;
        if (cam != null)
        {
            Vector3 toCam = Vector3.ProjectOnPlane(cam.transform.position - signPos, Vector3.up);
            if (toCam.sqrMagnitude > 0.01f) sign.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }
        Tint(sign, Color.white, fade * Mathf.Clamp01(alive / 0.25f));

        // Light column: strong when it lands, then a faint shaft
        float shaft = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(alive / 0.6f)) * fade;
        float shaftWidth = Mathf.Lerp(1.8f, 0.8f, Mathf.Clamp01(alive / 0.4f));
        beam.transform.position = to + Vector3.up * 7f;
        if (cam != null)
        {
            Vector3 toCam = Vector3.ProjectOnPlane(cam.transform.position - beam.transform.position, Vector3.up);
            if (toCam.sqrMagnitude > 0.01f) beam.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
        }
        beam.transform.localScale = new Vector3(14f, shaftWidth, 1f);             // Line texture: soft across its width
        Tint(beam, Holy, shaft);

        holyLight.intensity = (1.2f + 2.5f * Mathf.Clamp01(1f - alive / 0.5f)) * fade;

        if (endTime >= 0f)
        {
            if (end01 >= 1f) Destroy(gameObject);
            return;
        }

        if (time >= nextScan)
        {
            nextScan = time + 0.1f;
            ScanAllies();
        }
    }

    private void WaveBanner()
    {
        if (bannerMesh == null) return;
        for (int i = 0; i < bannerRest.Length; i++)
        {
            Vector3 p = bannerRest[i];
            float free = -p.y / BannerH;                  // Pinned at the crossbar, free at the tails
            float wave = Mathf.Sin(-p.y * 4f - time * 3.2f + p.x * 2.5f) * 0.09f + Mathf.Sin(-p.y * 7f - time * 5f) * 0.025f;
            float sway = Mathf.Sin(time * 1.3f - p.y) * 0.04f;
            bannerVerts[i] = p + new Vector3(sway * free * 0.5f, 0f, (wave + sway) * free);
        }
        bannerMesh.vertices = bannerVerts;
    }

    // Up into the sky, a beat while the target lights up, then straight down onto it
    private void Fly()
    {
        float slamStart = LaunchTime + HangTime;
        if (time < LaunchTime)
        {
            float u = time / LaunchTime;
            transform.position = from + Vector3.up * (SkyHeight * u * u);
        }
        else if (time < slamStart)
        {
            if (trail.emitting) { trail.emitting = false; trail.Clear(); }
            transform.position = to + Vector3.up * SkyHeight;
        }
        else
        {
            if (!trail.emitting) { trail.Clear(); trail.emitting = true; }
            float u = Mathf.Clamp01((time - slamStart) / SlamTime);
            transform.position = to + Vector3.up * (SkyHeight * (1f - u * u));
            if (u >= 1f) { Plant(); return; }
        }
        flag.localRotation = Quaternion.identity;

        // Target marker: closes in and brightens until the hit
        float t01 = Mathf.Clamp01(time / (slamStart + SlamTime));
        reticle.transform.position = to + Vector3.up * 0.05f;
        reticle.transform.rotation = Quaternion.Euler(90f, time * 240f, 0f);
        float rs = Mathf.Lerp(4.5f, 1.2f, t01 * t01);
        reticle.transform.localScale = new Vector3(rs, rs, 1f);
        Tint(reticle, Holy, Mathf.Lerp(0.5f, 1.2f, t01) + 0.2f * Mathf.Sin(time * 30f));
    }

    private void Plant()
    {
        planted = true;
        plantTime = time;
        transform.position = to;
        flag.localPosition = Vector3.down * ImpactDip;
        flag.localRotation = Quaternion.identity;
        trail.emitting = false;
        Destroy(reticle.gameObject);
        BuildZone();
        AmeliaVfx.SpawnFlagPlant(to, radius);
        AmeliaVfx.SpawnFlagSlam(to);
        CombatCameraFeedback.GroundSlam(to, Mathf.Max(16f, radius * 2f), 2.3f, 1.35f,
            new Color(1f, .76f, .25f));
        SkillSfx.Play("amelia_flag_plant", to, 0.95f, .82f);
        SkillSfx.PlayLoopFor("amelia_flag_loop", transform, 0.35f, activeTime, 0.3f, 0.5f);
        ScanAllies();
    }

    private void BeginEnd()
    {
        SkillSfx.Play("amelia_flag_end", to, 0.6f);
        endTime = time;
        ReleaseAll();
        if (motes != null) motes.StopSoon(0.5f);
        AmeliaVfx.SpawnInfinityBurst(to + Vector3.up * (TopY + 0.45f));
    }

    private void Tint(MeshRenderer r, Color c, float alpha)
    {
        alpha = Mathf.Max(0f, alpha);
        block.Clear();
        block.SetColor("_BaseColor", new Color(c.r * alpha, c.g * alpha, c.b * alpha, alpha));
        r.SetPropertyBlock(block);
    }

    #region Zone
    private void ScanAllies()
    {
        AllyTargeting.CollectAllies(allies);
        foreach (HealthManager ally in allies)
        {
            if (ally == null) continue;
            Vector3 d = ally.transform.position - to;
            bool isIn = !ally.dead && ally.isActiveAndEnabled && Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).magnitude <= radius;
            bool was = inside.ContainsKey(ally);
            if (isIn && !was) Enter(ally);
            else if (!isIn && was) Exit(ally);
        }
        // Allies that vanished
        List<HealthManager> gone = null;
        foreach (HealthManager h in inside.Keys)
            if (h == null) (gone ??= new List<HealthManager>()).Add(h);
        if (gone != null) foreach (HealthManager h in gone) inside.Remove(h);
    }

    private void Enter(HealthManager ally)
    {
        SetInfinite(ally, true);
        inside[ally] = SpawnHeadMark(ally.transform);
        SkillOverlay.Apply(ally.gameObject, "quartermaster", Color.Lerp(Holy, AmeliaVfx.GlossOrange, 0.5f), -1f, 1.1f, 2.6f);
        SkillSfx.Play("amelia_infinite", ally.transform.position, 0.5f);
    }

    private void Exit(HealthManager ally)
    {
        SetInfinite(ally, false);
        SkillOverlay.Remove(ally.gameObject, "quartermaster");
        if (inside.TryGetValue(ally, out GameObject mark) && mark != null) Destroy(mark);
        inside.Remove(ally);
    }

    private void ReleaseAll()
    {
        foreach (HealthManager ally in new List<HealthManager>(inside.Keys))
            if (ally != null) Exit(ally);
        inside.Clear();
    }

    private static void SetInfinite(HealthManager ally, bool on)
    {
        if (ally.TryGetComponent(out ShootBehaviour shoot))
        {
            if (on) shoot.AddInfiniteAmmo(); else shoot.RemoveInfiniteAmmo();
            return;
        }
        EnemyAI.StateController ai = ally.GetComponent<EnemyAI.StateController>();
        if (ai == null && ally.TryGetComponent(out AllyStateController allyState)) ai = allyState.CombatController;
        if (ai == null) return;
        if (on) ai.AddInfiniteAmmo(); else ai.RemoveInfiniteAmmo();
    }

    // Quartermaster emblem floating above the ally's head, facing the camera
    private GameObject SpawnHeadMark(Transform target)
    {
        GameObject go = new GameObject("VFX_InfiniteAmmoMark");
        InfinityMark mark = go.AddComponent<InfinityMark>();
        mark.target = target;
        MeshRenderer quad = HawkShape.CreateQuad("Emblem", go.transform, AmeliaVfx.Additive(SignTexture));
        quad.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        return go;
    }

    private void OnDestroy()
    {
        ReleaseAll();
        if (bannerMesh != null) Destroy(bannerMesh);
    }
    #endregion

    #region Materials
    private static Mesh standardMesh;
    private static Material goldMaterial, coreMaterial;

    // Blender model: Resources/VFX/Amelia/amelia_flag_mesh.bytes (Tools/Blender/amelia_flag_mesh.py)
    private static Mesh StandardMesh
    {
        get
        {
            if (standardMesh != null) return standardMesh;
            TextAsset data = Resources.Load<TextAsset>("VFX/Amelia/amelia_flag_mesh");
            if (data == null) return null;
            using BinaryReader reader = new BinaryReader(new MemoryStream(data.bytes));
            int count = reader.ReadInt32(), subCount = reader.ReadInt32();
            Vector3[] pos = new Vector3[count], nrm = new Vector3[count];
            Vector2[] uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                pos[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                nrm[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }
            standardMesh = new Mesh { name = "VFX_AmeliaStandard", vertices = pos, normals = nrm, uv = uv, subMeshCount = subCount };
            for (int sub = 0; sub < subCount; sub++)
            {
                int n = reader.ReadInt32();
                int[] tris = new int[n];
                for (int i = 0; i < n; i++) tris[i] = reader.ReadInt32();
                standardMesh.SetTriangles(tris, sub);
            }
            standardMesh.RecalculateBounds();
            return standardMesh;
        }
    }

    private static Material GoldMaterial
    {
        get
        {
            if (goldMaterial != null) return goldMaterial;
            goldMaterial = LitMaterial("VFX_FlagGold", new Color(0.85f, 0.66f, 0.3f), 0.95f, 0.72f);
            goldMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.07f));      // Faint holy warmth even in shade
            goldMaterial.EnableKeyword("_EMISSION");
            return goldMaterial;
        }
    }

    private static readonly Dictionary<string, Material> litCache = new();

    private static Material LitMaterial(string name, Color color, float metallic, float smoothness)
    {
        if (litCache.TryGetValue(name, out Material m) && m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        litCache[name] = m;
        return m;
    }

    private static Material CoreMaterial
    {
        get
        {
            if (coreMaterial != null) return coreMaterial;
            coreMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "VFX_FlagCore" };
            coreMaterial.SetColor("_BaseColor", Holy * 2.5f);
            return coreMaterial;
        }
    }

    private static Material BannerMaterial
    {
        get
        {
            if (bannerMaterial != null) return bannerMaterial;
            bannerMaterial = new Material(SkillVfxFactory.GetAlphaMaterial(BannerTexture)) { name = "VFX_FlagBanner" };
            if (bannerMaterial.HasProperty("_Cull")) bannerMaterial.SetFloat("_Cull", 0f);
            return bannerMaterial;
        }
    }

    // Painted gonfalon: Tools/Vfx/amelia_banner.py
    private static Texture2D BannerTexture => bannerTexture != null ? bannerTexture : bannerTexture = Resources.Load<Texture2D>("VFX/Amelia/amelia_banner");
    #endregion
}
