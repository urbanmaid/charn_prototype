using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI
{
    public partial class StateController
    {
        // Runtime configuration belongs to each companion, not the shared Action asset.
        private FollowPlayerAction followConfiguration;
        private bool followOwnsStop;
        private const float FollowResumeMargin = .35f;

        // Per-companion spacing shared by peaceful follow and combat regroup.
        private AllySquadManager spacingOwner;
        private int spacingMode = 1;
        private int spacingOrdinal;
        private Transform spacingTarget;
        private NavMeshPath outwardPath;
        private Vector3 outwardAnchor, outwardGoal, lastOutwardDirection;
        private float outwardElapsed;
        private const float OutwardTolerance = .15f;
        public enum OutwardMoveResult { None, Moving, Completed, NoSpace, Cancelled, TimedOut }
        public OutwardMoveResult FollowOutwardResult { get; private set; }
        public int FollowSpacingMode => spacingMode;
        public bool HasSquadFollowSpacing => spacingOwner != null;

        // Refuse unknown/mixed action states rather than mistaking search/cover for follow.
        public bool CanAdjustFollowSpacing
        {
            get
            {
                if (IsRegrouping || !isActiveAndEnabled || !aiActive || IsAnimationResetPending || reloading ||
                    IsPreparingApproach || IsCheckingDamageDirection || IsCounterfiring ||
                    HasDirectObservation || CommandTarget != null ||
                    nav == null || !nav.enabled || !nav.isOnNavMesh ||
                    (nav.isStopped && !followOwnsStop) || currentState == null ||
                    currentState.coverPhase != CoverPhase.None || currentState.actions == null ||
                    currentState.actions.Length == 0) return false;
                foreach (Action action in currentState.actions)
                    if (!(action is FollowPlayerAction) && !(action is PatrolAction)) return false;
                return GetComponent<AllyStateController>() != null;
            }
        }

        internal void SetSquadFollowSpacing(AllySquadManager owner, int mode, int ordinal)
        {
            float previous = EffectiveFollowDistance();
            CancelFollowOutward(OutwardMoveResult.Cancelled);
            spacingOwner = owner;
            spacingMode = Mathf.Clamp(mode, 0, 2);
            spacingOrdinal = ordinal;
            ResolveSquadFollowTarget();
            float distance = EffectiveFollowDistance();
            FollowOutwardResult = OutwardMoveResult.None;
            if (distance > previous && FollowTarget != null &&
                Vector3.Distance(transform.position, FollowTarget.position) < distance - OutwardTolerance)
                BeginFollowOutward(distance);
        }

        internal void ClearSquadFollowSpacing()
        {
            CancelCombatRegroup("Squad spacing cleared");
            CancelFollowOutward(OutwardMoveResult.Cancelled);
            spacingOwner = null;
            spacingTarget = null;
            spacingMode = 1;
        }

        public Transform ResolveSquadFollowTarget()
        {
            var player = spacingOwner != null ? spacingOwner.CurrentPlayer : null;
            Transform target = player != null && player.transform != transform ? player.transform : null;
            if (spacingTarget != target) CancelFollowOutward(OutwardMoveResult.Cancelled);
            spacingTarget = target;
            FollowTarget = target;
            return target;
        }

        private float EffectiveFollowDistance()
        {
            float baseline = followConfiguration != null ? Mathf.Max(0f, followConfiguration.followDistance) : 3f;
            if (spacingOwner == null || spacingMode == 1) return baseline;
            float playerRadius = .3f;
            if (FollowTarget != null && FollowTarget.TryGetComponent(out CapsuleCollider capsule))
                playerRadius = capsule.radius * Mathf.Max(Mathf.Abs(FollowTarget.lossyScale.x), Mathf.Abs(FollowTarget.lossyScale.z));
            float minimum = (nav != null ? nav.radius : .3f) + playerRadius + .2f;
            // Preserve unusually large existing settings rather than shrinking them by a fixed cap.
            return Mathf.Clamp(baseline * (spacingMode == 0 ? .6f : 1.5f), minimum, Mathf.Max(minimum, baseline * 1.5f));
        }

        private void BeginFollowOutward(float distance)
        {
            Vector3 radial = transform.position - FollowTarget.position;
            radial.y = 0f;
            if (radial.sqrMagnitude < .001f) radial = lastOutwardDirection;
            if (radial.sqrMagnitude < .001f) radial = Quaternion.Euler(0, spacingOrdinal * 137.5f, 0) * Vector3.forward;
            radial.Normalize();
            lastOutwardDirection = radial;
            outwardPath ??= new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
            for (int i = 0; i < 5; i++)
            {
                float angle = i == 0 ? 0f : ((i + 1) / 2) * 30f * (i % 2 == 1 ? 1f : -1f);
                Vector3 candidate = FollowTarget.position + Quaternion.Euler(0, angle, 0) * radial * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, .5f, filter) ||
                    Mathf.Abs(hit.position.y - transform.position.y) > 1f ||
                    Mathf.Abs(hit.position.y - FollowTarget.position.y) > 1f) continue;
                Vector3 fromPlayer = hit.position - FollowTarget.position; fromPlayer.y = 0f;
                if (fromPlayer.magnitude < distance - OutwardTolerance || !HasOutwardSpace(hit.position)) continue;
                if (!nav.CalculatePath(hit.position, outwardPath) || outwardPath.status != NavMeshPathStatus.PathComplete) continue;
                ReleaseFollowStop();
                nav.stoppingDistance = OutwardTolerance;
                if (!nav.SetPath(outwardPath)) continue;
                outwardGoal = hit.position;
                outwardAnchor = FollowTarget.position;
                outwardElapsed = 0f;
                FollowOutwardResult = OutwardMoveResult.Moving;
                return;
            }
            FollowOutwardResult = OutwardMoveResult.NoSpace;
            nav.stoppingDistance = EffectiveFollowDistance();
        }

        private bool HasOutwardSpace(Vector3 candidate)
        {
            // Current roster only; no global search and no fixed formation slots.
            foreach (GameObject member in spacingOwner.AllPAOnField)
            {
                if (member == null || member == gameObject || !member.activeInHierarchy) continue;
                if (Mathf.Abs(member.transform.position.y - candidate.y) > 1f) continue;
                float radius = member.TryGetComponent(out NavMeshAgent other) ? other.radius : .3f;
                Vector3 delta = member.transform.position - candidate; delta.y = 0f;
                float clearance = nav.radius + radius + .2f;
                if (delta.sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }

        private bool UpdateFollowOutward()
        {
            if (FollowOutwardResult != OutwardMoveResult.Moving) return false;
            if (!CanAdjustFollowSpacing || (FollowTarget.position - outwardAnchor).sqrMagnitude > .75f * .75f)
            { CancelFollowOutward(OutwardMoveResult.Cancelled); return false; }
            outwardElapsed += Time.deltaTime;
            if (outwardElapsed >= 4f) { CancelFollowOutward(OutwardMoveResult.TimedOut); return false; }
            if (!nav.pathPending && (nav.pathStatus != NavMeshPathStatus.PathComplete ||
                (!nav.hasPath && Vector3.Distance(transform.position, outwardGoal) > OutwardTolerance + .1f)))
            { CancelFollowOutward(OutwardMoveResult.NoSpace); return false; }
            if (!nav.pathPending && Vector3.Distance(transform.position, outwardGoal) <= OutwardTolerance + .1f)
            { CancelFollowOutward(OutwardMoveResult.Completed); return false; }
            nav.stoppingDistance = OutwardTolerance;
            return true;
        }

        internal void CancelSquadFollowMovement()
        {
            CancelCombatRegroup("Squad movement cancelled");
            CancelFollowOutward(OutwardMoveResult.Cancelled);
        }

        private void CancelFollowOutward(OutwardMoveResult result)
        {
            if (FollowOutwardResult != OutwardMoveResult.Moving) return;
            FollowOutwardResult = result;
            if (nav != null && nav.enabled && nav.isOnNavMesh)
            {
                // Do not erase a destination already taken over by another system.
                if ((nav.destination - outwardGoal).sqrMagnitude < .01f) nav.ResetPath();
                if (Mathf.Approximately(nav.stoppingDistance, OutwardTolerance))
                    nav.stoppingDistance = EffectiveFollowDistance();
            }
        }


        public void ConfigureFollow(FollowPlayerAction configuration, Transform target)
        {
            if (FollowTarget != target) CancelFollowOutward(OutwardMoveResult.Cancelled);
            followConfiguration = configuration;
            FollowTarget = target;
        }

        public void UpdateFollowMovement()
        {
            if (spacingOwner != null) ResolveSquadFollowTarget();
            if (nav == null || !nav.enabled || !nav.isOnNavMesh)
            { CancelFollowOutward(OutwardMoveResult.Cancelled); return; }
            if (FollowTarget == null || !FollowTarget.gameObject.activeInHierarchy)
            {
                ReleaseFollowStop();
                nav.ResetPath();
                return;
            }
            // Recover configuration if combat was the initial state of this character.
            if (followConfiguration == null && stateGroup != null)
                foreach (State state in stateGroup.states)
                {
                    if (state == null || state.actions == null) continue;
                    foreach (Action action in state.actions)
                        if (action is FollowPlayerAction follow) { followConfiguration = follow; break; }
                    if (followConfiguration != null) break;
                }
            float distance = EffectiveFollowDistance();
            nav.stoppingDistance = distance;
            nav.speed = followConfiguration != null ? Mathf.Max(0f, followConfiguration.followSpeed) : generalStats.chaseSpeed;
            if (IsRegrouping) { UpdateRegroupFollowMovement(distance); return; }
            if (UpdateFollowOutward()) return;
            float separation = Vector3.Distance(transform.position, FollowTarget.position);
            if (followOwnsStop && separation > distance + FollowResumeMargin) ReleaseFollowStop();
            if (!followOwnsStop && separation <= distance && !nav.isStopped)
            {
                followOwnsStop = true;
                nav.ResetPath();
                nav.isStopped = true;
                nav.velocity = Vector3.zero;
            }
            if (!followOwnsStop) nav.SetDestination(FollowTarget.position);
        }

        // Release before approach preparation captures navigation state, and before
        // the destination state's actions establish their own movement policy.
        private void ReleaseFollowStop()
        {
            CancelFollowOutward(OutwardMoveResult.Cancelled);
            if (!followOwnsStop) return;
            if (nav != null && nav.enabled && nav.isOnNavMesh) nav.isStopped = false;
            followOwnsStop = false;
        }
    }
}
