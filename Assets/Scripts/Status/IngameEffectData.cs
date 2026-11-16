using System;
using EnemyAI;
using UnityEngine;
#region Default Dataset
/// <summary>
/// Effect Data of ingame running, applied to player or other character
/// </summary>
// [CreateAssetMenu(fileName = "NewIngameEffect", menuName = "ScriptableObjects/IngameEffectData")]
[Serializable]
public abstract class IngameEffectData
{
    [Header("Basic Info")]
    public string effectName;
    //public string description;

    [Header("Category and type")]
    public EffectDetailType detailType;
    
    [Tooltip("If it is positive, set as buff, if negative, set as debuff.")]
    public float intensity = 1f;

    [Header("Duration of application")]
    [Tooltip("If 0, the effect will be used only once.")]
    public float duration = 5f;
    [Tooltip("If true, the effect will be turned off not following the duration, but the reload calls are in")]
    public bool deactivateOnReload = false;

    [Header("Application Range")]
    [Tooltip("If true, it will be applied including object that has effects of self")]
    public bool includeSelf = true;
    
    [Tooltip("Range of teammate application. Used for applying positive effects.")]
    public float teamRange = 0f;
    
    [Tooltip("Range of enemy application. Used for applying negative effects.")]
    public float enemyRange = 0f;

    [Header("Misc")]
    public bool isPersistentTrait = false;
    [HideInInspector, Tooltip("Variable which is used for once-only propagation.")] public bool isPropagated = false;

    /// <summary>
    /// Initializes the IngameEffectData with the implementation parameters defined in the sources [1], [2].
    /// Since ScriptableObjects should not use standard constructors, this method serves as the primary initializer.
    /// </summary>
    public void Initialize
    (
        string name, 
        EffectDetailType det, 
        float i, 
        float d, 
        bool iS = true, // Use for enemy debuff by setting it as false
        float tR = 0, // If <0, effect will be applied at other teammate
        float eR = 0, // If <0, effect will be applied at enemy
        bool dRL = false
    )
    {
        effectName = name;
        detailType = det;
        intensity = i;
        duration = d;
        includeSelf = iS;
        teamRange = tR;
        enemyRange = eR;
        deactivateOnReload = dRL;
    }

    /// <summary>
    /// Apply the effects of player by detail type.
    /// </summary>
    /// <param name="sb">The behavior hub providing access to Move, Aim, Shoot, Health, and Skill.</param>
    public abstract void ApplyEffect(IngameEffectBehavior sb);

    /// <summary>
    /// Logic to revert the changes made by ApplyEffect.
    /// </summary>
    /// <param name="sb">The behavior hub providing access to Move, Aim, Shoot, Health, and Skill.</param>
    public abstract void RemoveEffect(IngameEffectBehavior sb);

    public static void ApplyEffectOnOthers(IngameEffectBehavior sb, IngameEffectData effectData)
    {
        // Return if it is already propagated
        if (effectData.isPropagated || (effectData.teamRange <= 0f && effectData.enemyRange <= 0f)) return;

        // Get center
        Vector3 center = sb.transform.position;

        /*
        Both teammate and enemy should have IngameEffectBehavior to be affected.
        */

        // As Teammate
        if (effectData.teamRange > 0f)
        {
            Collider[] teamColliders = Physics.OverlapSphere(center, effectData.teamRange);
            foreach (var col in teamColliders)
            {
                // If self, skip
                if (!effectData.includeSelf && col.gameObject == sb.gameObject) 
                    continue;

                IngameEffectBehavior targetBehavior = col.GetComponent<IngameEffectBehavior>();
                if (targetBehavior != null)
                {
                    // Define that the object is ally or player
                    AllyHealthManager allyHealth = col.GetComponent<AllyHealthManager>();
                    AllyTargetTag allyTargetTag = col.GetComponent<AllyTargetTag>(); // For Tutorial!
                    PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();

                    if (allyHealth != null || allyTargetTag != null || playerHealth != null) 
                    {
                        // Clone new effect object and set propagated
                        IngameEffectData cloneEffect = effectData.Clone();
                        cloneEffect.isPropagated = true;
                        targetBehavior.AddIngameEffect(cloneEffect);
                    }
                }
            }
        }

        // As Enemy
        if (effectData.enemyRange > 0f)
        {
            Collider[] enemyColliders = Physics.OverlapSphere(center, effectData.enemyRange);
            foreach (var col in enemyColliders)
            {
                IngameEffectBehavior targetBehavior = col.GetComponent<IngameEffectBehavior>();
                if (targetBehavior != null)
                {
                    EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
                    TargetHealth targetHealth = col.GetComponent<TargetHealth>();

                    if (enemyHealth != null || targetHealth != null ) 
                    {
                        // Clone new effect object and set propagated
                        IngameEffectData cloneEffect = effectData.Clone();
                        cloneEffect.isPropagated = true;
                        targetBehavior.AddIngameEffect(effectData);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clone Effect Data
    /// </summary>
    public IngameEffectData Clone()
    {
        return (IngameEffectData) MemberwiseClone();
    }
}

/// <summary>
/// Effect detail type of application, used for applying effects
/// </summary>
public enum EffectDetailType
{
    Attack, Defense, Move, Utility, Misc
}
#endregion

// Declaration of All Effects
#region Move
[Serializable] 
public class IEMoveSpeed : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to multiply speed
            behavior.move.AddSpeedModifier(1 + (intensity * 0.01f));
        }
        else
        {
            // AI (enemy / ally): NavMeshAgent speed. Negative intensity = slow
            NavSpeedModifier.Get(behavior.gameObject)?.Add(1 + (intensity * 0.01f));
        }

        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to reset or divide back the speed
            behavior.move.RemoveSpeedModifier(1 + (intensity * 0.01f));
        }
        else
        {
            NavSpeedModifier modifier = behavior.GetComponent<NavSpeedModifier>();
            if (modifier != null) modifier.Remove(1 + (intensity * 0.01f));
        }
    }
}

[Serializable] 
public class IEMoveJumpHeight : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to multiply speed
            behavior.move.AddJumpHeightModifier(1 + (intensity * 0.01f));
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to reset or divide back the speed
            behavior.move.RemoveJumpHeightModifier(1 + (intensity * 0.01f));
        }
    }
}

