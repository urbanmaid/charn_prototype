using UnityEngine;

namespace EnemyAI
{
    // 피격을 즉시 타깃 변경으로 취급하지 않고, 방향 확인과 관측 후 임시 교전으로 나눈다.
    // Targeting이 관측/타깃 수명을 관리하고, 이 partial은 반응 대기와 제어 복원을 담당한다.
    // 별도 State 에셋으로 전환하지 않으므로 확인 중에도 기존 엄폐 상태와 숨기 타이머를 보존한다.
    public partial class StateController
    {
        [HideInInspector]
        [SerializeField, TextArea, Tooltip("피격 반응의 마지막 진입/보류/성공/종료 이유입니다. 시야와 타깃 전환 문제 진단에 사용합니다.")]
        private string damageReactionStatus;

        // true인 동안 StateController.Update는 일반 FSM 실행을 보류하고 EnemyAnimation만 방향을 확인한다.
        public bool IsCheckingDamageDirection { get; private set; }
        // 확인 진입 시 복사한 좌표. 이후 피격이나 공격자의 숨은 이동으로 현재 확인 방향을 바꾸지 않는다.
        public Vector3 DamageCheckPosition { get; private set; }
        // 마지막 반응 처리 메시지이며, 현재 FSM 상태나 임시 교전 활성 여부 자체를 뜻하지 않는다.
        public string DamageReactionStatus => damageReactionStatus;
        // 동일 프레임의 여러 피격도 구분하기 위한 개체별 순번. 대상별 소비 순번은 Knowledge에 저장한다.
        private int damageEventVersion;
        private Transform damageCheckTarget;
        private State damageCheckState;
        private float damageCheckStartedAt, nextDamageReactionAt;
        // 확인 종료 시 무조건 이동/조준을 켜지 않고, 진입 전에 누가 제어하고 있었는지 복원한다.
        private bool damageCheckStoppedBefore, damageCheckFocusBefore, damageCheckStrafeBefore, damageCheckAimBefore;

        // 이미 시작한 장전·엄폐 이동·짧은 반격을 피격마다 끊지 않는다. 피격 기록 자체는 계속 받는다.
        private bool DamageActionBusy => reloading || bullets <= 0 || IsCounterfiring ||
            (currentState != null && currentState.coverPhase == CoverPhase.Move);

        private bool HoldsEngagementMinimum => temporaryEngagementActive &&
            Time.time - temporaryStartedAt < Mathf.Max(0f,
                TemporaryReason == TemporaryEngagementReason.DamageResponse ? DamageEngagementMinimum : TemporaryMinimumDuration);

