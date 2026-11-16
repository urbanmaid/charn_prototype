using UnityEngine;
using UnityEngine.AI;
using EnemyAI;
using System.Collections;

public class AllyHealthManager : HealthManager, IPersonaReceiver
{
    [Header("Death")]
    public bool isPreservedWhenKilled;
    [SerializeField] private bool copyDeathPoseToRagdollAvatar = true;

    public float health;
    public float shieldHealth;
    private float totalHealth;
    private float totalShieldHealth;
    private float baseShieldHealth;

    public float TotalHealth => totalHealth;
    public float TotalShieldHealth => totalShieldHealth;

    [Header("Health HUD")]
    public GameObject healthHUD;

    private Transform _hud;
    private RectTransform _healthBar;
    private float _originalBarScale;
    private HealthBillboardManager _healthUI;
    private StateController _controller;
    private bool deathApplied;
    private Coroutine reviveRefresh;

    [HideInInspector] public bool wasHitThisFrame;
    [HideInInspector] public Vector3 lastHitDirection;

    void Awake()
    {
        _controller = GetComponent<StateController>();
    }

    void Start()
    {
        if (healthHUD != null)
        {
            _hud = Instantiate(healthHUD, transform).transform;
            _healthBar = _hud.Find("Bar").GetComponent<RectTransform>();
            _healthUI = _hud.GetComponent<HealthBillboardManager>();
            _originalBarScale = _healthBar.sizeDelta.x;
        }
    }

    public void ApplyPersona(PersonaData data)
    {
        totalHealth = data.genericStatData.health;
        health = totalHealth;
        baseShieldHealth = data.genericStatData.healthOfPreloadedShield;
        totalShieldHealth = baseShieldHealth;
        shieldHealth = totalShieldHealth;
        UpdateHealthBar();
    }

    public void RestoreRuntimeHealth(float currentHealth, float currentShieldHealth)
    {
        RestoreRuntimeHealth(currentHealth, currentShieldHealth, totalShieldHealth);
    }

    public void RestoreRuntimeHealth(float currentHealth, float currentShieldHealth, float maxShieldHealth)
    {
        health = Mathf.Clamp(currentHealth, 0f, totalHealth);
        totalShieldHealth = Mathf.Max(baseShieldHealth, Mathf.Max(maxShieldHealth, currentShieldHealth));
        shieldHealth = Mathf.Clamp(currentShieldHealth, 0f, totalShieldHealth);
        if (health <= 0f) Kill();
        else if (!deathApplied) dead = false;
        UpdateHealthBar();
    }

    public override void TakeDamage
    (
        Vector3 location,
        Vector3 direction,
        float damage,
        Collider bodyPart = null,
        GameObject origin = null,
        DamageType damageType = DamageType.Normal
    )
    {
        if (dead) return;
        float healthBefore = health;
        float shieldBefore = shieldHealth;

        // Damage reduction from ingame effects (same rule as PlayerHealth)
        if (modifier != null)
        {
            if (damageType == DamageType.Normal)
                damage *= 1 - modifier.damageDamp;
            else if (damageType == DamageType.Skill)
                damage *= 1 - modifier.damageDampSkill;
        }

        if (shieldHealth > 0f)
        {
            if (shieldHealth >= damage)
            {
                shieldHealth -= damage;
                damage = 0f;
            }
            else
            {
                damage -= shieldHealth;
                shieldHealth = 0f;
            }
        }

        health -= damage;
        Charm.Mechanism.Missions.CombatTelemetry.LostHealth(this, healthBefore, health);
        CombatKillFeedback.Confirm(origin, this, healthBefore - health, shieldBefore - shieldHealth,
            health <= 0f, shieldBroken: shieldBefore > 0f && shieldHealth <= 0f && shieldBefore > shieldHealth);
        wasHitThisFrame = true;
        lastHitDirection = direction;

        try
        {
            if (health > 0f && (health < healthBefore || shieldHealth < shieldBefore))
                _controller?.ReportDamageSource(origin, damageType);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        _healthUI?.SetVisible();
        UpdateHealthBar();

        if (health <= 0f)
        {
            health = 0f;
            Kill();
        }
    }

    private void UpdateHealthBar()
    {
        if (_healthBar == null) return;
        float scale = totalHealth > 0f ? health / totalHealth : 0f;
        _healthBar.sizeDelta = new Vector2(scale * _originalBarScale, _healthBar.sizeDelta.y);
    }

    private void RunKillStep(System.Action action, Object context = null)
    {
        try
        {
            action?.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, context != null ? context : this);
        }
    }

