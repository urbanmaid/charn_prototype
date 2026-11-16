using UnityEngine;

// ScriptableObject 상속을 제거하고 일반 [System.Serializable] 클래스로 변경합니다.
[System.Serializable] 
public class FionaSkillMove : SkillLogic
{
    public override void OnExecute(SkillContext context)
    {
        // Only if caster or onwer is available..
        if(context.caster != null || context.owner != null)
        {
            // If caster is available? find effect Behavior from SkillBehavior or get component from GameObject
            IngameEffectBehavior iFx = context.owner != null ? context.owner.effectBehavior : context.caster.GetComponent<IngameEffectBehavior>();
            if(iFx)
            {
                // Use cached instance or create it if it doesn't exist
                IEAttackDamage damageBuff = new();
                damageBuff.Initialize(
                    "Bloody Damage Mode!",
                    EffectDetailType.Attack,
                    25f,
                    6.4f,
                    true, 0, 0, true
                );

                // Add to behavior (This will trigger ApplyEffect and the duration Coroutine)
                iFx.AddIngameEffect(damageBuff);
            }
            else
            {
                Debug.LogWarning("There is no Ingame Effect Behavior in Player Object!");
            }
        }
    }
}

[System.Serializable] 
public class FionaSkillJump : SkillLogic
{
    private IEDefenseDamageDamp damageDampBuff; // Cached instance
    private IESpecialShield shieldBuff; // Cached instance

    public override void OnExecute(SkillContext context)
    {
        if(context.caster != null || context.owner != null)
        {
            IngameEffectBehavior iFx = context.owner != null ? context.owner.effectBehavior : context.caster.GetComponent<IngameEffectBehavior>();
            if(iFx)
            {
                // Use cached instance or create it if it doesn't exist
                damageDampBuff = new IEDefenseDamageDamp();
                if (damageDampBuff != null)
                {
                    damageDampBuff.Initialize(
                        "Damage Damp Buff",
                        EffectDetailType.Defense,
                        25f,
                        10f
                    );
                }

                shieldBuff = new IESpecialShield();
                if (shieldBuff != null)
                {
                    shieldBuff.Initialize(
                        "Shield activation",
                        EffectDetailType.Misc,
                        25f,
                        14f
                    );
                }

                // Add to behavior (This will trigger ApplyEffect and the duration Coroutine)
                iFx.AddIngameEffect(damageDampBuff);
                iFx.AddIngameEffect(shieldBuff);
            }
            else
            {
                Debug.LogWarning("There is no Ingame Effect Behavior in Player Object!");
            }
        }
    }
}


[System.Serializable] 
public class FionaGameChangerLog : SkillLogic
{
    public override void OnExecute(SkillContext context)
    {
        if(context.caster != null || context.owner != null)
        {
            // For test..

            IngameEffectBehavior iFx = context.owner != null ? context.owner.effectBehavior : context.caster.GetComponent<IngameEffectBehavior>();
            if(iFx)
            {
                // Use cached instance or create it if it doesn't exist
                IEMoveDashCooldown jumpBuff = new IEMoveDashCooldown();
                jumpBuff.Initialize(
                    "253665",
                    EffectDetailType.Move,
                    15f,
                    20f
                );
                IEMoveSpeedOnAimPreservation aimBalalnceBuff = new IEMoveSpeedOnAimPreservation();
                aimBalalnceBuff.Initialize(
                    "124223",
                    EffectDetailType.Move,
                    15f,
                    30f
                );
                IEAttackDamage ad = new IEAttackDamage();
                ad.Initialize(
                    "556123",
                    EffectDetailType.Move,
                    15f,
                    40f
                );
                IEAttackCrit cr = new IEAttackCrit();
                cr.Initialize(
                    "987521",
                    EffectDetailType.Move,
                    15f,
                    60f
                );
                IEAttackAimPreservation ap = new IEAttackAimPreservation();
                ap.Initialize(
                    "114865",
                    EffectDetailType.Move,
                    15f,
                    80f
                );
                IEAttackMagazine mag = new IEAttackMagazine();
                mag.Initialize(
                    "002659",
                    EffectDetailType.Move,
                    15f,
                    120f
                );

                // Add to behavior (This will trigger ApplyEffect and the duration Coroutine)
                iFx.AddIngameEffect(jumpBuff);
                iFx.AddIngameEffect(aimBalalnceBuff);
                iFx.AddIngameEffect(ad);
                iFx.AddIngameEffect(cr);
                iFx.AddIngameEffect(ap);
                iFx.AddIngameEffect(mag);
            }
            else
            {
                Debug.LogWarning("There is no Ingame Effect Behavior in Player Object!");
            }
        }
    }
}

/// <summary>
/// Skill 1 - Lizard's First Bite: reload venom rounds that poison enemies and slow them on hit.
/// </summary>
[System.Serializable]
public class FionaSkillLizardBite : SkillLogic
{
    // Generated effect textures are built here instead of on the first cast
    public override void OnEquip(SkillBehavior owner, SkillSlot slot) => FionaVfx.Prewarm();

    public override void OnUnequip(SkillBehavior owner, SkillSlot slot)
    {
        owner.GetComponent<VenomRounds>()?.Deactivate();
    }

    [SerializeField, Range(0f, 90f)] private float slowPercent = 25f;
    [SerializeField, Min(0.1f)] private float slowDuration = 4f;
    [SerializeField, Min(0f)] private float poisonDamagePerSecond = 8f;

