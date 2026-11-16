using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ScriptableObject 상속을 제거하고 일반 [System.Serializable] 클래스로 변경합니다.
[Serializable] 
public class LuciaSkillHealArea : SkillLogic
{
    [SerializeField] private GameObject healZone;
    [SerializeField] private float throwingPower = 12f;
    [SerializeField] private float duration = 6f;
    [SerializeField] private float healPerSecond = 25f;

    [Header("Barrier")]
    [Tooltip("Shield given once to each ally entering the zone. 0 = no barrier")]
    [SerializeField, Min(0f)] private float barrierAmount = 50f;
    [SerializeField, Min(0.1f)] private float barrierDuration = 6f;

    [Header("Visual")]
    [SerializeField] private bool playEffects = true;
    [SerializeField] private Color healColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color barrierColor = new Color(1f, 0.86f, 0.45f, 1f);

    public override void OnExecute(SkillContext context)
    {
        // Only if caster or onwer is available..
        if(context.owner != null)
        {
            // Get player sight vector
            // Set vector as direction of throwing grenade
            // Instantiate grenade and add force to it

            Transform startPoint = context.owner.transform;
            Quaternion throwingQuat = Quaternion.identity;

            // If Player use this one
            /**/
            if(context.owner.GetComponent<BasicBehaviour>() != null)
            {
                ThirdPersonOrbitCam tpo = context.owner.GetComponentInChildren<ThirdPersonOrbitCam>();
                if (tpo != null)
                {
                    startPoint = tpo.transform;
                    throwingQuat = tpo.transform.rotation;
                }
            }
            // If FSM use this one
            else
            {
                // TODO
                return;
            }

            //Debug.Log("Current sight value:" + throwingQuat + ", Current throwing position: " + context.position);

            if(startPoint)
            {
                Vector3 throwingPos = startPoint.position;
                GameObject grenadeInstance = healZone != null
                    ? GameObject.Instantiate(healZone, throwingPos, throwingQuat)
                    : CreateDefaultHealZone(throwingPos, throwingQuat);

                HealTrigger healTrigger = grenadeInstance.GetComponent<HealTrigger>();
                if (healTrigger == null)
                {
                    healTrigger = grenadeInstance.AddComponent<HealTrigger>();
                }
                healTrigger.Configure(healPerSecond);
                healTrigger.ConfigureBarrier(barrierAmount, barrierDuration, playEffects, barrierColor);

                EnsureHealTriggerCollider(grenadeInstance);

                if (playEffects)
                {
                    SkillVfxFactory.SpawnHealZone(grenadeInstance.transform, GetHealRadius(grenadeInstance), duration, healColor);
                    // Chime on landing, then a soft loop until the zone ends (no throw sound)
                    grenadeInstance.AddComponent<HealZoneSound>().Begin(duration);
                }

                AutoDestroyAfterSeconds autoDestroy = grenadeInstance.GetComponent<AutoDestroyAfterSeconds>();
                if (autoDestroy == null)
                {
                    autoDestroy = grenadeInstance.AddComponent<AutoDestroyAfterSeconds>();
                }
                autoDestroy.Configure(duration);

                if (grenadeInstance.GetComponent<StopRigidbodyOnLanding>() == null
                    && grenadeInstance.GetComponent<Rigidbody>() != null)
                {
                    grenadeInstance.AddComponent<StopRigidbodyOnLanding>();
                }

                // Assign origin
                ExplosiveProjectile explosive = grenadeInstance.GetComponent<ExplosiveProjectile>();
                if(explosive != null)
                {
                    explosive.SetOrigin(context.caster);
                }

                // Assign rigidbody force
                Rigidbody rb = grenadeInstance.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    Vector3 direction = context.direction.sqrMagnitude > 0.001f
                        ? context.direction.normalized
                        : startPoint.forward;
                    rb.AddForce(direction * throwingPower, ForceMode.VelocityChange);
                }
            }
        }
    }

    private GameObject CreateDefaultHealZone(Vector3 position, Quaternion rotation)
    {
        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        zone.name = "Lucia Heal Zone";
        zone.transform.SetPositionAndRotation(position, rotation);
        zone.transform.localScale = Vector3.one * 6f;

        Rigidbody rb = zone.AddComponent<Rigidbody>();
        rb.useGravity = true;

        return zone;
    }

    // World radius of heal trigger (sphere collider radius * scale)
    private float GetHealRadius(GameObject zone)
    {
        float radius = 0f;
        foreach (Collider collider in zone.GetComponentsInChildren<Collider>(true))
        {
            if (collider is SphereCollider sphere && sphere.isTrigger)
            {
                Vector3 s = sphere.transform.lossyScale;
                radius = Mathf.Max(radius, sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            }
        }
        return radius > 0f ? radius : 3f;
    }

    private void EnsureHealTriggerCollider(GameObject zone)
    {
        foreach (Collider collider in zone.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && collider.isTrigger)
            {
                return;
            }
        }

        GameObject trigger = new GameObject("Heal Area Trigger");
        trigger.transform.SetParent(zone.transform, false);
        trigger.transform.localPosition = Vector3.zero;
        trigger.transform.localRotation = Quaternion.identity;
        trigger.transform.localScale = Vector3.one;

        SphereCollider triggerCollider = trigger.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = 1f;
    }
}