        // 관측 갱신 후, 일반 근접/자동 선택 전에 실행한다. visible을 직접 켜거나 숨은 위치를 읽지 않는다.
        // true는 이번 프레임의 선택을 우선 유지하라는 뜻이다. 사격 가능 또는 관측 성공을 뜻하지 않는다.
        private bool UpdateDamageReaction(ref Transform selected, bool directOnly = false)
        {
            if (IsCheckingDamageDirection)
            {
                if (!EnableDamageReaction || DamageActionBusy || currentState != damageCheckState ||
                    nav == null || !nav.enabled || !nav.isOnNavMesh)
                {
                    FinishDamageCheck("Cancelled: disabled or protected action", false);
                    return false;
                }
                if (!IsValidCombatTarget(damageCheckTarget))
                {
                    FinishDamageCheck("Attacker ended", false);
                    return false;
                }
                if (ProtectCurrentCloseAttacker(selected, DamageCheckPosition))
                {
                    FinishDamageCheck("Current close attacker takes priority", false);
                    return true;
                }
                Knowledge checking = GetKnowledge(damageCheckTarget);
                if (checking.visible)
                {
                    Transform confirmed = damageCheckTarget;
                    FinishDamageCheck("Attacker directly observed", false);
                    BeginDamageEngagement(confirmed, ref selected);
                    return true;
                }
                if (Time.time - damageCheckStartedAt >= Mathf.Max(0f, DamageCheckDuration))
                {
                    FinishDamageCheck("No observation before deadline", true);
                    // 기존 대상이 없으면 일반 선택에 제어를 돌려 기존 피해 기억 기반 수색을 허용한다.
                    return IsValidCombatTarget(selected);
                }
                // 기존 교전 대상과 엄폐를 유지한다. 회전 대상은 전투 타깃이 아닌 별도의 고정 좌표다.
                return true;
            }

            if (!EnableDamageReaction || DamageReactionMemory <= 0f) return false;
            Transform best = null;
            int newest = -1;
            foreach (var entry in knownTargets)
            {
                Transform target = entry.Key;
                Knowledge info = entry.Value;
                if (directOnly && !info.visible) continue;
                if (!IsValidCombatTarget(target) || info.damageVersion <= info.consumedDamageVersion ||
                    Time.time - info.damagedAt > DamageReactionMemory) continue;
                if (target == selected && info.visible)
                {
                    info.consumedDamageVersion = info.damageVersion;
                    // 현재 보는 적에게 다시 맞았다고 사격을 끊고 돌아보지는 않는다.
                    // 근접 대응 중 첫 실제 피격만 DamageResponse로 승격하고 이후 피격은 유지시간을 늘리지 않는다.
                    if (temporaryEngagementActive && TemporaryReason == TemporaryEngagementReason.CloseThreat)
                        BeginDamageEngagement(target, ref selected);
                    continue;
                }
                if (Time.time < info.nextDamageCheckAt || ProtectCurrentCloseAttacker(selected, info.damagePosition)) continue;
                if (info.damageVersion > newest) { best = target; newest = info.damageVersion; }
            }
            if (best == null) return false;
            if (DamageActionBusy || HoldsEngagementMinimum || Time.time < nextDamageReactionAt)
            {
                // 보류 중에는 사건을 소비하지 않는다. 대기가 끝났을 때 아직 memory 이내라면 재평가한다.
                damageReactionStatus = $"Deferred: {best.name}; busy={DamageActionBusy}; minimum={HoldsEngagementMinimum}; cooldown={Time.time < nextDamageReactionAt}";
                return false;
            }
            Knowledge candidate = GetKnowledge(best);
            if (candidate.visible)
            {
                BeginDamageEngagement(best, ref selected);
                return true;
            }
            if (DamageCheckDuration <= 0f || DamageCheckTurnSpeed <= 0f || nav == null || !nav.enabled ||
                !nav.isOnNavMesh || enemyAnimation == null) return false;

            // 실제 관측에 실패한 경우에만 확인 동작을 시작한다. 이미 보이는 공격자는 위에서 바로 교전한다.
            candidate.consumedDamageVersion = candidate.damageVersion;
            damageCheckTarget = best;
            DamageCheckPosition = candidate.damagePosition;
            damageCheckStartedAt = Time.time;
            damageCheckState = currentState;
            damageCheckStoppedBefore = nav.isStopped;
            damageCheckFocusBefore = focusSight;
            damageCheckStrafeBefore = Strafing;
            damageCheckAimBefore = Aiming || enemyAnimation.HasPendingAim;
            IsCheckingDamageDirection = true;
            enemyAnimation.AbortPendingAim();
            focusSight = false;
            Strafing = false;
            nav.isStopped = true;
            nav.velocity = Vector3.zero;
            damageReactionStatus = $"Checking {best.name}; snapshot={DamageCheckPosition}; limit={DamageCheckDuration:F2}s";
            return true;
        }