    public void Kill()
    {
        if (deathApplied) return;
        CancelReviveInitialization();
        deathApplied = true;
        health = 0f;
        dead = true;
        ReviveWindow.RecordDeath(this);
        Charm.Mechanism.Missions.CombatTelemetry.Downed(this);

        RunKillStep(() =>
        {
            GetComponent<EnemyAnimation>()?.SetPersonaWeaponActive(false);
        });

        RunKillStep(() =>
        {
            GetComponent<PersonaBehavior>()?.ApplyRagdollAvatar(copyDeathPoseToRagdollAvatar);
        });

        // Turn off Controller
        RunKillStep(() =>
        {
            if (_controller != null) _controller.enabled = false;
        });

        // Turn off NavMeshAgent
        RunKillStep(() =>
        {
            var navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                return;
            }

            if (isPreservedWhenKilled)
            {
                navAgent.enabled = false;
            }
            else
            {
                Destroy(navAgent);
            }
        });

        // Turn off Animator
        RunKillStep(() =>
        {
            var animator = GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
        });

        // Turn off Collider
        RunKillStep(() =>
        {
            if (!isPreservedWhenKilled)
            {
                Collider colRoot = GetComponent<Collider>();

                // How?
                // Child collider which is grouped by ragdoll should be moved freeliy
                // But root object should be followed of child ragdoll root location

                foreach (Collider col in GetComponents<Collider>())
                {
                    if (col != null && colRoot) col.enabled = false;
                }
            }
        });