[Serializable] 
public class LuciaSkillReviveOneAlly : SkillLogic
{
    [SerializeField] private GameObject healingProjectile;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField, Range(0f, 1f)] private float healRatio = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float projectileCollisionActivationDelay = 0.5f;
    [SerializeField, Min(0f)] private float projectileTargetSearchRadius = 0.7f;

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = context.caster != null
            ? context.caster
            : context.owner != null ? context.owner.gameObject : null;

        if(caster == null)
        {
            return;
        }

        Transform firePoint = FindThrowingPoint(caster);
        Vector3 direction = context.direction.sqrMagnitude > 0.001f
            ? context.direction.normalized
            : firePoint.forward;

        GameObject projectileInstance = healingProjectile != null
            ? GameObject.Instantiate(healingProjectile, firePoint.position, Quaternion.LookRotation(direction))
            : CreateDefaultHealingProjectile(firePoint.position, Quaternion.LookRotation(direction));

        HealingProjectile projectile = projectileInstance.GetComponent<HealingProjectile>();
        if (projectile == null)
        {
            projectile = projectileInstance.AddComponent<HealingProjectile>();
        }

        // Initialize Projectile
        //projectile.SetIgnoringLayerMask(LayerMask.NameToLayer("Player"));
        projectile.SetOrigin(caster);
        projectile.Configure(healRatio, projectileCollisionActivationDelay, projectileTargetSearchRadius);

        Rigidbody rb = projectileInstance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = projectileInstance.AddComponent<Rigidbody>();
        }

        if (rb != null)
        {
            //rb.useGravity = false;
            rb.linearVelocity = direction * projectileSpeed;
        }
    }

    private Transform FindThrowingPoint(GameObject caster)
    {
        PersonaBehavior personaBehavior = caster.GetComponent<PersonaBehavior>();
        if (personaBehavior != null && personaBehavior.ThrowingPoint != null)
        {
            return personaBehavior.ThrowingPoint;
        }

        return FindMuzzle(caster);
    }

    private Transform FindMuzzle(GameObject caster)
    {
        foreach (Transform child in caster.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "muzzle")
            {
                return child;
            }
        }

        ThirdPersonOrbitCam orbitCam = caster.GetComponentInChildren<ThirdPersonOrbitCam>();
        return orbitCam != null ? orbitCam.transform : caster.transform;
    }

    private GameObject CreateDefaultHealingProjectile(Vector3 position, Quaternion rotation)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Lucia Healing Projectile";
        projectile.transform.SetPositionAndRotation(position, rotation);
        projectile.transform.localScale = Vector3.one * 0.35f;

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        return projectile;
    }
}


[Serializable]
public class LuciaSkillReviveMultiAllies : SkillLogic
{
    [SerializeField] private LayerMask scanFilterMask = ~0;
    [SerializeField] private float healRadius = 20f;
    [SerializeField, Range(0f, 1f)] private float healthHealRatio = 1f;
    [SerializeField, Range(0f, 1f)] private float shieldHealRatio = 0.6f;

