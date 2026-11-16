using UnityEngine;
using UnityEngine.AI;

public class testPA : MonoBehaviour
{
    public Transform target; // 따라갈 대상
    private NavMeshAgent agent;

    public float stopDistance = 2f; // 멈출 거리

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // target 없으면 Player 자동 찾기
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                target = player.transform;
            }
        }

        agent.stoppingDistance = stopDistance;
    }

    void Update()
    {
        if (target == null) return;

        // 플레이어 위치로 이동
        agent.SetDestination(target.position);
    }
}