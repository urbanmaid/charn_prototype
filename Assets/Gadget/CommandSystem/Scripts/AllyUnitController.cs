using EnemyAI;
using UnityEngine;
using UnityEngine.AI;
public class AllyUnitController : MonoBehaviour
{
    //public string HeroName;
    NavMeshAgent agent;
    private IAllyEnemyTargetReceiver enemyTargetReceiver;
    enum AIState
    {
        Idle,
        Move,
        Attack,
        Retreat,
        Death
    }

    AIState currentState;

    Vector3 targetPosition;
    Transform attackTarget;
    /*
    private void OnEnable()
    {
        CommandManager.Instance.OnCommandIssued += HandleCommand;
    }*/

    private void OnDisable()
    {
        if (CommandManager.Instance != null)
            CommandManager.Instance.OnCommandIssued -= HandleCommand;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();        
    }
    private void Start()
    {
        //HeroName = GetComponent<PersonaBehavior>().GetPersona().personaName;
        enemyTargetReceiver = GetComponent<IAllyEnemyTargetReceiver>();

        if (CommandManager.Instance != null)
            CommandManager.Instance.OnCommandIssued += HandleCommand;
        else
            Debug.LogError("CommandManager.Instance NULL");
    }
    

    void HandleCommand(CommandData cmd)
    {
       
        //  선택된 유닛만 명령 받기
        if (cmd.Type !=CommandType.Attack)//공격은 다 같이
            return;

        Debug.Log("HandleCommand");
        switch (cmd.Type)
        {
            case CommandType.Move:
                currentState = AIState.Move;
                targetPosition = cmd.Position;
                break;

            case CommandType.Attack:
                Debug.Log("[HandleCommand] ATTACK 처리");
                currentState = AIState.Attack;
                attackTarget = CombatTargetOwner.Resolve(cmd.Target);
                enemyTargetReceiver ??= GetComponent<IAllyEnemyTargetReceiver>();
                if (attackTarget == null || enemyTargetReceiver == null ||
                    !enemyTargetReceiver.TrySetEnemyTarget(attackTarget.gameObject, true)) EndAttackCommand();
                break;

            case CommandType.Retreat:
                currentState = AIState.Retreat;
                break;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case AIState.Move:
                Move();
                break;

            case AIState.Attack:
                Attack();
                break;
        }
    }

    void Move()
    {
        agent.SetDestination(targetPosition);

        if (!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            currentState = AIState.Idle;
        }
    }

    void Attack()
    {
        if (attackTarget == null || !attackTarget.gameObject.activeInHierarchy)
        {
            EndAttackCommand();
            return;
        }

        var hm = attackTarget.GetComponent<HealthManager>();
        if (hm != null && hm.dead)
        {
            EndAttackCommand();
            return;
        }

        if (enemyTargetReceiver == null || enemyTargetReceiver.EnemyTarget != attackTarget.gameObject)
            EndAttackCommand();
    }

    private void EndAttackCommand()
    {
        Transform previousTarget = attackTarget;
        attackTarget = null;
        currentState = AIState.Idle;

        // Another source may already have assigned a replacement to the adapter.
        // Retire only this command's assignment; the FSM also protects live targets.
        GameObject assignedTarget = enemyTargetReceiver?.EnemyTarget;
        if (assignedTarget == null || (previousTarget != null &&
            CombatTargetOwner.IsSame(assignedTarget.transform, previousTarget)))
            enemyTargetReceiver?.ClearEnemyTarget();
    }
}
