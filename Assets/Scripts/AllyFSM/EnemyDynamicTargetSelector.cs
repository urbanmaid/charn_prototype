using System.Collections.Generic;
using UnityEngine;
using EnemyAI;

// 적/아군 공용 동적 타겟 셀렉터.
//   SelectTarget (0.2초 주기): OverlapSphere로 후보 수집 → root 디듀프 → 진영 필터 →
//                              FOV + LOS 통과한 것 중 최근접을 aimTarget으로 선정
//   Update (매 프레임): 선정된 타겟 root의 안정적 가슴 지점으로 personalTarget 갱신
//
// [v2 수정]
//   (A) 조준점: 예전엔 OverlapSphere가 잡은 "가장 가까운 본 콜라이더"의 bounds.center를 추적 →
//       팔/발 같은 말단 본을 조준하거나 애니메이션/래그돌로 요동쳐 척추 over-twist(뒤틀린 포즈).
//       → 타겟 root의 (root.position + up*aimHeight) 고정 지점을 조준 → 안정적.
//   (B) 진영 필터: 예전엔 "자기 자신 제외 + HealthManager 있으면 전부" 조준 → 아군끼리/플레이어
//       오조준(팀킬). → FactionOf로 판별해 적대 진영만 타겟. 레이어 설정과 무관하게 방어.
[RequireComponent(typeof(StateController))]
public class EnemyDynamicTargetSelector : MonoBehaviour
{
    [Tooltip("타겟 root 기준 조준 지점 높이(m). 휴머노이드 가슴 높이 권장(≈1.4).")]
    [SerializeField] private float aimHeight = 1.4f;

    private StateController _controller;
    private AllyStateController _allyAdapter;
    private Transform _targetRoot;      // 현재 추적 중인 타겟 캐릭터 root (본이 아니라 root)
    private Transform _defaultTarget;   // 적 전용: 최초 aimTarget(보통 Player) — 타겟 없을 때 폴백

    // AllyEngageDecision이 사용 — Selector가 현재 유효 타겟을 확보 중인가
    public bool HasTarget => _targetRoot != null;

    private enum Faction { Enemy, Ally, Player }

    void Awake()
    {
        _controller = GetComponent<StateController>();
        _allyAdapter = GetComponent<AllyStateController>();
        _defaultTarget = _controller != null ? _controller.aimTarget : null;
    }

    void Start()
    {
        InvokeRepeating(nameof(SelectTarget), 0f, 0.2f);
    }

    void Update()
    {
        try
        {
            if (_targetRoot == null) return;
            if (_controller == null || _controller.aimTarget == null || _controller.aimTarget == _controller.transform) return;
            _controller.personalTarget = AimPointOf(_targetRoot);
        }
        catch (System.Exception exception)
        {
            _targetRoot = null;
            Debug.LogException(exception, this);
        }
    }

    private void SelectTarget()
    {
        try
        {
            SelectTargetInternal();
        }
        catch (System.Exception exception)
        {
            _targetRoot = null;
            Debug.LogException(exception, this);
        }
    }

