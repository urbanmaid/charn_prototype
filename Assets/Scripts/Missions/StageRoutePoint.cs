using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Inspectable reach/pickup/interaction point for the route review prefab.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageObjectId))]
    public sealed class StageRoutePoint : MonoBehaviour
    {
        [SerializeField] private MissionObjectiveType objectiveType = MissionObjectiveType.ReachLocation;
        [SerializeField, Min(0.1f)] private float radius = 3f;
        [SerializeField, Min(0.1f)] private float heightTolerance = 3f;
        [SerializeField] private MissionItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private bool consumeItem;
        [Tooltip("추가로 모두 보유해야 하는 물품. 소비하지 않습니다.")]
        [SerializeField] private MissionItemDefinition[] requiredItems = System.Array.Empty<MissionItemDefinition>();
        [SerializeField] private string prompt = "상호작용";
        [SerializeField] private GameObject visual;
        [Header("Objective presentation")]
        [SerializeField] private bool manageObjectivePresentation;
        [SerializeField] private GameObject[] objectiveIndicators = System.Array.Empty<GameObject>();
        [SerializeField] private MissionGoalAnimator interactionAnimator;
        [SerializeField] private MissionGoalBeacon goalBeacon;
        [SerializeField] private bool collectOnApproach;
        private StageObjectId identity;
        public string Id => (identity != null ? identity : identity = GetComponent<StageObjectId>()).Id;
        public string Prompt => prompt;
        public bool NeedsInput => objectiveType != MissionObjectiveType.ReachLocation
            && !(collectOnApproach && objectiveType == MissionObjectiveType.CollectItems);
        public MissionObjectiveType ObjectiveType => objectiveType;

        public bool Contains(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            return Mathf.Abs(delta.y) <= heightTolerance && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
        }

        public bool Apply(StageMissionRuntime runtime, Vector3 actorPosition, bool interactionPressed)
        {
            if (runtime == null || !runtime.HasStarted || runtime.IsCleared || !Contains(actorPosition) || (NeedsInput && !interactionPressed)) return false;
            MissionDefinition active = runtime.Definitions[runtime.CurrentMissionIndex];
            if (active.Type != objectiveType && objectiveType != MissionObjectiveType.CollectItems) return false;
            bool matches = false;
            foreach (string target in active.TargetIds) if (target == Id) matches = true;
            // Collection is a stage fact even before its mission becomes active.
            if (objectiveType == MissionObjectiveType.CollectItems)
                matches = item != null && !runtime.Actions.WasCollected(Id);
            if (!matches) return false;
            bool success;
            switch (objectiveType)
            {
                case MissionObjectiveType.ReachLocation:
                    return runtime.RecordLocationReached(Id);
                case MissionObjectiveType.CollectItems:
                    if (item == null || quantity <= 0) return false;
                    success = runtime.RecordItemCollected(Id, item.ItemId, quantity);
                    break;
                case MissionObjectiveType.InteractWithTarget:
                    if (runtime.Actions.WasInteracted(Id)) return false;
                    foreach (var required in requiredItems)
                        if (required == null || runtime.Actions.HeldQuantity(required.ItemId) < 1) return false;
                    if (item != null)
                    {
                        if (quantity <= 0 || runtime.Actions.HeldQuantity(item.ItemId) < quantity) return false;
                        if (consumeItem && !runtime.TryConsumeItem(item.ItemId, quantity)) return false;
                    }
                    // Review interaction commits a state change only; full door/repair actions are separate adapters.
                    success = runtime.RecordInteractionSucceeded(Id);
                    break;
                default: return false;
            }
            if (success)
            {
                if (interactionAnimator != null && (objectiveType == MissionObjectiveType.InteractWithTarget || collectOnApproach))
                    interactionAnimator.OnGoalReached();
                else if (visual != null) visual.SetActive(false);
            }
            return success;
        }

        /// <summary>Reconcile visuals from facts, including Restore, without replaying completion audio.</summary>
        public void RefreshPresentation(StageMissionRuntime runtime, bool currentTarget)
        {
            if (!manageObjectivePresentation || runtime == null) return;
            bool completed = objectiveType == MissionObjectiveType.CollectItems
                ? runtime.Actions.WasCollected(Id) : runtime.Actions.WasInteracted(Id);
            bool highlighted = currentTarget && !completed && !runtime.IsCleared;
            if (visual != null && objectiveType == MissionObjectiveType.CollectItems)
                SetVisible(visual, !completed);
            if (interactionAnimator != null)
            {
                foreach (var part in interactionAnimator.DisabledObjectsWhenGoalReached) SetVisible(part, highlighted);
                foreach (var part in interactionAnimator.EnabledObjectsWhenGoalReached) SetVisible(part, !highlighted);
            }
            foreach (var indicator in objectiveIndicators) SetVisible(indicator, highlighted);
            if (goalBeacon != null && goalBeacon.enabled != highlighted) goalBeacon.enabled = highlighted;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = objectiveType == MissionObjectiveType.CollectItems ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