        // Make some rigidbody collsion effect when dead
        RunKillStep(() =>
        {
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                RunKillStep(() =>
                {
                    if (rb == null)
                    {
                        return;
                    }

                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    if (lastHitDirection != Vector3.zero)
                    {
                        rb.AddForce(2f * lastHitDirection.normalized, ForceMode.Impulse);
                    }
                }, rb);
            }
        });

        // Get component of rigidbody and set it disabled
        RunKillStep(() =>
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if(rb)
            {
                rb.isKinematic = true;
            }

            Collider colRoot = GetComponent<Collider>();
            if(colRoot)
            {
                colRoot.enabled = false;
            }
        });

        // Remove HUD
        RunKillStep(() =>
        {
            if (_hud != null) Destroy(_hud.gameObject);
        });

        // Destroy self
        RunKillStep(() =>
        {
            foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            {
                RunKillStep(() =>
                {
                    if (this == mb || mb is PersonaBehavior)
                    {
                        return;
                    }

                    if (isPreservedWhenKilled)
                    {
                        mb.enabled = false;
                    }
                    else
                    {
                        Destroy(mb);
                    }
                }, mb != null ? mb : this);
            }
        });
    }

    public override bool HealDamage(float amount)
    {
        if (dead || deathApplied) return false;
        if (health >= totalHealth) return false;
        float beforeHealing = health;
        health = Mathf.Min(health + amount, totalHealth);
        Charm.Mechanism.Missions.CombatTelemetry.Healed(this, beforeHealing, health);
        dead = health <= 0f;
        _healthUI?.SetVisible();
        UpdateHealthBar();
        return true;
    }

    public override bool HealByRatio(float ratio)
    {
        return HealDamage(totalHealth * Mathf.Clamp01(ratio));
    }

    public override bool RestoreShieldByRatio(float ratio)
    {
        if (totalShieldHealth <= 0f) return false;

        float before = shieldHealth;
        shieldHealth = Mathf.Min(shieldHealth + totalShieldHealth * Mathf.Clamp01(ratio), totalShieldHealth);
        UpdateHealthBar();
        return shieldHealth > before;
    }

    public override bool ReviveWithHealthRatio(float ratio)
    {
        if (!(ratio > 0f) || totalHealth <= 0f) return false;
        if (!dead && !deathApplied && health > 0f) return HealByRatio(ratio);

        CancelReviveInitialization();
        _controller = GetComponent<StateController>();
        if (_controller != null)
        {
            _controller.SetAnimationResetPending(this, true);
            // 사망 전에 진행하던 장전 클립은 복원되지 않는다. 부활 완료 후 다시 판단한다.
            _controller.reloading = false;
        }
        dead = false;
        deathApplied = false;
        health = totalHealth * Mathf.Clamp01(ratio);
        try
        {
            GetComponent<PersonaBehavior>()?.ApplyAliveAvatar();

            NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = true;
                if (navAgent.isOnNavMesh)
                {
                    navAgent.ResetPath();
                    navAgent.isStopped = false;
                }
            }

            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            EnemyAnimation enemyAnimation = GetComponent<EnemyAnimation>();
            enemyAnimation?.RefreshAnimatorReferences();
            ReapplyPersonaWeapon();

            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb != null)
                {
                    // 부활 시 움직이던 몸체만 먼저 정지시키고 Kinematic으로 전환한다.
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                }
            }

            if (_hud == null && healthHUD != null)
            {
                _hud = Instantiate(healthHUD, transform).transform;
                _healthBar = _hud.Find("Bar").GetComponent<RectTransform>();
                _healthUI = _hud.GetComponent<HealthBillboardManager>();
                _originalBarScale = _healthBar.sizeDelta.x;
            }

            UpdateHealthBar();
            ReviveWindow.RestoreDeathTime(this, null);
            Charm.Mechanism.Missions.CombatTelemetry.Revived(this);
            reviveRefresh = StartCoroutine(RefreshAnimatorAfterRevive());
            return true;
        }
        catch
        {
            CancelReviveInitialization();
            throw;
        }
    }

    private void OnDisable() => CancelReviveInitialization();

    private void CancelReviveInitialization()
    {
        if (reviveRefresh != null) StopCoroutine(reviveRefresh);
        reviveRefresh = null;
        if (_controller != null) _controller.SetAnimationResetPending(this, false);
    }

    private void RestoreDisabledBehaviours()
    {
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb != null)
            {
                mb.enabled = true;
            }
        }

        _controller = GetComponent<StateController>();
    }

    private IEnumerator RefreshAnimatorAfterRevive()
    {
        yield return null;
        try
        {
            if (dead || health <= 0f) yield break;
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }
            GetComponent<EnemyAnimation>()?.RefreshAnimatorReferences();
            ReapplyPersonaWeapon();
            // 외형/무기/물리 복원이 끝난 후 행동 컴포넌트를 재개한다.
            RestoreDisabledBehaviours();
        }
        finally
        {
            reviveRefresh = null;
            if (_controller != null) _controller.SetAnimationResetPending(this, false);
        }
    }

    private void ReapplyPersonaWeapon()
    {
        EnemyAnimation enemyAnimation = GetComponent<EnemyAnimation>();
        if (enemyAnimation == null)
        {
            return;
        }

        PersonaBehavior personaBehavior = GetComponent<PersonaBehavior>();
        PersonaData personaData = personaBehavior != null ? personaBehavior.GetPersona() : null;
        if (personaData != null)
        {
            enemyAnimation.ApplyPersona(personaData);
        }
        else
        {
            enemyAnimation.SetPersonaWeaponActive(true);
        }
    }

    public override void RestoreDamage()
    {
        HealDamage(totalHealth);
    }

    public override void AddShield(float value)
    {
        totalShieldHealth += value;
        shieldHealth += value;
        UpdateHealthBar();
    }

    public override void RemoveShield(float value)
    {
        totalShieldHealth = Mathf.Max(totalShieldHealth - value, baseShieldHealth);
        shieldHealth = Mathf.Max(shieldHealth - value, 0f);
        shieldHealth = Mathf.Min(shieldHealth, totalShieldHealth);
        UpdateHealthBar();
    }

    public float GetHealthRatio()
    {
        return totalHealth > 0f ? health / totalHealth : 1f;
    }

    void LateUpdate()
    {
        // Public health can also be changed by external systems/Inspector.
        if ((dead || health <= 0f) && !deathApplied) Kill();
        wasHitThisFrame = false;
    }
}
