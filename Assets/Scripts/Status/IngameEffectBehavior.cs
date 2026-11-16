using System;
using System.Collections.Generic;
using EnemyAI;
using UnityEngine;

[Serializable]
public class IngameEffectRuntimeState
{
    [SerializeReference, SubclassSelector]
    public IngameEffectData effect;
    public float originalDuration;
    public float remainingDuration;
    public bool deactivateOnReload;
    public bool isPersistentTrait;

    public bool IsTimed => effect != null && !deactivateOnReload && originalDuration > 0f;
    public float RemainingRatio => IsTimed ? Mathf.Clamp01(remainingDuration / originalDuration) : 1f;

    public IngameEffectRuntimeState()
    {
    }

    public IngameEffectRuntimeState(IngameEffectData effect, float remainingDuration = -1f)
    {
        this.effect = effect;
        originalDuration = effect != null ? Mathf.Max(0f, effect.duration) : 0f;
        this.remainingDuration = remainingDuration >= 0f
            ? Mathf.Clamp(remainingDuration, 0f, originalDuration)
            : originalDuration;
        deactivateOnReload = effect != null && effect.deactivateOnReload;
        isPersistentTrait = effect != null && effect.isPersistentTrait;
    }

    public IngameEffectRuntimeState CloneForTransfer()
    {
        IngameEffectData clonedEffect = effect != null ? effect.Clone() : null;
        return new IngameEffectRuntimeState(clonedEffect, remainingDuration)
        {
            originalDuration = originalDuration,
            deactivateOnReload = deactivateOnReload,
            isPersistentTrait = isPersistentTrait
        };
    }
}

public class IngameEffectBehavior : MonoBehaviour, IIngameEffectsStorage
{
    // Component of each effect appication - Player
    internal MoveBehaviour move; // Movement
    internal AimBehaviour aim; // Attack - Aim
    internal ShootBehaviour shoot; // Attack - Shoot
    internal HealthManager health; // Defense
    internal SkillBehavior skill; // Resource Management - Skill
    internal StateController state;
    // Effect self has no internal interface
    //internal bG?

    [SerializeReference, SubclassSelector]
    public List<IngameEffectData> currentIngameEffect = new List<IngameEffectData>();

    [SerializeField]
    private List<IngameEffectRuntimeState> currentIngameEffectStates = new List<IngameEffectRuntimeState>();

	[SerializeField] protected BehaviorModifier modifier = new(); // Initialized for components added at runtime
	[Serializable] protected class BehaviorModifier
	{
		public float blockNegativeEffects = 0.025f;
	}

    [Header("UI")]
    public HUDIngameEffects hUDIngameEffects;
    //[Tooltip("Apply only if target or enemy")] public HUDIngameEffects hUDIngameEffects;

    void Start()
    {
        RefreshComponentReferences();

        if (hUDIngameEffects != null)
        {
            hUDIngameEffects.Init(this);
        }
    }

    void Update()
    {
        TickEffects(Time.deltaTime);
        hUDIngameEffects?.RefreshRuntimeEffects();
    }

    public void RefreshComponentReferences()
    {
        GameObject g = gameObject;
        HealthManager th = g.GetComponent<HealthManager>();
        if(th == null)
        {
            return;
        }

        if (shoot != null)
        {
            shoot.OnReloadStarted -= DeactivateEffectWhenReload;
        }

        move = null;
        aim = null;
        shoot = null;
        skill = null;
        state = null;
        health = th;

        // Define behavior component by health manager type
        if(health is PlayerHealth)
        {
            move = g.GetComponent<MoveBehaviour>(); 
            aim = g.GetComponent<AimBehaviour>(); 
            shoot = g.GetComponent<ShootBehaviour>();
            health = g.GetComponent<PlayerHealth>();
            skill = g.GetComponent<SkillBehavior>();
            // bG = ? // Gadget

            // If player, Subscribe event for reload.
            if (shoot != null)
            {
                shoot.OnReloadStarted += DeactivateEffectWhenReload;
            }
        }
        // If not, it refers others which is essential
        else
        {
            // Set the case of target(not FSM based enemy)
            if(th is TargetHealth)
            {
                Debug.LogFormat("Object {0} is sandbag (by Ingame Effect Behavior).", gameObject.name);
            }
            else if(th is EnemyHealth || th is AllyHealthManager)
            {
                Debug.LogFormat("Object {0} is living (by Ingame Effect Behavior).", gameObject.name);
                state = g.GetComponent<StateController>();
            }
        }
    }

