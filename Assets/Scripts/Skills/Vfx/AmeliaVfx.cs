using UnityEngine;
using static SkillVfxFactory;

/// <summary>
/// Amelia skill effects: supply wave (white marks spreading to the skill range), reinforced rounds
/// (flame mark inside the body, glowing red-hot) and the quartermaster's infinity marks.
/// Colors follow her sunglasses gloss (orange -> pink) and uniform.
/// </summary>
public static class AmeliaVfx
{
    public static readonly Color SupplyWhite = new Color(0.95f, 0.97f, 1f, 1f);
    public static readonly Color FlameOrange = new Color(1f, 0.45f, 0.12f, 1f);
    public static readonly Color FlameRed = new Color(0.95f, 0.18f, 0.05f, 1f);
    public static readonly Color GlossOrange = new Color(0.85f, 0.53f, 0.2f, 1f);   // #D98734
    public static readonly Color GlossPink = new Color(0.85f, 0.2f, 0.64f, 1f);     // #D934A3

    // Ring texture draws its ring at 85% of the half size
    private const float RingAt = 0.85f;

    private static Texture2D infinityTexture;
    private static readonly System.Collections.Generic.Dictionary<Texture2D, Material> twoSided = new();

    private static readonly System.Collections.Generic.Dictionary<string, Material> tuned = new();

    /// <summary> Two-sided additive material pushed over 1 (HDR) so thin glowing lines read on bright ground. </summary>
    public static Material AdditiveBright(Texture2D texture, float intensity)
    {
        string key = texture.name + "_" + intensity;
        if (tuned.TryGetValue(key, out Material cached) && cached != null) return cached;
        Material m = new Material(Additive(texture)) { name = "VFX_AdditiveHDR_" + key };
        m.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
        m.renderQueue = 3010;                     // Over scorch marks
        tuned[key] = m;
        return m;
    }

    /// <summary> Two-sided alpha-blended material (dark scorch marks). </summary>
    public static Material AlphaTwoSided(Texture2D texture)
    {
        string key = "alpha_" + texture.name;
        if (tuned.TryGetValue(key, out Material cached) && cached != null) return cached;
        Material m = new Material(GetAlphaMaterial(texture)) { name = "VFX_Alpha2S_" + texture.name };
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        m.renderQueue = 2990;                     // Under glowing lines
        tuned[key] = m;
        return m;
    }

    /// <summary> Additive material drawn from both sides (flat ground quads and billboards). </summary>
    public static Material Additive(Texture2D texture)
    {
        if (twoSided.TryGetValue(texture, out Material cached) && cached != null) return cached;
        Material m = new Material(GetAdditiveMaterial(texture)) { name = "VFX_Additive2S_" + texture.name };
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        twoSided[texture] = m;
        return m;
    }

    #region Range wave (skill 1 / 2)
    /// <summary> Wave spreading from the caster to exactly the skill radius, holding there briefly so the range reads. </summary>
    public static void SpawnRangeWave(Transform caster, float radius, Color color, bool embers)
    {
        GameObject root = CreateRoot("VFX_AmeliaRange", null, caster.position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.8f;
        runner.stopEmittingAt = 0.2f;

        float size = radius * 2f / RingAt;
        SpawnGroundRing(root.transform, color, size * 0.08f, size, 0.5f, 0f);          // Travelling wave
        SpawnGroundRing(root.transform, WithAlpha(color, 0.55f), size, size, 1.1f, 0.4f); // Range edge lingers

        // Marks racing out along the ground to the edge
        ParticleSystem marks = CreateParticles(root.transform, "Marks", Vector3.up * 0.15f, Kenney(embers ? "flame_05" : "star_07"));
        var main = marks.main;
        main.duration = 0.1f;
        main.startLifetime = 0.5f;
        main.startSpeed = radius / 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(embers ? 0.35f : 0.25f, embers ? 0.6f : 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = embers ? new ParticleSystem.MinMaxGradient(FlameOrange, FlameRed) : new ParticleSystem.MinMaxGradient(color);
        SetShapeCircle(marks, 0.3f, Vector3.right * -90f, 0f);
        var shape = marks.shape;
        shape.arc = 360f;
        SetBurst(marks, embers ? 36 : 28);
        SetFadeOut(marks);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.8f, color, radius), 2.5f, 0.05f, 0.5f);
        runner.Play();
    }
    #endregion