    [Header("Revive Rule (Overwatch style)")]
    [Tooltip("Dead allies can be revived only within this time after death")]
    [SerializeField, Min(0.5f)] private float reviveWindow = Charm.Mechanism.Missions.CombatTelemetry.CommonReviveSeconds;
    [Tooltip("Channel time before revive happens")]
    [SerializeField, Min(0f)] private float castTime = 1.5f;
    [Tooltip("Move speed multiplier while casting (0.3 = 30% speed)")]
    [SerializeField, Range(0.05f, 1f)] private float castMoveSpeedMultiplier = 0.3f;
    [Tooltip("Skill can't be used unless at least this many revivable dead allies are in radius")]
    [SerializeField, Min(1)] private int minReviveTargets = 1;

    [Header("Visual")]
    [SerializeField] private bool playEffects = true;
    [SerializeField] private Color reviveIconColor = new Color(1f, 0.82f, 0.38f, 1f);
    [Tooltip("Optional. If empty, procedural light column effect is used.")]
    [SerializeField] private GameObject reviveEffectPrefab;
    [Tooltip("Optional. If empty, procedural burst effect is used.")]
    [SerializeField] private GameObject casterEffectPrefab;

    public override void OnEquip(SkillBehavior owner, SkillSlot slot)
    {
        ReviveWindow.EnsureTracker();

        // Revive icons over dead allies, only for the human player
        // 빈사 플레이어 교환에서는 태그 복원보다 OnEquip이 먼저 호출된다. 조종 슬롯으로 구분한다.
        if (owner.GetComponent<BasicBehaviour>() == null) return;
        ReviveIndicator indicator = owner.GetComponent<ReviveIndicator>();
        if (indicator == null) indicator = owner.gameObject.AddComponent<ReviveIndicator>();
        indicator.Configure(reviveWindow, healRadius, reviveIconColor);
    }

    public override void OnUnequip(SkillBehavior owner, SkillSlot slot)
    {
        ReviveIndicator indicator = owner.GetComponent<ReviveIndicator>();
        if (indicator != null) UnityEngine.Object.Destroy(indicator);
    }

    public override bool CanExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return false;

        // Not while already casting
        ReviveCaster reviveCaster = caster.GetComponent<ReviveCaster>();
        if (reviveCaster != null && reviveCaster.IsCasting) return false;