    // Unsubscribe event for reducing memory leakage
    private void OnDestroy()
    {
        if (shoot != null)
        {
            shoot.OnReloadStarted -= DeactivateEffectWhenReload;
        }
    }

    /// <summary>
    /// Adds an effect and starts the lifecycle management coroutine.
    /// </summary>
    public void AddIngameEffect(IngameEffectData iE)
    {
        /*
        if (health.dead)
        {
            return;
        }
        */

        if (iE == null)
        {
            Debug.LogWarning("Effect is not valid");
            return;
        }

        // Calculate percentage of applying negative effects
        bool acceptNegativeEffects = iE.intensity >= 1 || (UnityEngine.Random.Range(0f, 1f) > modifier.blockNegativeEffects);
        if(!acceptNegativeEffects) return;

        // Check gameObject has set as ally and able to get effects
        if(
            health is TargetHealth && 
            // If it is target
            GetComponent<AllyTargetTag>() == null && 

            // And if it has no AllyTargetTag(not treated as Ally)

            GetComponent<InitialEffectApplication>() == null 
            // And if it has no InitialEffectApplication(applied Effects at start of runtime)
        )
        {
            return;
        }
        
        IngameEffectRuntimeState existingState = FindRuntimeState(iE);
        if (existingState != null)
        {
            RefreshRuntimeState(existingState);
            hUDIngameEffects?.UpdateIngameEffects();
        }
        else
        {
            AddRuntimeState(new IngameEffectRuntimeState(iE), true);
        }
    }

    public List<IngameEffectRuntimeState> CaptureRuntimeStatus(bool includeDeactivateOnReload = true)
    {
        List<IngameEffectRuntimeState> result = new List<IngameEffectRuntimeState>();
        foreach (IngameEffectRuntimeState state in currentIngameEffectStates)
        {
            if (state?.effect == null)
                continue;

            if (!includeDeactivateOnReload && state.deactivateOnReload)
                continue;

            result.Add(state.CloneForTransfer());
        }

        return result;
    }

    public void RestoreRuntimeStatus(List<IngameEffectRuntimeState> states)
    {
        RefreshComponentReferences();
        ClearRuntimeEffects(false);

        if (states != null)
        {
            foreach (IngameEffectRuntimeState state in states)
            {
                if (state?.effect == null)
                    continue;

                IngameEffectRuntimeState restored = state.CloneForTransfer();
                AddRuntimeState(restored, true);
            }
        }

        hUDIngameEffects?.UpdateIngameEffects();
    }

    public void ClearRuntimeEffects(bool updateUI = true)
    {
        for (int i = currentIngameEffectStates.Count - 1; i >= 0; i--)
        {
            RemoveRuntimeStateAt(i, true);
        }

        if (updateUI)
        {
            hUDIngameEffects?.UpdateIngameEffects();
        }
    }

    /// <summary>
    /// Deactivate all effect which is set as deactivateOnReload (Called outside of this class)
    /// </summary>
    public void DeactivateEffectWhenReload()
    {
        // Get index first to prevent errors while traverse
        List<IngameEffectData> effectsToRemove = new List<IngameEffectData>();

        foreach (var state in currentIngameEffectStates)
        {
            if (state?.effect != null && state.deactivateOnReload)
            {
                effectsToRemove.Add(state.effect);
            }
        }

        // Deactivate effects at once
        foreach (var effect in effectsToRemove)
        {
            RemoveRuntimeState(effect, true);
        }

        // Update UI
        if (effectsToRemove.Count > 0)
        {
            hUDIngameEffects?.UpdateIngameEffects();
        }
    }

    /// <summary> Remove one effect now (ex: a buff that ends when the magazine is emptied). </summary>
    public void RemoveIngameEffect(IngameEffectData effect)
    {
        if (effect == null) return;
        RemoveRuntimeState(effect, true);
        hUDIngameEffects?.UpdateIngameEffects();
    }

    public List<IngameEffectData> GetIngameEffectsCurrent()
    {
        return currentIngameEffect;
    }

