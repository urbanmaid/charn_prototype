using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 스테이지 시작 시 플레이어와 아군 FSM의 시작 위치를 설정한다.
/// 이 컴포넌트가 붙은 GameObject의 위치가 플레이어의 시작 중심점이 된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStartpoint : MonoBehaviour
{
    [Header("Ally FSM Spawn")]
    [SerializeField, Min(0f)]
    private float allyFSMLocationDistance = 3f;

    /// <summary>
    /// 아군 FSM이 시작 중심점에서 배치될 수 있는 최대 거리(m)이다.
    /// 인스펙터에서 이 값을 변경해 원형 범위를 오버라이드할 수 있다.
    /// </summary>
    public float AllyFSMLocationDistance
    {
        get => allyFSMLocationDistance;
        set => allyFSMLocationDistance = Mathf.Max(0f, value);
    }

    private const int RandomPositionAttempts = 8;

    /// <summary>
    /// 플레이어는 중심에, 모든 아군 FSM은 중심 주변 원형 범위 안에 배치한다.
    /// </summary>
    public void InitializePlayersAndAllies()
    {
        // [기존 코드 보관] 아군에도 Player 태그가 있으면 조종 대상 대신 아군이 선택될 수 있었다.
        // GameObject player = GameObject.FindGameObjectWithTag("Player");

        // [수정 의도] 조종 교환 시스템과 같은 기준으로 실제 플레이어 슬롯만 이동한다.
        // 분대 관리자가 없는 씬에서는 활성 BasicBehaviour가 하나일 때만 자동으로 결정한다.
        BasicBehaviour controlledPlayer = AllySquadManager.Instance != null
            ? AllySquadManager.Instance.CurrentPlayer
            : PlayerControlResolver.Resolve(null);
        GameObject player = controlledPlayer != null ? controlledPlayer.gameObject : null;
        if (player != null)
        {
            SetWorldPose(player, transform.position, transform.rotation);
        }
        else
        {
            // [기존 코드 보관]
            // Debug.LogWarning("PlayerStartpoint: Player 태그를 가진 플레이어를 찾지 못했습니다.", this);
            // [수정 의도] 변경된 식별 기준에 맞춰 확인해야 할 컴포넌트와 설정을 안내한다.
            Debug.LogWarning("PlayerStartpoint: 플레이어 조종 슬롯을 찾지 못했습니다. BasicBehaviour와 분대 관리자의 Controlled Player를 확인해 주세요.", this);
        }

        AllyStateController[] allies = FindObjectsByType<AllyStateController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AllyStateController ally in allies)
        {
            // [기존 코드 보관] 로드된 다른 씬의 아군도 이동 대상에 포함될 수 있었다.
            // if (ally == null || ally.gameObject == player)
            // [수정 의도] 이번 시작 지점과 같은 씬에 속한 아군만 배치한다.
            if (ally == null || ally.gameObject == player || ally.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            Vector3 spawnPosition = GetRandomAllyPosition();
            SetWorldPose(ally.gameObject, spawnPosition, ally.transform.rotation);
        }
    }

    private Vector3 GetRandomAllyPosition()
    {
        if (AllyFSMLocationDistance <= 0f)
        {
            return transform.position;
        }

        for (int attempt = 0; attempt < RandomPositionAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * AllyFSMLocationDistance;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                HorizontalDistance(transform.position, hit.position) <= AllyFSMLocationDistance)
            {
                return hit.position;
            }
        }

        // NavMesh가 없거나 범위 안에 유효한 NavMesh가 없으면 범위 안의 위치를 사용한다.
        Vector2 fallbackOffset = Random.insideUnitCircle * AllyFSMLocationDistance;
        return transform.position + new Vector3(fallbackOffset.x, 0f, fallbackOffset.y);
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private static void SetWorldPose(GameObject target, Vector3 position, Quaternion rotation)
    {
        if (target.TryGetComponent(out NavMeshAgent navAgent) &&
            navAgent.enabled &&
            navAgent.gameObject.activeInHierarchy &&
            navAgent.isOnNavMesh)
        {
            navAgent.Warp(position);
            target.transform.rotation = rotation;
            return;
        }

        if (target.TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.position = position;
            rigidbody.rotation = rotation;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            return;
        }

        target.transform.SetPositionAndRotation(position, rotation);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);

        const int segmentCount = 48;
        Vector3 previous = transform.position + Vector3.right * AllyFSMLocationDistance;
        for (int i = 1; i <= segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / segmentCount;
            Vector3 next = transform.position + new Vector3(
                Mathf.Cos(angle) * AllyFSMLocationDistance,
                0f,
                Mathf.Sin(angle) * AllyFSMLocationDistance);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }
}
