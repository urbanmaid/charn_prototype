using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI
{
    public partial class StateController
    {
        public enum RegroupPhase { None, PendingSafeExit, Returning, Settling, Completed, Failed, Cancelled }
        public RegroupPhase CombatRegroupPhase { get; private set; }
        public string CombatRegroupReason { get; private set; }
        public bool IsRegrouping => CombatRegroupPhase == RegroupPhase.PendingSafeExit ||
            CombatRegroupPhase == RegroupPhase.Returning || CombatRegroupPhase == RegroupPhase.Settling;
        private State regroupReturnState;
        private Transform regroupPlayer;
        private float regroupWait, regroupElapsed, regroupStable, regroupSettle;
        private bool regroupOwnsWaitStop, regroupPreviousStop, regroupPathValid;
        private NavMeshPath regroupPath;
        private Vector3[] regroupCorners;
        private Vector3 regroupAnchor, regroupGoal;
        private float regroupNextPath, regroupPathDistance, regroupProgressTime, regroupBestDistance;
        private int regroupRetries;
        private float regroupWatchUntil;
        public bool IsRegroupWatchActive => regroupWatchUntil > Time.time;

        // Resolve a unique, already connected follow State by Action role, never by name.
        // Ambiguous/custom groups are rejected instead of guessing or adding a new State.
        private State ResolveRegroupReturnState()
        {
            if (stateGroup == null || stateGroup.states == null) return null;
            State found = null;
            foreach (State state in stateGroup.states)
            {
                if (state == null || state.coverPhase != CoverPhase.None || state.actions == null ||
                    state.actions.Length != 1 || !(state.actions[0] is FollowPlayerAction)) continue;
                if (found != null && found != state) return null;
                found = state;
            }
            return found;
        }

        public bool CanAcceptCombatRegroup => isActiveAndEnabled && aiActive && !IsAnimationResetPending &&
            nav != null && nav.enabled && nav.isOnNavMesh && enemyAnimation != null &&
            enemyAnimation.anim != null && variables != null && GetComponent<AllyStateController>() != null &&
            GetComponent<HealthManager>() is HealthManager health && !health.dead &&
            ResolveRegroupReturnState() != null;

        internal void RequestCombatRegroup(AllySquadManager owner, int ordinal)
        {
            // Repeated input is accepted without restarting any deadline or entering Actions twice.
            if (IsRegrouping) return;
            regroupWatchUntil = 0f;
            regroupReturnState = ResolveRegroupReturnState();
            regroupPlayer = owner.CurrentPlayer.transform;
            ConfigureFollow((FollowPlayerAction)regroupReturnState.actions[0], regroupPlayer);
            SetSquadFollowSpacing(owner, 0, ordinal);
            CombatRegroupPhase = RegroupPhase.PendingSafeExit;
            CombatRegroupReason = "Waiting for existing reload/cast";
            regroupWait = regroupElapsed = regroupStable = regroupSettle = 0f;
            regroupProgressTime = 0f; regroupBestDistance = float.PositiveInfinity; regroupRetries = 0;
            regroupNextPath = 0f; regroupPathValid = false;
            regroupCorners ??= new Vector3[64];
            FinishDamageCheck("Regroup", false, false);
            if (IsCounterfiring) FinishCounterfire("Regroup");
            SettlePendingRegroupShot();
            // Do not reset Reload: its real animation completion event remains authoritative.
            enemyAnimation.anim.ResetTrigger("Shooting");
            ReleaseApproachPreparation();
            ResetCoverApproach();
            EndTemporaryEngagement();
            GetComponent<AllyStateController>().ClearEnemyTarget();
            ClearCommandTarget(CommandTarget);
            SelectKnownTarget(null);
            regroupPreviousStop = nav.isStopped;
            regroupOwnsWaitStop = true;
            nav.isStopped = true;
            nav.velocity = Vector3.zero;
        }

        private void SettlePendingRegroupShot()
        {
            if (variables.shotTimer <= 0f) return;
            if (!ConsumeCounterfireRoundSettlement())
            {
                if (!HasInfiniteAmmo) bullets = Mathf.Max(0, bullets - 1);
                variables.currentShots++;
            }
            variables.shotTimer = 0f;
        }

        private void ReleaseRegroupWaitStop()
        {
            if (regroupOwnsWaitStop && nav != null && nav.enabled && nav.isOnNavMesh)
                nav.isStopped = regroupPreviousStop;
            regroupOwnsWaitStop = false;
        }

        private bool UpdateCombatRegroup()
        {
            if (!IsRegrouping) return false;
            var health = GetComponent<HealthManager>();
            if (!aiActive || IsAnimationResetPending || health == null || health.dead ||
                spacingOwner == null || !spacingOwner.isActiveAndEnabled ||
                spacingOwner.CurrentPlayer == null || spacingOwner.CurrentPlayer.transform != regroupPlayer ||
                !regroupPlayer.gameObject.activeInHierarchy || nav == null || !nav.enabled || !nav.isOnNavMesh)
            { EndCombatRegroup(RegroupPhase.Cancelled, "Actor/player/navigation unavailable"); return true; }
            var playerHealth = regroupPlayer.GetComponent<HealthManager>();
            if (playerHealth != null && playerHealth.dead)
            { EndCombatRegroup(RegroupPhase.Cancelled, "Controlled player downed"); return true; }
            // Death/restore/terminal reset must cancel even when the stage has paused time.
            if (Time.timeScale <= 0f) return true;

            // Keep evidence fresh, without selection/close threats/damage reactions taking movement ownership.
            UpdateTargetKnowledge(true);
            if (CombatRegroupPhase == RegroupPhase.PendingSafeExit)
            {
                regroupWait += Time.deltaTime;
                var caster = GetComponent<ReviveCaster>();
                if (reloading || (caster != null && caster.IsCasting))
                {
                    if (regroupWait >= 6f) EndCombatRegroup(RegroupPhase.Failed, "Existing action did not finish");
                    return true;
                }
                ReleaseRegroupWaitStop();
                SyncCoverMotion(null);
                CoverSpot = Vector3.positiveInfinity; coverHash = -1; selectedCoverCollider = null;
                pendingCoverRelocation = null; recoveredCoverSpot = Vector3.positiveInfinity;
                pendingCoverReloadState = null; hasCoverAssessment = false; SelectedCoverAssessment = default;
                attemptedCoverPositions.Clear(); coverEpochThreats.Clear();
                NextCoverAttemptAt = nextCoverRelocationAt = 0f;
                ReleaseFollowStop();
                nav.ResetPath(); nav.isStopped = false;
                focusSight = false; Strafing = false; Aiming = false;
                enemyAnimation.AbortPendingAim(); enemyAnimation.anim.SetBool("Crouch", false);
                if (currentState != regroupReturnState)
                { currentState = regroupReturnState; currentState.OnEnableActions(this); }
                CombatRegroupPhase = RegroupPhase.Returning;
                CombatRegroupReason = "Returning to controlled player";
            }
            regroupElapsed += Time.deltaTime;
            if (regroupElapsed >= 12f)
            { EndCombatRegroup(RegroupPhase.Failed, "Return timed out"); return true; }
            UpdateFollowMovement();
            if (!IsRegrouping) return true;
            float distance = EffectiveFollowDistance();
            bool near = regroupPathValid && !nav.pathPending &&
                regroupPathDistance <= distance + .05f &&
                Vector3.Distance(transform.position, regroupPlayer.position) <= distance + .05f &&
                Mathf.Abs(transform.position.y - regroupPlayer.position.y) <= 1f;
            if (CombatRegroupPhase == RegroupPhase.Settling)
            {
                if (!near) { CombatRegroupPhase = RegroupPhase.Returning; regroupStable = regroupSettle = 0f; }
                else if ((regroupSettle += Time.deltaTime) >= .75f)
                    EndCombatRegroup(RegroupPhase.Completed, "Arrived; reassess from current observation");
            }
            else
            {
                regroupStable = near && nav.velocity.sqrMagnitude <= .04f ? regroupStable + Time.deltaTime : 0f;
                if (regroupStable >= .4f) { CombatRegroupPhase = RegroupPhase.Settling; regroupSettle = 0f; }
            }
            return true;
        }

        // Called only from the common follow path. Normal follow cadence is unchanged.
        private void UpdateRegroupFollowMovement(float distance)
        {
            if (CombatRegroupPhase == RegroupPhase.PendingSafeExit) return;
            // On a slope, do not stop outside the height tolerance and wait forever.
            if (Mathf.Abs(transform.position.y - regroupPlayer.position.y) > 1f)
                distance = Mathf.Min(distance, .15f);
            nav.stoppingDistance = distance;
            bool moved = (regroupPlayer.position - regroupAnchor).sqrMagnitude > .25f * .25f;
            if (moved) regroupPathValid = false;
            if (Time.time >= regroupNextPath || !regroupPathValid)
            {
                regroupNextPath = Time.time + .25f;
                var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
                regroupPath ??= new NavMeshPath();
                if (!NavMesh.SamplePosition(regroupPlayer.position, out NavMeshHit hit, .75f, filter) ||
                    Mathf.Abs(hit.position.y - regroupPlayer.position.y) > 1f ||
                    !nav.CalculatePath(hit.position, regroupPath) || regroupPath.status != NavMeshPathStatus.PathComplete)
                { EndCombatRegroup(RegroupPhase.Failed, "No complete return path"); return; }
                int count = regroupPath.GetCornersNonAlloc(regroupCorners);
                if (count == 0 || count >= regroupCorners.Length)
                { EndCombatRegroup(RegroupPhase.Failed, "Path evidence unavailable"); return; }
                float length = Vector3.Distance(transform.position, regroupCorners[0]);
                for (int i = 1; i < count; i++) length += Vector3.Distance(regroupCorners[i-1], regroupCorners[i]);
                regroupPathDistance = length;
                regroupAnchor = regroupPlayer.position; regroupGoal = hit.position; regroupPathValid = true;
                if (moved) { regroupBestDistance = length; regroupProgressTime = regroupElapsed; }
                if (length < regroupBestDistance - .2f) { regroupBestDistance = length; regroupProgressTime = regroupElapsed; }
                bool retry = length > distance && regroupElapsed - regroupProgressTime >= 2f;
                if (retry)
                {
                    if (++regroupRetries > 2) { EndCombatRegroup(RegroupPhase.Failed, "No return progress"); return; }
                    regroupProgressTime = regroupElapsed;
                }
                if (length > distance && (moved || !nav.hasPath || nav.isPathStale || retry))
                {
                    ReleaseFollowStop();
                    if (!nav.SetPath(regroupPath)) { EndCombatRegroup(RegroupPhase.Failed, "Return path rejected"); return; }
                }
            }
            // Path length prevents a nearby point across a wall being mistaken for arrival.
            if (followOwnsStop && regroupPathDistance > distance + FollowResumeMargin) ReleaseFollowStop();
            if (!followOwnsStop && regroupPathDistance <= distance && !nav.isStopped)
            { followOwnsStop = true; nav.ResetPath(); nav.isStopped = true; nav.velocity = Vector3.zero; }
        }

        internal void CancelCombatRegroup(string reason)
        {
            regroupWatchUntil = 0f;
            if (IsRegrouping) EndCombatRegroup(RegroupPhase.Cancelled, reason);
        }

        private void EndCombatRegroup(RegroupPhase phase, string reason)
        {
            CombatRegroupPhase = phase; CombatRegroupReason = reason;
            ReleaseRegroupWaitStop();
            ReleaseFollowStop();
            if (nav != null && nav.enabled && nav.isOnNavMesh && regroupPathValid &&
                (nav.destination - regroupGoal).sqrMagnitude < .01f) nav.ResetPath();
            regroupPathValid = false;
            if (phase == RegroupPhase.Completed)
            {
                // Discard selection locks, not observations. Reuse this frame's fresh sensing
                // once, then let the existing Follow State's Decisions enter combat next frame.
                EndTemporaryEngagement();
                SelectKnownTarget(null);
                regroupWatchUntil = Time.time + 2f;
                SelectTargetFromCurrentObservation();
            }
            else { regroupWatchUntil = 0f; knowledgeFrame = -1; }
        }
    }
}
