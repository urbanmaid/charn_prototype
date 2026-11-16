using UnityEngine;
using static SkillVfxFactory;

/// <summary>
/// Fiona skill effects: Lizard's First Bite (venom), Crow's Wingbeat (dark feather shield), Hawk's Ambush (diving strike).
/// Uses Kenney Particle Pack textures (CC0) in Resources/VFX/Kenney.
/// </summary>
public static class FionaVfx
{
    public static readonly Color Venom = new Color(0.46f, 0.62f, 0.2f, 1f);      // Murky swamp green
    public static readonly Color VenomDeep = new Color(0.22f, 0.32f, 0.08f, 1f);
    public static readonly Color CrowDark = new Color(0.04f, 0.045f, 0.06f, 1f);
    public static readonly Color CrowGlow = new Color(0.72f, 0.82f, 1f, 1f);   // Silver / blue-gray sheen
    public static readonly Color HawkAmber = new Color(1f, 0.72f, 0.25f, 1f);
    public static readonly Color HawkBrown = new Color(0.35f, 0.22f, 0.1f, 1f);

    /// <summary> Build the generated hawk/crow/lizard textures up front (on equip) so the first cast doesn't hitch. </summary>
    public static void Prewarm()
    {
        _ = HawkShape.Gold;
        _ = HawkShape.Dark;
        _ = HawkShape.Crow;
        LizardBite.Prewarm();
    }

    #region Lizard's First Bite (venom)
    /// <summary>
    /// Activation: a swamp lizard darts out beside the gun and bites the magazine; onBite fires at the bite
    /// (venom splash on the magazine). Murky mist and a small ring at the feet.
    /// </summary>
    public static LizardBite SpawnVenomActivate(Transform caster, Transform muzzle, System.Action onBite = null)
    {
        GameObject root = CreateRoot("VFX_VenomActivate", caster, Vector3.zero);
        root.transform.localRotation = Quaternion.identity; // Face with caster
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.8f;
        runner.stopEmittingAt = 0.3f;

        SpawnGroundRing(root.transform, Venom, 0.5f, 2.6f, 0.6f, 0f);

        // Swamp mist curling up around the caster
        ParticleSystem mist = Particles(root.transform, "Mist", Vector3.up * 0.9f, "smoke_07", true);
        var mistMain = mist.main;
        mistMain.duration = 0.2f;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mistMain.startColor = WithAlpha(VenomDeep, 0.35f);
        Sphere(mist, 0.4f);
        SetBurst(mist, 8);
        SizeCurve(mist, 0.8f, 1.6f);
        SetFadeOut(mist);
        runner.Play();

        // The lizard coils around the gun model and bites the muzzle
        InteractiveWeapon weapon = muzzle != null ? muzzle.GetComponentInParent<InteractiveWeapon>() : null;
        LizardBite lizard = null;
        if (weapon != null && TryGetMeshBounds(weapon.gameObject, out Bounds gun))
        {
            lizard = LizardBite.Spawn(weapon.transform, muzzle, gun, point =>
            {
                SpawnVenomMagazineBite(point);
                onBite?.Invoke();
            }, SpawnVenomSoak);
        }
        if (lizard == null)
        {
            // No gun to wrap (AI or missing model): venom loads straight away
            onBite?.Invoke();
        }
        return lizard;
    }

    // The lizard melts into the gun: venom mist rises along the coil
    private static void SpawnVenomSoak(System.Collections.Generic.List<Vector3> points)
    {
        foreach (Vector3 point in points)
        {
            GameObject root = CreateRoot("VFX_VenomSoak", null, point);
            SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
            runner.lifetime = 1.4f;
            runner.stopEmittingAt = 0.2f;

            ParticleSystem mist = Particles(root.transform, "Mist", Vector3.zero, "smoke_07", true);
            var main = mist.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = WithAlpha(VenomDeep, 0.5f);
            main.gravityModifier = -0.05f;
            Sphere(mist, 0.03f);
            SetBurst(mist, 3);
            SizeCurve(mist, 0.6f, 1.6f);
            SetFadeOut(mist);

            ParticleSystem drops = Particles(root.transform, "Drops", Vector3.zero, "circle_05", true);
            var dropMain = drops.main;
            dropMain.duration = 0.2f;
            dropMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            dropMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            dropMain.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            dropMain.startColor = new ParticleSystem.MinMaxGradient(Venom, VenomDeep);
            dropMain.gravityModifier = 1f;
            Sphere(drops, 0.02f);
            SetBurst(drops, 3);
            SetFadeOut(drops);
            runner.Play();
        }
    }

