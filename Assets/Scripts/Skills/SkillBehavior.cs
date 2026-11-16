using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Behavior to apply skills on player object, need PersonaBehavior and PersonaData to be activated
/// </summary>
public class SkillBehavior : GenericBehaviour, IPersonaReceiver
{
    private Dictionary<SkillSlot, SkillData> skills;
    protected Dictionary<SkillSlot, float> cooldowns;//private 에서 protected로 김동주
    public int gameChangerPoint = 0;
    private int gameChangerPointRequirement = 0;
    private Coroutine updateGameChangerCoroutine;

    // Cache components for fast access in OnExecute
    public IngameEffectBehavior effectBehavior { get; private set; }

    // Inputs
    protected InputAction skill1Button, skill2Button, gameChangerButton;//private 에서 protected로 김동주
    public bool IsPreparingGrenade { get; private set; }

	[Header("UI")]
	[SerializeField] private HUDSkills hUDSkills;

    [Header("Interaction between skill usage and stage object")]
    public List<InteractionBySkill> OnSkillUsedInteraction;
    [Serializable]
    public struct InteractionBySkill
    {
        public SkillSlot slot;
        public UnityEvent interactionEvent;
    }


    // Start and Init Input
    void Start()
    {
        InitiaizeInput();
        effectBehavior = GetComponent<IngameEffectBehavior>();

        OnStart(); //AllySkillBehavior.cs 아군 PA 초기화용 플레이어에서는 빈스크립트

        if(hUDSkills)
        {
            hUDSkills.Init(this);
        }
    }

	protected override void InitiaizeInput()
	{
		var pInput = GetComponent<PlayerInput>();
		var actions = pInput ? pInput.actions : null;
		skill1Button = actions ? actions.FindAction("Skill1", false) : null;
		skill2Button = actions ? actions.FindAction("Skill2", false) : null;
		gameChangerButton = actions ? actions.FindAction("GameChanger", false) : null;
	}

    public void ApplyPersona(PersonaData data)
    {
        // Apply Skills and Game Changer
        PersonaDataSkill dataSkill = data ? data.personaDataSkill : null;
        ApplySkill(dataSkill);

        // Game Changer Point Definition
        gameChangerPointRequirement = dataSkill && dataSkill.gameChanger != null
            ? Mathf.FloorToInt(dataSkill.gameChanger.requiredPoint) : 0;
        gameChangerPoint = 0;
        if (updateGameChangerCoroutine != null)
        {
            StopCoroutine(updateGameChangerCoroutine);
        }
        updateGameChangerCoroutine = null;
        if (gameChangerPointRequirement > 0 && isActiveAndEnabled)
            updateGameChangerCoroutine = StartCoroutine(UpdateGameChanger());
    }

    void ApplySkill(PersonaDataSkill ds)
    {
        IsPreparingGrenade = false;
        // Unequip previous persona skills (ex: remove targeting indicator)
        if (skills != null)
        {
            foreach (var pair in skills)
                pair.Value?.logic?.OnUnequip(this, pair.Key);
        }

        // Amelia's legacy asset stores Supply Boost and Reinforced Materials in separate slots.
        // Present them as one Q skill and use the freed E slot for the cluster grenade.
        bool mergeAmeliaSupply = ds != null && ds.skill1 != null &&
            (ds.skill1.logic is AmeliaSkillExtendMagazine || ds.skill1.logic is AmeliaSkillSupplyOverdrive);
        SkillData skill1 = ds ? ds.skill1 : null;
        if (mergeAmeliaSupply && skill1 is NormalSkillData original)
        {
            skill1 = new NormalSkillData
            {
                skillName = "Supply Overdrive",
                icon = original.icon,
                desc = "You and allies within 5m get a fresh 250% magazine. Its rounds deal 160% damage until empty or reloaded.",
                cooldown = 16f,
                logic = original.logic
            };
        }

        SkillData skill2 = ds ? ds.skill2 : null;
        if (mergeAmeliaSupply)
        {
            skill2 = new NormalSkillData
            {
                skillName = "Cluster Grenade",
                icon = ds.skill2 != null ? ds.skill2.icon : null,
                desc = "Throw a cluster grenade. The main blast releases four damaging submunitions.",
                cooldown = 18f,
                logic = new AmeliaSkillClusterGrenade()
            };
        }

        // Active
        skills = new Dictionary<SkillSlot, SkillData>
        {
            { SkillSlot.Skill1, skill1 },
            { SkillSlot.Skill2, skill2 },
            { SkillSlot.GameChanger, ds ? ds.gameChanger : null}
        };

        foreach (var pair in skills)
            pair.Value?.logic?.OnEquip(this, pair.Key);

        // Set Cooldown
        cooldowns = new Dictionary<SkillSlot, float>();
        foreach (var slot in skills.Keys)
            cooldowns[slot] = 0f;

        // Passive
        if (ds && ds.passive != null)
        {
            ds.passive.Execute(new SkillContext
            {
                caster = gameObject
            });
        }

        if (hUDSkills)
        {
            hUDSkills.Init(this);
        }
    }

