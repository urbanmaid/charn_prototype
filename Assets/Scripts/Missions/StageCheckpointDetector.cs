using System;
using System.Collections.Generic;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Configured only after stage/world initialization; never creates partial saves on its own.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageMissionController))]
    public sealed class StageCheckpointDetector : MonoBehaviour
    {
        private StageMissionController controller;
        private StageCheckpoint[] points;
        private CheckpointTransaction transaction;
        private StageMissionRuntime boundRuntime;
        private Func<StageCheckpoint, List<WorldObjectSnapshot>> capture;
        private Func<List<PersonaCombatTotals>> combat;
        private Action<StageCheckpointSnapshot> captureStatistics;
        private float nextCheck;
        public bool HasPending => transaction != null && transaction.HasPending;
        public event Action<string> SaveFailed;
        public event Action<string> CheckpointSaved;

        public void Initialize(StageSaveData saved, Func<StageSaveData, bool> persist,
            Func<StageCheckpoint, List<WorldObjectSnapshot>> captureWorld,
            Func<List<PersonaCombatTotals>> captureCombat, Action<StageCheckpointSnapshot> captureStatistics = null)
        {
            if (captureWorld == null || captureCombat == null) throw new ArgumentNullException("Snapshot providers are required.");
            controller = GetComponent<StageMissionController>();
            if (controller.Runtime == null) throw new InvalidOperationException("Mission runtime is not initialized.");
            var candidate = new CheckpointTransaction(controller.Runtime, saved, persist);
            var candidates = GetComponentsInChildren<StageCheckpoint>(true);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var point in candidates)
                if (string.IsNullOrWhiteSpace(point.Id) || !ids.Add(point.Id))
                    throw new InvalidOperationException("Missing or duplicate checkpoint identity.");
            transaction = candidate; boundRuntime = controller.Runtime;
            points = candidates; capture = captureWorld; combat = captureCombat; this.captureStatistics = captureStatistics;
            nextCheck = 0;
        }

        private void Update()
        {
            if (transaction == null || controller == null || !controller.IsGameplayRunning
                || Time.timeScale <= 0 || !GameplayCursor.AllowsGameplayInput || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .2f;
            var player = PlayerControlResolver.Resolve(null);
            if (player != null) Evaluate(player.transform.position);
        }

        public bool Evaluate(Vector3 controlledPosition)
        {
            if (transaction == null || controller == null || !controller.IsGameplayRunning || controller.Runtime.IsCleared) return false;
            if (!ReferenceEquals(boundRuntime, controller.Runtime)) { Suspend(); return false; }
            string mission = controller.Runtime.Definitions[controller.Runtime.CurrentMissionIndex].Id;
            StageCheckpoint selected = null; int highestOrder = -1;
            foreach (var point in points)
                if (point != null && point.Contains(controlledPosition) && point.TryGetOrder(mission, controller.Runtime.Actions, out int order) && order > highestOrder)
                { selected = point; highestOrder = order; }
            if (selected == null) return false;
            try
            {
                if (transaction.TryCapture(selected, controlledPosition, capture, combat(), out string error, captureStatistics))
                { CheckpointSaved?.Invoke(selected.Id); return true; }
                if (error != null) { nextCheck = Time.unscaledTime + 2f; SaveFailed?.Invoke(error); }
            }
            catch (Exception exception) { nextCheck = Time.unscaledTime + 2f; SaveFailed?.Invoke(exception.Message); }
            return false;
        }

        // Runtime replacement invalidates the transaction. Reinitialize after a completed restore.
        public void Suspend() { transaction?.DiscardPending(); transaction = null; }

        public void DiscardPending() => transaction?.DiscardPending();

        public bool RetryPending()
        {
            if (transaction == null || !ReferenceEquals(boundRuntime, controller.Runtime)) return false;
            string id = transaction.PendingCheckpointId;
            bool success = transaction.TryRetry(out string error);
            if (success) CheckpointSaved?.Invoke(id);
            else if (error != null) SaveFailed?.Invoke(error);
            return success;
        }
    }
}
