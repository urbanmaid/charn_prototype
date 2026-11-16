using System;
using System.Collections.Generic;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Owns only checkpoint transaction boundaries; world adapters own game state.</summary>
    public sealed class CheckpointTransaction
    {
        private readonly StageMissionRuntime runtime;
        private readonly Func<StageSaveData, bool> persist;
        private StageSaveData committed;
        private StageSaveData pending;
        public bool HasPending => pending != null;
        public string PendingCheckpointId => pending?.latestCheckpoint?.checkpointId;
        public void DiscardPending() => pending = null;
        public StageSaveData ReadCommitted() => Copy(committed);

        public CheckpointTransaction(StageMissionRuntime runtime, StageSaveData saved, Func<StageSaveData, bool> persist)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.persist = persist ?? throw new ArgumentNullException(nameof(persist));
            if (saved == null || saved.stageId != runtime.StageId) throw new ArgumentException("Stage mismatch.");
            committed = Copy(saved);
        }

        public bool TryCapture(StageCheckpoint point, Vector3 actorPosition,
            Func<StageCheckpoint, List<WorldObjectSnapshot>> captureWorld,
            List<PersonaCombatTotals> validCombat, out string error, Action<StageCheckpointSnapshot> captureStatistics = null)
        {
            error = null;
            if (!runtime.HasStarted || runtime.IsCleared || point == null || !point.Contains(actorPosition)) return false;
            string mission = runtime.Definitions[runtime.CurrentMissionIndex].Id;
            if (!point.TryGetOrder(mission, runtime.Actions, out int order)
                || !StageSaveData.CanAdvanceCheckpoint(mission, mission, order, committed.latestCheckpoint)) return false;
            if (pending != null && pending.latestCheckpoint.missionId == mission
                && order <= pending.latestCheckpoint.checkpointOrder) return TryRetry(out error);
            if (captureWorld == null) { error = "World capture provider is required."; return false; }
            try
            {
                // Do not acknowledge a checkpoint with missing world/party state.
                var world = captureWorld(point);
                if (world == null || world.Count == 0) throw new InvalidOperationException("World snapshot is empty.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in world)
                    if (entry == null || string.IsNullOrWhiteSpace(entry.objectId) || !ids.Add(entry.objectId)
                        || string.IsNullOrWhiteSpace(entry.adapterId) || entry.version < 1 || entry.payload == null)
                        throw new InvalidOperationException("Invalid or duplicate world snapshot entry.");
                if (runtime.IsCleared || runtime.Definitions[runtime.CurrentMissionIndex].Id != mission)
                    throw new InvalidOperationException("Mission changed during capture.");
                var candidate = Copy(committed);
                candidate.actualPlaySeconds = Math.Max(candidate.actualPlaySeconds, runtime.ActualPlaySeconds);
                candidate.latestCheckpoint = new StageCheckpointSnapshot
                {
                    checkpointId = point.Id, missionId = mission, checkpointOrder = order,
                    progress = runtime.CaptureProgress(), world = world,
                    validCombat = validCombat ?? new List<PersonaCombatTotals>()
                };
                captureStatistics?.Invoke(candidate.latestCheckpoint);
                if (string.IsNullOrWhiteSpace(point.Id)) throw new InvalidOperationException("Missing checkpoint ID.");
                // CaptureProgress retains the authored mission-start ID. The outer snapshot identifies
                // the reached CP; this preserves existing mission signature/restore validation.
                candidate = Copy(candidate);
                pending = candidate;
                return TryRetry(out error);
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public bool TryRetry(out string error)
        {
            error = null;
            if (pending == null) return false;
            if (runtime.IsCleared || runtime.Definitions[runtime.CurrentMissionIndex].Id != pending.latestCheckpoint.missionId)
            { DiscardPending(); return false; }
            // Retry the captured world, but never roll cumulative active time backwards.
            pending.actualPlaySeconds = Math.Max(pending.actualPlaySeconds, runtime.ActualPlaySeconds);
            try
            {
                if (!persist(Copy(pending))) { error = "Checkpoint could not be saved."; return false; }
                committed = pending;
                pending = null;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static StageSaveData Copy(StageSaveData value) => JsonUtility.FromJson<StageSaveData>(JsonUtility.ToJson(value));
    }
}
