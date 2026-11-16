using System.Collections.Generic;
using UnityEngine;

namespace EnemyAI
{
    public enum TargetPositionSource { None, DirectSight, Hearing, Command, SharedMark, Memory, Damage }
    // 근접 대응은 거리 이탈로 종료하지만, 피격 대응은 멀어져도 관측이 유지되면 계속 교전한다.
    public enum TemporaryEngagementReason { None, CloseThreat, DamageResponse }

    // 관측/기억/명령과 타깃 선택을 한곳에서 관리한다. 실행 데이터는 공유 State/Decision이 아닌 NPC별로 소유한다.
    public partial class StateController
    {
        public Transform CommandTarget { get; private set; }
        public bool CommandSuspended { get; private set; }
        public TargetPositionSource PositionSource { get; private set; }
        public float LastPositionUpdate { get; private set; } = float.NegativeInfinity;
        public bool HasKnownTarget => IsValidCombatTarget(aimTarget) && HasValidPersonalTarget;
        public bool HasDirectObservation => HasKnownTarget && targetInSight;

        public Transform TemporaryTarget { get; private set; }
        public bool IsTemporarilyEngaging => temporaryEngagementActive;
        public TemporaryEngagementReason TemporaryReason { get; private set; }
        private bool temporaryEngagementActive;
        private Transform temporaryReturnTarget;
        private float temporaryStartedAt, temporaryLastSeenAt, nextTemporaryEngagementAt;

        private sealed class Knowledge
        {
            public Vector3 position;
            public float observedAt = float.NegativeInfinity;
            public float availableAt = float.NegativeInfinity;
            public TargetPositionSource source;
            public bool visible, heard;
            public float damagedAt = float.NegativeInfinity;
            // 일반 position은 관측/청각/표식 등으로 갱신된다. 피격 반응용 좌표와 사건 소비 여부는 별도로 보관한다.
            public Vector3 damagePosition;
            public int damageVersion, consumedDamageVersion;
            public float nextDamageCheckAt;
        }
        private struct MarkReport
        {
            public Object provider;
            public Transform target;
            public Vector3 position;
            public float observedAt, expiresAt;
        }
        private readonly Dictionary<Transform, Knowledge> knownTargets = new Dictionary<Transform, Knowledge>();
        private readonly Dictionary<Transform, Vector3> heardPositions = new Dictionary<Transform, Vector3>();
        private readonly List<MarkReport> marks = new List<MarkReport>();
        private readonly HashSet<Transform> candidates = new HashSet<Transform>();
        private readonly List<Transform> retiredTargets = new List<Transform>();
        private float nextHearingSample;
        private int knowledgeFrame = -1;

        private static bool Finite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        private Knowledge GetKnowledge(Transform target)
        {
            if (!knownTargets.TryGetValue(target, out Knowledge info))
                knownTargets[target] = info = new Knowledge();
            return info;
        }
        private void Remember(Transform target, Vector3 position, float observedAt, TargetPositionSource source)
        {
            Knowledge info = GetKnowledge(target);
            if (!Finite(position) || observedAt < info.observedAt) return;
            info.position = position;
            info.observedAt = observedAt;
            info.availableAt = Time.time;
            info.source = source;
        }

        // The caller supplies the initial command position once. Repeated adapter calls
        // cannot refresh an unseen target or restart a suspended search.
        public bool SetCommandTarget(Transform target, Vector3 initialPosition, bool renew = false)
        {
            if (IsRegrouping) return false;
            if (!IsValidCombatTarget(target) || !Finite(initialPosition)) return false;
            target = CombatTargetOwner.Resolve(target);
            if (CommandTarget == target && !renew) return true;
            ResetDamageReactionForCommand();
            bool wasTemporary = temporaryEngagementActive;
            EndTemporaryEngagement();
            CommandTarget = target;
            CommandSuspended = false;
            Remember(target, initialPosition, Time.time, TargetPositionSource.Command);
            SelectKnownTarget(target, wasTemporary);
            return true;
        }
        public void ClearCommandTarget(Transform expectedTarget)
        {
            if (CommandTarget == CombatTargetOwner.Resolve(expectedTarget))
            {
                CommandTarget = null;
                CommandSuspended = false;
                EndTemporaryEngagement();
            }
        }