        // 단순히 가까운 적이 아니라 '현재 보이고 최근 실제로 피해를 준 근접 적'만 원거리 피격보다 보호한다.
        // TemporaryEngageDistance를 늘리면 일반 근접 대응 진입뿐 아니라 이 보호 범위도 함께 넓어진다.
        private bool ProtectCurrentCloseAttacker(Transform selected, Vector3 newHitPosition)
        {
            if (!IsValidCombatTarget(selected) || !knownTargets.TryGetValue(selected, out Knowledge current)) return false;
            float radius = Mathf.Max(0f, TemporaryEngageDistance);
            bool protect = current.visible && RecentlyDamagedBy(current) &&
                HorizontalDistanceSquared(current.position) <= radius * radius &&
                HorizontalDistanceSquared(newHitPosition) > radius * radius;
            if (protect) damageReactionStatus = $"Deferred: current close attacker {selected.name}";
            return protect;
        }

        private void BeginDamageEngagement(Transform target, ref Transform selected)
        {
            // A→B→C로 연속 대응해도 복귀 대상 A를 덮어쓰지 않는다. CommandTarget도 그대로 둔다.
            if (!temporaryEngagementActive) temporaryReturnTarget = selected;
            TemporaryTarget = target;
            TemporaryReason = TemporaryEngagementReason.DamageResponse;
            temporaryEngagementActive = true;
            temporaryStartedAt = temporaryLastSeenAt = Time.time;
            selected = target;
            Knowledge info = GetKnowledge(target);
            info.consumedDamageVersion = info.damageVersion;
            nextDamageReactionAt = Time.time + Mathf.Max(0f, DamageReactionCooldown);
            damageReactionStatus = $"Engaging observed attacker {target.name}; return={temporaryReturnTarget?.name ?? "none"}";
        }

        // 성공·시간 초과·새 명령·상태 변경·비활성화가 공유하는 정리 경로.
        // failed는 동일 공격자 재확인 대기를 추가하며, 모든 종료는 전역 반응 간격도 적용한다.
        private void FinishDamageCheck(string reason, bool failed, bool restoreAim = true)
        {
            if (!IsCheckingDamageDirection) return;
            if (damageCheckTarget != null && knownTargets.TryGetValue(damageCheckTarget, out Knowledge info))
            {
                // 확인 도중 같은 공격자에게 받은 피격까지 소비해, 종료 직후 같은 확인을 반복하지 않는다.
                info.consumedDamageVersion = info.damageVersion;
                if (failed) info.nextDamageCheckAt = Time.time + Mathf.Max(0f, DamageFailedCheckCooldown);
            }
            IsCheckingDamageDirection = false;
            // 엄폐 Hold가 이미 정지시켰다면 정지를 유지한다. 조준은 즉시 발사 자세가 아닌 대기 요청으로 복구한다.
            if (nav != null && nav.enabled && nav.isOnNavMesh) nav.isStopped = damageCheckStoppedBefore;
            focusSight = damageCheckFocusBefore;
            Strafing = damageCheckStrafeBefore;
            if (restoreAim && damageCheckAimBefore && HasKnownTarget && enemyAnimation != null)
                enemyAnimation.ActivatePendingAim();
            nextDamageReactionAt = Time.time + Mathf.Max(0f, DamageReactionCooldown);
            damageReactionStatus = $"Check ended: {reason}; elapsed={Time.time - damageCheckStartedAt:F2}s";
            damageCheckTarget = null;
        }

        private void ResetDamageReactionForCommand()
        {
            // 새 명령 이전의 사건은 소진하되 새 명령 이후의 피격까지 일괄 차단하지 않는다.
            FinishDamageCheck("New command", false);
            foreach (Knowledge info in knownTargets.Values) info.consumedDamageVersion = info.damageVersion;
            nextDamageReactionAt = Time.time;
        }

        private void OnDisable()
        {
            CancelCombatRegroup("Controller disabled");
            // 정지 소유권은 반환하지만 비활성화되는 개체에 새 조준 요청을 예약하지 않는다.
            FinishDamageCheck("Controller disabled", false, false);
            if (IsCounterfiring) FinishCounterfire("Controller disabled");
            ReleaseApproachPreparation();
            ReleaseFollowStop();
            ResetCoverApproach();
        }
    }
}