        // Needs revivable dead allies around (point is not consumed otherwise)
        ReviveWindow.EnsureTracker();
        if (CountRevivable(caster.transform.position) < minReviveTargets)
        {
            if (caster.CompareTag("Player"))
            {
                SkillToast.Show("NO ALLY TO REVIVE", reviveIconColor);
                SkillSfx.Play("skill_error", caster.transform.position, 0.6f);
            }
            return false;
        }
        return true;
    }

    private int CountRevivable(Vector3 center)
    {
        int count = 0;
        foreach (HealthManager dead in ReviveWindow.DeadAllies)
        {
            if (dead != null && IsInRadius(center, dead) && ReviveWindow.IsRevivable(dead, reviveWindow))
                count++;
        }
        return count;
    }

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = context.caster != null
            ? context.caster
            : context.owner != null ? context.owner.gameObject : null;

        if(caster == null)
        {
            return;
        }

        ReviveWindow.EnsureTracker();

        // Channel first, revive when cast completes
        if (castTime > 0f)
        {
            ReviveCaster reviveCaster = caster.GetComponent<ReviveCaster>();
            if (reviveCaster == null) reviveCaster = caster.AddComponent<ReviveCaster>();

            List<Transform> beamTargets = new();
            // Targets are also used to hold their downed timers, even with VFX disabled.
            {
                foreach (HealthManager dead in ReviveWindow.DeadAllies)
                {
                    if (dead != null && IsInRadius(caster.transform.position, dead) && ReviveWindow.IsRevivable(dead, reviveWindow))
                        beamTargets.Add(dead.transform);
                }
            }

            // Refund game changer point when cancelled (left click or caster died)
            SkillBehavior owner = context.owner;
            int pointBefore = owner != null ? owner.gameChangerPoint : 0;
            reviveCaster.Begin(castTime, castMoveSpeedMultiplier, beamTargets,
                () => PerformRevive(caster),
                () => { if (owner != null) owner.gameChangerPoint = Mathf.Max(owner.gameChangerPoint, pointBefore); });
        }
        else
        {
            PerformRevive(caster);
        }
    }

    private bool IsInRadius(Vector3 center, HealthManager health)
    {
        return (health.transform.position - center).sqrMagnitude <= healRadius * healRadius;
    }

    private void PerformRevive(GameObject caster)
    {
        if (caster == null) return;

        if (playEffects)
        {
            if (casterEffectPrefab != null)
                GameObject.Instantiate(casterEffectPrefab, caster.transform.position, Quaternion.identity);
            else
                SkillVfxFactory.SpawnReviveCasterBurst(caster.transform.position, healRadius);
        }

        HashSet<HealthManager> healedTargets = new();
        Collider[] hits = Physics.OverlapSphere(caster.transform.position, healRadius, scanFilterMask);

        foreach (Collider hit in hits)
        {
            HealthManager healthManager = hit.transform.root.GetComponent<HealthManager>();
            if (healthManager == null)
            {
                healthManager = hit.GetComponentInParent<HealthManager>();
            }

            TryApplyGameChangerHeal(caster.transform.position, healedTargets, healthManager);
        }

        foreach (HealthManager healthManager in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include))
        {
            TryApplyGameChangerHeal(caster.transform.position, healedTargets, healthManager);
        }
    }

    private void TryApplyGameChangerHeal(Vector3 center, HashSet<HealthManager> healedTargets, HealthManager healthManager)
    {
        if (healthManager == null || healedTargets.Contains(healthManager) || !IsAlly(healthManager))
        {
            return;
        }

        if ((healthManager.transform.position - center).sqrMagnitude > healRadius * healRadius)
        {
            return;
        }

        // Revive window passed: cannot be revived anymore
        if (healthManager.dead && !ReviveWindow.IsRevivable(healthManager, reviveWindow))
        {
            return;
        }

        bool wasDead = healthManager.dead;
        healthManager.ReviveWithHealthRatio(healthHealRatio);
        healthManager.RestoreShieldByRatio(shieldHealRatio);
        healedTargets.Add(healthManager);

        if (playEffects)
            PlayReviveEffect(healthManager.transform, wasDead);
    }

    private void PlayReviveEffect(Transform target, bool revived)
    {
        if (!revived)
        {
            SkillVfxFactory.SpawnHealPulse(target);
            return;
        }

        if (reviveEffectPrefab != null)
            GameObject.Instantiate(reviveEffectPrefab, target.position, Quaternion.identity, target);
        else
            SkillVfxFactory.SpawnReviveColumn(target);
        SkillSfx.Play("lucia_revive_bell", target.position, 0.7f);
        SkillSfx.Play("lucia_revive_rise", target.position, 0.5f);
    }

    private bool IsAlly(HealthManager healthManager)
    {
        return healthManager is PlayerHealth
            || healthManager is AllyHealthManager
            || healthManager.GetComponent<AllyTargetTag>() != null;
    }
}


/// <summary>
/// Ana(Overwatch)-like boost. Square marker is shown on the ally nearest to crosshair,
/// and using it gives damage reduction, faster fire rate and bigger magazine (minigun-like sustained fire) for a while.
/// </summary>
[Serializable]
public class LuciaSkillNanoBoost : SkillLogic
{
    private SkillSlot equippedSlot;

    [Header("Targeting")]
    [SerializeField] private float range = 40f;
    [Tooltip("Max angle from crosshair to select an ally")]
    [SerializeField, Range(1f, 45f)] private float aimAssistAngle = 10f;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Buff")]
    [SerializeField, Min(0.1f)] private float duration = 8f;
    [Tooltip("Percent. 50 = takes 50% less damage")]
    [SerializeField, Range(0f, 99f)] private float damageReductionPercent = 50f;
    [Tooltip("Percent. 100 = x2 rate of fire")]
    [SerializeField, Min(0f)] private float fireRatePercent = 100f;
    [Tooltip("Percent. 200 = x3 magazine (bonus bullets given at once)")]
    [SerializeField, Min(0f)] private float magazinePercent = 200f;
    [Tooltip("Single/burst weapons fire automatically while boosted")]
    [SerializeField] private bool forceAutomatic = true;