[Serializable] 
public class IEMoveDashCooldown : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to multiply speed
            //behavior.move.AddJumpHeightModifier(1 + (intensity * 0.01f));
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            // Assuming MoveBehaviour has a method to reset or divide back the speed
            //behavior.move.RemoveJumpHeightModifier(1 + (intensity * 0.01f));
        }
    }
}

[Serializable] 
public class IEMoveSpeedOnAimPreservation : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            //behavior.move.AddJumpHeightModifier(1 + (intensity * 0.01f));
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            //behavior.move.RemoveJumpHeightModifier(1 + (intensity * 0.01f));
        }
    }
}

#endregion
#region Attack
[Serializable] 
public class IEAttackDamage : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackDamageModifier(intensity * 0.01f);
        }
        else if (behavior.state != null)
        {
            behavior.state.AddDamageModifier(intensity * 0.01f);
        }
        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.RemoveAttackDamageModifier(intensity * 0.01f);
        }
        else if (behavior.state != null)
        {
            behavior.state.RemoveDamageModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEAttackCrit : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackCritModifier(intensity * 0.01f);
        }
        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.RemoveAttackCritModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEAttackReloadSpeed : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackReloadSpeedModifier(intensity * 0.01f);
        }
        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.move != null)
        {
            behavior.shoot.RemoveAttackReloadSpeedModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEAttackAimPreservation : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackAimPreservationModifier(intensity * 0.01f);
        }
        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.RemoveAttackAimPreservationModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEAttackMagazine : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            // Todo
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            
        }
    }
}

/// <summary>
/// Rate of fire. intensity is percent (100 = x2 rate). Works on player(ShootBehaviour) and AI(StateController).
/// </summary>
[Serializable] 
public class IEAttackFireRate : IngameEffectData
{
    [Tooltip("If true, SEMI/BURST weapons of player fire as AUTO while this effect is active.")]
    public bool forceAutomatic = false;

    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackFireRateModifier(intensity * 0.01f);
            if (forceAutomatic) behavior.shoot.AddForceAutoFire();
        }
        else if (behavior.state != null)
        {
            behavior.state.AddShotRateModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.RemoveAttackFireRateModifier(intensity * 0.01f);
            if (forceAutomatic) behavior.shoot.RemoveForceAutoFire();
        }
        else if (behavior.state != null)
        {
            behavior.state.RemoveShotRateModifier(intensity * 0.01f);
        }
    }
}

/// <summary>
/// Magazine capacity. intensity is percent (200 = x3 mag). Bonus bullets are filled at once and cut back when it ends.
/// </summary>
[Serializable] 
public class IEAttackMagazineCapacity : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.AddAttackMagazineModifier(intensity * 0.01f);
        }
        else if (behavior.state != null)
        {
            behavior.state.AddMagazineModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);
    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.shoot != null)
        {
            behavior.shoot.RemoveAttackMagazineModifier(intensity * 0.01f);
        }
        else if (behavior.state != null)
        {
            behavior.state.RemoveMagazineModifier(intensity * 0.01f);
        }
    }
}
#endregion
#region Defense
[Serializable] 
public class IEDefenseDamageDamp : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.AddDamageDampModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.RemoveDamageDampModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEDefenseDamageDampSkill : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.AddDamageDampSkillModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.RemoveDamageDampModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEDefenseHealAmplification : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.AddHealAmplificationModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.RemoveHealAmplificationModifier(intensity * 0.01f);
        }
    }
}

[Serializable] 
public class IEDefenseBlockNegativeEffects : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.AddBlockNegativeEffectsModifier(intensity * 0.01f);
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.RemoveBlockNegativeEffectsModifier(intensity * 0.01f);
        }
    }
}

#endregion
#region Utility

#endregion
#region Special
[Serializable] 
public class IESpecialShield : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.AddShield(intensity * 10);
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            behavior.health.RemoveShield(intensity * 10);
        }
    }
}

public class IEIgnoreDamageDamp : IngameEffectData
{
    public override void ApplyEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            
        }

        ApplyEffectOnOthers(behavior, this);

    }

    public override void RemoveEffect(IngameEffectBehavior behavior)
    {
        if (behavior.health != null)
        {
            
        }
    }
}

#endregion