    // Magazine goes in at 1.7s of rifle_reload/pistol_reload (2.67s clips), where the gun's own reload sound clicks
    private const float MagazineSeatTime = 1.7f;

    public override bool CanExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return false;

        ShootBehaviour shoot = caster.GetComponent<ShootBehaviour>();
        if (shoot != null && !shoot.HasActiveWeapon)
        {
            SkillToast.Show("NO WEAPON", Color.white);
            SkillSfx.Play("skill_error", caster.transform.position, 0.6f);
            return false;
        }
        return true;
    }

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return;

        ShootBehaviour shoot = caster.GetComponent<ShootBehaviour>();
        if (shoot != null)
        {
            // Reload with the real reload motion; the venom magazine clicks in when the motion seats it
            float reloadSpeed = shoot.SkillReload();
            VenomRounds venom = caster.GetComponent<VenomRounds>();
            if (venom == null) venom = caster.AddComponent<VenomRounds>();
            venom.Activate(slowPercent, slowDuration, poisonDamagePerSecond);
            Transform casterTransform = caster.transform;
            SkillSfx.Play("venom_load", casterTransform.position, 1f, 1f, 0f, MagazineSeatTime / Mathf.Max(0.1f, reloadSpeed));
            // Lizard hisses in, coils around the gun and keeps biting it while the venom rounds last
            venom.HoldLizard(FionaVfx.SpawnVenomActivate(casterTransform, shoot.GunMuzzle));
            SkillSfx.Play("venom_bite", casterTransform.position, 0.9f);
        }
        else
        {
            // AI: instant reload only (AI shots have no hit hook)
            EnemyAI.StateController controller = caster.GetComponent<EnemyAI.StateController>();
            if (controller != null) controller.bullets = controller.EffectiveMagBullets;
            Transform casterTransform = caster.transform;
            FionaVfx.SpawnVenomActivate(casterTransform, null,
                () => SkillSfx.Play("venom_load", casterTransform.position, 1f, 1f, 0f, 0.12f));   // Clicks in right after the snap
            SkillSfx.Play("venom_bite", casterTransform.position, 0.9f);
        }
    }
}

/// <summary>
/// Skill 2 - Crow's Wingbeat: 200 shield for 8s (gone when depleted), and 25% damage reduction for 10s.
/// </summary>
[System.Serializable]
public class FionaSkillCrowWingbeat : SkillLogic
{
    // Generated effect textures are built here instead of on the first cast
    public override void OnEquip(SkillBehavior owner, SkillSlot slot) => FionaVfx.Prewarm();

    [SerializeField, Min(1f)] private float shieldAmount = 200f;
    [SerializeField, Min(0.1f)] private float shieldDuration = 8f;
    [SerializeField, Range(0f, 99f)] private float damageReductionPercent = 25f;
    [SerializeField, Min(0.1f)] private float damageReductionDuration = 10f;

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return;

        CrowShield crow = caster.GetComponent<CrowShield>();
        if (crow == null) crow = caster.AddComponent<CrowShield>();
        crow.Begin(shieldAmount, shieldDuration, damageReductionPercent, damageReductionDuration);
    }
}

/// <summary>
/// Game Changer - Hawk's Ambush: wait, then fire to hit all enemies in sight.
/// 180 damage right away, 300 after 2s (fixed after that). Waiting 4s without firing refunds 65% of game changer point.
/// </summary>
[System.Serializable]
public class FionaSkillHawkAmbush : SkillLogic
{
    // Generated effect textures are built here instead of on the first cast
    public override void OnEquip(SkillBehavior owner, SkillSlot slot) => FionaVfx.Prewarm();

    [SerializeField] private float minDamage = 180f;
    [SerializeField] private float maxDamage = 300f;
    [SerializeField, Min(0.01f)] private float chargeTime = 2f;
    [SerializeField, Min(0.1f)] private float maxWait = 4f;
    [SerializeField, Range(0f, 1f)] private float refundRatio = 0.65f;
    [SerializeField, Min(1f)] private float range = 80f;
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("AI caster strikes automatically after this time")]
    [SerializeField, Min(0f)] private float aiStrikeTime = 2f;

    public override bool CanExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return false;
        HawkAmbush ambush = caster.GetComponent<HawkAmbush>();
        return ambush == null || !ambush.IsActive;
    }

    public override void OnExecute(SkillContext context)
    {
        GameObject caster = context.caster != null ? context.caster : context.owner != null ? context.owner.gameObject : null;
        if (caster == null) return;

        HawkAmbush ambush = caster.GetComponent<HawkAmbush>();
        if (ambush == null) ambush = caster.AddComponent<HawkAmbush>();

        // Point is still full here (SkillBehavior resets it after execute). Refund base is capped at the required point.
        int pointBefore = context.owner != null ? context.owner.gameChangerPoint : 0;
        if (context.owner != null && context.owner.GetSkills() != null
            && context.owner.GetSkills().TryGetValue(SkillSlot.GameChanger, out SkillData data) && data is GameChangerData gcData)
            pointBefore = Mathf.Min(pointBefore, gcData.requiredPoint);
        ambush.Begin(new HawkAmbush.Settings
        {
            minDamage = minDamage,
            maxDamage = maxDamage,
            chargeTime = chargeTime,
            maxWait = maxWait,
            refundRatio = refundRatio,
            range = range,
            aiStrikeTime = aiStrikeTime,
            requireLineOfSight = requireLineOfSight
        }, context.owner, pointBefore);
    }
}
