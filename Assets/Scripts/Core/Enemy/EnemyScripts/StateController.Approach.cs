using UnityEngine;

namespace EnemyAI
{
    public partial class StateController
    {
        // Preparation belongs to this NPC, never to a shared State/Action asset.
        public bool IsPreparingApproach { get; private set; }
        private bool approachOwnsStop, approachPreviousStop, approachInitialEntry, approachReloadRequested;
        private float coverApproachSince = -1f, nextCoverApproachCheck;
        private Transform coverApproachTarget;

        public bool HasCoverApproachIntent => CoverApproachDelay >= 0f &&
            coverApproachTarget == aimTarget && HasKnownTarget && coverApproachSince >= 0f;
        public bool CoverApproachReady => HasCoverApproachIntent && Time.time - coverApproachSince >= CoverApproachDelay;

        public void ResetCoverApproach()
        {
            coverApproachSince = -1f;
            nextCoverApproachCheck = 0f;
            coverApproachTarget = null;
        }

        // Called by the hold action, after the controller validates cover safety.
        // This clock continues through reloads without altering the burst/hide clock.
        public void UpdateCoverApproach()
        {
            if (currentState == null || currentState.coverPhase != CoverPhase.Hold ||
                CoverApproachDelay < 0f || !HasKnownTarget)
            { ResetCoverApproach(); return; }
            if (coverApproachTarget != aimTarget) ResetCoverApproach();
            if (Time.time < nextCoverApproachCheck && (reloading || !CoverApproachReady)) return;
            coverApproachTarget = aimTarget;
            nextCoverApproachCheck = Time.time + .2f;
            bool needsApproach = false;
            if (!HasDirectObservation)
                needsApproach = IsApproachState(currentState.unobservedTargetState) && CanUseState(currentState.unobservedTargetState);
            else
            {
                State attack = currentState.coverFailureState;
                if (attack != null && attack.transitions != null)
                    foreach (Transition transition in attack.transitions)
                        if (transition != null && transition.decision is ClearShotDecision shot &&
                            IsApproachState(transition.falseState) && CanUseState(transition.falseState))
                        {
                            needsApproach = !shot.CanShootWithoutLeavingCover(this);
                            break;
                        }
            }
            if (!needsApproach) { coverApproachSince = -1f; return; }
            if (coverApproachSince < 0f) coverApproachSince = Time.time;
            // Reload completion remains owned by the real animation event. Hold's
            // transition guard prevents departure until both waits have finished.
            TryStartWeaponReload(true);
        }

        private static bool IsApproachState(State state)
        {
            if (state == null || state.coverPhase != CoverPhase.None || state.actions == null) return false;
            foreach (Action action in state.actions)
                if (action is SearchAction || action is GoToShotSpotAction) return true;
            return false;
        }

        // ReloadAction keeps its empty-magazine policy. Only approach preparation
        // requests a top-up. Both paths use the same animation completion event.
        public bool TryStartWeaponReload(bool allowPartial = false)
        {
            // FSM 외부 요청도 교환 초기화 구간에서는 장전 트리거를 만들지 않는다.
            if (IsAnimationResetPending || IsRegrouping) return false;
            // A weapon can be absent while an NPC is spawning, unequipped, or being removed.
            if (enemyAnimation == null || enemyAnimation.anim == null || classStats == null || variables == null)
                return false;
            if (reloading) return true;
            if (bullets > 0 && (!allowPartial || HasInfiniteAmmo)) return false;
            if (allowPartial && bullets >= EffectiveMagBullets && variables.shotTimer <= 0f) return false;

            // A shot is normally debited after its interval. Settle it before a
            // reload so the first shot AFTER reloading cannot debit that old round.
            if (variables.shotTimer > 0f)
            {
                if (!ConsumeCounterfireRoundSettlement())
                {
                    if (!HasInfiniteAmmo) bullets = Mathf.Max(0, bullets - 1);
                    variables.currentShots++;
                }
                variables.shotTimer = 0f;
            }
            enemyAnimation.anim.ResetTrigger("Shooting");
            enemyAnimation.anim.SetTrigger("Reload");
            reloading = true;
            if (classStats.reloadSound != null)
                AudioSource.PlayClipAtPoint(classStats.reloadSound,
                    enemyAnimation.gunMuzzle != null ? enemyAnimation.gunMuzzle.position : transform.position + Vector3.up, 2f);
            return true;
        }

        private bool TryPrepareApproach(State next)
        {
            if (!IsApproachState(next) || !HasKnownTarget) return false;
            if (!reloading && ((HasInfiniteAmmo && bullets > 0) ||
                (bullets >= EffectiveMagBullets && variables.shotTimer <= 0f))) return false;
            if (IsPreparingApproach) return true;
            // Defer an escape decision to Update. Relocating inside TransitionToState
            // would make its caller run the movement entry actions a second time.
            bool needsEscape = ApproachNeedsEscape();
            if (!needsEscape && !TryStartWeaponReload(true)) return false;
            approachReloadRequested = !needsEscape;
            IsPreparingApproach = true;
            if (nav != null && nav.enabled && nav.isOnNavMesh)
            {
                approachPreviousStop = nav.isStopped;
                approachOwnsStop = true;
                nav.isStopped = true;
                nav.velocity = Vector3.zero;
            }
            return true;
        }

        private bool ApproachNeedsEscape() => HasKnownTarget &&
            (HasUnprotectedCloseThreat || (CoverLookup.IsFinite(CoverSpot) && !CoverReady));

        private bool HandleApproachPreparation()
        {
            if (!IsPreparingApproach) return false;
            // Preserve the reload event while allowing a confirmed safer escape
            // path to start. TryRelocateCover already rate-limits failed searches.
            if (ApproachNeedsEscape() && TryRelocateCover()) return true;
            if (!approachReloadRequested)
            {
                approachReloadRequested = true;
                TryStartWeaponReload(true);
            }
            if (reloading) return true;
            bool initialEntry = approachInitialEntry;
            ReleaseApproachPreparation();
            // Do not execute a saved destination or fire another burst before
            // reevaluating: the target may have died, changed, or become visible.
            if (HandleInvalidTargetState() || HandleEmptyMagazineState() ||
                HandleCoverState() || HandleUnobservedTargetState()) return true;
            if (initialEntry) currentState.OnEnableActions(this);
            else currentState.CheckTransitions(this);
            return true;
        }

        private void ReleaseApproachPreparation()
        {
            if (approachOwnsStop && nav != null && nav.enabled && nav.isOnNavMesh)
                nav.isStopped = approachPreviousStop;
            IsPreparingApproach = approachOwnsStop = approachInitialEntry = approachReloadRequested = false;
        }
    }
}
