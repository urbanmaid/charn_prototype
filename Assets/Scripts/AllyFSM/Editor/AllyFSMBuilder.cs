using UnityEditor;
using UnityEngine;
using EnemyAI;

// Unity 메뉴 Ally AI > Build FSM Assets 실행 시 Assets/FSM/ 하위에 모든 .asset 파일 생성
// v19: 전투 루프 수정 — 버스트 후 엄폐 복귀 (AttackState ↔ WaitState 무한루프 해소)
//
// 문제점 (v18까지):
//   TakeCoverDecision이 AttackState에서 절대 true 불가
//   - 사격 중: currentShots < shotsInRound → false
//   - 버스트 완료: EndBurstDecision이 먼저 발동 → TakeCoverDecision 평가 안 됨
//   - WaitState→AttackState: waitInCoverTimer가 coverTime 초과 유지 → false
//   결과: AttackState ↔ WaitState 무한루프, 엄폐 복귀 없음
//
// 수정 (v19):
//   EndBurstDecision → ReturnCoverState (WaitState 대신)
//   ReturnCoverState: 커버 있으면 TakeCoverState, 없으면 WaitState(개활지 대기)
//
// 아군 전투 루프 (v19):
//   [커버] FindCoverState → TakeCoverState(엄폐+장전) → AttackState(사격)
//          → ReturnCoverState → TakeCoverState → (반복)
//   [개활지] AttackState(사격) → ReturnCoverState → WaitState(장전+대기)
//            → FindCoverState(커버 재탐색) → (커버 있음) TakeCoverState
//                                          → (없음)    AttackState
//
// aligned 설정 메커니즘:
//   AllySpotFocusAction.Act(): focusSight=true + nav.destination=self(desiredVelocity=0 안정) + angularSpeed==0 → aligned=true
//
// 아군 변경점 (기술적 이유로 필요한 최소):
//   - PatrolAction      → AllyFollowAction      (포메이션 이동)
//   - WaitedDecision    → AllyWaitedDecision     (SO 공유 타이머 충돌 방지)
//   - TakeCoverAction   → AllyTakeCoverAction    (이동 중 Rotating → Root Motion 방해 방지)
//   - AttackAction      → AllyAttackAction       (target.root: 래그돌 자식 본 데미지 적용)
//   - EndBurstDecision  → AllyEndBurstDecision   (aligned 조건 포함)
//   - SpotFocusAction   → AllySpotFocusAction    (aligned 관리: angularSpeed==0 감지)
//   - LookDecision        → AllyLookDecision         (aimTarget 직접 확인 + personalTarget 레이캐스트)
//   - ReturnToCoverAction → AllyReturnToCoverAction  (focusSight=true+Strafing=true+evadeSpeed: 적 향한 채 스트레이프 복귀)
//   - FindCoverAction     → AllyFindCoverAction      (focusSight=true+Strafing=true: 적 향한 채 스트레이프 이동 + FindCoverState에 attackAct 추가로 이동 중 반격)
//   - (신규) AllyEngageDecision: Selector.HasTarget 기반 타겟 소실 감지 (적AI EngageDecision 등가)
//
// EnemyAI 원본 직접 사용:
//   ReachedPointDecision,
//   LookDecision, FeelAlertDecision, TargetDeadDecision, TakeCoverDecision
public static class AllyFSMBuilder
{
    private const string ActionDir   = "Assets/FSM/Actions";
    private const string DecisionDir = "Assets/FSM/Decisions";
    private const string StateDir    = "Assets/FSM/States";