        // Receiver-scoped API: the ability chooses which allies receive the report.
        // Both times use Time.time. A report is a supplied snapshot, never a permission
        // for the FSM to read a hidden Transform's live position.
        public bool ReportSharedTargetPosition(Object provider, Transform target, Vector3 position,
            float observedAt, float expiresAt)
        {
            if (provider == null || !IsValidCombatTarget(target) || !Finite(position) ||
                !float.IsFinite(observedAt) || !float.IsFinite(expiresAt) || observedAt > Time.time ||
                expiresAt <= Time.time || expiresAt <= observedAt) return false;
            target = CombatTargetOwner.Resolve(target);
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i].provider != provider || marks[i].target != target) continue;
                if (observedAt < marks[i].observedAt) return false;
                marks[i] = new MarkReport { provider = provider, target = target, position = position,
                    observedAt = observedAt, expiresAt = expiresAt };
                return true;
            }
            marks.Add(new MarkReport { provider = provider, target = target, position = position,
                observedAt = observedAt, expiresAt = expiresAt });
            return true;
        }
        public void RevokeSharedTargetPosition(Object provider, Transform target)
        {
            Transform root = CombatTargetOwner.Resolve(target);
            marks.RemoveAll(report => report.provider == provider && report.target == root);
        }
        public void ReportHeardPosition(Transform target, Vector3 position)
        {
            if (!IsValidCombatTarget(target) || !Finite(position)) return;
            target = CombatTargetOwner.Resolve(target);
            Knowledge info = GetKnowledge(target);
            if (!info.visible) Remember(target, position, Time.time, TargetPositionSource.Hearing);
            info.heard = true;
        }

        // Health 경로에서 실제 체력/방패가 감소했고 수신자가 생존한 경우 호출한다.
        // 현재 hitscan이 전달한 공격자의 피격 당시 위치를 한 번 기록한다. 직접 관측이나 즉시 타깃 변경은 아니다.
        public bool ReportDamageSource(GameObject origin, HealthManager.DamageType damageType)
        {
            if (origin == null || damageType == HealthManager.DamageType.Environment) return false;
            Transform target = CombatTargetOwner.Resolve(origin.transform);
            // IsValidCombatTarget also supports non-character legacy targets. Damage
            // acquisition deliberately requires a character health owner.
            if (target.GetComponent<HealthManager>() == null ||
                !TeamCodeUtility.TryGetTeamCode(target, out _) || !IsValidCombatTarget(target)) return false;
            Vector3 position = GetAimPoint(target);
            if (!Finite(position)) return false;
            Remember(target, position, Time.time, TargetPositionSource.Damage);
            GetKnowledge(target).damagedAt = Time.time;
            GetKnowledge(target).damagePosition = position;
            GetKnowledge(target).damageVersion = ++damageEventVersion;
            return true;
        }

        public bool CanObserveTarget(Transform target, float radius, bool requireFieldOfView)
        {
            return TryGetObservationTarget(target, out _, out Vector3 point) &&
                IsWithinObservationRange(point, radius, requireFieldOfView) && !IsSightBlockedAt(point);
        }
        // Results live only within this call: later actions must see death/disable/pose changes.
        private bool TryGetObservationTarget(Transform target, out Transform owner, out Vector3 point)
        {
            owner = null;
            point = default;
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            owner = CombatTargetOwner.Resolve(target);
            if (!IsValidCombatOwner(owner)) return false;
            point = GetOwnerAimPoint(owner);
            return true;
        }
        private bool IsWithinObservationRange(Vector3 point, float radius, bool requireFieldOfView)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude > radius * radius) return false;
            if (requireFieldOfView && Vector3.Angle(transform.forward, direction) >= viewAngle * .5f) return false;
            return true;
        }
        public bool IsSightBlocked(Transform target)
        {
            return !TryGetObservationTarget(target, out _, out Vector3 point) || IsSightBlockedAt(point);
        }
        private bool IsSightBlockedAt(Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * generalStats.aboveCoverHeight;
            Vector3 direction = point - origin;
            return Physics.Raycast(origin, direction, direction.magnitude,
                generalStats.coverMask | generalStats.obstacleMask, QueryTriggerInteraction.Ignore);
        }
        public bool ObserveSelectedTarget(float radius, bool requireFieldOfView)
        {
            if (!TryGetObservationTarget(aimTarget, out Transform target, out Vector3 point) ||
                !IsWithinObservationRange(point, radius, requireFieldOfView) || IsSightBlockedAt(point)) return false;
            Remember(target, point, Time.time, TargetPositionSource.DirectSight);
            GetKnowledge(target).visible = true;
            ApplyKnowledge(target);
            return true;
        }
        public bool HasSearchEvidence => HasKnownTarget && !HasDirectObservation;

        // Called once before actions. Decisions read the selected target rather than
        // mutating it while evaluating different candidate colliders.
        private void UpdateTargetKnowledge(bool observationOnly = false)
        {
            if (knowledgeFrame == Time.frameCount) return;
            knowledgeFrame = Time.frameCount;
            retiredTargets.Clear();
            foreach (var entry in knownTargets)
            {
                entry.Value.visible = false;
                entry.Value.heard = entry.Value.source == TargetPositionSource.Hearing &&
                    Time.time - entry.Value.observedAt <= .5f;
                if (!IsValidCombatTarget(entry.Key)) retiredTargets.Add(entry.Key);
            }
            foreach (Transform target in retiredTargets) { knownTargets.Remove(target); heardPositions.Remove(target); }
            if (!IsValidCombatTarget(CommandTarget)) { CommandTarget = null; CommandSuspended = false; }

            candidates.Clear();
            foreach (var entry in knownTargets)
                if (Time.time - entry.Value.damagedAt <= blindEngageTime) candidates.Add(entry.Key);
            float radius = Mathf.Max(viewRadius, perceptionRadius);
            foreach (Collider hit in Physics.OverlapSphere(transform.position, radius, CombatTargetMask))
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                Transform owner = CombatTargetOwner.Resolve(hit.transform);
                // Accepted owners are deduplicated; rejected owners are not retained.
                if (!candidates.Contains(owner) && IsValidCombatOwner(owner)) candidates.Add(owner);
            }
            if (IsValidCombatTarget(aimTarget)) candidates.Add(CombatTargetOwner.Resolve(aimTarget));
            if (CommandTarget != null) candidates.Add(CommandTarget);
            bool sampleHearing = Time.time >= nextHearingSample;
            if (sampleHearing) nextHearingSample = Time.time + .5f;
            foreach (Transform target in candidates)
            {
                Knowledge info = GetKnowledge(target);
                // The three policies share one aim point and one occlusion result.
                bool visible = TryGetObservationTarget(target, out _, out Vector3 point) &&
                    (IsWithinObservationRange(point, viewRadius, true) ||
                     IsWithinObservationRange(point, nearRadius, false) ||
                     (target == aimTarget && IsWithinObservationRange(point, viewRadius, false))) &&
                    !IsSightBlockedAt(point);
                if (visible)
                {
                    Remember(target, point, Time.time, TargetPositionSource.DirectSight);
                    info.visible = true;
                }
                // Preserve the existing movement-based hearing, sampled per NPC/character.
                // This is a separate local perception source, not global tracking.
                if (sampleHearing)
                {
                    Vector3 hearingPoint = target.position;
                    if ((hearingPoint - transform.position).sqrMagnitude <= perceptionRadius * perceptionRadius)
                    {
                        if (heardPositions.TryGetValue(target, out Vector3 previous) &&
                            (previous - hearingPoint).sqrMagnitude > .01f) ReportHeardPosition(target, hearingPoint);
                        heardPositions[target] = hearingPoint;
                    }
                    else heardPositions.Remove(target);
                }
            }
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                MarkReport report = marks[i];
                if (report.provider == null || !IsValidCombatTarget(report.target) || report.expiresAt <= Time.time)
                { marks.RemoveAt(i); continue; }
                // Unselected marks provide information without forcing acquisition.
                if (report.target != aimTarget && report.target != CommandTarget) continue;
                Knowledge info = GetKnowledge(report.target);
                if (info.visible || report.observedAt < info.observedAt) continue;
                Remember(report.target, report.position, report.observedAt, TargetPositionSource.SharedMark);
            }

            if (observationOnly || IsRegrouping) return;
            SelectTargetFromCurrentObservation();
        }

        // Shared selection policy; ordinary combat calls this without the post-regroup filter.
        private void SelectTargetFromCurrentObservation()
        {
            bool directOnly = IsRegroupWatchActive;
            Transform selected = IsValidCombatTarget(aimTarget) ? CombatTargetOwner.Resolve(aimTarget) : null;
            if (CommandTarget != null)
            {
                Knowledge command = GetKnowledge(CommandTarget);
                bool fresh = Time.time - command.availableAt <= blindEngageTime;
                CommandSuspended = !fresh;
                if (fresh) selected = CommandTarget;
                else if (selected == CommandTarget) selected = null;
            }
            // 평가 순서: 기존 임시 교전의 유지/종료 → 피격 반응 → 일반 근접 대응 → 일반 자동 선택.
            // 가까운 비공격자를 먼저 선택해 최소 유지시간으로 새로운 실제 피격을 밀어내지 않도록 순서를 지킨다.
            // locked는 아래의 일반 자동 선택만 막는다. 명령 대상이 있어도 앞선 피격/근접 대응은 평가한다.
            bool locked = CommandTarget != null && !CommandSuspended;
            bool temporarySelection = temporaryEngagementActive;
            if (IsCheckingDamageDirection) selected = IsValidCombatTarget(aimTarget) ? aimTarget : null;
            else if (MaintainTemporaryEngagement(ref selected, locked)) locked = true;
            if (UpdateDamageReaction(ref selected, directOnly)) locked = true;
            else if (!IsCheckingDamageDirection && TrySelectCloseThreat(ref selected)) locked = true;
            // 진입뿐 아니라 임시 교전 종료·원래 대상 복귀에서도 기존 엄폐를 재평가해 재사용할 수 있게 한다.
            temporarySelection |= temporaryEngagementActive;
            Knowledge current = selected != null ? GetKnowledge(selected) : null;
            bool canAcquireFromDamage = current == null || Time.time - current.availableAt > blindEngageTime;
            if (!locked && (current == null || Time.time - current.availableAt >= TargetSwitchGrace))
            {
                Transform best = null;
                float bestDistance = float.PositiveInfinity;
                bool bestVisible = false;
                foreach (Transform candidate in candidates)
                {
                    Knowledge info = GetKnowledge(candidate);
                    if (directOnly && (!info.visible || !IsValidCombatTarget(candidate))) continue;
                    bool damageEvidence = canAcquireFromDamage && Time.time - info.damagedAt <= blindEngageTime;
                    if (!info.visible && !info.heard && !damageEvidence) continue;
                    float distance = (info.position - transform.position).sqrMagnitude;
                    if (best == null || (info.visible && !bestVisible) || (info.visible == bestVisible &&
                        (distance < bestDistance || (distance == bestDistance && candidate.GetInstanceID() < best.GetInstanceID()))))
                    { best = candidate; bestDistance = distance; bestVisible = info.visible; }
                }
                if (best != null) selected = best;
            }
            if (selected != null && Time.time - GetKnowledge(selected).availableAt > blindEngageTime) selected = null;
            SelectKnownTarget(selected, temporarySelection);
            if (directOnly && HasDirectObservation) regroupWatchUntil = 0f;
        }
        // 근접 우선순위는 수평 거리다. 루트→몸통의 3D 거리를 쓰는 관측 판정과 구분한다.
        private float HorizontalDistanceSquared(Vector3 point)
        {
            Vector3 delta = point - transform.position;
            return delta.x * delta.x + delta.z * delta.z;
        }

        private void EndTemporaryEngagement()
        {
            TemporaryTarget = null;
            temporaryReturnTarget = null;
            temporaryEngagementActive = false;
            TemporaryReason = TemporaryEngagementReason.None;
            nextTemporaryEngagementAt = Time.time + Mathf.Max(0f, TemporaryCooldown);
        }

        // 타깃 수명과 종료만 처리한다. 다른 후보를 고르는 과정과 분리해야 피격 반응을 먼저 평가할 수 있다.
        private bool MaintainTemporaryEngagement(ref Transform selected, bool commandFresh)
        {
            if (temporaryEngagementActive)
            {
                bool valid = IsValidCombatTarget(TemporaryTarget) &&
                    knownTargets.TryGetValue(TemporaryTarget, out _);
                Knowledge info = valid ? GetKnowledge(TemporaryTarget) : null;
                if (valid && info.visible) temporaryLastSeenAt = Time.time;
                float release = Mathf.Max(TemporaryEngageDistance, TemporaryReleaseDistance);
                bool damageResponse = TemporaryReason == TemporaryEngagementReason.DamageResponse;
                float minimum = damageResponse ? DamageEngagementMinimum : TemporaryMinimumDuration;
                float sightGrace = damageResponse ? DamageEngagementSightGrace : TemporarySightGrace;
                // 사망/무효/기억 만료는 최소 유지시간과 무관하게 종료한다.
                // DamageResponse에는 근접 해제 거리를 적용하지 않아 원거리 공격자에게 대응하자마자 복귀하지 않는다.
                bool finished = !valid || Time.time - info.availableAt > blindEngageTime ||
                    (damageResponse && !EnableDamageReaction) ||
                    (Time.time - temporaryStartedAt >= Mathf.Max(0f, minimum) &&
                    ((!damageResponse && info.visible && HorizontalDistanceSquared(info.position) > release * release) ||
                     (!info.visible && Time.time - temporaryLastSeenAt >= Mathf.Max(0f, sightGrace))));
                if (!finished)
                {
                    selected = TemporaryTarget;
                    return true;
                }
                Transform resume = commandFresh ? CommandTarget : temporaryReturnTarget;
                EndTemporaryEngagement();
                // 생존하고 기억이 유효한 원래 대상으로만 복귀한다. 숨은 Transform에서 새 위치를 얻지 않는다.
                selected = IsValidCombatTarget(resume) && knownTargets.TryGetValue(resume, out Knowledge old) &&
                    Time.time - old.availableAt <= blindEngageTime ? resume : null;
                return false;
            }
            return false;
        }

        // 피격 반응 평가 다음의 일반 근접 선택. 임시 교전 중에는 단순 거리 변화보다 새 근접 공격자만 평가한다.
        private bool TrySelectCloseThreat(ref Transform selected)
        {
            if (temporaryEngagementActive)
            {
                if (HoldsEngagementMinimum || !CanChangeCloseThreat()) return true;
                Transform attacker = FindCloseThreat(TemporaryTarget, true);
                if (attacker != null)
                {
                    TemporaryTarget = attacker;
                    TemporaryReason = TemporaryEngagementReason.CloseThreat;
                    temporaryStartedAt = temporaryLastSeenAt = Time.time;
                }
                selected = TemporaryTarget;
                return true;
            }
            if (Time.time < nextTemporaryEngagementAt || TemporaryEngageDistance <= 0f ||
                !CanChangeCloseThreat() || !IsValidCombatTarget(selected)) return false;
            Transform best = FindCloseThreat(selected, false);
            if (best == null) return false;
            temporaryReturnTarget = selected;
            TemporaryTarget = best;
            temporaryEngagementActive = true;
            TemporaryReason = TemporaryEngagementReason.CloseThreat;
            temporaryStartedAt = temporaryLastSeenAt = Time.time;
            selected = best;
            return true;
        }

        // 새 기능이 켜져 있으면 일반 근접 선택도 동일한 보호 행동/반응 간격을 지켜 우회 전환을 막는다.
        private bool CanChangeCloseThreat() => !reloading &&
            (!EnableDamageReaction || (!DamageActionBusy && Time.time >= nextDamageReactionAt)) &&
            (currentState == null || currentState.coverPhase != CoverPhase.Move);

        private bool RecentlyDamagedBy(Knowledge info) => CloseThreatDamageMemory > 0f &&
            Time.time - info.damagedAt <= CloseThreatDamageMemory;

        // 반드시 직접 관측한 후보만 비교한다. 피격 우선권이 없고 현재 적도 가까우면 충분한 거리 우위를 요구한다.
        // attackersOnly는 임시 교전 중 더 가까운 비공격자 때문에 타깃이 계속 바뀌는 것을 막는다.
        private Transform FindCloseThreat(Transform currentTarget, bool attackersOnly)
        {
            if (TemporaryEngageDistance <= 0f) return null;
            float radiusSquared = TemporaryEngageDistance * TemporaryEngageDistance;
            Knowledge current = GetKnowledge(currentTarget);
            float currentDistance = Mathf.Sqrt(HorizontalDistanceSquared(current.position));
            bool currentClose = current.visible && currentDistance <= TemporaryEngageDistance;
            bool currentAttacker = currentClose && RecentlyDamagedBy(current);
            Transform best = null;
            float bestDistance = float.PositiveInfinity;
            bool bestAttacker = false;
            foreach (Transform candidate in candidates)
            {
                Knowledge info = GetKnowledge(candidate);
                if (candidate == currentTarget || !info.visible) continue;
                float distance = HorizontalDistanceSquared(info.position);
                if (distance > radiusSquared) continue;
                bool attacker = RecentlyDamagedBy(info);
                bool newAttack = attacker && (!currentAttacker || info.damagedAt > current.damagedAt);
                if (attackersOnly && !newAttack) continue;
                if (!newAttack && currentClose && (currentAttacker ||
                    Mathf.Sqrt(distance) + Mathf.Max(.1f, CloseThreatDistanceAdvantage) >= currentDistance)) continue;
                if (best == null || (attacker && !bestAttacker) || (attacker == bestAttacker &&
                    (distance < bestDistance || (distance == bestDistance && candidate.GetInstanceID() < best.GetInstanceID()))))
                { best = candidate; bestDistance = distance; bestAttacker = attacker; }
            }
            return best;
        }

        private void SelectKnownTarget(Transform target, bool revalidateCover = false)
        {
            bool changed = aimTarget != target;
            if (changed)
            {
                aimTarget = target;
                personalTarget = Vector3.positiveInfinity;
                checkedOnLoop = false;
                hadClearShot = haveClearShot = false;
                variables.feelAlert = variables.hearAlert = false;
                variables.blindEngageTimer = 0f;
                // 일반 변경은 예약을 비운다. 임시 대응/복귀는 아래에서 새 적에 대한 보호 여부를 확인한 뒤 재사용한다.
                if (!revalidateCover) CoverSpot = Vector3.positiveInfinity;
            }
            if (target == null)
            {
                if (changed) CoverSpot = Vector3.positiveInfinity;
                PositionSource = TargetPositionSource.None;
                ClearInvalidCombatTarget();
                return;
            }
            ApplyKnowledge(target);
            if (changed && revalidateCover && (coverLookup == null ||
                !coverLookup.TryGetExistingSpot(this, out _))) CoverSpot = Vector3.positiveInfinity;
        }
        private void ApplyKnowledge(Transform target)
        {
            Knowledge info = GetKnowledge(target);
            personalTarget = float.IsNegativeInfinity(info.observedAt) ? Vector3.positiveInfinity : info.position;
            targetInSight = info.visible;
            LastPositionUpdate = info.observedAt;
            PositionSource = info.visible ? TargetPositionSource.DirectSight :
                (info.availableAt == Time.time ? info.source : TargetPositionSource.Memory);
            variables.blindEngageTimer = Mathf.Max(0f, Time.time - info.availableAt);
        }
        private bool HandleUnobservedTargetState()
        {
            if (!HasKnownTarget || HasDirectObservation || currentState.unobservedTargetState == null || reloading || CanKeepCoverWithoutSight(currentState) || NeedsCoverFallbackReload(currentState)) return false;
            State previous = currentState;
            TransitionToState(previous.unobservedTargetState, null);
            if (currentState != previous) currentState.OnEnableActions(this);
            return true;
        }
    }
}