    void Update()
    {
        if (IsPreparingGrenade)
        {
            var health = GetComponent<HealthManager>();
            if (!CompareTag("Player") || !GameplayCursor.AllowsGameplayInput || Time.timeScale <= 0f ||
                (health && health.dead) || skill2Button == null || !skill2Button.enabled)
            {
                IsPreparingGrenade = false;
                return;
            }
            if (skill2Button.WasReleasedThisFrame())
            {
                IsPreparingGrenade = false;
                UseSkill(SkillSlot.Skill2);
            }
            return;
        }
        // IsPressed() 대신 WasPressedThisFrame() 사용
        if(skill1Button?.WasPressedThisFrame() == true)
        {
            UseSkill(SkillSlot.Skill1);
        }
        else if(skill2Button?.WasPressedThisFrame() == true)
        {
            if (CompareTag("Player") && skills != null &&
                skills.TryGetValue(SkillSlot.Skill2, out var grenade) && grenade?.logic is AmeliaSkillClusterGrenade)
            {
                var health = GetComponent<HealthManager>();
                IsPreparingGrenade = GameplayCursor.AllowsGameplayInput && Time.timeScale > 0f &&
                    !(health && health.dead) && IsSkillReady(SkillSlot.Skill2) &&
                    !AmeliaGrenadeThrowMotion.Active(gameObject);
            }
            else UseSkill(SkillSlot.Skill2);
        }
        else if(gameChangerButton?.WasPressedThisFrame() == true)
        {
            UseSkill(SkillSlot.GameChanger);
        }
    }

    void OnDisable() { IsPreparingGrenade = false; }

    /// <summary>
    /// Use skill with the input skill slot, called by input
    /// </summary>
    /// <param name="slot"> input skill code for selecting skill </param>
    public void UseSkill(SkillSlot slot)
    {
        if (Time.timeScale <= 0f) return;
        // Check if the skill is not assigned on slot (Persona has no skill)
        if (skills == null || !skills.ContainsKey(slot) || skills[slot] == null || skills[slot].logic == null)
        {
            Debug.LogWarning("There is no skill assigned on this slot: " + slot);
            return;    
        }

        // Assign the skill
        SkillData data = skills[slot];

        // Check the type of skill and set the condition of usage
        if (data is NormalSkillData)
        {
            // If it is normal skill, check cooldown time
            if (cooldowns[slot] > Time.time)
            {
                Debug.LogFormat("Estimated skill cooldown time: {0:F1}s", cooldowns[slot] - Time.time);
                return;
            }
        }
        else if (data is GameChangerData gcData)
        {
            // If it is gamechanger, check point
            if (gameChangerPoint < gcData.requiredPoint)
            {
                Debug.LogFormat("Game changer point is not enough!");
                return;
            }
        }

        // Run skill as context
        var context = new SkillContext
        {
            caster = gameObject,
            target = GetTargetObject(),
            
            position = transform.position,
            direction = GetSkillDirection(),
            owner = this
        };

        // Skill can refuse usage (ex: no valid target), then cooldown and point are kept
        if (!data.logic.CanExecute(context))
        {
            return;
        }
        data.Execute(context);
        CombatSkillAccent.Cast(context, data.logic, slot);

        // Reset skill cooldown or gamechanger point
        if (data is NormalSkillData normal)
        {
            cooldowns[slot] = Time.time + normal.cooldown;
        }
        else if (data is GameChangerData)
        {
            gameChangerPoint = 0;           
        }

        OnSkillUsed(slot);//AllySkillBehavior.cs에서 씀 김동주

        // Interaction Between skill usage and interaction object
        if (OnSkillUsedInteraction == null || OnSkillUsedInteraction.Count == 0)
        {
            return;
        }
        else
        {
            foreach(InteractionBySkill s in OnSkillUsedInteraction)
            {
                if(s.slot == slot)
                {
                    s.interactionEvent?.Invoke();
                    return;
                }
            }
        }
    }

