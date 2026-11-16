using EnemyAI;
using System.Collections;
using UnityEngine;

public class AIFOV : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewDistance = 10f;
    public float viewAngle = 90f;

    [Header("Layer Masks")]
    public LayerMask targetMask;
    public LayerMask obstacleMask;

    [Header("Current Target")]
    public Transform currentTarget;

    // --- Health by PAHealthManager ---
    public float maxHP = 100f;
    public float currentHP = 100f;

    private bool isLowHPTriggered = false;
    private bool isDeadTriggered = false;
    // -------------------
    private PersonaData paData;
    private AllyHealthManager PAHealthManager;
    void Start()
    {
        //StartCoroutine(TargetCheckRoutine());
        paData = GetComponent<PersonaBehavior>()?.GetPersona();
        PAHealthManager = GetComponent<AllyHealthManager>();
        maxHP = paData.genericStatData.health;
    }

    void Update()
    {
        checkPAHealth();                
    }
    /*
    IEnumerator TargetCheckRoutine()
    {
        while (true)
        {            
            Transform newTarget = FindTarget();

            // 타겟이 바뀌었을 때만 실행
            if (newTarget != currentTarget)
            {
                currentTarget = newTarget;

                if (currentTarget != null)
                {
                    

                    if (paData != null && CommandManager.Instance != null)
                    {
                        Sprite paPortrait = paData.icon;
                        string paName = paData.personaName;

                        CommandManager.Instance
                            .FeedBackByMessage(
                                "Enemy Found!",
                                paPortrait,
                                paName
                            );
                    }
                }
            }

            // 0.2초마다 검사
            yield return new WaitForSeconds(0.5f);
        }
    }

    Transform FindTarget()
    {
        Collider[] targets =
            Physics.OverlapSphere(transform.position, viewDistance, targetMask);

        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider col in targets)
        {
            Transform targetRoot = col.transform.root;

            // 거리/각도 계산은 collider 위치 기준
            Vector3 targetPos = col.bounds.center;

            Vector3 dir =
                (targetPos - transform.position).normalized;

            float dist =
                Vector3.Distance(transform.position, targetPos);

            if (targetRoot == transform.root)
                continue;

            float angle = Vector3.Angle(transform.forward, dir);

            if (angle > viewAngle * 0.5f)
                continue;

            if (!Physics.Raycast(
                    transform.position,
                    dir,
                    dist,
                    obstacleMask))
            {
                if (dist < closestDistance)
                {
                    closestDistance = dist;

                    // 저장은 root
                    bestTarget = targetRoot;
                }
            }
        }

        return bestTarget;
    }
    */
   
    void checkPAHealth()
    {        
        currentHP = PAHealthManager.health;
        
        float hpPercent = currentHP / maxHP;

        // 죽음
        if (currentHP <= 0 && !isDeadTriggered)
        {
            isDeadTriggered = true;

            if (paData != null && CommandManager.Instance != null)
            {
                Sprite paPortrait = paData.icon;
                string paName = paData.personaName;

                CommandManager.Instance.FeedBackByMessage("Dead when operation. Revive with proper gadget.", paPortrait, paName);
            }                
        }

        // 체력 10% 이하
        else if (hpPercent <= 0.1f && !isLowHPTriggered)
        {
            isLowHPTriggered = true;

            if (paData != null && CommandManager.Instance != null)
            {
                Sprite paPortrait = paData.icon;
                string paName = paData.personaName;

                CommandManager.Instance.FeedBackByMessage("Health is under 10%", paPortrait, paName);
            }            
        }

        // 회복 시 플래그 초기화
        else if (hpPercent > 0.1f)
        {
            isDeadTriggered = false;
            isLowHPTriggered = false;
        }
    }

    

    // ------------------------------
    // Gizmo
    // ------------------------------
    void OnDrawGizmos()
    {
        // 시야 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // 시야각
        Vector3 left =
            Quaternion.Euler(0, -viewAngle / 2, 0)
            * transform.forward;

        Vector3 right =
            Quaternion.Euler(0, viewAngle / 2, 0)
            * transform.forward;

        Gizmos.color = Color.blue;

        Gizmos.DrawLine(
            transform.position,
            transform.position + left * viewDistance);

        Gizmos.DrawLine(
            transform.position,
            transform.position + right * viewDistance);

        // 현재 타겟 표시
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawLine(
                transform.position,
                currentTarget.position);
        }
    }
}