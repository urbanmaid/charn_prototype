using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fiona "Crow's Wingbeat": self shield (removed after duration or when depleted) + damage reduction.
/// A flock bursts out on cast and three crows circle the caster while the shield holds; hits on the shield
/// show a silver ripple, and the circling crows scatter when it ends. Added at runtime to the caster.
/// </summary>
public class CrowShield : MonoBehaviour
{
    private const int OrbitCrows = 3;
    private const float OrbitRadius = 1.1f, OrbitSpeed = 2.2f;
    private const float HitInterval = 0.2f;

    public bool IsActive { get; private set; }

    private HealthManager health;
    private float shieldBaseline, duration, elapsed;
    private float lastShield, hitCooldown;
    private SkillVfxRunner aura;
    private AudioSource auraSound;
    private readonly List<CrowFlyer> crows = new();

    public void Begin(float shieldAmount, float shieldDuration, float damageReductionPercent, float damageReductionDuration)
    {
        health = GetComponent<HealthManager>();
        IngameEffectBehavior iFx = GetComponent<IngameEffectBehavior>();
        if (iFx == null)
        {
            iFx = gameObject.AddComponent<IngameEffectBehavior>();
            iFx.RefreshComponentReferences();
        }

        // Recast: previous feather shield ends (effect data refreshes itself by name)
        if (IsActive && aura != null) aura.StopSoon(0.3f);
        ScatterCrows(5f);

        float before = CurrentShield();

        // IESpecialShield gives intensity * 10 shield and takes it back after duration
        IESpecialShield shield = new();
        shield.Initialize("Crow Shield", EffectDetailType.Defense, shieldAmount / 10f, shieldDuration);
        iFx.AddIngameEffect(shield);

        IEDefenseDamageDamp damp = new();
        damp.Initialize("Crow Damage Reduction", EffectDetailType.Defense, damageReductionPercent, damageReductionDuration);
        iFx.AddIngameEffect(damp);

        // Shield from before this cast doesn't count as feather shield
        shieldBaseline = IsActive ? shieldBaseline : before;
        duration = shieldDuration;
        elapsed = 0f;
        hitCooldown = 0f;
        lastShield = CurrentShield();
        IsActive = true;

        FionaVfx.SpawnCrowShieldBurst(transform);
        aura = FionaVfx.SpawnCrowShieldAura(transform, shieldDuration);
        for (int i = 0; i < OrbitCrows; i++)
        {
            float startAngle = i * Mathf.PI * 2f / OrbitCrows;
            crows.Add(CrowFlyer.Orbit(transform, OrbitRadius, startAngle, 0.9f + i * 0.5f, 0.75f, OrbitSpeed));
        }

        SkillSfx.Play("crow_wing", transform.position, 1f);
        SkillSfx.Play("crow_caw", transform.position, 0.8f, 1f, 0f, 0.1f);

        // Soft wingbeats linger around the caster until the shield ends
        SkillSfx.Stop(auraSound, 0.5f);
        auraSound = SkillSfx.PlayLoop("crow_aura", transform, 0.7f, 1.2f);
    }

    private void Update()
    {
        if (!IsActive) return;
        elapsed += Time.deltaTime;
        hitCooldown -= Time.deltaTime;

        if (elapsed >= duration || (health != null && health.dead))
        {
            End(false);
            return;
        }

        float current = CurrentShield();
        if (elapsed > 0.1f && current <= shieldBaseline + 0.01f)
        {
            // All 200 shield consumed
            End(true);
            return;
        }

        // Shield absorbed a hit
        if (current < lastShield - 0.5f && hitCooldown <= 0f)
        {
            hitCooldown = HitInterval;
            FionaVfx.SpawnCrowShieldHit(transform);
            var sound = SkillSfx.Play("crow_hit", transform.position, 1f, 1.16f, 0.04f);
            Camera view = Camera.main;
            var orbit = view ? view.GetComponent<ThirdPersonOrbitCam>() : null;
            if (orbit && orbit.player && health is PlayerHealth &&
                orbit.player.GetComponentInParent<PlayerHealth>() == health)
            {
                CombatCameraFeedback.Get(orbit).ShieldDeflect();
                SkillScreenFx.SkillEdges(FionaVfx.CrowGlow, .24f, .3f, 40f);
                if (sound) { sound.spatialBlend = 0f; sound.priority = 40; }
            }
        }
        lastShield = current;
    }

    private void End(bool broken)
    {
        IsActive = false;
        if (aura != null) aura.StopSoon(broken ? 0.4f : 1f);
        aura = null;
        ScatterCrows(broken ? 7f : 4f);
        SkillSfx.Stop(auraSound, broken ? 0.6f : 1.5f);
        auraSound = null;
        if (broken)
        {
            FionaVfx.SpawnCrowShieldBreak(transform);
            SkillSfx.Play("crow_break", transform.position, 0.9f);
        }
        else
        {
            // Expired: crows just fly off
            SkillSfx.Play("crow_break", transform.position, 0.5f);
        }
    }

    private void ScatterCrows(float speed)
    {
        foreach (CrowFlyer crow in crows)
            if (crow != null) crow.Scatter(speed);
        crows.Clear();
    }

    private void OnDisable()
    {
        ScatterCrows(4f);
        // Owner is going away (can't reparent while being deactivated): just drop the loop
        if (auraSound != null) Destroy(auraSound.gameObject);
        auraSound = null;
    }

    private float CurrentShield()
    {
        if (health is PlayerHealth player) return player.shieldHealth;
        if (health is AllyHealthManager ally) return ally.shieldHealth;
        return 0f;
    }
}
