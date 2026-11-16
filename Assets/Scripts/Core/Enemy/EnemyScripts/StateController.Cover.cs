using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI
{
    public enum CoverPhase { None, Move, Hold }
    public enum CoverFailure { None, NoCandidate, InvalidPath, LostProtection, NoProgress, RetryCooldown }

    public partial class StateController
    {
        public CoverFailure LastCoverFailure { get; private set; }
        public float NextCoverAttemptAt { get; private set; }
        private Collider selectedCoverCollider;
        private State pendingCoverReloadState;
        private bool NeedsCoverFallbackReload(State state) => state != null && state == pendingCoverReloadState && bullets <= 0;
        private float coverProgressAt, coverBestRemaining;
        private bool recoveringCover;
        private float coverRecoveryStarted;
        private Vector3 recoveredCoverSpot = Vector3.positiveInfinity;
        [HideInInspector]
        [SerializeField, TextArea, Tooltip("실행 중 마지막 점사 완료 후 엄폐 복귀 가능 여부와 거절 사유입니다. 조정값이 아니라 동작 확인용 기록입니다.")]
        private string coverReturnStatus;
        [Tooltip("실행 중 마지막 엄폐 재배치 판단·완료·실패 사유입니다. 조정값이 아니라 동작 확인용 기록입니다.")]
        [HideInInspector]
        [SerializeField, TextArea] private string coverRelocationStatus;
        private float nextCoverRelocationAt;
        private bool relocatingCover;
        private CoverLookup.CoverCandidate? pendingCoverRelocation;
        private bool aligningCover;

        // Map lifetime invalidation is not a failed tactical choice. Keep target/reload
        // ownership intact; the ordinary cover-state fallback handles the next update.
        internal void InvalidateCoverGeometry()
        {
            bool ownedMovement = CoverLookup.IsFinite(CoverSpot) || recoveringCover ||
                relocatingCover || aligningCover;
            if (IsCounterfiring) FinishCounterfire("Cover geometry changed");
            SyncCoverMotion(null);
            CoverSpot = Vector3.positiveInfinity;
            coverHash = -1;
            selectedCoverCollider = null;
            pendingCoverRelocation = null;
            recoveredCoverSpot = Vector3.positiveInfinity;
            hasCoverAssessment = false;
            SelectedCoverAssessment = default;
            attemptedCoverPositions.Clear();
            coverEpochThreats.Clear();
            NextCoverAttemptAt = nextCoverRelocationAt = 0f;
            LastCoverFailure = CoverFailure.None;
            CoverSelectionReason = "Cover geometry changed; awaiting fresh selection";
            if (ownedMovement && nav != null && nav.enabled && nav.isOnNavMesh) nav.ResetPath();
        }

        // Called by runtime destructible cover and the separate Editor validation assembly.
        public void NotifyCoverRemoved(int hash)
        {
            if (coverHash == hash) InvalidateCoverGeometry();
        }

        [HideInInspector]
        [SerializeField, TextArea, Tooltip("마지막 탄창 소진 반격의 진행·종료 사유입니다. 다른 위치에서 안정적으로 엄폐한 뒤 같은 적에 대한 시도 기록을 초기화합니다.")]
        private string closeCounterfireStatus;
        public bool IsCounterfiring { get; private set; }
        private Transform counterfireTarget;
        private State counterfireState;
        private float counterfireStartedAt;
        private float counterfireLastShotAt, counterfireSightLostAt = -1f, counterfireProtectedSince = -1f;
        private int counterfireShots;
        private bool counterfireRoundSettled;
        private bool counterfirePreviousStop;
        private Vector3 counterfireOrigin;
        private readonly System.Collections.Generic.HashSet<Transform> counterfireAttempts =
            new System.Collections.Generic.HashSet<Transform>();

        public void RecordCounterfireShot()
        {
            counterfireRoundSettled = false;
            if (IsCounterfiring)
            {
                counterfireShots++;
                counterfireLastShotAt = Time.time;
            }
        }

        public bool ConsumeCounterfireRoundSettlement()
        {
            bool settled = counterfireRoundSettled;
            counterfireRoundSettled = false;
            return settled;
        }

        // Enemy and ally share the remaining-magazine policy. Only actual shots
        // refresh progress; aim animation and normal shot intervals are not shots.
        private string CounterfireExitReason()
        {
            if (!EnableCloseCounterfire || currentState != counterfireState) return "Disabled or state changed";
            if (!HasKnownTarget || aimTarget != counterfireTarget) return "Target ended or changed";
            if (reloading || bullets <= 0) return "Reloading or empty magazine";
            if (CoverReady) return "Cover protection restored";
            if (!HasDirectObservation || BlockedSight())
            {
                if (counterfireSightLostAt < 0f) counterfireSightLostAt = Time.time;
                return Time.time - counterfireSightLostAt >= CounterfireSightLossGrace ? "Sight loss timeout" : null;
            }
            counterfireSightLostAt = -1f;
            // Infinite ammo cannot reach an empty magazine. Retain the old time
            // setting only for this exception, never as a finite-magazine limit.
            if (HasInfiniteAmmo && Time.time - counterfireStartedAt >= Mathf.Max(.1f, CloseCounterfireDuration))
                return "Infinite ammo time limit";
            float interval = classStats != null && classStats.shotRateFactor > 0f
                ? (.1f + 2f * Time.deltaTime) / classStats.shotRateFactor + Time.deltaTime : 0f;
            float progressAt = counterfireShots > 0 ? counterfireLastShotAt : counterfireStartedAt + CounterfireAimGrace;
            if (Time.time - progressAt >= Mathf.Max(CounterfireNoShotTimeout, interval + .1f))
                return "No shot progress timeout";
            return null;
        }

        private bool HandleCloseCounterfire()
        {
            if (IsCounterfiring)
            {
                string reason = CounterfireExitReason();
                if (reason != null)
                {
                    FinishCounterfire(reason);
                    if (EnableCloseCounterfire && currentState == counterfireState && HasKnownTarget &&
                        !CoverReady && !reloading)
                    {
                        if (TryRelocateCover()) return true;
                        FailCover(CoverFailure.LostProtection);
                    }
                    return false;
                }
                currentState.DoActions(this);
                return true; // Skip burst/approach transitions until empty or an exception ends counterfire.
            }

            if (!EnableCloseCounterfire) return false;
            counterfireAttempts.RemoveWhere(target => !IsValidCombatTarget(target));
            if (counterfireAttempts.Count > 0 && IsHoldingCover && MaintainingCover &&
                Vector3.Distance(transform.position, counterfireOrigin) > Mathf.Max(CoverHoldTolerance, CoverArrivalTolerance))
            {
                if (counterfireProtectedSince < 0f) counterfireProtectedSince = Time.time;
                if (Time.time - counterfireProtectedSince >= CounterfireRearmHold)
                {
                    counterfireAttempts.Clear();
                    counterfireProtectedSince = -1f;
                }
            }
            else counterfireProtectedSince = -1f;
            if (currentState == null || currentState.coverPhase == CoverPhase.Move ||
                (currentState.coverPhase != CoverPhase.Hold && currentState.emptyMagazineCoverState == null) ||
                CoverReady || !HasDirectObservation || reloading || bullets <= 0 ||
                CloseCounterfireDistance <= 0f ||
                nav == null || !nav.enabled || !nav.isOnNavMesh ||
                HorizontalDistanceSquared(personalTarget) > CloseCounterfireDistance * CloseCounterfireDistance ||
                BlockedSight() || counterfireAttempts.Contains(aimTarget)) return false;
            // Attack without a reservation is ordinary combat, not a cover-loss event.
            if (currentState.coverPhase == CoverPhase.None && !CoverLookup.IsFinite(CoverSpot)) return false;
            State attack = currentState.coverPhase == CoverPhase.Hold ? currentState.coverFailureState : currentState;
            if (attack == null || attack.coverPhase != CoverPhase.None || !CanUseState(attack) ||
                attack.actions == null || !System.Array.Exists(attack.actions, action => action is AttackAction)) return false;
            counterfireAttempts.Add(aimTarget);
            counterfireTarget = aimTarget;
            counterfireOrigin = transform.position;
            counterfireStartedAt = Time.time;
            counterfireLastShotAt = Time.time;
            counterfireSightLostAt = counterfireProtectedSince = -1f;
            counterfireShots = 0;
            if (currentState != attack)
            {
                TransitionToState(attack, null);
                if (currentState != attack) return false;
                currentState.OnEnableActions(this);
            }
            counterfireState = currentState;
            counterfirePreviousStop = nav.isStopped;
            nav.isStopped = true;
            nav.velocity = Vector3.zero;
            IsCounterfiring = true;
            closeCounterfireStatus = $"Counterfire started: {counterfireTarget.name}; remaining rounds={bullets}; infinite={HasInfiniteAmmo}";
            currentState.DoActions(this);
            return true;
        }

        private void FinishCounterfire(string reason)
        {
            // AttackAction normally debits a fired round after its shot interval.
            // Settle that pending round once before leaving for movement/search.
            if (variables.shotTimer > 0f && !counterfireRoundSettled)
            {
                if (!HasInfiniteAmmo) bullets = Mathf.Max(0, bullets - 1);
                variables.currentShots++;
                // Retain the remaining shot interval if relocation fails and normal
                // fire resumes. AttackAction consumes this flag instead of debiting twice.
                counterfireRoundSettled = true;
            }
            IsCounterfiring = false;
            if (nav != null && nav.enabled && nav.isOnNavMesh) nav.isStopped = counterfirePreviousStop;
            closeCounterfireStatus = $"Counterfire ended: {reason}; shots={counterfireShots}; elapsed={Time.time - counterfireStartedAt:F2}s";
        }

        public bool CoverReady => CoverLookup.IsFinite(CoverSpot) &&
            IsSelectedCoverProtected(CoverSpot);
        public bool ArrivedAtCover => CoverReady && nav != null && nav.enabled && nav.isOnNavMesh &&
            !nav.pathPending && Vector3.Distance(transform.position, CoverSpot) <=
                (aligningCover ? Mathf.Min(.1f, CoverArrivalTolerance) : CoverArrivalTolerance) &&
            (!aligningCover || Vector3.Distance(transform.position + Vector3.up * CoverProbeHeight,
                selectedCoverCollider.ClosestPoint(transform.position + Vector3.up * CoverProbeHeight)) <= CoverAlignmentWallGap) &&
            IsSelectedCoverProtected(transform.position);

        public bool IsHoldingCover => currentState != null && currentState.coverPhase == CoverPhase.Hold;
        private bool coverStoppedAgent;
        public bool MaintainingCover => CoverReady && nav != null && nav.enabled && nav.isOnNavMesh &&
            Vector3.Distance(transform.position, CoverSpot) <= Mathf.Max(CoverArrivalTolerance, CoverHoldTolerance) &&
            IsSelectedCoverProtected(transform.position);
        public bool HasUnprotectedCloseThreat => HasDirectObservation &&
            (personalTarget - transform.position).sqrMagnitude <= nearRadius * nearRadius &&
            !IsSelectedCoverProtected(transform.position);

        // Own only the stop imposed by Hold; every exit releases it before the next action.
        private void SyncCoverMotion(State next)
        {
            if (relocatingCover && (next == null || next.coverPhase != CoverPhase.Move))
                coverRelocationStatus = next != null && next.coverPhase == CoverPhase.Hold
                    ? "Relocation completed; holding protected cover" : "Relocation interrupted by state change";
            if (next == null || next.coverPhase != CoverPhase.Move) recoveringCover = false;
            if (next == null || next.coverPhase != CoverPhase.Move) relocatingCover = false;
            if (next == null || next.coverPhase != CoverPhase.Move) aligningCover = false;
            if (nav == null || !nav.enabled || !nav.isOnNavMesh) { coverStoppedAgent = false; return; }
            if (next != null && next.coverPhase == CoverPhase.Hold)
            {
                if (!coverStoppedAgent) nav.ResetPath();
                nav.isStopped = true;
                nav.velocity = Vector3.zero;
                coverStoppedAgent = true;
            }
            else if (coverStoppedAgent)
            {
                nav.isStopped = false;
                coverStoppedAgent = false;
            }
        }

        public void BeginCoverMovement(bool reuse, float speed)
        {
            if (Time.time < NextCoverAttemptAt) { LastCoverFailure = CoverFailure.RetryCooldown; return; }
            CoverLookup.CoverCandidate candidate = pendingCoverRelocation.GetValueOrDefault();
            bool committed = pendingCoverRelocation.HasValue;
            pendingCoverRelocation = null;
            // 확정된 새 엄폐 경로는 평가에 사용한 회피 속도로 이동한다. 기존 엄폐 복귀/정렬 속도는 유지한다.
            if (committed) speed = generalStats.evadeSpeed;
            bool found = committed || (coverLookup != null && (reuse
                ? coverLookup.TryGetExistingSpot(this, out candidate)
                : coverLookup.TryGetBestCoverSpot(this, speed, out candidate)));
            bool reused = found && reuse && !committed;
            if (!found && coverLookup != null)
                found = coverLookup.TryGetLocalCover(this, out candidate);
            aligningCover = false;
            if (!found && coverLookup != null)
            {
                found = coverLookup.TryGetAlignmentSpot(this, speed, out candidate);
                aligningCover = found;
            }
            // An empty magazine gets one alternative search if the old spot is unusable.
            if (!found && reuse && bullets <= 0 && coverLookup != null)
            {
                // 기존 위치 복귀가 아닌 새 엄폐 탐색으로 전환하므로 노출 평가와 이동 속도를 함께 바꾼다.
                speed = generalStats.evadeSpeed;
                found = coverLookup.TryGetBestCoverSpot(this, speed, out candidate);
            }
            // An advance request with no improvement must not discard usable cover.
            if (!found && !reuse && coverLookup != null)
            {
                found = coverLookup.TryGetExistingSpot(this, out candidate);
                reused = found;
            }
            if (!found) { FailCover(CoverFailure.NoCandidate); return; }
            // One validated candidate is adopted as a whole. Do not re-rank it by target distance here.
            if (CoverLookup.IsFinite(recoveredCoverSpot) &&
                Vector3.Distance(recoveredCoverSpot, candidate.Position) > CoverArrivalTolerance)
                recoveredCoverSpot = Vector3.positiveInfinity;
            AdoptCoverAssessment(candidate, reused);
            coverHash = candidate.Hash;
            CoverSpot = candidate.Position;
            selectedCoverCollider = candidate.Collider;
            LastCoverFailure = CoverFailure.None;
            nav.stoppingDistance = recoveringCover ? .01f :
                (aligningCover ? Mathf.Min(.05f, CoverArrivalTolerance * .5f) : Mathf.Max(.01f, CoverArrivalTolerance * .5f));
            nav.speed = speed;
            coverStoppedAgent = false;
            nav.isStopped = false;
            if (!nav.SetPath(candidate.Path)) { FailCover(CoverFailure.InvalidPath); return; }
            coverProgressAt = Time.time;
            coverBestRemaining = float.PositiveInfinity;
        }

        private void FailCover(CoverFailure reason)
        {
            if (relocatingCover) coverRelocationStatus = $"Relocation failed: {reason}";
            recoveringCover = false;
            relocatingCover = false;
            aligningCover = false;
            pendingCoverRelocation = null;
            LastCoverFailure = reason;
            NextCoverAttemptAt = Time.time + CoverRetryDelay;
            CoverSpot = Vector3.positiveInfinity;
            coverHash = -1;
            selectedCoverCollider = null;
            focusSight = false;
            if (nav != null && nav.enabled && nav.isOnNavMesh) nav.ResetPath();
        }

        // Finish a committed move using known coordinates; never refresh hidden targets.
        private bool CanKeepCoverWithoutSight(State state)
        {
            if (state == null || !HasKnownTarget) return false;
            if (state.coverPhase == CoverPhase.Move)
                return state != currentState || CoverReady;
            return state.coverPhase == CoverPhase.Hold && MaintainingCover;
        }

        private bool HandleEmptyMagazineState()
        {
            if (currentState.emptyMagazineCoverState == null || bullets > 0 || !HasKnownTarget) return false;
            if (reloading) return true;
            State previous = currentState;
            TransitionToState(previous.emptyMagazineCoverState, null);
            if (currentState != previous) currentState.OnEnableActions(this);
            return currentState != previous;
        }

        public bool ShouldReturnAfterBurst()
        {
            if (reloading || bullets <= 0 || variables.currentShots < variables.shotsInRound) return false;
            if (Time.time < NextCoverAttemptAt)
            { coverReturnStatus = $"Retry cooldown; last failure={LastCoverFailure}"; return false; }
            if (coverLookup == null)
            { coverReturnStatus = "CoverLookup missing"; return false; }
            if (CoverReady && !HasUnprotectedCloseThreat && coverLookup.TryGetExistingSpot(this, out _))
            { coverReturnStatus = "Return to reserved cover"; return true; }
            if (coverLookup.TryGetLocalCover(this, out _))
            { coverReturnStatus = "Resume protected cover at current position"; return true; }
            if (coverLookup.TryGetAlignmentSpot(this, generalStats.chaseSpeed, out _))
            { coverReturnStatus = "Align with nearby protecting wall before hiding"; return true; }
            coverReturnStatus = $"No usable reserved/local/alignment cover; target={aimTarget?.name}; " +
                $"spot={CoverSpot}; ready={CoverReady}; last failure={LastCoverFailure}; last search={CoverSelectionReason}";
            return false;
        }

        private float RemainingCoverPathDistance()
        {
            // remainingDistance can stay Infinity even while a complete path is followed.
            Vector3[] corners = nav.path.corners;
            if (corners.Length == 0) return float.PositiveInfinity;
            float distance = Vector3.Distance(transform.position, corners[0]);
            for (int i = 1; i < corners.Length; i++)
                distance += Vector3.Distance(corners[i - 1], corners[i]);
            return distance;
        }

        private State CoverFallback(State state)
        {
            if (bullets <= 0 && state.coverReloadState != null)
            {
                pendingCoverReloadState = state.coverReloadState;
                return pendingCoverReloadState;
            }
            return state.coverFailureState;
        }

        private State ValidateCoverTransition(State next)
        {
            if (next != null && next.emptyMagazineCoverState != null && bullets <= 0 &&
                !reloading && HasKnownTarget) next = next.emptyMagazineCoverState;
            if (next == null || next.coverPhase == CoverPhase.None) return next;
            if (next.coverPhase == CoverPhase.Move && Time.time < NextCoverAttemptAt)
                return CoverFallback(next);
            if (next.coverPhase == CoverPhase.Hold && !ArrivedAtCover)
            {
                FailCover(CoverFailure.LostProtection);
                return CoverFallback(next);
            }
            return next;
        }

        private bool HandleCoverState()
        {
            SyncCoverMotion(currentState);
            if (currentState.coverPhase == CoverPhase.None)
            {
                // Attack owns a return state; unrelated Follow/Search states must not start relocation.
                if (currentState.emptyMagazineCoverState != null && CoverLookup.IsFinite(CoverSpot) && !CoverReady)
                {
                    if (reloading) return true;
                    if (TryRelocateCover()) return true;
                    FailCover(CoverFailure.LostProtection);
                }
                return false;
            }
            if (LastCoverFailure == CoverFailure.None)
            {
                if (!CoverReady)
                {
                    if (currentState.coverPhase == CoverPhase.Hold)
                    {
                        if (reloading) return true;
                        if (TryRelocateCover()) return true;
                    }
                    FailCover(CoverFailure.LostProtection);
                }
                else if (currentState.coverPhase == CoverPhase.Hold && !MaintainingCover)
                {
                    // Never interrupt a committed reload to restart movement/animation.
                    if (reloading) return true;
                    if (TryRecoverCover()) return true;
                    FailCover(CoverFailure.LostProtection);
                }
                else if (currentState.coverPhase == CoverPhase.Move)
                {
                    if (nav == null || !nav.enabled || !nav.isOnNavMesh)
                        FailCover(CoverFailure.InvalidPath);
                    else if (!nav.pathPending)
                    {
                        if (!ArrivedAtCover && (!nav.hasPath || nav.pathStatus != NavMeshPathStatus.PathComplete ||
                            Vector3.Distance(nav.destination, CoverSpot) > CoverArrivalTolerance))
                            FailCover(CoverFailure.InvalidPath);
                        else
                        {
                            float remaining = RemainingCoverPathDistance();
                            if (float.IsFinite(remaining) && remaining < coverBestRemaining - .05f)
                            { coverBestRemaining = remaining; coverProgressAt = Time.time; }
                        }
                    }
                    if (LastCoverFailure == CoverFailure.None && !ArrivedAtCover &&
                        Time.time - coverProgressAt > CoverNoProgressTimeout) FailCover(CoverFailure.NoProgress);
                    if (recoveringCover && LastCoverFailure == CoverFailure.None)
                    {
                        if (!ArrivedAtCover && Time.time - coverRecoveryStarted >= CoverRecoveryTimeout)
                            FailCover(CoverFailure.NoProgress);
                        else if (!ArrivedAtCover)
                        {
                            // Do not let the close-threat decision erase this bounded recovery.
                            focusSight = false;
                            return true;
                        }
                    }
                    if ((relocatingCover || aligningCover) && LastCoverFailure == CoverFailure.None && !ArrivedAtCover)
                    {
                        // Finish the escape instead of cancelling it merely because the flanker is near.
                        focusSight = false;
                        return true;
                    }
                }
            }
            if (LastCoverFailure == CoverFailure.None) return false;
            // Keep the real reload event intact even when the cover becomes invalid.
            if (reloading) return true;
            State previous = currentState;
            TransitionToState(CoverFallback(previous), null);
            if (currentState != previous) currentState.OnEnableActions(this);
            return true;
        }

        private bool TryRelocateCover()
        {
            State move = currentState.emptyMagazineCoverState != null ? currentState.emptyMagazineCoverState :
                (currentState.coverFailureState != null ? currentState.coverFailureState.emptyMagazineCoverState : null);
            if (move == null || move.coverPhase != CoverPhase.Move || !CanUseState(move) ||
                !HasKnownTarget || coverLookup == null || Time.time < NextCoverAttemptAt ||
                Time.time < nextCoverRelocationAt) return false;
            nextCoverRelocationAt = Time.time + CoverRelocationInterval;
            if (!coverLookup.TryGetBestCoverSpot(this, generalStats.evadeSpeed, out var candidate, true))
            {
                coverRelocationStatus = "No safer unattempted destination; use combat fallback";
                return false;
            }
            // Carry the scored path through state entry; ReturnToCover must not select again.
            pendingCoverRelocation = candidate;
            TransitionToState(move, null);
            if (currentState != move) { pendingCoverRelocation = null; return false; }
            relocatingCover = true;
            coverRelocationStatus = $"Relocating to {candidate.Position}; {CoverSelectionReason}";
            enemyAnimation.anim.SetBool("Crouch", false);
            currentState.OnEnableActions(this);
            return true;
        }

        private bool TryRecoverCover()
        {
            State recovery = currentState.coverFailureState != null
                ? currentState.coverFailureState.emptyMagazineCoverState : null;
            if (recovery == null || recovery.coverPhase != CoverPhase.Move || !CanUseState(recovery) ||
                coverLookup == null || nav == null || !nav.enabled || !nav.isOnNavMesh ||
                Vector3.Distance(transform.position, CoverSpot) > Mathf.Max(CoverArrivalTolerance, CoverHoldTolerance) ||
                (CoverLookup.IsFinite(recoveredCoverSpot) &&
                 Vector3.Distance(recoveredCoverSpot, CoverSpot) <= CoverArrivalTolerance) ||
                !coverLookup.TryGetExistingSpot(this, out var candidate)) return false;
            // A short positional correction must not turn into a detour around the wall.
            float length = 0f;
            Vector3 previous = transform.position;
            foreach (Vector3 corner in candidate.Path.corners)
            { length += Vector3.Distance(previous, corner); previous = corner; }
            if (length > Mathf.Max(CoverArrivalTolerance, CoverHoldTolerance) * 2f) return false;
            recoveredCoverSpot = CoverSpot;
            TransitionToState(recovery, null);
            if (currentState != recovery) return false;
            recoveringCover = true;
            coverRecoveryStarted = Time.time;
            // Return normally starts from standing Attack; this path starts from crouched Hold.
            enemyAnimation.anim.SetBool("Crouch", false);
            currentState.OnEnableActions(this);
            return true;
        }
    }
}