    private static bool TryGetMeshBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    // Bite on the magazine: jaws snap, venom splashes and soaks in
    private static void SpawnVenomMagazineBite(Vector3 point)
    {
        GameObject root = CreateRoot("VFX_VenomMagBite", null, point);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.2f;
        runner.stopEmittingAt = 0.15f;

        ParticleSystem bite = Particles(root.transform, "Bite", Vector3.zero, "scratch_01", false);
        var biteMain = bite.main;
        biteMain.duration = 0.1f;
        biteMain.startLifetime = 0.3f;
        biteMain.startSpeed = 0f;
        biteMain.startSize = 0.7f;
        biteMain.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        biteMain.startColor = Venom;
        SetBurst(bite, 1);
        SizeCurve(bite, 0.6f, 1.15f);
        SetFadeOut(bite);

        ParticleSystem splash = Particles(root.transform, "Splash", Vector3.zero, "circle_05", true);
        var splashMain = splash.main;
        splashMain.duration = 0.1f;
        splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        splashMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        splashMain.startColor = new ParticleSystem.MinMaxGradient(Venom, VenomDeep);
        splashMain.gravityModifier = 1.2f;
        Sphere(splash, 0.05f);
        SetBurst(splash, 16);
        SetFadeOut(splash);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, Venom, 1.5f), 1.5f, 0.02f, 0.4f);
        runner.Play();
    }

    /// <summary> Looping venom drip on the gun while venom rounds are loaded. Call StopSoon on the runner to end. </summary>
    public static SkillVfxRunner SpawnVenomWeaponAura(Transform muzzle)
    {
        GameObject root = CreateRoot("VFX_VenomWeapon", muzzle, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 9999f;

        ParticleSystem drip = Particles(root.transform, "Drip", Vector3.zero, "circle_05", false);
        var main = drip.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(Venom, VenomDeep);
        main.gravityModifier = 0.35f;
        Sphere(drip, 0.06f);
        var emission = drip.emission;
        emission.rateOverTime = 20f;
        SetFadeOut(drip);

        ParticleSystem mist = Particles(root.transform, "Mist", Vector3.zero, "smoke_04", false);
        var mistMain = mist.main;
        mistMain.loop = true;
        mistMain.duration = 1f;
        mistMain.startLifetime = 0.6f;
        mistMain.startSpeed = 0.1f;
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mistMain.startColor = WithAlpha(Venom, 0.2f);
        Sphere(mist, 0.05f);
        var mistEmission = mist.emission;
        mistEmission.rateOverTime = 7f;
        SizeCurve(mist, 0.6f, 1.5f);
        SetFadeOut(mist);

        ParticleSystem glint = Particles(root.transform, "Glint", Vector3.zero, "star_07", false);
        var glintMain = glint.main;
        glintMain.loop = true;
        glintMain.duration = 1f;
        glintMain.startLifetime = 0.25f;
        glintMain.startSpeed = 0f;
        glintMain.startSize = 0.14f;
        glintMain.startColor = Venom;
        var glintEmission = glint.emission;
        glintEmission.rateOverTime = 2.5f;
        SetFadeOut(glint);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, Venom, 2f), 1.5f, 0.2f, 0.3f);
        runner.Play();
        return runner;
    }

    private static Texture2D needleTexture;

    // Horizontal needle streak: thin bright core, soft glow, tapered ends (black background, additive)
    private static Texture2D NeedleTexture
    {
        get
        {
            if (needleTexture != null) return needleTexture;
            const int w = 128, h = 16;
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float taper = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f) * Mathf.Lerp(0.5f, 1f, u);   // Brighter toward the tip
                    float core = Mathf.Exp(-v * v * 40f), glow = Mathf.Exp(-v * v * 5f) * 0.35f;
                    float i = Mathf.Clamp01((core + glow) * taper);
                    pixels[y * w + x] = new Color(i, i, i, i);
                }
            }
            needleTexture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "VFX_VenomNeedle", wrapMode = TextureWrapMode.Clamp };
            needleTexture.SetPixels32(pixels);
            needleTexture.Apply(true);
            return needleTexture;
        }
    }

    /// <summary> Venom needle for each shot: a streaking green needle with a thin murky trail. </summary>
    public static void SpawnVenomTracer(Vector3 from, Vector3 to)
    {
        GameObject root = CreateRoot("VFX_VenomTracer", null, from);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 0.5f;
        LineRenderer line = CreateBeamRenderer(root, WithAlpha(Venom, 0.6f), WithAlpha(VenomDeep, 0.3f));
        line.useWorldSpace = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        runner.AddBeam(line, 0.09f, 0.02f, 0.02f, 0.24f);

        // Needle: one stretched particle travelling from the muzzle to the hit point
        Vector3 path = to - from;
        float distance = path.magnitude;
        if (distance > 0.1f)
        {
            const float speed = 140f;
            ParticleSystem needle = Particles(root.transform, "Needle", Vector3.zero, "trace_01", false);
            needle.transform.rotation = Quaternion.LookRotation(path);
            var main = needle.main;
            main.duration = 0.1f;
            main.startLifetime = distance / speed;
            main.startSpeed = speed;
            main.startSize = 0.12f;
            main.startColor = Color.Lerp(Venom, Color.white, 0.25f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = needle.shape;
            shape.enabled = false;                       // Straight along the shot
            SetBurst(needle, 1);
            ParticleSystemRenderer renderer = needle.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetAdditiveMaterial(NeedleTexture);   // Stretched billboards stretch along the texture's x
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.012f;
            renderer.lengthScale = 4f;
        }
        runner.Play();
    }

    /// <summary> Venom bite on hit point: bite marks, splatter and puff. </summary>
    public static void SpawnVenomBite(Vector3 point, Vector3 normal)
    {
        CombatSkillAccent.Burst(point + normal * 0.03f, Venom, 1.1f);
        GameObject root = CreateRoot("VFX_VenomBite", null, point + normal * 0.05f);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.3f;
        runner.stopEmittingAt = 0.15f;

        ParticleSystem bite = Particles(root.transform, "Bite", Vector3.zero, "scratch_01", false);
        var biteMain = bite.main;
        biteMain.duration = 0.1f;
        biteMain.startLifetime = 0.4f;
        biteMain.startSpeed = 0f;
        biteMain.startSize = 2.1f;
        biteMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        biteMain.startColor = new ParticleSystem.MinMaxGradient(Venom, Color.white);
        SetBurst(bite, 1);
        SizeCurve(bite, 0.7f, 1.2f);
        SetFadeOut(bite);

        ParticleSystem splat = Particles(root.transform, "Splat", Vector3.zero, "dirt_02", false);
        var splatMain = splat.main;
        splatMain.duration = 0.1f;
        splatMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        splatMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        splatMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
        splatMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        splatMain.startColor = new ParticleSystem.MinMaxGradient(Venom, VenomDeep);
        splatMain.gravityModifier = 1.2f;
        Sphere(splat, 0.08f);
        SetBurst(splat, 26);
        SetFadeOut(splat);

        ParticleSystem puff = Particles(root.transform, "Puff", Vector3.zero, "smoke_07", false);
        var puffMain = puff.main;
        puffMain.duration = 0.1f;
        puffMain.startLifetime = 0.7f;
        puffMain.startSpeed = 0.4f;
        puffMain.startSize = 0.8f;
        puffMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        puffMain.startColor = WithAlpha(Venom, 0.4f);
        Sphere(puff, 0.1f);
        SetBurst(puff, 3);
        SizeCurve(puff, 0.7f, 2f);
        SetFadeOut(puff);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, Venom, 2f), 2f, 0.02f, 0.25f);
        runner.Play();
    }

    /// <summary> Slowed debuff on an enemy: green outline, poison bubbles and smoke on the body, venom drips. </summary>
    public static void SpawnOrExtendVenomDebuff(Transform target, float duration)
    {
        SkillOverlay.Apply(target.gameObject, "venom", Venom * 1.4f, duration, 1.8f, 2.2f);

        const string auraName = "VFX_VenomSlow";
        Transform existing = target.Find(auraName);
        if (existing != null && existing.TryGetComponent(out SkillVfxRunner existingRunner))
        {
            existingRunner.Restart(duration, duration + 1f);
            return;
        }

        GameObject root = CreateRoot(auraName, target, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.stopEmittingAt = duration;
        runner.lifetime = duration + 1f;

        // Poisoned glow on the body
        ParticleSystem glow = Particles(root.transform, "Glow", Vector3.up * 1f, "circle_05", false);
        var glowMain = glow.main;
        glowMain.loop = true;
        glowMain.duration = 1f;
        glowMain.startLifetime = 0.8f;
        glowMain.startSpeed = 0f;
        glowMain.startSize = 3.1f;
        glowMain.startColor = WithAlpha(Venom, 0.25f);
        glowMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var glowEmission = glow.emission;
        glowEmission.rateOverTime = 3f;
        SizeCurve(glow, 0.85f, 1.1f);
        SetFadeOut(glow);

        // Poison bubbles rising around the upper body
        ParticleSystem bubbles = Particles(root.transform, "Bubbles", Vector3.up * 1.2f, "circle_05", false);
        var bubbleMain = bubbles.main;
        bubbleMain.loop = true;
        bubbleMain.duration = 1f;
        bubbleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        bubbleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        bubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        bubbleMain.startColor = new ParticleSystem.MinMaxGradient(Venom, Color.white);
        SetShapeCircle(bubbles, 0.4f, Vector3.right * -90f, 15f);
        var bubbleEmission = bubbles.emission;
        bubbleEmission.rateOverTime = 12f;
        SetFadeOut(bubbles);

        ParticleSystem drip = Particles(root.transform, "Drip", Vector3.up * 1.3f, "circle_05", false);
        var main = drip.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(Venom, VenomDeep);
        main.gravityModifier = 0.5f;
        SetShapeCircle(drip, 0.35f, Vector3.right * -90f, 0f);
        var emission = drip.emission;
        emission.rateOverTime = 22f;
        SetFadeOut(drip);

        ParticleSystem mist = Particles(root.transform, "Smoke", Vector3.up * 1f, "smoke_04", true);
        var mistMain = mist.main;
        mistMain.loop = true;
        mistMain.duration = 1f;
        mistMain.startLifetime = 1.2f;
        mistMain.startSpeed = 0.35f;
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mistMain.startColor = WithAlpha(VenomDeep, 0.4f);
        SetShapeCircle(mist, 0.3f, Vector3.right * -90f, 10f);
        var mistEmission = mist.emission;
        mistEmission.rateOverTime = 5f;
        SizeCurve(mist, 0.8f, 1.4f);
        SetFadeOut(mist);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.8f, Venom, 2f), 0.7f, 0.2f, 0.5f);
        runner.Play();
    }
    #endregion

    #region Crow's Wingbeat (shield)
    /// <summary> Activation: a flock of crows bursts out of the caster in every direction, feathers, wing sweep and shockwave. </summary>
    public static void SpawnCrowShieldBurst(Transform caster)
    {
        GameObject root = CreateRoot("VFX_CrowBurst", caster, Vector3.zero);
        root.transform.localRotation = Quaternion.identity; // Face with caster
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2.4f;
        runner.stopEmittingAt = 0.25f;

        SpawnCrowFlock(caster.position + Vector3.up * 1.1f, 10, 5f, 8f);
        FeatherBurst(root.transform, Vector3.up * 1.1f, CrowDark, CrowGlow, 45, 18, 3f, 7f);

        // Wing sweeps (twirl) spinning around the body
        for (int i = 0; i < 2; i++)
        {
            ParticleSystem wing = Particles(root.transform, "Wing", Vector3.up * 1.1f, "twirl_01", false);
            var main = wing.main;
            main.duration = 0.1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 3.4f;
            main.startRotation = i * Mathf.PI;
            main.startColor = CrowGlow;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            SetBurst(wing, 1);
            SizeCurve(wing, 0.5f, 1.1f);
            Spin(wing, 420f * Mathf.Deg2Rad * (i == 0 ? 1f : -1f));
            SetFadeOut(wing);
        }

        // Wing flaps on both sides
        for (int side = -1; side <= 1; side += 2)
        {
            ParticleSystem flap = Particles(root.transform, "Flap", Vector3.up * 1.3f + Vector3.right * 0.7f * side, "slash_03", false);
            var main = flap.main;
            main.duration = 0.1f;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 2.2f;
            main.startRotation = side > 0 ? 0f : Mathf.PI;
            main.startColor = CrowGlow;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            SetBurst(flap, 1);
            SizeCurve(flap, 0.6f, 1.2f);
            SetFadeOut(flap);
        }

        SpawnGroundRing(root.transform, CrowGlow, 0.5f, 4.5f, 0.6f, 0f);

        // Dark smoke at feet
        ParticleSystem smoke = Particles(root.transform, "Smoke", Vector3.up * 0.2f, "smoke_08", true);
        var smokeMain = smoke.main;
        smokeMain.duration = 0.2f;
        smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        smokeMain.startColor = WithAlpha(CrowDark, 0.55f);
        SetShapeCircle(smoke, 0.6f, Vector3.right * -90f, 70f);
        SetBurst(smoke, 9);
        SizeCurve(smoke, 0.8f, 1.8f);
        SetFadeOut(smoke);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.2f, CrowGlow, 5f), 4f, 0.05f, 0.8f);
        runner.Play();
    }

    /// <summary> Shield while active: faint silver bubble, orbiting dark feathers and glints (crows circle separately). StopSoon to end. </summary>
    public static SkillVfxRunner SpawnCrowShieldAura(Transform caster, float duration)
    {
        GameObject root = CreateRoot("VFX_CrowShield", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.stopEmittingAt = duration;
        runner.lifetime = duration + 1f;

        ParticleSystem bubble = Particles(root.transform, "Bubble", Vector3.up * 1f, "light_03", false);
        var bubbleMain = bubble.main;
        bubbleMain.loop = true;
        bubbleMain.duration = 1f;
        bubbleMain.startLifetime = 1f;
        bubbleMain.startSpeed = 0f;
        bubbleMain.startSize = 2.5f;
        bubbleMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        bubbleMain.startColor = WithAlpha(CrowGlow, 0.1f);
        bubbleMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var bubbleEmission = bubble.emission;
        bubbleEmission.rateOverTime = 2.2f;
        SizeCurve(bubble, 0.95f, 1.05f);
        SetFadeOut(bubble);

        ParticleSystem rim = Particles(root.transform, "Rim", Vector3.up * 1f, "circle_03", false);
        var rimMain = rim.main;
        rimMain.loop = true;
        rimMain.duration = 1f;
        rimMain.startLifetime = 1.2f;
        rimMain.startSpeed = 0f;
        rimMain.startSize = 2.4f;
        rimMain.startColor = WithAlpha(CrowGlow, 0.16f);
        rimMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var rimEmission = rim.emission;
        rimEmission.rateOverTime = 1.5f;
        SetFadeOut(rim);

        // Orbiting dark feathers
        ParticleSystem feathers = Particles(root.transform, "Feathers", Vector3.up * 0.3f, "flame_06", true);
        var featherMain = feathers.main;
        featherMain.loop = true;
        featherMain.duration = 1f;
        featherMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.4f);
        featherMain.startSpeed = 0f;
        featherMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        featherMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        featherMain.startColor = WithAlpha(CrowDark, 0.95f);
        featherMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(feathers, 0.8f, Vector3.right * -90f, 0f);
        var featherShape = feathers.shape;
        featherShape.radiusThickness = 0f;
        var featherEmission = feathers.emission;
        featherEmission.rateOverTime = 11f;
        var velocity = feathers.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.6f);
        Spin(feathers, 3f);
        SetFadeOut(feathers);

        ParticleSystem glints = Particles(root.transform, "Glints", Vector3.up * 1f, "star_07", false);
        var glintMain = glints.main;
        glintMain.loop = true;
        glintMain.duration = 1f;
        glintMain.startLifetime = 0.35f;
        glintMain.startSpeed = 0f;
        glintMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        glintMain.startColor = CrowGlow;
        glintMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        Sphere(glints, 0.95f);
        var glintEmission = glints.emission;
        glintEmission.rateOverTime = 4f;
        SetFadeOut(glints);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1f, CrowGlow, 2.5f), 1.2f, 0.2f, 0.6f);
        runner.Play();
        return runner;
    }

    /// <summary> Shield took a hit: silver ripple and a few feathers knocked loose. </summary>
    public static void SpawnCrowShieldHit(Transform caster)
    {
        GameObject root = CreateRoot("VFX_CrowHit", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.2f;
        runner.stopEmittingAt = 0.1f;

        FeatherBurst(root.transform, Vector3.up * 1.1f, CrowDark, CrowGlow, 14, 7, 4f, 7f);

        // Fast outward streaks distinguish a deflection from the slow shield aura.
        ParticleSystem sparks = Particles(root.transform, "DeflectStreaks", Vector3.up * 1.1f, "trace_07", false);
        var sparkMain = sparks.main;
        sparkMain.duration = .05f;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .24f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(.08f, .16f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(CrowGlow, Color.white);
        Sphere(sparks, .65f);
        SetBurst(sparks, 16);
        SetFadeOut(sparks);
        var streakRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        streakRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        streakRenderer.velocityScale = .045f;
        streakRenderer.lengthScale = 2.5f;

        ParticleSystem ripple = Particles(root.transform, "Ripple", Vector3.up * 1.1f, "circle_03", false);
        var main = ripple.main;
        main.duration = 0.1f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 2.8f;
        main.startColor = WithAlpha(CrowGlow, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(ripple, 1);
        SizeCurve(ripple, 0.55f, 1.4f);
        SetFadeOut(ripple);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.1f, CrowGlow, 3f), 1.5f, 0.02f, 0.25f);
        runner.Play();
    }

    /// <summary> Shield depleted: feathers scatter with a silver flash, a few crows break away. </summary>
    public static void SpawnCrowShieldBreak(Transform caster)
    {
        CombatSkillAccent.Burst(caster.position + Vector3.up, CrowGlow, 1.3f);
        GameObject root = CreateRoot("VFX_CrowBreak", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.8f;
        runner.stopEmittingAt = 0.2f;

        SpawnCrowFlock(caster.position + Vector3.up * 1.1f, 4, 5f, 7f);
        FeatherBurst(root.transform, Vector3.up * 1f, CrowDark, CrowGlow, 30, 12, 4f, 8f);

        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.up * 1f, "circle_03", false);
        var main = flash.main;
        main.duration = 0.1f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 3f;
        main.startColor = CrowGlow;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.7f, 1.3f);
        SetFadeOut(flash);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1f, CrowGlow, 4f), 3f, 0.02f, 0.4f);
        runner.Play();
    }

    // Crows flying off from one point in every direction, mostly outward and upward
    private static void SpawnCrowFlock(Vector3 origin, int count, float minSpeed, float maxSpeed)
    {
        for (int i = 0; i < count; i++)
        {
            float yaw = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(yaw), Random.Range(0.15f, 0.8f), Mathf.Sin(yaw)).normalized;
            CrowFlyer.Fly(origin + dir * 0.3f, dir * Random.Range(minSpeed, maxSpeed), Random.Range(0.7f, 1f), Random.Range(1.1f, 1.6f));
        }
    }
    #endregion

    #region Hawk's Ambush (game changer)
    /// <summary> Activation: golden feathers swirl up, shockwave and eye flash. </summary>
    public static void SpawnHawkActivate(Transform caster)
    {
        GameObject root = CreateRoot("VFX_HawkActivate", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2f;
        runner.stopEmittingAt = 0.25f;

        FeatherBurst(root.transform, Vector3.up * 0.6f, HawkBrown, HawkAmber, 26, 14, 2f, 5f, -0.4f);
        SpawnGroundRing(root.transform, HawkAmber, 0.5f, 5f, 0.6f, 0f);

        ParticleSystem flash = Particles(root.transform, "EyeFlash", Vector3.up * 1.65f, "star_08", false);
        var main = flash.main;
        main.duration = 0.1f;
        main.startLifetime = 0.4f;
        main.startSpeed = 0f;
        main.startSize = 1.6f;
        main.startColor = HawkAmber;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.4f, 1.2f);
        Spin(flash, 2f);
        SetFadeOut(flash);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.5f, HawkAmber, 5f), 4f, 0.05f, 0.7f);
        runner.Play();
    }

    /// <summary> Looping while waiting to strike: rotating rune at feet, rising golden feathers, eye gleam. </summary>
    public static SkillVfxRunner SpawnHawkReady(Transform caster)
    {
        GameObject root = CreateRoot("VFX_HawkReady", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 9999f;

        Rune(root.transform, Vector3.up * 0.06f, WithAlpha(HawkAmber, 0.8f), 2.6f, 1.2f, 0, 50f, 1.5f);

        ParticleSystem feathers = Particles(root.transform, "Feathers", Vector3.up * 0.1f, "flame_05", false);
        var main = feathers.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(HawkAmber, Color.white);
        SetShapeCircle(feathers, 0.8f, Vector3.right * -90f, 5f);
        var emission = feathers.emission;
        emission.rateOverTime = 8f;
        Spin(feathers, 2f);
        SetFadeOut(feathers);

        ParticleSystem eye = Particles(root.transform, "Eye", Vector3.up * 1.65f, "flare_01", false);
        var eyeMain = eye.main;
        eyeMain.loop = true;
        eyeMain.duration = 1f;
        eyeMain.startLifetime = 0.6f;
        eyeMain.startSpeed = 0f;
        eyeMain.startSize = 1.1f;
        eyeMain.startColor = WithAlpha(HawkAmber, 0.8f);
        eyeMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var eyeEmission = eye.emission;
        eyeEmission.rateOverTime = 1.8f;
        SetFadeOut(eye);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1f, HawkAmber, 3f), 1.5f, 0.3f, 0.5f);
        runner.Play();
        return runner;
    }

    /// <summary> Hawk hits an enemy: flash, shockwave, sparks and feathers. </summary>
    public static void SpawnHawkImpact(Vector3 hitPoint)
    {
        CombatCameraFeedback.Explosion(hitPoint, 8f);
        CombatSkillAccent.Burst(hitPoint, HawkAmber, 1.2f);
        GameObject root = CreateRoot("VFX_HawkImpact", null, hitPoint);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.6f;
        runner.stopEmittingAt = 0.3f;

        // Impact flash
        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.zero, "star_08", false);
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.startDelay = 0f;
        flashMain.startLifetime = 0.3f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 2.4f;
        flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        flashMain.startColor = HawkAmber;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.5f, 1.2f);
        SetFadeOut(flash);

        ParticleSystem shock = Particles(root.transform, "Shock", Vector3.zero, "circle_03", false);
        var shockMain = shock.main;
        shockMain.duration = 0.1f;
        shockMain.startDelay = 0f;
        shockMain.startLifetime = 0.4f;
        shockMain.startSpeed = 0f;
        shockMain.startSize = 3.2f;
        shockMain.startColor = HawkAmber;
        SetBurst(shock, 1);
        SizeCurve(shock, 0.15f, 1f);
        SetFadeOut(shock);

        // Sparks shooting out
        ParticleSystem sparks = Particles(root.transform, "Sparks", Vector3.zero, "trace_07", false);
        var sparkMain = sparks.main;
        sparkMain.duration = 0.1f;
        sparkMain.startDelay = 0f;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(HawkAmber, Color.white);
        Sphere(sparks, 0.1f);
        SetBurst(sparks, 22);
        SetFadeOut(sparks);
        ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.velocityScale = 0.05f;
        sparkRenderer.lengthScale = 2f;

        FeatherBurst(root.transform, Vector3.zero, HawkBrown, HawkAmber, 22, 10, 3f, 6f, 0.3f);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.5f, HawkAmber, 5f), 6f, 0.06f, 0.4f);
        runner.Play();
    }

    /// <summary> Dazzling sun glint when a hawk reaches the top of its flight. </summary>
    public static void SpawnHawkGlint(Vector3 position, float scale = 1f)
    {
        GameObject root = CreateRoot("VFX_HawkGlint", null, position);
        root.transform.localScale = Vector3.one * scale;
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.4f;
        runner.stopEmittingAt = 0.15f;

        // Big star flash
        ParticleSystem star = Particles(root.transform, "Star", Vector3.zero, "star_08", false);
        var starMain = star.main;
        starMain.duration = 0.1f;
        starMain.startLifetime = 0.45f;
        starMain.startSpeed = 0f;
        starMain.startSize = 6f;
        starMain.startColor = Color.white;
        SetBurst(star, 1);
        SizeCurve(star, 0.2f, 1.1f);
        Spin(star, 1.5f);
        SetFadeOut(star);

        // Horizontal lens flare streak
        ParticleSystem streak = Particles(root.transform, "Streak", Vector3.zero, "flare_01", false);
        var streakMain = streak.main;
        streakMain.duration = 0.1f;
        streakMain.startLifetime = 0.5f;
        streakMain.startSpeed = 0f;
        streakMain.startSize = 12f;
        streakMain.startColor = new Color(1f, 0.9f, 0.7f, 1f);
        SetBurst(streak, 1);
        SizeCurve(streak, 0.4f, 1.2f);
        SetFadeOut(streak);

        // Warm glow + expanding ring
        ParticleSystem glow = Particles(root.transform, "Glow", Vector3.zero, "circle_05", false);
        var glowMain = glow.main;
        glowMain.duration = 0.1f;
        glowMain.startLifetime = 0.6f;
        glowMain.startSpeed = 0f;
        glowMain.startSize = 5f;
        glowMain.startColor = WithAlpha(HawkAmber, 0.7f);
        SetBurst(glow, 1);
        SizeCurve(glow, 0.6f, 1.3f);
        SetFadeOut(glow);

        ParticleSystem ring = Particles(root.transform, "Ring", Vector3.zero, "circle_03", false);
        var ringMain = ring.main;
        ringMain.duration = 0.1f;
        ringMain.startLifetime = 0.5f;
        ringMain.startSpeed = 0f;
        ringMain.startSize = 6f;
        ringMain.startColor = new Color(1f, 0.95f, 0.8f, 0.9f);
        SetBurst(ring, 1);
        SizeCurve(ring, 0.1f, 1f);
        SetFadeOut(ring);

        // Glitter falling from the flash
        ParticleSystem glitter = Particles(root.transform, "Glitter", Vector3.zero, "star_07", false);
        var glitterMain = glitter.main;
        glitterMain.duration = 0.1f;
        glitterMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        glitterMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        glitterMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        glitterMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, HawkAmber);
        glitterMain.gravityModifier = 0.25f;
        Sphere(glitter, 0.3f);
        SetBurst(glitter, 28);
        SetFadeOut(glitter);

        runner.AddLight(CreateLight(root.transform, Vector3.zero, new Color(1f, 0.9f, 0.7f), 14f), 10f, 0.03f, 0.6f);
        runner.Play();
    }

    /// <summary> Quick golden talon streak from the landing hawk to a target. </summary>
    public static void SpawnTalonStreak(Vector3 from, Vector3 to)
    {
        GameObject root = CreateRoot("VFX_TalonStreak", null, from);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 0.45f;
        LineRenderer line = CreateBeamRenderer(root, WithAlpha(Color.white, 0.9f), HawkAmber);
        line.useWorldSpace = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        runner.AddBeam(line, 0.35f, 0.02f, 0.05f, 0.3f);
        runner.Play();
    }

    /// <summary> Giant hawk lands: ground shockwave, flash, feather storm. </summary>
    public static void SpawnHawkLanding(Vector3 position, float scale = 1f)
    {
        GameObject root = CreateRoot("VFX_HawkLanding", null, position);
        root.transform.localScale = Vector3.one * scale;
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2.2f;
        runner.stopEmittingAt = 0.3f;

        SpawnGroundRing(root.transform, HawkAmber, 1f, 14f, 0.8f, 0f);
        SpawnGroundRing(root.transform, Color.white, 0.5f, 8f, 0.5f, 0.05f);

        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.up * 1f, "star_08", false);
        var main = flash.main;
        main.duration = 0.1f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 7f;
        main.startColor = HawkAmber;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.3f, 1.2f);
        SetFadeOut(flash);

        FeatherBurst(root.transform, Vector3.up * 1f, HawkBrown, HawkAmber, 50, 30, 5f, 11f, 0.4f);

        ParticleSystem dust = Particles(root.transform, "Dust", Vector3.up * 0.2f, "smoke_07", true);
        var dustMain = dust.main;
        dustMain.duration = 0.2f;
        dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        dustMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        dustMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        dustMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dustMain.startColor = new Color(0.55f, 0.48f, 0.38f, 0.5f);
        SetShapeCircle(dust, 1f, Vector3.right * -90f, 85f);
        SetBurst(dust, 18);
        var limit = dust.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.15f;
        SizeCurve(dust, 0.6f, 1.6f);
        SetFadeOut(dust);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 2f, HawkAmber, 16f), 10f, 0.03f, 0.8f);
        runner.Play();
    }

    /// <summary> Wind swirling around the shooter while the ambush charges. StopSoon to end. </summary>
    public static SkillVfxRunner SpawnHawkWindSwirl(Transform caster)
    {
        GameObject root = CreateRoot("VFX_HawkWind", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 9999f;

        // Streaks orbiting the body
        ParticleSystem streaks = Particles(root.transform, "Streaks", Vector3.up * 0.3f, "trace_01", false);
        var main = streaks.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(streaks, 2.2f, Vector3.right * -90f, 0f);
        var shape = streaks.shape;
        shape.radiusThickness = 0.2f;
        var emission = streaks.emission;
        emission.rateOverTime = 40f;
        var velocity = streaks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(4.5f);
        SetFadeOut(streaks);
        ParticleSystemRenderer r = streaks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.25f;
        r.lengthScale = 1.5f;

        // Dust kicked up at the feet
        ParticleSystem dust = Particles(root.transform, "Dust", Vector3.up * 0.1f, "smoke_07", true);
        var dustMain = dust.main;
        dustMain.loop = true;
        dustMain.duration = 1f;
        dustMain.startLifetime = 1f;
        dustMain.startSpeed = 0f;
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        dustMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dustMain.startColor = new Color(0.6f, 0.55f, 0.45f, 0.25f);
        dustMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(dust, 1.8f, Vector3.right * -90f, 0f);
        var dustEmission = dust.emission;
        dustEmission.rateOverTime = 8f;
        var dustVel = dust.velocityOverLifetime;
        dustVel.enabled = true;
        dustVel.space = ParticleSystemSimulationSpace.Local;
        dustVel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        dustVel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        dustVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        dustVel.orbitalY = new ParticleSystem.MinMaxCurve(3f);
        SizeCurve(dust, 0.7f, 1.5f);
        SetFadeOut(dust);

        // Loose feathers riding the swirl
        ParticleSystem feathers = Particles(root.transform, "Feathers", Vector3.up * 0.5f, "flame_05", false);
        var fMain = feathers.main;
        fMain.loop = true;
        fMain.duration = 1f;
        fMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        fMain.startSpeed = 0f;
        fMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        fMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fMain.startColor = new ParticleSystem.MinMaxGradient(HawkAmber, Color.white);
        fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(feathers, 2f, Vector3.right * -90f, 0f);
        var fEmission = feathers.emission;
        fEmission.rateOverTime = 6f;
        var fVel = feathers.velocityOverLifetime;
        fVel.enabled = true;
        fVel.space = ParticleSystemSimulationSpace.Local;
        fVel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        fVel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        fVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        fVel.orbitalY = new ParticleSystem.MinMaxCurve(4f);
        Spin(feathers, 4f);
        SetFadeOut(feathers);

        runner.Play();
        return runner;
    }

    /// <summary> Strong gusts rushing forward on both sides of the shooter when the hawks are released. </summary>
    public static void SpawnHawkWindGust(Transform caster, Vector3 forward)
    {
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : caster.forward;

        GameObject root = CreateRoot("VFX_HawkGust", null, caster.position);
        root.transform.rotation = Quaternion.LookRotation(forward);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.6f;
        runner.stopEmittingAt = 0.35f;

        for (int side = -1; side <= 1; side += 2)
        {
            // Air streaks starting a bit behind, rushing forward past the shooter
            ParticleSystem streaks = Particles(root.transform, side < 0 ? "GustL" : "GustR", new Vector3(side * 1.8f, 1.1f, -2f), "trace_01", false);
            var main = streaks.main;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.55f);
            var shape = streaks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 2f, 0.5f);
            var emission = streaks.emission;
            emission.rateOverTime = 160f;
            SetFadeOut(streaks);
            ParticleSystemRenderer r = streaks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.08f;
            r.lengthScale = 2f;

            // Dust blown along the ground on that side
            ParticleSystem dust = Particles(root.transform, "Dust", new Vector3(side * 1.6f, 0.2f, -0.5f), "smoke_07", true);
            var dustMain = dust.main;
            dustMain.duration = 0.2f;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            dustMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            dustMain.startColor = new Color(0.6f, 0.55f, 0.45f, 0.35f);
            var dustShape = dust.shape;
            dustShape.enabled = true;
            dustShape.shapeType = ParticleSystemShapeType.Cone;
            dustShape.angle = 12f;
            dustShape.radius = 0.4f;
            dustShape.rotation = new Vector3(0f, side * 6f, 0f);
            SetBurst(dust, 8);
            var limit = dust.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1f;
            limit.dampen = 0.1f;
            SizeCurve(dust, 0.6f, 1.8f);
            SetFadeOut(dust);

            // Feathers carried by the gust
            FeatherBurst(root.transform, new Vector3(side * 1.5f, 1.2f, 0f), HawkBrown, HawkAmber, 10, 6, 6f, 12f, 0.1f);
        }

        runner.Play();
    }

    /// <summary> Explosion on a target hit by a hawk: flash, fireball, shockwave, sparks, debris and smoke. </summary>
    public static void SpawnHawkExplosion(Vector3 point)
    {
        CombatCameraFeedback.GroundSlam(point, 24f, 2.3f, 1.15f, HawkAmber);
        GameObject root = CreateRoot("VFX_HawkExplosion", null, point);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2.2f;
        runner.stopEmittingAt = 0.2f;

        // Blinding core flash
        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.zero, "star_08", false);
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.startLifetime = 0.22f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 4.5f;
        flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        flashMain.startColor = Color.white;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.4f, 1.1f);
        SetFadeOut(flash);

        // Fireball: glowing puffs expanding
        ParticleSystem fire = Particles(root.transform, "Fireball", Vector3.zero, "smoke_04", false);
        var fireMain = fire.main;
        fireMain.duration = 0.1f;
        fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        fireMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fireMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.85f, 0.4f, 1f));
        Sphere(fire, 0.3f);
        SetBurst(fire, 14);
        var fireLimit = fire.limitVelocityOverLifetime;
        fireLimit.enabled = true;
        fireLimit.limit = 0.3f;
        fireLimit.dampen = 0.2f;
        SizeCurve(fire, 0.5f, 1.4f);
        SetFadeOut(fire);

        // Shockwave ring (camera-facing) + ground ring
        ParticleSystem shock = Particles(root.transform, "Shock", Vector3.zero, "circle_03", false);
        var shockMain = shock.main;
        shockMain.duration = 0.1f;
        shockMain.startLifetime = 0.65f;
        shockMain.startSpeed = 0f;
        shockMain.startSize = 8f;
        shockMain.startColor = new Color(1f, 0.8f, 0.45f, 0.9f);
        SetBurst(shock, 1);
        SizeCurve(shock, 0.1f, 1f);
        SetFadeOut(shock);
        SpawnGroundRing(root.transform, HawkAmber, 0.5f, 8f, 0.8f, 0f);

        // Sparks flying out
        ParticleSystem sparks = Particles(root.transform, "Sparks", Vector3.zero, "trace_07", false);
        var sparkMain = sparks.main;
        sparkMain.duration = 0.1f;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(HawkAmber, Color.white);
        sparkMain.gravityModifier = 0.8f;
        Sphere(sparks, 0.2f);
        SetBurst(sparks, 36);
        SetFadeOut(sparks);
        ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.velocityScale = 0.05f;
        sparkRenderer.lengthScale = 2f;

        // Debris chunks
        ParticleSystem debris = Particles(root.transform, "Debris", Vector3.zero, "dirt_02", true);
        var debrisMain = debris.main;
        debrisMain.duration = 0.1f;
        debrisMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        debrisMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        debrisMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        debrisMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        debrisMain.startColor = new Color(0.25f, 0.2f, 0.15f, 1f);
        debrisMain.gravityModifier = 1.5f;
        Sphere(debris, 0.2f);
        SetBurst(debris, 16);
        SetFadeOut(debris);

        // Dark smoke lingering after the blast
        ParticleSystem smoke = Particles(root.transform, "Smoke", Vector3.zero, "smoke_08", true);
        var smokeMain = smoke.main;
        smokeMain.duration = 0.1f;
        smokeMain.startDelay = 0.08f;
        smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        smokeMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        smokeMain.startColor = new Color(0.12f, 0.1f, 0.09f, 0.6f);
        smokeMain.gravityModifier = -0.1f;
        Sphere(smoke, 0.5f);
        SetBurst(smoke, 10);
        var smokeLimit = smoke.limitVelocityOverLifetime;
        smokeLimit.enabled = true;
        smokeLimit.limit = 0.4f;
        smokeLimit.dampen = 0.1f;
        SizeCurve(smoke, 0.6f, 1.6f);
        SetFadeOut(smoke);

        FeatherBurst(root.transform, Vector3.zero, HawkBrown, HawkAmber, 14, 8, 3f, 7f, 0.4f);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 0.5f, new Color(1f, 0.7f, 0.35f), 8f), 9f, 0.02f, 0.5f);
        runner.Play();
    }

    /// <summary> Muzzle burst when hawks are released. </summary>
    public static void SpawnHawkLaunch(Vector3 position)
    {
        GameObject root = CreateRoot("VFX_HawkLaunch", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.4f;
        runner.stopEmittingAt = 0.2f;

        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.zero, "star_08", false);
        var main = flash.main;
        main.duration = 0.1f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 1.8f;
        main.startColor = HawkAmber;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.4f, 1.2f);
        SetFadeOut(flash);

        FeatherBurst(root.transform, Vector3.zero, HawkBrown, HawkAmber, 16, 10, 2f, 5f, 0.3f);
        runner.AddLight(CreateLight(root.transform, Vector3.zero, HawkAmber, 5f), 5f, 0.02f, 0.3f);
        runner.Play();
    }

    /// <summary> Ambush expired without striking: feathers drift away. </summary>
    public static void SpawnHawkExpire(Transform caster)
    {
        GameObject root = CreateRoot("VFX_HawkExpire", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2f;
        runner.stopEmittingAt = 0.2f;
        FeatherBurst(root.transform, Vector3.up * 1f, HawkBrown, HawkAmber, 14, 8, 0.5f, 1.5f, -0.1f);
        runner.Play();
    }
    #endregion

    #region Building Blocks
    // Particle with a Kenney texture, additive or alpha blended
    private static ParticleSystem Particles(Transform parent, string name, Vector3 localPosition, string texture, bool alphaBlend)
    {
        Texture2D tex = Kenney(texture);
        ParticleSystem ps = CreateParticles(parent, name, localPosition, tex);
        if (alphaBlend)
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetAlphaMaterial(tex);
        return ps;
    }

    private static void Sphere(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
    }

    private static void SizeCurve(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }

    private static void Spin(ParticleSystem ps, float radiansPerSecond)
    {
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(radiansPerSecond * 0.6f, radiansPerSecond);
    }

    // Rotating magic circle lying on the ground
    private static void Rune(Transform parent, Vector3 localPosition, Color color, float size, float lifetime, int burst, float degreesPerSecond, float rate = 0f)
    {
        ParticleSystem rune = Particles(parent, "Rune", localPosition, "magic_02", false);
        var main = rune.main;
        main.loop = rate > 0f;
        main.duration = rate > 0f ? 1f : 0.1f;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = rune.emission;
        emission.rateOverTime = rate;
        if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
        var rotation = rune.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = degreesPerSecond * Mathf.Deg2Rad;
        SizeCurve(rune, 0.85f, 1.05f);
        SetFadeOut(rune);
        rune.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    // Feathers: dark (alpha) + glowing (additive), exploding with a little drift
    private static void FeatherBurst(Transform parent, Vector3 localPosition, Color dark, Color glow, int darkCount, int glowCount,
        float minSpeed, float maxSpeed, float gravity = 0.35f, float delay = 0f)
    {
        for (int layer = 0; layer < 2; layer++)
        {
            bool isDark = layer == 0;
            ParticleSystem feathers = Particles(parent, isDark ? "FeathersDark" : "FeathersGlow", localPosition, isDark ? "flame_05" : "flame_06", isDark);
            var main = feathers.main;
            main.duration = 0.1f;
            main.startDelay = delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(isDark ? 0.18f : 0.12f, isDark ? 0.35f : 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = isDark ? dark : glow;
            main.gravityModifier = gravity;
            Sphere(feathers, 0.35f);
            SetBurst(feathers, isDark ? darkCount : glowCount);
            var limit = feathers.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.12f;
            Spin(feathers, 4f);
            SetFadeOut(feathers);
        }
    }

    private static void MuzzleFlash(Transform muzzle, Color color)
    {
        GameObject root = CreateRoot("VFX_MuzzleFlash", muzzle, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 0.6f;
        runner.stopEmittingAt = 0.1f;

        ParticleSystem flash = Particles(root.transform, "Flash", Vector3.zero, "star_08", false);
        var main = flash.main;
        main.duration = 0.1f;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = 0.8f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetBurst(flash, 1);
        SizeCurve(flash, 0.5f, 1.2f);
        SetFadeOut(flash);
        runner.Play();
    }
    #endregion
}