    private void SelectTargetInternal()
    {
        if (_controller == null)
        {
            return;
        }

        // Command-designated targets are owned by the companion adapter, not
        // by the combat FSM subclass.
        if (_allyAdapter != null && _allyAdapter.EnemyTarget != null)
        {
            var phm = _allyAdapter.EnemyTarget.GetComponent<HealthManager>();
            if (phm != null && !phm.dead)
            {
                Transform pRoot = _allyAdapter.EnemyTarget.transform;
                _targetRoot = pRoot;
                _controller.personalTarget = AimPointOf(pRoot);
                return;
            }

            _allyAdapter.ClearEnemyTarget();
        }

        bool isAlly = _allyAdapter != null;
        Transform fallback = isAlly ? transform : (_defaultTarget != null ? _defaultTarget : transform);

        // 현재 타겟이 죽었으면 폴백으로 리셋 (아군=self, 적=최초 Player)
        bool currentDead = _controller.aimTarget == null
            || (_controller.aimTarget != transform
                && (_controller.aimTarget.root.GetComponent<HealthManager>() == null
                    || _controller.aimTarget.root.GetComponent<HealthManager>().dead));

        bool enemyStuckOnSelf = !isAlly && _controller.aimTarget == transform && fallback != transform;
        if (currentDead || enemyStuckOnSelf)
        {
            _controller.aimTarget = fallback;
            _targetRoot = null;
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _controller.viewRadius,
            _controller.generalStats.targetMask);

        Transform bestRoot = null;
        float bestDist = float.MaxValue;
        // 본 콜라이더 다수가 같은 캐릭터를 가리키므로 root 기준으로 1회만 평가 (중복 제거 + GetComponent 절약)
        HashSet<Transform> evaluated = new HashSet<Transform>();

        int blockMask = BuildBlockMask();

        foreach (Collider col in hits)
        {
            try
            {
                if (col == null) continue;
                Transform root = col.transform.root;

                if (root == transform) continue;      // 자기 자신 제외
                if (!evaluated.Add(root)) continue;    // 이 캐릭터는 이미 평가함

                var hm = root.GetComponent<HealthManager>();
                if (hm == null || hm.dead) continue;   // HealthManager 없거나 죽었으면 제외

                if (!IsHostile(isAlly, root)) continue; // 진영 필터: 적대 진영만

                // 조준/판정 지점 = 타겟 root의 안정적 가슴 지점 (본 요동 없음)
                Vector3 aimPoint = AimPointOf(root);

                // FOV
                Vector3 dir = (aimPoint - transform.position).normalized;
                if (Vector3.Angle(transform.forward, dir) > _controller.viewAngle / 2f) continue;

                // LOS (가슴 높이에서 가슴 지점으로)
                Vector3 origin = transform.position + Vector3.up * _controller.generalStats.aboveCoverHeight;
                Vector3 toTarget = aimPoint - origin;
                if (Physics.Raycast(origin, toTarget.normalized, toTarget.magnitude, blockMask)) continue;

                float dist = toTarget.sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestRoot = root;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, col != null ? (Object)col : this);
            }
        }

        if (bestRoot != null)
        {
            if (isAlly)
                _allyAdapter.TrySetEnemyTarget(bestRoot.gameObject);
            else
                _controller.aimTarget = bestRoot;
            _targetRoot = bestRoot;
            _controller.personalTarget = AimPointOf(bestRoot);
        }
        else
        {
            _targetRoot = null;

            // 타겟 없음 방어: personalTarget이 ∞(적 PatrolAction 기본값)로 남으면
            //   원본 SpotFocus/ExitFocus/GoToShotSpot 등이 nav.destination = personalTarget으로
            //   밀어넣어 "setting destination to infinity is ignored" 경고가 뜬다.
            //   적(원본 FSM)만 해당 — ∞일 때만 자기 위치로 덮어써 경고 차단.
            //   ※ 유효한 '마지막 목격 위치'는 보존(∞만 교체) → SearchAction 수색 동작 유지.
            //   ※ 아군은 nav.destination에 personalTarget을 절대 쓰지 않으므로 대상 아님.
            if (!isAlly && float.IsInfinity(_controller.personalTarget.x))
                _controller.personalTarget = transform.position;
        }
    }

    // 타겟 root의 안정적 조준 지점 (발밑 원점 + 가슴 높이)
    private Vector3 AimPointOf(Transform root)
    {
        StateController targetController = root.GetComponent<StateController>();
        return targetController != null
            ? targetController.GetAimPoint()
            : root.position + Vector3.up * aimHeight;
    }

    // coverMask | obstacleMask | Default 레이어를 시야 차단으로 취급
    private int BuildBlockMask()
    {
        int mask = _controller.generalStats.coverMask | _controller.generalStats.obstacleMask;
        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
        {
            mask |= 1 << defaultLayer;
        }
        return mask;
    }

    // 이 셀렉터(isAlly) 기준으로 root가 적대 진영인가
    //   아군 → 적만 / 적 → 플레이어·아군만
    private static bool IsHostile(bool selfIsAlly, Transform root)
    {
        Faction f = FactionOf(root);
        return selfIsAlly ? (f == Faction.Enemy) : (f == Faction.Ally || f == Faction.Player);
    }

    // root의 진영 판별 (레이어 설정과 무관하게 컴포넌트/태그로 판정)
    private static Faction FactionOf(Transform root)
    {
        if (root.GetComponent<AllyStateController>() != null) return Faction.Ally;
        if (root.GetComponent<PlayerHealth>() != null || root.CompareTag("Player")) return Faction.Player;
        return Faction.Enemy;
    }
}