    [Header("Visual")]
    [SerializeField] private Color color = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private string markerLabel = "BOOST";
    [Tooltip("Optional. If empty, procedural effect is used.")]
    [SerializeField] private GameObject boostEffectPrefab;

    public override void OnEquip(SkillBehavior owner, SkillSlot slot)
    {
        equippedSlot = slot;
        // Marker is only for the human player
        if (!owner.CompareTag("Player"))
            return;

        SkillTargetIndicator indicator = SkillTargetIndicator.Find(owner.gameObject, slot);
        if (indicator == null)
            indicator = owner.gameObject.AddComponent<SkillTargetIndicator>();
        indicator.Configure(slot, range, aimAssistAngle, requireLineOfSight, color, markerLabel);
    }

    public override void OnUnequip(SkillBehavior owner, SkillSlot slot)
    {
        SkillTargetIndicator indicator = SkillTargetIndicator.Find(owner.gameObject, slot);
        if (indicator != null)
            UnityEngine.Object.Destroy(indicator);
    }

    public override bool CanExecute(SkillContext context)
    {
        GameObject caster = context.caster != null
            ? context.caster
            : context.owner != null ? context.owner.gameObject : null;
        if (caster == null)
            return false;

        HealthManager target = null;

        if (caster.CompareTag("Player"))
        {
            // Resolve the current crosshair target on this button press so a single E press
            // applies the buff to the ally currently being aimed at.
            SkillTargetIndicator indicator = SkillTargetIndicator.Find(caster, equippedSlot);
            if (indicator != null)
            {
                target = indicator.SelectTargetNow();
            }

            if (target == null && context.target != null)
            {
                HealthManager aimed = context.target.transform.root.GetComponent<HealthManager>();
                if (AllyTargeting.IsValidAlly(aimed, caster.transform.root))
                    target = aimed;
            }
        }
        else
        {
            target = AllyTargeting.FindAllyForAI(caster.transform.root, range);
        }

        if (target == null)
        {
            Debug.Log("[LuciaSkillNanoBoost] No ally target. Skill is not used.");
            if (caster.CompareTag("Player"))
            {
                SkillToast.Show("NO ALLY TARGET", color);
                SkillSfx.Play("skill_error", caster.transform.position, 0.6f);
            }
            return false;
        }

        context.target = target.gameObject;
        return true;
    }

    public override void OnExecute(SkillContext context)
    {
        if (context.target == null)
            return;

        GameObject target = context.target;
        IngameEffectBehavior iFx = target.GetComponent<IngameEffectBehavior>();
        if (iFx == null)
        {
            iFx = target.AddComponent<IngameEffectBehavior>();
            iFx.RefreshComponentReferences();
        }

        IEDefenseDamageDamp damp = new();
        damp.Initialize("Boost Damage Reduction", EffectDetailType.Defense, damageReductionPercent, duration);

        IEAttackFireRate fireRate = new() { forceAutomatic = forceAutomatic };
        fireRate.Initialize("Boost Fire Rate", EffectDetailType.Attack, fireRatePercent, duration);

        IEAttackMagazineCapacity magazine = new();
        magazine.Initialize("Boost Magazine", EffectDetailType.Attack, magazinePercent, duration);

        iFx.AddIngameEffect(damp);
        iFx.AddIngameEffect(fireRate);
        iFx.AddIngameEffect(magazine);

        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (boostEffectPrefab != null)
            GameObject.Instantiate(boostEffectPrefab, target.transform.position, Quaternion.identity, target.transform);
        else
            SkillVfxFactory.SpawnBoost(caster != null ? caster.transform : null, target.transform, duration, color);
        // Launch from Lucia, power-up on the ally, then a soft energy hum on the ally for the whole boost
        Vector3 from = caster != null ? caster.transform.position : target.transform.position;
        SkillSfx.Play("lucia_nano", from, 0.7f);
        SkillSfx.Play("lucia_nano_up", target.transform.position, 0.7f, 1f, 0f, 0.1f);
        foreach (AudioSource running in target.GetComponentsInChildren<AudioSource>())
            if (running.name == "SFX_lucia_nano_loop") SkillSfx.Stop(running, 0.3f);   // Recast: one hum at a time
        SkillSfx.PlayLoopFor("lucia_nano_loop", target.transform, 0.4f, duration, 0.6f, 1.2f);
    }
}
