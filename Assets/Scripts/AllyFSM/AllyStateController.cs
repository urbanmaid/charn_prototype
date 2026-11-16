using EnemyAI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Interface used by commands and target selectors to assign a hostile target
/// to a companion. It deliberately exposes no FSM state or movement controls.
/// </summary>
public interface IAllyEnemyTargetReceiver
{
    GameObject EnemyTarget { get; }
    bool TrySetEnemyTarget(GameObject target, bool renewCommand = false);
    void ClearEnemyTarget();
}

/// <summary>
/// Companion-only adapter. Combat state, navigation and sensing stay on the
/// normal TPSBundle StateController beside this component.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(StateController))]
public sealed class AllyStateController : MonoBehaviour, IPersonaReceiver, IAllyEnemyTargetReceiver
{
    [SerializeField, Tooltip("현재 페르소나의 아군용 자세 설정 사용. 미설정 페르소나는 기존 경로를 유지합니다.")]
    private bool enablePersonaPoseCorrection;
    public bool SupportsPersonaPoseCorrection => enablePersonaPoseCorrection;
    public void EnablePersonaPoseCorrection() { enablePersonaPoseCorrection = true; }
    [Header("Action and target as 'induction rather than immediate'")]
    [SerializeField] private StateController combatController;
    [FormerlySerializedAs("priorityTarget")]
    [SerializeField] private GameObject enemyTarget;
    // Preserve finite player reserves without changing the AI reload policy.
    private PersonaData ammoPersona;
    private AmmoRuntimeStatus transferredAmmo;
    private bool hasTransferredAmmo;

    // 스킬 실행 컴포넌트가 없는 아군도 교환된 캐릭터의 상태를 개체별로 보관한다.
    // 공유 PersonaData에는 쓰지 않으므로 같은 페르소나인 아군끼리도 값이 섞이지 않는다.
    private PersonaData skillPersona;
    private SkillRuntimeStatus transferredSkills;
    private float skillsRestoredAt;

    public SkillRuntimeStatus CaptureSkillStatus()
    {
        // 기존 SkillBehavior와 같은 게임 시간을 사용한다. 일시정지 중에는 감소하지 않는다.
        float elapsed = Mathf.Max(0f, Time.time - skillsRestoredAt);
        return new SkillRuntimeStatus
        {
            skill1CooldownRemaining = Mathf.Max(0f, transferredSkills.skill1CooldownRemaining - elapsed),
            skill2CooldownRemaining = Mathf.Max(0f, transferredSkills.skill2CooldownRemaining - elapsed),
            gameChangerCooldownRemaining = Mathf.Max(0f, transferredSkills.gameChangerCooldownRemaining - elapsed),
            gameChangerPoint = transferredSkills.gameChangerPoint
        };
    }

    public void RestoreSkillStatus(SkillRuntimeStatus status)
    {
        transferredSkills = status;
        transferredSkills.gameChangerPoint = Mathf.Max(0, status.gameChangerPoint);
        skillsRestoredAt = Time.time;
    }

    private System.Collections.Generic.List<GameObject> transferredGadgets = new();
    public System.Collections.Generic.List<GameObject> CaptureGadgetObjects() => new(transferredGadgets);
    public void RestoreGadgetObjects(System.Collections.Generic.List<GameObject> gadgets)
    {
        transferredGadgets = gadgets == null ? new() : gadgets.FindAll(g => g != null);
        if (transferredGadgets.Count > 4) throw new System.ArgumentException("Gadget inventory exceeds capacity.");
    }

    public StateController CombatController => combatController;
    public GameObject EnemyTarget => enemyTarget;

    public AmmoRuntimeStatus CaptureAmmoStatus()
    {
        var status = hasTransferredAmmo ? transferredAmmo : new AmmoRuntimeStatus
        {
            hasWeapon = true,
            activeWeapon = 1,
            reserveBullets = -1
        };
        ResolveCombatController();
        var controller = combatController;
        status.currentMagazine = controller != null ? controller.bullets : 0;
        return status;
    }

    public void RestoreAmmoStatus(AmmoRuntimeStatus status)
    {
        transferredAmmo = status;
        hasTransferredAmmo = true;
        ResolveCombatController();
        var controller = combatController;
        if (controller == null) return;
        // Swap Rebind discards the outgoing reload clip. Cancel its pending state.
        controller.reloading = false;
        controller.bullets = Mathf.Max(0, status.currentMagazine);
        var animator = GetComponent<Animator>();
        if (animator != null) animator.ResetTrigger("Reload");
    }

    private void Awake()
    {
        ResolveCombatController();
    }

    private void Start()
    {
        // The companion prefab defers native Agent registration until the stage
        // has been created and PlayerStartpoint has placed the squad (Awake).
        var agent = GetComponent<NavMeshAgent>();
        if (agent == null || agent.enabled) return;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, filter))
        {
            transform.position = hit.position;
            agent.enabled = true;
        }
        else
        {
            Debug.LogError($"{name}: squad start position is not on the agent's NavMesh.", this);
        }
    }

    private void LateUpdate()
    {
        // Include Unity's destroyed-object case, which compares equal to null
        // while still retaining a managed reference.
        if (!ReferenceEquals(enemyTarget, null) &&
            (enemyTarget == null || (ResolveCombatController() &&
             !combatController.IsValidCombatTarget(enemyTarget.transform))))
            ClearEnemyTarget();
    }

    /// <summary>
    /// Visual and health receivers apply their own PersonaData. This adapter
    /// only applies local movement data, never modifying shared GeneralStats.
    /// </summary>
    public void ApplyPersona(PersonaData data)
    {
        if (data == null)
            return;

        if (skillPersona != data)
        {
            skillPersona = data;
            transferredSkills = default;
            skillsRestoredAt = Time.time;
        }

        if (ammoPersona != data)
        {
            ammoPersona = data;
            hasTransferredAmmo = false;
            transferredAmmo = default;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed = data.genericStatData.moveSpeedMax;
    }

    public bool TrySetEnemyTarget(GameObject target, bool renewCommand = false)
    {
        if (!ResolveCombatController() || target == null)
            return false;

        Transform root = CombatTargetOwner.Resolve(target.transform);
        HealthManager health = root.GetComponent<HealthManager>();
        if (health == null || health.dead || !combatController.IsValidCombatTarget(root))
            return false;

        if (enemyTarget == root.gameObject && !renewCommand) return true;
        if (!combatController.SetCommandTarget(root, StateController.GetAimPoint(root), renewCommand)) return false;
        enemyTarget = root.gameObject;

        return true;
    }

    public void ClearEnemyTarget()
    {
        Transform previous = enemyTarget != null ? CombatTargetOwner.Resolve(enemyTarget.transform) : null;
        enemyTarget = null;
        if (ResolveCombatController())
        {
            combatController.ClearCommandTarget(previous);
            combatController.ClearInvalidCombatTarget();
        }
    }

    private bool ResolveCombatController()
    {
        if (combatController == null)
            combatController = GetComponent<StateController>();

        return combatController != null;
    }
}

