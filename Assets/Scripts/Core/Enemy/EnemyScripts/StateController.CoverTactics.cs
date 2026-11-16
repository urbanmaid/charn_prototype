using System.Collections.Generic;
using UnityEngine;

namespace EnemyAI
{
    public readonly struct CoverThreat
    {
        public readonly int Id;
        public readonly Vector3 Position;
        public readonly float Weight;
        public CoverThreat(int id, Vector3 position, float weight)
        { Id = id; Position = position; Weight = weight; }
    }

    public readonly struct CoverAssessment
    {
        public readonly float Risk, ExposureSeconds, ContinuousSeconds, RangeError, TravelSeconds;
        public bool FullyProtected => Risk <= .0001f;
        public CoverAssessment(float risk, float exposure, float continuous, float range, float travel)
        { Risk = risk; ExposureSeconds = exposure; ContinuousSeconds = continuous; RangeError = range; TravelSeconds = travel; }
    }

    public partial class StateController
    {
        public CoverAssessment SelectedCoverAssessment { get; private set; }
        public string CoverSelectionReason { get; internal set; }
        private bool hasCoverAssessment;
        private readonly List<CoverThreat> coverEpochThreats = new List<CoverThreat>();
        private readonly List<Vector3> attemptedCoverPositions = new List<Vector3>();
        private readonly List<CoverThreat> validationThreats = new List<CoverThreat>();

        // Read stored evidence only: never query an unseen target's Transform position.
        public void CopyCoverThreats(List<CoverThreat> result)
        {
            result.Clear();
            foreach (var entry in knownTargets)
            {
                Knowledge info = entry.Value;
                float age = Time.time - info.observedAt;
                if (!IsValidCombatTarget(entry.Key) || !Finite(info.position) ||
                    !float.IsFinite(age) || age > blindEngageTime) continue;
                float confidence = info.visible ? 1f : Mathf.Lerp(1f, .25f,
                    Mathf.Clamp01(age / Mathf.Max(.01f, blindEngageTime)));
                if (!info.visible && info.source == TargetPositionSource.Hearing) confidence *= .75f;
                result.Add(new CoverThreat(entry.Key.GetInstanceID(), info.position, confidence));
            }
            // Compatibility for callers that supply an explicit known personalTarget.
            Transform selectedOwner = CombatTargetOwner.Resolve(aimTarget);
            if (HasKnownTarget && !knownTargets.ContainsKey(selectedOwner))
                result.Add(new CoverThreat(selectedOwner.GetInstanceID(), personalTarget, 1f));
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        internal void RefreshCoverEpoch(List<CoverThreat> threats)
        {
            bool changed = threats.Count != coverEpochThreats.Count;
            for (int i = 0; !changed && i < threats.Count; i++)
                changed = threats[i].Id != coverEpochThreats[i].Id ||
                    (threats[i].Position - coverEpochThreats[i].Position).sqrMagnitude >=
                    CoverThreatChangeDistance * CoverThreatChangeDistance;
            if (!changed) return;
            coverEpochThreats.Clear(); coverEpochThreats.AddRange(threats);
            attemptedCoverPositions.Clear();
        }

        internal bool AlreadyAttemptedCover(Vector3 point) => attemptedCoverPositions.Exists(
            previous => (previous - point).sqrMagnitude < Mathf.Max(.5f, CoverArrivalTolerance * 2f) *
                Mathf.Max(.5f, CoverArrivalTolerance * 2f));

        private void AdoptCoverAssessment(CoverLookup.CoverCandidate candidate, bool reuse)
        {
            if (!reuse)
            {
                if (CoverLookup.IsFinite(CoverSpot) && !AlreadyAttemptedCover(CoverSpot))
                    attemptedCoverPositions.Add(CoverSpot);
                if (!AlreadyAttemptedCover(candidate.Position)) attemptedCoverPositions.Add(candidate.Position);
            }
            SelectedCoverAssessment = candidate.Assessment;
            hasCoverAssessment = true;
        }

        private bool IsSelectedCoverProtected(Vector3 point)
        {
            if (!HasKnownTarget || !CoverLookup.IsFinite(point) || selectedCoverCollider == null ||
                !selectedCoverCollider.enabled || !selectedCoverCollider.gameObject.activeInHierarchy) return false;
            CopyCoverThreats(validationThreats);
            if (validationThreats.Count == 0) return false;
            float risk = CoverLookup.ExposureAt(this, point, CoverProbeHeight, validationThreats);
            // Partial cover remains partial; do not immediately reject it with the old single-target test.
            float limit = hasCoverAssessment ? SelectedCoverAssessment.Risk : 0f;
            return risk < .9999f && risk <= limit + .001f;
        }
    }
}
