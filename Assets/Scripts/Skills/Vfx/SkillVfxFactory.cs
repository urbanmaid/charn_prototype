using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedural skill effects (no texture/prefab asset needed).
/// Textures and materials are generated once at runtime and shared.
/// </summary>
public static class SkillVfxFactory
{
    public static readonly Color ReviveGold = new Color(1f, 0.82f, 0.38f, 1f);
    public static readonly Color ReviveCore = new Color(1f, 0.97f, 0.85f, 1f);
    public static readonly Color BoostBlue = new Color(0.35f, 0.8f, 1f, 1f);
    public static readonly Color HealGreen = new Color(0.45f, 1f, 0.55f, 1f);
    public static readonly Color BarrierGold = new Color(1f, 0.86f, 0.45f, 1f);

    private static Texture2D softDotTexture, ringTexture, beamTexture;
    private static readonly Dictionary<Texture2D, Material> additiveMaterials = new();

    #region Public Effects
    /// <summary>
    /// Mercy-like resurrection: light column falls on the target, golden motes rise, rings spread and a halo floats above head.
    /// </summary>
    public static GameObject SpawnReviveColumn(Transform target, float columnHeight = 14f)
    {
        CombatSkillAccent.Burst(target.position + Vector3.up, ReviveGold, 1.4f);
        GameObject root = CreateRoot("VFX_Revive", target, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2.6f;
        runner.stopEmittingAt = 1.1f;

        // Light column (outer soft glow + inner core)
        runner.AddBeam(CreateLocalBeam(root.transform, "Beam_Outer", columnHeight, WithAlpha(ReviveGold, 0.55f), WithAlpha(ReviveGold, 0f)), 1.6f, 0.12f, 0.55f, 0.6f);
        runner.AddBeam(CreateLocalBeam(root.transform, "Beam_Core", columnHeight, ReviveCore, WithAlpha(ReviveCore, 0f)), 0.45f, 0.08f, 0.6f, 0.5f);

        // Rising golden motes
        ParticleSystem motes = CreateParticles(root.transform, "Motes", Vector3.zero, SoftDot);
        var main = motes.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(ReviveGold, ReviveCore);
        main.gravityModifier = -0.15f;
        SetShapeCircle(motes, 0.8f, Vector3.right * -90f);
        SetEmission(motes, 45f, 60);
        SetFadeOut(motes);

        // Ground rings
        SpawnGroundRing(root.transform, ReviveGold, 0.6f, 5f, 0.9f, 0f);
        SpawnGroundRing(root.transform, ReviveCore, 0.3f, 3.2f, 0.7f, 0.25f);

        // Halo above head
        ParticleSystem halo = CreateParticles(root.transform, "Halo", Vector3.up * 2.15f, Ring);
        var haloMain = halo.main;
        haloMain.duration = 0.1f;
        haloMain.startLifetime = 1.8f;
        haloMain.startSpeed = 0f;
        haloMain.startSize = 0.75f;
        haloMain.startColor = ReviveGold;
        SetBurst(halo, 1);
        SetFadeOut(halo);
        halo.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.5f, ReviveGold, 6f), 7f, 0.12f, 1.6f);

