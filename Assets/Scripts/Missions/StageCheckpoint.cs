using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    [Serializable]
    public sealed class CheckpointMissionBinding
    {
        public string missionId;
        [Min(0)] public int order;
    }

    /// <summary>Explicit membership, independent of route objective type and object name.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageObjectId))]
    public sealed class StageCheckpoint : MonoBehaviour
    {
        [SerializeField] private CheckpointMissionBinding[] missions = Array.Empty<CheckpointMissionBinding>();
        [SerializeField, Min(.1f)] private float radius = 4f;
        [SerializeField, Min(.1f)] private float verticalTolerance = 3f;
        [SerializeField] private Transform resumePoint;
        [SerializeField] private bool collectionCheckpoint;
        [SerializeField] private bool initialAnchorOnly;
        public bool TryGetOrder(string missionId, StageActionLedger actions, out int order)
        {
            if (!TryGetOrder(missionId, out order)) return false;
            if (collectionCheckpoint) order = actions.CollectionSequence(Id);
            return !collectionCheckpoint || order > 0;
        }
        public string Id => GetComponent<StageObjectId>().Id;
        public Transform ResumePoint => resumePoint != null ? resumePoint : transform;

        public bool TryGetOrder(string missionId, out int order)
        {
            order = -1;
            if (missions == null || string.IsNullOrWhiteSpace(missionId)) return false;
            foreach (var binding in missions)
            {
                if (binding == null || binding.missionId != missionId) continue;
                if (order >= 0 || binding.order < 0) return false;
                order = binding.order;
            }
            return order >= 0;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            return !initialAnchorOnly && isActiveAndEnabled && radius > 0 && verticalTolerance > 0
                && Mathf.Abs(offset.y) <= verticalTolerance
                && offset.x * offset.x + offset.z * offset.z <= radius * radius;
        }
    }
}
