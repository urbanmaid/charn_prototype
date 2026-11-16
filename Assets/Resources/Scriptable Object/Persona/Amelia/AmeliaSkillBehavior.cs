using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared helpers for Amelia's supply skills.
/// </summary>
public static class AmeliaSupply
{
    /// <summary> Amelia and every living ally within radius (horizontal) of the caster. </summary>
    public static List<HealthManager> AlliesInRange(GameObject caster, float radius)
    {
        List<HealthManager> all = new();
        AllyTargeting.CollectAllies(all);
        List<HealthManager> result = new();
        Vector3 center = caster.transform.position;
        foreach (HealthManager h in all)
        {
            if (h == null || h.dead || !h.isActiveAndEnabled) continue;
            Vector3 d = h.transform.position - center;
            if (Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).magnitude <= radius) result.Add(h);
        }
        HealthManager self = caster.GetComponent<HealthManager>();
        if (self != null && !self.dead && !result.Contains(self)) result.Add(self);
        return result;
    }

    public static GameObject Caster(SkillContext context) =>
        context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
}

/// <summary>
/// Skill 1 - Supply Boost: Amelia and allies within 5m get a fresh magazine at once (no reload time) holding
/// 250% bullets. Back to the normal magazine once it runs dry or they reload. Adds up with other magazine
/// buffs (ex: Lucia's boost +200% -> 450%).
/// </summary>
[System.Serializable]
public class AmeliaSkillExtendMagazine : SkillLogic
{
    [SerializeField, Min(0.5f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float magazineBonusPercent = 150f;   // +150% = 250% bullets

    public override void OnExecute(SkillContext context)
    {
        AmeliaSkillSupplyOverdrive.Execute(context, radius, magazineBonusPercent, 60f);
    }
}

/// <summary>
/// Q - Supply Overdrive: combines Supply Boost and Reinforced Materials into one magazine-bound skill.
/// Amelia and nearby allies receive a 250% magazine whose rounds deal 160% damage until dry or reloaded.
/// </summary>
[System.Serializable]
public class AmeliaSkillSupplyOverdrive : SkillLogic
{
    [SerializeField, Min(0.5f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float magazineBonusPercent = 150f;
    [SerializeField, Min(0f)] private float damageBonusPercent = 60f;

    public override void OnExecute(SkillContext context)
    {
        Execute(context, radius, magazineBonusPercent, damageBonusPercent);
    }

    public static void Execute(SkillContext context, float radius, float magazineBonusPercent, float damageBonusPercent)
    {
        GameObject caster = AmeliaSupply.Caster(context);
        if (caster == null) return;

        radius = radius > 0f ? radius : 5f;
        magazineBonusPercent = magazineBonusPercent > 0f ? magazineBonusPercent : 150f;
        damageBonusPercent = damageBonusPercent > 0f ? damageBonusPercent : 60f;

        List<HealthManager> receivers = AmeliaSupply.AlliesInRange(caster, radius);
        AmeliaSupplyScan.Play(caster.transform, radius, receivers);
        CombatSkillAccent.Burst(caster.transform.position + Vector3.up, new Color(0.4f, 0.8f, 1f), 1.1f);
        CombatSkillAccent.Burst(caster.transform.position + Vector3.up * 1.05f, new Color(1f, 0.55f, 0.2f), 1.1f);
        SkillSfx.Play("amelia_supply", caster.transform.position, 0.8f);
        SkillSfx.Play("amelia_reinforce", caster.transform.position, 0.8f);

        foreach (HealthManager ally in receivers)
        {
            SupplyBuffs supply = SupplyBuffs.On(ally.gameObject);
            IEAttackMagazineCapacity capacity = new();
            capacity.Initialize("Supply Magazine", EffectDetailType.Attack, magazineBonusPercent, 99999f);
            supply.Give("supply_magazine", capacity);

            IEAttackDamage damage = new();
            damage.Initialize("Reinforced Rounds", EffectDetailType.Attack, damageBonusPercent, 99999f);
            Transform body = ally.transform;
            supply.Give("reinforced", damage, () => AmeliaHeatAura.EndOn(body));
            supply.Refill();
        }

        AmeliaForgeStrike.Play(caster.transform, radius, receivers,
            ally => { if (ally != null) AmeliaHeatAura.Ignite(ally.transform); });
    }
}

/// <summary>
/// Skill 2 - Reinforced Materials: Amelia and allies within 5m get a fresh magazine at once whose rounds deal
/// 160% damage, until that magazine runs dry (or they reload). A supply magazine given later keeps it.
/// </summary>
[System.Serializable]
public class AmeliaSkillPowerBuff : SkillLogic
{
    [SerializeField, Min(0.5f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float damageBonusPercent = 60f;      // +60% = 160% damage

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = AmeliaSupply.Caster(context);
        if (caster == null) return;

        List<HealthManager> receivers = AmeliaSupply.AlliesInRange(caster, radius);
        CombatSkillAccent.Burst(caster.transform.position + Vector3.up, new Color(1f, 0.55f, 0.2f), 1.1f);
        SkillSfx.Play("amelia_reinforce", caster.transform.position, 0.8f);

        foreach (HealthManager ally in receivers)
        {
            SupplyBuffs supply = SupplyBuffs.On(ally.gameObject);
            IEAttackDamage damage = new();
            damage.Initialize("Reinforced Rounds", EffectDetailType.Attack, damageBonusPercent, 99999f);
            Transform body = ally.transform;
            supply.Give("reinforced", damage, () => AmeliaHeatAura.EndOn(body));   // Magazine used up: the emblem cools off
            supply.Refill();
        }

        // Forge strike: molten cracks run out to the radius and ignite each ally when they reach them
        AmeliaForgeStrike.Play(caster.transform, radius, receivers, ally => { if (ally != null) AmeliaHeatAura.Ignite(ally.transform); });
    }
}

/// <summary> E - Paint Shells inspired cluster grenade with a damaging primary blast and four bomblets. </summary>
[System.Serializable]
public class AmeliaSkillClusterGrenade : SkillLogic
{
    [SerializeField, Min(1f)] private float throwSpeed = 22f;
    [SerializeField, Min(1f)] private float primaryRadius = 8.5f;
    [SerializeField, Min(1f)] private float submunitionRadius = 4.5f;

    public override void OnEquip(SkillBehavior owner, SkillSlot slot)
    {
        AmeliaPaintMark.Prepare();
        var preview = owner.GetComponent<AmeliaGrenadeTrajectory>();
        if (preview == null) preview = owner.gameObject.AddComponent<AmeliaGrenadeTrajectory>();
        preview.Configure(slot, throwSpeed);

    }

    public override void OnUnequip(SkillBehavior owner, SkillSlot slot)
    {
        var preview = owner.GetComponent<AmeliaGrenadeTrajectory>();
        if (preview != null) { preview.enabled = false; UnityEngine.Object.Destroy(preview); }
    }

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = AmeliaSupply.Caster(context);
        if (caster == null) return;
        Vector3 aim = AmeliaClusterGrenadeProjectile.AimPoint(caster);
        var combatSource = Charm.Mechanism.Missions.CombatTelemetry.Capture(caster);
        if (AmeliaGrenadeThrowMotion.Play(caster, aim, handPosition =>
        {
            using (Charm.Mechanism.Missions.CombatTelemetry.UseSource(combatSource))
                AmeliaClusterGrenadeProjectile.Throw(caster, handPosition, aim, throwSpeed, primaryRadius, submunitionRadius);
        })) return;
        Vector3 origin = caster.transform.position + Vector3.up * 1.4f + caster.transform.forward * .65f;
        AmeliaClusterGrenadeProjectile.Throw(caster, origin, aim, throwSpeed, primaryRadius, submunitionRadius);
    }
}

/// <summary>
/// Game changer - Quartermastering: throw a supply flag to the aimed spot (up to 15m) and plant it.
/// For 4.5s every ally inside its 10m radius fires without using ammo (also allies who walk in later;
/// leaving the zone ends it).
/// </summary>
[System.Serializable]
public class AmeliaSkillInfiniteMagazine : SkillLogic
{
    [SerializeField, Min(1f)] private float throwRange = 15f;
    [SerializeField, Min(1f)] private float radius = 10f;
    [SerializeField, Min(0.5f)] private float duration = 4.5f;

    // Build the generated infinity texture ahead of the first use
    public override void OnEquip(SkillBehavior owner, SkillSlot slot) { _ = QuartermasterFlag.SignTexture; }

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = AmeliaSupply.Caster(context);
        if (caster == null) return;

        Vector3 target = AimPoint(caster.transform);
        QuartermasterFlag.Throw(caster.transform, target, radius, duration);
    }

    // Player: where the camera aims (within throw range). AI: a few meters ahead. Snapped to the ground.
    private Vector3 AimPoint(Transform caster)
    {
        Vector3 point = caster.position + caster.forward * Mathf.Min(6f, throwRange);
        Camera cam = Camera.main;
        if (caster.CompareTag("Player") && cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            point = Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(caster)
                ? hit.point
                : ray.origin + ray.direction * 200f;
            Vector3 flat = point - caster.position;
            flat.y = 0f;
            if (flat.magnitude > throwRange) point = caster.position + flat.normalized * throwRange;
        }
        return QuartermasterFlag.GroundPoint(point, caster.position.y);   // Never on top of a character
    }
}