        runner.Play();
        return root;
    }

    /// <summary>
    /// Revive casting: golden motes converge to the caster, ring shrinks at feet, light grows,
    /// and beams stretch toward the targets during the cast.
    /// </summary>
    public static GameObject SpawnReviveCast(Transform caster, IList<Transform> targets, float duration)
    {
        GameObject root = CreateRoot("VFX_ReviveCast", caster, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.stopEmittingAt = duration;
        runner.lifetime = duration + 0.8f;

        // Converging motes
        ParticleSystem motes = CreateParticles(root.transform, "Converge", Vector3.up * 0.1f, SoftDot);
        var main = motes.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.7f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(ReviveGold, ReviveCore);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(motes, 2.2f, Vector3.right * -90f, 0f);
        var shape = motes.shape;
        shape.radiusThickness = 0f;
        var emission = motes.emission;
        emission.rateOverTime = 70f;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-3f);
        SetFadeOut(motes);

        // Shrinking ring at feet
        ParticleSystem ring = CreateParticles(root.transform, "Ring", Vector3.up * 0.05f, Ring);
        var ringMain = ring.main;
        ringMain.loop = true;
        ringMain.duration = 1f;
        ringMain.startLifetime = 0.6f;
        ringMain.startSpeed = 0f;
        ringMain.startSize = 4f;
        ringMain.startColor = ReviveGold;
        ringMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var ringEmission = ring.emission;
        ringEmission.rateOverTime = 3f;
        var ringSize = ring.sizeOverLifetime;
        ringSize.enabled = true;
        ringSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
        SetFadeOut(ring);
        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        // Beams toward targets
        if (targets != null)
        {
            foreach (Transform target in targets)
            {
                if (target == null) continue;
                GameObject beamObject = new GameObject("Beam");
                beamObject.transform.SetParent(root.transform, false);
                LineRenderer line = CreateBeamRenderer(beamObject, WithAlpha(ReviveCore, 0.9f), WithAlpha(ReviveGold, 0.7f));
                line.useWorldSpace = true;
                runner.AddBeam(line, 0.18f, duration, 0.1f, 0.4f, caster, Vector3.up * 1.6f, target, Vector3.up * 0.5f);
            }
        }

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1.6f, ReviveGold, 4f), 4f, duration, 0.5f);
        runner.Play();
        return root;
    }

    /// <summary>
    /// Burst around caster when mass revive is used. Ring grows to the effective radius.
    /// </summary>
    public static GameObject SpawnReviveCasterBurst(Vector3 position, float radius)
    {
        GameObject root = CreateRoot("VFX_ReviveBurst", null, position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 2f;
        runner.stopEmittingAt = 0.5f;

        SpawnGroundRing(root.transform, ReviveGold, 1f, radius * 2f, 1.1f, 0f);
        SpawnGroundRing(root.transform, ReviveCore, 0.5f, radius * 1.2f, 0.8f, 0.1f);

        ParticleSystem motes = CreateParticles(root.transform, "Motes", Vector3.up * 0.1f, SoftDot);
        var main = motes.main;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(ReviveGold, ReviveCore);
        SetShapeCircle(motes, 1.2f, Vector3.right * -90f, 35f);
        SetBurst(motes, 80);
        SetFadeOut(motes);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 2f, ReviveGold, radius), 5f, 0.08f, 1f);

        runner.Play();
        return root;
    }

    /// <summary>
    /// Small golden pulse for allies healed (not revived) by the same skill.
    /// </summary>
    public static GameObject SpawnHealPulse(Transform target)
    {
        CombatSkillAccent.Burst(target.position + Vector3.up, HealGreen, 0.65f);
        GameObject root = CreateRoot("VFX_HealPulse", target, Vector3.zero);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.6f;
        runner.stopEmittingAt = 0.3f;

        SpawnGroundRing(root.transform, ReviveGold, 0.4f, 2.4f, 0.7f, 0f);

        ParticleSystem motes = CreateParticles(root.transform, "Motes", Vector3.zero, SoftDot);
        var main = motes.main;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = ReviveGold;
        SetShapeCircle(motes, 0.6f, Vector3.right * -90f);
        SetBurst(motes, 25);
        SetFadeOut(motes);

        runner.Play();
        return root;
    }

    /// <summary>
    /// Ana-like boost: quick link beam from caster to target, burst on target and a looping aura during buff.
    /// </summary>
    public static void SpawnBoost(Transform caster, Transform target, float duration, Color color)
    {
        CombatSkillAccent.Burst(target.position + Vector3.up, color, 1.1f);
        // Link beam
        if (caster != null)
        {
            GameObject link = CreateRoot("VFX_BoostLink", null, Vector3.zero);
            SkillVfxRunner linkRunner = link.AddComponent<SkillVfxRunner>();
            linkRunner.lifetime = 0.45f;
            LineRenderer line = CreateBeamRenderer(link, WithAlpha(color, 0.9f), WithAlpha(Color.white, 0.9f));
            line.useWorldSpace = true;
            linkRunner.AddBeam(line, 0.35f, 0.03f, 0.1f, 0.3f, caster, Vector3.up * 1.3f, target, Vector3.up * 1.2f);
            linkRunner.Play();
        }

        // Burst
        GameObject burst = CreateRoot("VFX_BoostBurst", target, Vector3.zero);
        SkillVfxRunner burstRunner = burst.AddComponent<SkillVfxRunner>();
        burstRunner.lifetime = 1.6f;
        burstRunner.stopEmittingAt = 0.3f;

        SpawnGroundRing(burst.transform, color, 0.5f, 4f, 0.6f, 0f);

        ParticleSystem streaks = CreateParticles(burst.transform, "Streaks", Vector3.zero, SoftDot);
        var main = streaks.main;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        SetShapeCircle(streaks, 0.7f, Vector3.right * -90f, 20f);
        SetBurst(streaks, 40);
        SetFadeOut(streaks);
        ParticleSystemRenderer streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
        streakRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        streakRenderer.velocityScale = 0.08f;
        streakRenderer.lengthScale = 2f;

        burstRunner.AddLight(CreateLight(burst.transform, Vector3.up * 1.2f, color, 5f), 5f, 0.05f, 0.8f);
        burstRunner.Play();

        SpawnOrExtendAura("VFX_BoostAura", target, duration, color);
    }

    /// <summary>
    /// Heal zone visual: pulsing ground rings and rising motes in the radius. Follows the zone object and fades when it's destroyed.
    /// </summary>
    public static GameObject SpawnHealZone(Transform zone, float radius, float duration, Color color)
    {
        GameObject root = CreateRoot("VFX_HealZone", null, zone.position);
        SkillVfxRunner runner = root.AddComponent<SkillVfxRunner>();
        runner.stopEmittingAt = duration;
        runner.lifetime = duration + 1.2f;
        runner.Follow(zone, true);

        // Border ring (steady) + pulses from center
        ParticleSystem border = CreateParticles(root.transform, "Border", Vector3.up * 0.05f, Ring);
        var borderMain = border.main;
        borderMain.loop = true;
        borderMain.duration = 1f;
        borderMain.startLifetime = 1.2f;
        borderMain.startSpeed = 0f;
        borderMain.startSize = radius * 2f;
        borderMain.startColor = WithAlpha(color, 0.6f);
        borderMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var borderEmission = border.emission;
        borderEmission.rateOverTime = 2.5f;
        SetFadeOut(border);
        border.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        ParticleSystem pulse = CreateParticles(root.transform, "Pulse", Vector3.up * 0.06f, Ring);
        var pulseMain = pulse.main;
        pulseMain.loop = true;
        pulseMain.duration = 1f;
        pulseMain.startLifetime = 1.4f;
        pulseMain.startSpeed = 0f;
        pulseMain.startSize = radius * 2f;
        pulseMain.startColor = WithAlpha(color, 0.5f);
        pulseMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var pulseEmission = pulse.emission;
        pulseEmission.rateOverTime = 0.8f;
        var pulseSize = pulse.sizeOverLifetime;
        pulseSize.enabled = true;
        pulseSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
        SetFadeOut(pulse);
        pulse.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        // Rising motes in whole area
        ParticleSystem motes = CreateParticles(root.transform, "Motes", Vector3.zero, SoftDot);
        var main = motes.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        SetShapeCircle(motes, radius, Vector3.right * -90f, 0f);
        var emission = motes.emission;
        emission.rateOverTime = Mathf.Clamp(radius * radius * 3f, 15f, 120f);
        SetFadeOut(motes);

        runner.AddLight(CreateLight(root.transform, Vector3.up * 1f, color, radius * 1.5f), 1.5f, 0.3f, 1f);
        runner.Play();
        return root;
    }

    /// <summary>
    /// Barrier granted: stacked rings flash around the body, then a golden aura lasts while barrier is active.
    /// </summary>
    public static void SpawnBarrier(Transform target, float duration, Color color)
    {
        GameObject burst = CreateRoot("VFX_BarrierBurst", target, Vector3.zero);
        SkillVfxRunner runner = burst.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.4f;
        runner.stopEmittingAt = 0.3f;

        // Cage-like rings at feet, waist and head
        float[] heights = { 0.15f, 1.0f, 1.85f };
        for (int i = 0; i < heights.Length; i++)
        {
            ParticleSystem ring = CreateParticles(burst.transform, "CageRing", Vector3.up * heights[i], Ring);
            var main = ring.main;
            main.duration = 0.1f;
            main.startDelay = i * 0.06f;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 1.5f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            SetBurst(ring, 1);
            var size = ring.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.1f));
            SetFadeOut(ring);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        runner.AddLight(CreateLight(burst.transform, Vector3.up * 1f, color, 3f), 3f, 0.05f, 0.6f);
        runner.Play();

        SpawnOrExtendAura("VFX_BarrierAura", target, duration, color);
    }
    #endregion

    #region Aura
    private static void SpawnOrExtendAura(string auraName, Transform target, float duration, Color color)
    {
        // Refresh the aura when buff is reapplied
        Transform existing = target.Find(auraName);
        if (existing != null && existing.TryGetComponent(out SkillVfxRunner existingRunner))
        {
            existingRunner.Restart(duration, duration + 1.2f);
            return;
        }

        GameObject aura = CreateRoot(auraName, target, Vector3.zero);
        SkillVfxRunner runner = aura.AddComponent<SkillVfxRunner>();
        runner.stopEmittingAt = duration;
        runner.lifetime = duration + 1.2f;

        // Orbiting motes around body
        ParticleSystem motes = CreateParticles(aura.transform, "Orbit", Vector3.up * 0.1f, SoftDot);
        var main = motes.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        SetShapeCircle(motes, 0.55f, Vector3.right * -90f);
        var shape = motes.shape;
        shape.radiusThickness = 0f;
        var emission = motes.emission;
        emission.rateOverTime = 28f;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        // x/y/z must share the same curve mode
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(3f);
        SetFadeOut(motes);

        // Pulsing ring at feet
        ParticleSystem pulse = CreateParticles(aura.transform, "Pulse", Vector3.up * 0.05f, Ring);
        var pulseMain = pulse.main;
        pulseMain.loop = true;
        pulseMain.duration = 1f;
        pulseMain.startLifetime = 0.9f;
        pulseMain.startSpeed = 0f;
        pulseMain.startSize = 1.6f;
        pulseMain.startColor = WithAlpha(color, 0.7f);
        pulseMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var pulseEmission = pulse.emission;
        pulseEmission.rateOverTime = 1.4f;
        var size = pulse.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
        SetFadeOut(pulse);
        pulse.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        runner.AddLight(CreateLight(aura.transform, Vector3.up * 1f, color, 2.5f), 1.2f, 0.2f, 0.6f);
        runner.Play();
    }
    #endregion

    #region Building Blocks
    internal static GameObject CreateRoot(string name, Transform parent, Vector3 position)
    {
        GameObject root = new GameObject(name);
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.rotation = Quaternion.identity;
            // Keep world scale 1 even under scaled characters (effects are authored in meters)
            Vector3 ls = parent.lossyScale;
            root.transform.localScale = new Vector3(SafeInverse(ls.x), SafeInverse(ls.y), SafeInverse(ls.z));
        }
        else
        {
            root.transform.position = position;
        }
        return root;
    }

    private static float SafeInverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;

    internal static void SpawnGroundRing(Transform parent, Color color, float startSize, float endSize, float lifetime, float delay)
    {
        ParticleSystem ring = CreateParticles(parent, "Ring", Vector3.up * 0.05f, Ring);
        var main = ring.main;
        main.duration = 0.1f;
        main.startDelay = delay;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = endSize;
        main.startColor = color;
        SetBurst(ring, 1);
        var size = ring.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, startSize / Mathf.Max(0.01f, endSize), 0f, 3f),
            new Keyframe(1f, 1f, 0f, 0f)));
        SetFadeOut(ring);
        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    internal static ParticleSystem CreateParticles(Transform parent, string name, Vector3 localPosition, Texture2D texture)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // Must stop before changing duration
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetAdditiveMaterial(texture);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }

    // Narrow cone lying on the ground disc, emitting upward (rotation -90 on X turns +Z to up)
    internal static void SetShapeCircle(ParticleSystem ps, float radius, Vector3 rotation, float spreadAngle = 8f)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = spreadAngle;
        shape.radius = radius;
        shape.rotation = rotation;
    }

    internal static void SetEmission(ParticleSystem ps, float rate, int burst)
    {
        var emission = ps.emission;
        emission.rateOverTime = rate;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
    }

    internal static void SetBurst(ParticleSystem ps, int count)
    {
        SetEmission(ps, 0f, count);
    }

    internal static void SetFadeOut(ParticleSystem ps)
    {
        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    internal static LineRenderer CreateLocalBeam(Transform parent, string name, float height, Color bottom, Color top)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = CreateBeamRenderer(go, bottom, top);
        line.useWorldSpace = false;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.up * height);
        return line;
    }

    internal static LineRenderer CreateBeamRenderer(GameObject go, Color start, Color end)
    {
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.sharedMaterial = GetAdditiveMaterial(Beam);
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 4;
        line.widthMultiplier = 0f;
        line.startColor = start;
        line.endColor = end;
        return line;
    }

    internal static Light CreateLight(Transform parent, Vector3 localPosition, Color color, float range)
    {
        GameObject go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        return light;
    }

    internal static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
    #endregion

    #region Generated Resources
    internal static Texture2D SoftDot => softDotTexture != null ? softDotTexture : softDotTexture = GenerateRadial(64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));

    // Thin bright ring with soft edges
    internal static Texture2D Ring => ringTexture != null ? ringTexture : ringTexture = GenerateRadial(128, r =>
    {
        float d = Mathf.Abs(r - 0.85f);
        return Mathf.Clamp01(1f - d / 0.12f) * Mathf.Clamp01(1f - d / 0.12f) + Mathf.Clamp01(0.15f - r * 0.15f);
    });

    // Fades across beam width (v), full along length (u)
    private static Texture2D Beam
    {
        get
        {
            if (beamTexture != null) return beamTexture;
            const int h = 64;
            beamTexture = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VFX_Beam" };
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs(y / (float)(h - 1) * 2f - 1f);
                float a = Mathf.Pow(1f - v, 1.8f);
                for (int x = 0; x < 4; x++)
                    beamTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            beamTexture.Apply();
            return beamTexture;
        }
    }

    // CC0 textures from Kenney Particle Pack (Assets/Resources/VFX/Kenney). Falls back to generated dot.
    private static readonly Dictionary<string, Texture2D> kenneyTextures = new();

    internal static Texture2D Kenney(string name)
    {
        if (kenneyTextures.TryGetValue(name, out Texture2D cached) && cached != null) return cached;
        Texture2D tex = Resources.Load<Texture2D>("VFX/Kenney/" + name);
        if (tex == null)
        {
            Debug.LogWarning("[SkillVfx] Missing texture VFX/Kenney/" + name + ", using fallback.");
            tex = SoftDot;
        }
        kenneyTextures[name] = tex;
        return tex;
    }

    private static readonly Dictionary<Texture2D, Material> alphaMaterials = new();

    /// <summary> Alpha blended (not additive) material, for dark particles like feathers and smoke. </summary>
    internal static Material GetAlphaMaterial(Texture2D texture)
    {
        if (alphaMaterials.TryGetValue(texture, out Material cached) && cached != null)
            return cached;

        Material material;
        Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpParticle != null)
        {
            material = new Material(urpParticle);
            material.SetFloat("_Surface", 1f);       // Transparent
            material.SetFloat("_Blend", 0f);         // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
        }
        else
        {
            material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
            material.mainTexture = texture;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        material.name = "VFX_Alpha_" + texture.name;
        alphaMaterials[texture] = material;
        return material;
    }

    private static Texture2D GenerateRadial(int size, System.Func<float, float> alphaByRadius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VFX_Radial" };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(alphaByRadius(r))));
            }
        }
        tex.Apply();
        return tex;
    }

    internal static Material GetAdditiveMaterial(Texture2D texture)
    {
        if (additiveMaterials.TryGetValue(texture, out Material cached) && cached != null)
            return cached;

        Material material;
        Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpParticle != null)
        {
            material = new Material(urpParticle);
            material.SetFloat("_Surface", 1f);       // Transparent
            material.SetFloat("_Blend", 2f);         // Additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
        }
        else
        {
            Shader fallback = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            material = new Material(fallback);
            material.mainTexture = texture;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        material.name = "VFX_Additive_" + texture.name;
        additiveMaterials[texture] = material;
        return material;
    }
    #endregion
}