    public List<IngameEffectRuntimeState> GetIngameEffectRuntimeStates()
    {
        return currentIngameEffectStates;
    }

    private void TickEffects(float deltaTime)
    {
        bool changed = false;
        for (int i = currentIngameEffectStates.Count - 1; i >= 0; i--)
        {
            IngameEffectRuntimeState state = currentIngameEffectStates[i];
            if (state?.effect == null)
            {
                RemoveRuntimeStateAt(i, false);
                changed = true;
                continue;
            }

            if (!state.IsTimed)
                continue;

            state.remainingDuration -= deltaTime;
            if (state.remainingDuration <= 0f)
            {
                RemoveRuntimeStateAt(i, true);
                changed = true;
            }
        }

        if (changed)
        {
            hUDIngameEffects?.UpdateIngameEffects();
        }
    }

    private void AddRuntimeState(IngameEffectRuntimeState state, bool applyEffect)
    {
        if (state?.effect == null)
            return;

        currentIngameEffectStates.Add(state);
        currentIngameEffect.Add(state.effect);
        hUDIngameEffects?.UpdateIngameEffects();

        if (applyEffect)
        {
            state.effect.ApplyEffect(this);
        }
    }

    private void RefreshRuntimeState(IngameEffectRuntimeState state)
    {
        if (state?.effect == null)
            return;

        state.originalDuration = Mathf.Max(0f, state.effect.duration);
        state.remainingDuration = state.originalDuration;
        state.deactivateOnReload = state.effect.deactivateOnReload;
        state.isPersistentTrait = state.effect.isPersistentTrait;
    }

    private void RemoveRuntimeState(IngameEffectData effect, bool removeEffect)
    {
        for (int i = currentIngameEffectStates.Count - 1; i >= 0; i--)
        {
            if (currentIngameEffectStates[i]?.effect == effect)
            {
                RemoveRuntimeStateAt(i, removeEffect);
                return;
            }
        }
    }

    private void RemoveRuntimeStateAt(int index, bool removeEffect)
    {
        if (index < 0 || index >= currentIngameEffectStates.Count)
            return;

        IngameEffectRuntimeState state = currentIngameEffectStates[index];
        if (state?.effect != null)
        {
            if (removeEffect)
            {
                state.effect.RemoveEffect(this);
            }

            currentIngameEffect.Remove(state.effect);
        }

        currentIngameEffectStates.RemoveAt(index);
    }

    private IngameEffectRuntimeState FindRuntimeState(IngameEffectData effect)
    {
        if (effect == null)
            return null;

        foreach (IngameEffectRuntimeState state in currentIngameEffectStates)
        {
            if (state?.effect == null)
                continue;

            if (state.effect == effect)
                return state;

            if (state.effect.GetType() == effect.GetType()
                && state.effect.effectName == effect.effectName)
                return state;
        }

        return null;
    }

    #region Modifier
	private List<float> aimPreservarionModifiers = new();
	
	public void AddBlockNegativeEffectsModifier(float multiplier)
	{
		if (!aimPreservarionModifiers.Contains(multiplier))
		{
			aimPreservarionModifiers.Add(multiplier);
			UpdateBlockNegativeEffects();
		}
	}

	public void RemoveBlockNegativeEffectsModifier(float multiplier)
	{
		if (aimPreservarionModifiers.Contains(multiplier))
		{
			aimPreservarionModifiers.Remove(multiplier);
			UpdateBlockNegativeEffects();
		}
	}

	private void UpdateBlockNegativeEffects()
	{
		float totalMultiplier = 0.025f;

		foreach (var mod in aimPreservarionModifiers)
		{
			totalMultiplier += mod;
		}

		if(aimPreservarionModifiers.Count == 0)
		{
			totalMultiplier = 0.025f;
		}

		modifier.blockNegativeEffects = Math.Clamp(totalMultiplier, 0.0f, 0.5f);	
	}
    #endregion
}

/// <summary>
/// Interface sending current ingame effect of specific object.
/// </summary>
public interface IIngameEffectsStorage
{
    public List<IngameEffectData> GetIngameEffectsCurrent();
    public List<IngameEffectRuntimeState> GetIngameEffectRuntimeStates();
}