    private GameObject GetTargetObject()
    {
        GameObject targetObject = null;

        // As a case used by player
        if(gameObject.CompareTag("Player"))
        {
            ThirdPersonOrbitCam tpo = GetComponentInChildren<ThirdPersonOrbitCam>();
            if(tpo)
            {
                targetObject = tpo.targetObject;
            }
        }
        else // As a case used by ally
        {
            // TODO
        }

        return targetObject;
    }

    private Vector3 GetSkillDirection()
    {
        Vector3 d = Vector3.zero;

        // As a case used by player
        if(gameObject.CompareTag("Player"))
        {
            ThirdPersonOrbitCam tpo = GetComponentInChildren<ThirdPersonOrbitCam>();
            if(tpo)
            {
                d = tpo.transform.forward;
            }
        }
        else // As a case used by ally
        {
            // TODO
        }

        return d;
    }

    private IEnumerator UpdateGameChanger()
    {
        while(true)
        {
            yield return new WaitForSeconds(1f);
            if(gameChangerPointRequirement > gameChangerPoint)
            {
                gameChangerPoint++;
            }
        }
    }

    private int rewardSuppressionDepth;

    // Suppress only damage/kill rewards from this ultimate's impact, not unrelated shots.
    public void WithoutGameChangerReward(System.Action applyDamage)
    {
        rewardSuppressionDepth++;
        try { applyDamage(); }
        finally { rewardSuppressionDepth--; }
    }

    internal void AddGameChangerPoint(int value)
    {
        if (rewardSuppressionDepth > 0) return;
        if(gameChangerPointRequirement > gameChangerPoint)
        {
            gameChangerPoint += value;
        }
    }

    public SkillRuntimeStatus CaptureRuntimeStatus()
    {
        return new SkillRuntimeStatus
        {
            skill1CooldownRemaining = GetCooldownRemaining(SkillSlot.Skill1),
            skill2CooldownRemaining = GetCooldownRemaining(SkillSlot.Skill2),
            gameChangerCooldownRemaining = GetCooldownRemaining(SkillSlot.GameChanger),
            gameChangerPoint = gameChangerPoint
        };
    }

    public void RestoreRuntimeStatus(SkillRuntimeStatus status)
    {
        if (cooldowns != null)
        {
            SetCooldownRemaining(SkillSlot.Skill1, status.skill1CooldownRemaining);
            SetCooldownRemaining(SkillSlot.Skill2, status.skill2CooldownRemaining);
            SetCooldownRemaining(SkillSlot.GameChanger, status.gameChangerCooldownRemaining);
        }

        gameChangerPoint = Mathf.Max(0, status.gameChangerPoint);
    }

    private float GetCooldownRemaining(SkillSlot slot)
    {
        if (cooldowns == null || !cooldowns.TryGetValue(slot, out float readyAt))
        {
            return 0f;
        }

        return Mathf.Max(0f, readyAt - Time.time);
    }

    private void SetCooldownRemaining(SkillSlot slot, float remaining)
    {
        if (cooldowns == null || !cooldowns.ContainsKey(slot))
        {
            return;
        }

        cooldowns[slot] = remaining > 0f ? Time.time + remaining : 0f;
    }

    protected virtual void OnStart() { }

    // UI Transition
    public Dictionary<SkillSlot, SkillData> GetSkills()
    {
        return skills;
    }

    public Dictionary<SkillSlot, float> GetCooldowns()
    {
        return cooldowns;
    }

    public bool IsSkillReady(SkillSlot slot)
    {
        if (skills == null || !skills.TryGetValue(slot, out SkillData data) || data == null)
            return false;

        if (data is GameChangerData gcData)
            return gameChangerPoint >= gcData.requiredPoint;

        return cooldowns == null || !cooldowns.TryGetValue(slot, out float readyAt) || readyAt <= Time.time;
    }
    protected virtual void OnSkillUsed(SkillSlot slot) { }
}

[Serializable]
public struct SkillRuntimeStatus
{
    public float skill1CooldownRemaining;
    public float skill2CooldownRemaining;
    public float gameChangerCooldownRemaining;
    public int gameChangerPoint;
}