    [MenuItem("Ally AI/Build FSM Assets")]
    public static void Build()
    {
        EnsureDir(ActionDir);
        EnsureDir(DecisionDir);
        EnsureDir(StateDir);

        // ── Actions ──────────────────────────────────────────────────────
        var followAct    = Make<AllyFollowAction>   (ActionDir, "AllyFollowAction");
        var takeCoverAct = Make<AllyTakeCoverAction> (ActionDir, "AllyTakeCoverAction");
        var attackAct    = Make<AllyAttackAction>    (ActionDir, "AllyAttackAction");

        // EnemyAI 원본 (상태 없음 → 공유 안전)
        var reloadAct      = LoadOrMake<ReloadAction>   (ActionDir, "ReloadAction");

        // AllyFindCoverAction: FindCoverAction 대체
        //   원본은 focusSight=false → 적에게 등 보이며 이동 → 사망
        //   → focusSight=true + Strafing=true: 적 향한 채 스트레이프로 엄폐 이동
        var findCoverAct = Make<AllyFindCoverAction>(ActionDir, "AllyFindCoverAction");

        // AllyReturnToCoverAction: ReturnToCoverAction 대체
        //   원본은 focusSight=false → 적에게 등 보이며 이동 → 사망
        //   → focusSight=true + Strafing=true: 적 향한 채 스트레이프로 커버 복귀
        var returnCoverAct = Make<AllyReturnToCoverAction>(ActionDir, "AllyReturnToCoverAction");

        // AllySpotFocusAction: SpotFocusAction + aligned 관리 (angularSpeed==0 → aligned=true)
        // AttackState와 WaitState 양쪽에서 몸통 정렬을 감지하기 위해 사용
        var allySpotFocusAct = Make<AllySpotFocusAction>(ActionDir, "AllySpotFocusAction");

        // ── Decisions ─────────────────────────────────────────────────────
        // AllyLookDecision: LookDecision 대체
        //   - aimTarget 직접 확인 (targetsInViewRadius[0] 의존 없음)
        //   - personalTarget (bounds.center) 레이캐스트 → 낮은 엄폐물 뒤 적 정확히 탐지
        //   - personalTarget 덮어쓰기 없음 (Selector가 소유)
        var lookDec      = Make<AllyLookDecision>    (DecisionDir, "AllyLookDecision");
        var feelAlertDec = LoadOrMake<FeelAlertDecision>(DecisionDir, "Ally_FeelAlertDecision");
        var targetDeadDec = LoadOrMake<TargetDeadDecision>(DecisionDir, "Ally_TargetDeadDecision");
        var takeCoverDec  = LoadOrMake<TakeCoverDecision> (DecisionDir, "Ally_TakeCoverDecision");
        var reachedCoverDec = LoadOrMake<ReachedPointDecision>(DecisionDir, "Ally_ReachedPointDecision");

        // 아군 전용
        var endBurstDec      = Make<AllyEndBurstDecision>    (DecisionDir, "AllyEndBurstDecision");
        var coverNotFoundDec = Make<AllyCoverNotFoundDecision>(DecisionDir, "AllyCoverNotFoundDecision");

        // AllyWaitedDecision: 타이밍을 AllyStateController per-ally 필드에 저장
        var waitedDec = Make<AllyWaitedDecision>(DecisionDir, "AllyWaitedDecision");
        waitedDec.maxTimeToWait = 2f;
        EditorUtility.SetDirty(waitedDec);

        // AllyEngageDecision: Selector.HasTarget 기반 타겟 소실 감지
        //   lostTargetTime = 30f: 적AI blindEngageTime (30초)과 일치
        var engageDec = Make<AllyEngageDecision>(DecisionDir, "AllyEngageDecision");
        engageDec.lostTargetTime = 30f;
        EditorUtility.SetDirty(engageDec);
        var returnPlayerDec = Make<AllyReturnToPlayerDecision>(DecisionDir, "AllyReturnToPlayerDecision");

        // ── States ────────────────────────────────────────────────────────
        var remainState      = Make<State>(StateDir, "RemainInState");
        var followState      = Make<State>(StateDir, "AllyFollowState");
        var findCoverState   = Make<State>(StateDir, "AllyFindCoverState");
        var takeCoverState   = Make<State>(StateDir, "AllyTakeCoverState");
        var attackState      = Make<State>(StateDir, "AllyAttackState");
        var waitState        = Make<State>(StateDir, "AllyWaitState");
        var returnCoverState = Make<State>(StateDir, "AllyReturnCoverState");

        Wire(remainState, Color.grey, new Action[0], new Transition[0]);

        // ── FollowState ───────────────────────────────────────────────────
        Wire(followState, Color.blue,
            new Action[] { followAct },
            new[]
            {
                T(lookDec,      findCoverState, remainState),
                T(feelAlertDec, findCoverState, remainState),
            });

        // ── FindCoverState ────────────────────────────────────────────────
        // AllyFindCoverAction(focusSight+Strafing) + AllyAttackAction: 적 주시하며 이동 + 반격
        // reloadAct: 엄폐 이동 중에도 탄 떨어지면 재장전 (도착 후 또 대기 안 하도록)
        // coverNotFound: CoverSpot==infinity → 즉시 AttackState
        // ※ engageDec 추가하지 않음: aimTarget==self(타겟 미확보) 상태로도 feelAlert에 의해
        //   정상 진입하는 케이스가 있는데, engageDec을 넣으면 첫 프레임에 바로 FollowState로
        //   튕겨나가 엄폐 이동을 시작도 못함. (Attack↔Follow↔FindCover 무한루프는
        //   AllyEngageDecision의 feelAlert=false 리셋으로 해결됨 — 여긴 불필요)
        Wire(findCoverState, new Color(1f, 0.5f, 0f),
            new Action[] { findCoverAct, attackAct, reloadAct },
            new[]
            {
                T(returnPlayerDec, followState,    remainState),
                T(targetDeadDec,    followState,    remainState),
                T(coverNotFoundDec, attackState,    remainState),
                T(reachedCoverDec,  takeCoverState, remainState),
            });

        // ── TakeCoverState ────────────────────────────────────────────────
        // TakeCoverDecision: true=엄폐 유지, false=peek(AttackState)
        // AllyEngageDecision: takeCoverDec=true(대기 중)일 때만 도달
        //   → 엄폐 대기 중 타겟 유실(blindEngageTimer 초과) 시 FollowState
        //   → 적 AI EngageDecision과 동일 역할: 엄폐 중 타겟 소실 처리
        // 전이 우선순위 (CheckTransitions는 첫 상태 변경에서 break):
        //   1. 타겟 사망       → followState
        //   2. takeCoverDec=false(coverTime 경과) → attackState (peek)
        //   3. takeCoverDec=true 이면 engageDec 도달:
        //        타겟 유효 → remain / 유실 → followState
        Wire(takeCoverState, Color.yellow,
            new Action[] { takeCoverAct, reloadAct },
            new[]
            {
                T(returnPlayerDec, followState, remainState),
                T(targetDeadDec, followState, remainState),
                T(takeCoverDec,  remainState, attackState), // false=peek
                T(engageDec,     remainState, followState), // 엄폐 대기 중 타겟 유실 처리
            });

        // ── AttackState ───────────────────────────────────────────────────
        // AllySpotFocusAction: 적 방향 주시 + aligned 관리
        // AllyEndBurstDecision: aligned && shots>=shotsInRound → ReturnCoverState
        //   (커버 있으면 TakeCoverState로, 없으면 WaitState로 분기는 ReturnCoverState가 담당)
        // AllyEngageDecision: 타겟 잃으면 FollowState [최저 우선순위]
        // ※ TakeCoverDecision 제거: 버스트 중 currentShots < shotsInRound → 항상 false이므로 무의미
        Wire(attackState, Color.red,
            new Action[] { attackAct, allySpotFocusAct },
            new[]
            {
                T(returnPlayerDec, followState,      remainState),
                T(targetDeadDec, followState,      remainState),
                T(endBurstDec,   returnCoverState, remainState), // 버스트 완료 → 엄폐 복귀
                T(engageDec,     remainState,      followState), // 타겟 없으면 복귀
            });

        // ── WaitState ─────────────────────────────────────────────────────
        // AllySpotFocusAction: 대기 중에도 몸통 정렬 유지 → aligned 계속 관리
        //   (EnemyAI FocusWaitState도 FocusMoveAction을 WaitState에서 유지하는 것과 동일 패턴)
        // AllyWaitedDecision: aligned && 타이머 만료 → FindCoverState(현재 적 위치 기준 커버 재탐색)
        //   ※ attackState 직행 X: 개활지 루프에 갇혀 FindCoverState를 영원히 재방문 못하는 문제 방지
        //   FindCoverState → (커버 있음) TakeCoverState / (없음) AttackState
        // AllyEngageDecision:  WaitState 중 타겟 유실 → FollowState
        Wire(waitState, Color.yellow,
            new Action[] { reloadAct, allySpotFocusAct },
            new[]
            {
                T(returnPlayerDec, followState,    remainState),
                T(targetDeadDec, followState,    remainState),
                T(waitedDec,     findCoverState, remainState), // 재장전 후 커버 재탐색
                T(engageDec,     remainState,    followState),
            });

        // ── ReturnCoverState ──────────────────────────────────────────────
        // AttackState 버스트 완료 후 진입
        // 커버 있음: TakeCoverState(엄폐+장전) → AttackState(peek) 루프 유지
        // 커버 없음: WaitState(개활지 장전+대기) → FindCoverState(커버 재탐색)
        //   WaitState에서 재장전 후 FindCoverState로 가는 경로로 매 버스트 후 커버 재탐색 가능
        // AllyEngageDecision: 엄폐 이동 중 타겟 유실 시 FollowState 복귀
        // attackAct 추가: returnCoverAct가 Aiming=true 유지한 채 후퇴 → 사거리/조준각 맞으면 반격
        //   (FindCoverState와 동일 패턴 — 안 그러면 적을 조준한 채 사격 없이 바라만 보다 피격사)
        Wire(returnCoverState, new Color(1f, 0.5f, 0f),
            new Action[] { returnCoverAct, attackAct },
            new[]
            {
                T(returnPlayerDec, followState,    remainState),
                T(targetDeadDec,    followState,    remainState),
                T(coverNotFoundDec, waitState,      remainState),   // 커버 없음 → 개활지 대기
                T(engageDec,        remainState,    followState),   // 이동 중 타겟 유실 → FollowState
                T(reachedCoverDec,  takeCoverState, remainState),   // 커버 도착 → 엄폐
            });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AllyFSMBuilder v19] 완료 — Assets/FSM/ 폴더를 확인하세요.");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }

    static T Make<T>(string dir, string name) where T : ScriptableObject
    {
        string path = $"{dir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static T LoadOrMake<T>(string dir, string name) where T : ScriptableObject
    {
        string path = $"{dir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        string[] guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            var found = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null) return found;
        }

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[AllyFSMBuilder] {name}.asset 신규 생성");
        return asset;
    }

    static void Wire(State state, Color color, Action[] actions, Transition[] transitions)
    {
        state.sceneGizmoColor = color;
        state.actions         = actions;
        state.transitions     = transitions;
        EditorUtility.SetDirty(state);
    }

    static Transition T(Decision dec, State trueState, State falseState)
        => new() { decision = dec, trueState = trueState, falseState = falseState };
}