    #region Skill 1 - supply
    /// <summary> Fresh magazine handed over: white flash at the chest and marks rising. </summary>
    public static void SpawnSupplyReceive(Transform target)
    {
        GameObject root = CreateRoot("VFX_AmeliaSupply", target, Vector3.up * 1.1f);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.3f;
        runner.stopEmittingAt = 0.15f;

        ParticleSystem flash = CreateParticles(root.transform, "Flash", Vector3.zero, Kenney("star_08"));
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.startLifetime = 0.35f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 0.55f;
        flashMain.startColor = SupplyWhite;
        flashMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(flash, 1);
        SetFadeOut(flash);

        ParticleSystem rise = CreateParticles(root.transform, "Rise", Vector3.down * 0.6f, Kenney("star_07"));
        var main = rise.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = SupplyWhite;
        SetShapeCircle(rise, 0.35f, Vector3.right * -90f, 10f);
        SetBurst(rise, 8);
        SetFadeOut(rise);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, SupplyWhite, 1.5f), 0.6f, 0.02f, 0.3f);
        runner.Play();
    }
    #endregion

    #region Skill 2 - reinforced rounds
    /// <summary> Heat surge when the reinforced magazine goes in. </summary>
    public static void SpawnReinforceReceive(Transform target)
    {
        GameObject root = CreateRoot("VFX_AmeliaReinforce", target, Vector3.up * 1.1f);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.3f;
        runner.stopEmittingAt = 0.15f;

        ParticleSystem flash = CreateParticles(root.transform, "Flash", Vector3.zero, Kenney("star_08"));
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.startLifetime = 0.2f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 0.7f;
        flashMain.startColor = new Color(1f, 0.7f, 0.35f);
        flashMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(flash, 1);
        SetFadeOut(flash);

        ParticleSystem sparks = CreateParticles(root.transform, "Sparks", Vector3.zero, Kenney("spark_01"));
        var main = sparks.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), FlameOrange);
        main.gravityModifier = 0.6f;
        Sphere(sparks, 0.15f);
        SetBurst(sparks, 22);
        SetFadeOut(sparks);
        ParticleSystemRenderer sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.05f;
        sr.lengthScale = 1.2f;

        runner.AddLight(CreateLight(root.transform, Vector3.zero, FlameOrange, 2f), 1.2f, 0.02f, 0.35f);
        runner.Play();
    }

    private static Texture2D lineTexture;

    /// <summary> Soft line across its width, full along its length (for glowing cracks). </summary>
    public static Texture2D LineTexture
    {
        get
        {
            if (lineTexture != null) return lineTexture;
            const int h = 64;
            lineTexture = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VFX_AmeliaLine" };
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs(y / (h - 1f) * 2f - 1f);
                float a = Mathf.Pow(1f - v, 1.6f);
                for (int x = 0; x < 4; x++) lineTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            lineTexture.Apply();
            return lineTexture;
        }
    }

    /// <summary> Hammer strike on the anvil: white-hot flash, heat shockwave, spark fountain. </summary>
    public static void SpawnForgeImpact(Vector3 position)
    {
        GameObject root = CreateRoot("VFX_AmeliaForgeImpact", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2f;
        runner.stopEmittingAt = 0.12f;

        ParticleSystem flash = CreateParticles(root.transform, "Flash", Vector3.up * 0.3f, Kenney("star_08"));
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.startLifetime = 0.18f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 2.2f;
        flashMain.startColor = new Color(1f, 0.85f, 0.55f);
        SetBurst(flash, 1);
        SetFadeOut(flash);

        SpawnGroundRing(root.transform, FlameOrange, 0.3f, 4.5f, 0.3f, 0f);            // Heat shockwave

        // Sparks fly up and rain down, stretched by speed
        ParticleSystem sparks = CreateParticles(root.transform, "Sparks", Vector3.up * 0.15f, Kenney("spark_01"));
        var main = sparks.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), FlameOrange);
        main.gravityModifier = 1.4f;
        main.maxParticles = 300;
        SetShapeCircle(sparks, 0.15f, Vector3.right * -90f, 40f);
        SetBurst(sparks, 90);
        SetFadeOut(sparks);
        ParticleSystemRenderer sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.04f;
        sr.lengthScale = 1.5f;

        // Heavy smoke puff low to the ground
        ParticleSystem smoke = CreateParticles(root.transform, "Smoke", Vector3.up * 0.2f, Kenney("smoke_07"));
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(Kenney("smoke_07"));
        var smokeMain = smoke.main;
        smokeMain.duration = 0.1f;
        smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        smokeMain.startColor = new Color(0.25f, 0.2f, 0.18f, 0.5f);
        SetShapeCircle(smoke, 0.2f, Vector3.right * -90f, 75f);
        SetBurst(smoke, 8);
        SetFadeOut(smoke);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.6f, new Color(1f, 0.6f, 0.25f), 5f), 3f, 0.01f, 0.35f);
        runner.Play();
    }

    /// <summary> A crack reaching an ally's feet: little burst of sparks. </summary>
    public static void SpawnForgeCrackEnd(Vector3 position)
    {
        GameObject root = CreateRoot("VFX_AmeliaCrackEnd", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.2f;
        runner.stopEmittingAt = 0.1f;

        ParticleSystem sparks = CreateParticles(root.transform, "Sparks", Vector3.up * 0.05f, Kenney("spark_01"));
        var main = sparks.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), FlameOrange);
        main.gravityModifier = 1f;
        SetShapeCircle(sparks, 0.1f, Vector3.right * -90f, 35f);
        SetBurst(sparks, 18);
        SetFadeOut(sparks);
        ParticleSystemRenderer sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.05f;
        sr.lengthScale = 1.2f;
        runner.Play();
    }

    /// <summary> While reinforced: embers rising from the body. StopSoon to end. </summary>
    public static SkillVfxRunner SpawnHeatEmbers(Transform target)
    {
        GameObject root = CreateRoot("VFX_AmeliaEmbers", target, Vector3.up * 0.6f);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 9999f;

        ParticleSystem embers = CreateParticles(root.transform, "Embers", Vector3.zero, Kenney("spark_01"));
        var main = embers.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(FlameOrange, new Color(1f, 0.75f, 0.35f));
        main.gravityModifier = -0.12f;
        SetShapeCircle(embers, 0.25f, Vector3.right * -90f, 12f);
        var emission = embers.emission;
        emission.rateOverTime = 10f;
        SetFadeOut(embers);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.6f, FlameOrange, 1.5f), 0.35f, 0.2f, 0.5f);
        runner.Play();
        return runner;
    }

    /// <summary> Reinforced rounds used up: the emblem cools off into a puff of embers. </summary>
    public static void SpawnHeatCoolDown(Transform target)
    {
        GameObject root = CreateRoot("VFX_AmeliaCool", target, Vector3.up * 1.2f);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.4f;
        runner.stopEmittingAt = 0.1f;

        ParticleSystem puff = CreateParticles(root.transform, "Embers", Vector3.zero, Kenney("spark_01"));
        var main = puff.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(FlameOrange, FlameRed);
        main.gravityModifier = -0.05f;
        Sphere(puff, 0.2f);
        SetBurst(puff, 16);
        SetFadeOut(puff);

        ParticleSystem smoke = CreateParticles(root.transform, "Smoke", Vector3.zero, Kenney("smoke_07"));
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(Kenney("smoke_07"));
        var sm = smoke.main;
        sm.duration = 0.1f;
        sm.startLifetime = 0.9f;
        sm.startSpeed = 0.4f;
        sm.startSize = 0.35f;
        sm.startColor = new Color(0.3f, 0.26f, 0.24f, 0.35f);
        Sphere(smoke, 0.1f);
        SetBurst(smoke, 3);
        SetFadeOut(smoke);
        runner.Play();
    }
    #endregion

    #region Game changer - quartermastering
    /// <summary> Infinity sign (lemniscate), soft glow on black, for additive quads. </summary>
    public static Texture2D InfinityTexture
    {
        get
        {
            if (infinityTexture != null) return infinityTexture;
            const int w = 256, h = 128;
            // Sample the curve once
            const int samples = 400;
            Vector2[] curve = new Vector2[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples * Mathf.PI * 2f;
                float d = 1f + Mathf.Sin(t) * Mathf.Sin(t);
                curve[i] = new Vector2(Mathf.Cos(t) / d * 0.44f, Mathf.Sin(t) * Mathf.Cos(t) / d * 0.44f * 2f);
            }
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / w - 0.5f, ((y + 0.5f) / h - 0.5f) * (h / (float)w) * 2f);
                    float best = float.MaxValue;
                    foreach (Vector2 c in curve) best = Mathf.Min(best, (p - new Vector2(c.x, c.y * 0.5f)).sqrMagnitude);
                    float dist = Mathf.Sqrt(best);
                    float line = Mathf.Clamp01(1f - dist / 0.018f);
                    float glow = Mathf.Exp(-dist * dist / 0.0025f) * 0.45f;
                    float v = Mathf.Clamp01(line + glow);
                    pixels[y * w + x] = new Color(v, v, v, v);
                }
            }
            infinityTexture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "VFX_Infinity", wrapMode = TextureWrapMode.Clamp };
            infinityTexture.SetPixels32(pixels);
            infinityTexture.Apply(true);
            return infinityTexture;
        }
    }

    /// <summary> Flag planted: dust, shockwave and a gloss-colored flash. </summary>
    public static void SpawnFlagPlant(Vector3 position, float radius)
    {
        GameObject root = CreateRoot("VFX_AmeliaFlagPlant", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2f;
        runner.stopEmittingAt = 0.2f;

        float size = radius * 2f / RingAt;
        SpawnGroundRing(root.transform, HolyGold, 0.5f, size, 0.55f, 0f);
        SpawnGroundRing(root.transform, GlossPink, 0.5f, size * 0.6f, 0.45f, 0.05f);

        ParticleSystem dust = CreateParticles(root.transform, "Dust", Vector3.up * 0.1f, Kenney("smoke_07"));
        dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(Kenney("smoke_07"));
        var main = dust.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.55f, 0.5f, 0.42f, 0.45f);
        SetShapeCircle(dust, 0.3f, Vector3.right * -90f, 80f);
        SetBurst(dust, 14);
        SetFadeOut(dust);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1f, HolyGold, 8f), 5f, 0.02f, 0.6f);
        runner.Play();
    }

    /// <summary> The flag slams into the ground: white-gold flash, fast shock ring, flying debris and a dust wall. </summary>
    public static void SpawnFlagSlam(Vector3 position)
    {
        GameObject root = CreateRoot("VFX_AmeliaFlagSlam", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2.2f;
        runner.stopEmittingAt = 0.15f;

        SpawnGroundRing(root.transform, Color.white, 0.35f, 7f, 0.9f, 0f);
        SpawnGroundRing(root.transform, HolyGold, 0.5f, 4f, 0.8f, 0.02f);

        ParticleSystem flash = CreateParticles(root.transform, "Flash", Vector3.up * 0.6f, Kenney("light_03"));
        var fm = flash.main;
        fm.duration = 0.05f;
        fm.startLifetime = 0.18f;
        fm.startSpeed = 0f;
        fm.startSize = 5f;
        fm.startColor = new Color(1f, 0.95f, 0.8f, 1f);
        SetBurst(flash, 1);
        SetFadeOut(flash);

        ParticleSystem debris = CreateParticles(root.transform, "Debris", Vector3.up * 0.1f, Kenney("dirt_02"));
        debris.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(Kenney("dirt_02"));
        var dm = debris.main;
        dm.duration = 0.1f;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        dm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        dm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dm.startColor = new Color(0.32f, 0.28f, 0.22f, 1f);
        dm.gravityModifier = 2.2f;
        SetShapeCircle(debris, 0.3f, Vector3.right * -90f, 55f);
        SetBurst(debris, 26);
        SetFadeOut(debris);

        ParticleSystem wall = CreateParticles(root.transform, "DustWall", Vector3.up * 0.2f, Kenney("smoke_07"));
        wall.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(Kenney("smoke_07"));
        var wm = wall.main;
        wm.duration = 0.1f;
        wm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        wm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
        wm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        wm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        wm.startColor = new Color(0.6f, 0.55f, 0.47f, 0.5f);
        SetShapeCircle(wall, 0.4f, Vector3.right * -90f, 88f);
        var drag = wall.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 4f;
        SetBurst(wall, 24);
        SetFadeOut(wall);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.2f, HolyGold, 12f), 9f, 0.01f, 0.5f);
        runner.Play();
    }

    public static readonly Color HolyGold = new Color(1f, 0.86f, 0.55f, 1f);

    /// <summary> While the flag stands: small gold light motes drifting slowly up inside the zone. StopSoon to end. </summary>
    public static SkillVfxRunner SpawnHolyMotes(Vector3 position, float radius)
    {
        GameObject root = CreateRoot("VFX_AmeliaHolyMotes", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 9999f;

        ParticleSystem motes = CreateParticles(root.transform, "Motes", Vector3.up * 0.1f, Kenney("spark_01"));
        var main = motes.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(HolyGold, Color.Lerp(HolyGold, GlossPink, 0.35f));
        main.gravityModifier = -0.04f;
        SetShapeCircle(motes, radius * 0.9f, Vector3.right * -90f, 5f);
        var emission = motes.emission;
        emission.rateOverTime = 26f;
        SetFadeOut(motes);

        // Brighter motes streaming up the pole
        ParticleSystem rise = CreateParticles(root.transform, "Rise", Vector3.up * 0.2f, Kenney("spark_01"));
        var rm = rise.main;
        rm.loop = true;
        rm.duration = 1f;
        rm.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        rm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        rm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        rm.startColor = new Color(1f, 0.95f, 0.8f, 1f);
        SetShapeCircle(rise, 0.35f, Vector3.right * -90f, 3f);
        var re = rise.emission;
        re.rateOverTime = 10f;
        SetFadeOut(rise);

        runner.Play();
        return runner;
    }

    /// <summary> Flag time over: the infinity sign breaks into gold and pink sparks. </summary>
    public static void SpawnInfinityBurst(Vector3 position)
    {
        GameObject root = CreateRoot("VFX_AmeliaInfinityBurst", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.6f;
        runner.stopEmittingAt = 0.1f;

        ParticleSystem sparks = CreateParticles(root.transform, "Sparks", Vector3.zero, Kenney("spark_01"));
        var main = sparks.main;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(HolyGold, GlossPink);
        main.gravityModifier = 0.15f;
        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.4f, 0.5f, 0.1f);
        SetBurst(sparks, 40);
        SetFadeOut(sparks);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, HolyGold, 3f), 2f, 0.02f, 0.4f);
        runner.Play();
    }
    #endregion

    private static void Sphere(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
    }
}
