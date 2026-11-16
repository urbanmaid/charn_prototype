using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Charm.Mechanism.Missions
{
    /// <summary>Pure sequential mission state. Adapters authorize actors and report successful actions.</summary>
    public sealed class StageMissionRuntime
    {
        private readonly List<MissionDefinition> definitions;
        private readonly List<MissionTimeRecord> completed = new List<MissionTimeRecord>();
        private readonly string signature;
        private IObjectiveEvaluator evaluator;
        private bool mutating;
        public string StageId { get; }
        public IReadOnlyList<MissionDefinition> Definitions { get; }
        public StageActionLedger Actions { get; private set; } = new StageActionLedger();
        public bool HasStarted { get; private set; }
        public bool IsFailed { get; private set; }
        public int CurrentMissionIndex { get; private set; }
        public bool IsCleared => HasStarted && CurrentMissionIndex == definitions.Count;
        public double CurrentMissionSeconds { get; private set; }
        public double ActualPlaySeconds { get; private set; }
        public double StageRecordSeconds
        {
            get { double sum = 0; foreach (var record in completed) sum += record.seconds; return sum; }
        }
        public int ObjectiveCurrent => evaluator?.Current ?? 0;
        public int ObjectiveRequired => evaluator?.Required ?? 0;
        public event Action Changed;
        public event Action<int> MissionCompleted;
        public event Action StageCleared;

        public StageMissionRuntime(string stageId, IEnumerable<MissionDefinition> missions, double actualPlaySeconds = 0)
        {
            StageId = MissionIds.Require(stageId, nameof(stageId));
            MissionIds.RequireSeconds(actualPlaySeconds);
            ActualPlaySeconds = actualPlaySeconds;
            if (missions == null) throw new ArgumentNullException(nameof(missions));
            definitions = new List<MissionDefinition>(missions);
            if (definitions.Count == 0) throw new ArgumentException("A stage must contain at least one mission.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mission in definitions)
            {
                if (mission == null || !ids.Add(mission.Id)) throw new ArgumentException("Missing/duplicate mission.");
                if (!ObjectiveEvaluatorFactory.Supports(mission.Type))
                    throw new NotSupportedException("Objective policy is not implemented: " + mission.Type);
            }
            Definitions = new ReadOnlyCollection<MissionDefinition>(definitions);
            signature = MakeSignature();
        }

        public void Start()
        {
            EnterMutation();
            try
            {
                if (HasStarted) throw new InvalidOperationException("A run can only start once. Restore or create a new run.");
                HasStarted = true;
                evaluator = ObjectiveEvaluatorFactory.Create(definitions[0], Actions);
                PublishTransitions();
            }
            finally { mutating = false; }
        }

        /// <summary>Only call with active gameplay time. Pause/loading/menu gating belongs to the host.</summary>
        public void AdvanceActiveTime(double seconds)
        {
            MissionIds.RequireSeconds(seconds);
            EnterMutation();
            try
            {
                if (!HasStarted || IsCleared || IsFailed) return;
                double actual = ActualPlaySeconds + seconds;
                double current = CurrentMissionSeconds + seconds;
                MissionIds.RequireSeconds(actual);
                MissionIds.RequireSeconds(current);
                ActualPlaySeconds = actual;
                CurrentMissionSeconds = current;
            }
            finally { mutating = false; }
        }

        // A failed run cannot accept late combat callbacks or further time. Restart creates a new run.
        public bool Fail()
        {
            EnterMutation();
            try
            {
                if (!HasStarted || IsCleared || IsFailed) return false;
                IsFailed = true;
                Changed?.Invoke();
                return true;
            }
            finally { mutating = false; }
        }

        public bool RecordEnemyDefeated(string enemyId) => Report(StageActionKind.EnemyDefeated, enemyId, null, 0);
        public bool RecordItemCollected(string objectId, string itemId, int quantity) => Report(StageActionKind.ItemCollected, objectId, itemId, quantity);
        public bool RecordLocationReached(string targetId) => Report(StageActionKind.LocationReached, targetId, null, 0);
        public bool RecordInteractionSucceeded(string targetId) => Report(StageActionKind.InteractionSucceeded, targetId, null, 0);

        private bool Report(StageActionKind kind, string targetId, string itemId, int quantity)
        {
            MissionIds.Require(targetId, nameof(targetId));
            EnterMutation();
            try
            {
                if (!HasStarted || IsCleared || IsFailed) return false;
                bool accepted;
                switch (kind)
                {
                    case StageActionKind.EnemyDefeated: accepted = Actions.RecordDefeat(targetId); break;
                    case StageActionKind.ItemCollected: accepted = Actions.RecordPickup(targetId, itemId, quantity); break;
                    case StageActionKind.InteractionSucceeded: accepted = Actions.RecordInteraction(targetId); break;
                    default: accepted = true; break;
                }
                if (!accepted) return false;
                evaluator.Observe(kind, targetId);
                // Only the currently active evaluator receives reach/interaction events.
                // Future collection/elimination evaluators read the ledger when activated.
                PublishTransitions();
                return true;
            }
            finally { mutating = false; }
        }

        public bool TryConsumeItem(string itemId, int quantity)
        {
            EnterMutation();
            try
            {
                if (!HasStarted || IsCleared || IsFailed) return false;
                bool consumed = Actions.TryConsume(itemId, quantity);
                if (consumed) Changed?.Invoke();
                return consumed;
            }
            finally { mutating = false; }
        }

        public MissionDisplayState GetMissionState(int index)
        {
            if (index < 0 || index >= definitions.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < CurrentMissionIndex) return MissionDisplayState.Completed;
            return HasStarted && index == CurrentMissionIndex ? MissionDisplayState.Active : MissionDisplayState.Locked;
        }

        public MissionProgressSnapshot CaptureProgress()
        {
            if (!HasStarted) throw new InvalidOperationException("Start or restore the run first.");
            var data = new MissionProgressSnapshot
            {
                stageId = StageId, definitionSignature = signature,
                checkpointId = definitions[Math.Min(CurrentMissionIndex, definitions.Count - 1)].CheckpointId,
                currentMissionIndex = CurrentMissionIndex, currentMissionSeconds = CurrentMissionSeconds,
                actions = Actions.Capture()
            };
            foreach (var record in completed)
                data.completedMissions.Add(new MissionTimeRecord { missionId = record.missionId, seconds = record.seconds });
            return data;
        }

        /// <summary>Restores only mission data. The caller must restore the matching world snapshot before resuming.</summary>
        public void RestoreProgress(MissionProgressSnapshot data)
        {
            EnterMutation();
            try
            {
                if (data == null || data.schemaVersion != 1 || data.stageId != StageId || data.definitionSignature != signature)
                    throw new ArgumentException("Snapshot version, stage or definition does not match.");
                int index = data.currentMissionIndex;
                if (index < 0 || index > definitions.Count || data.completedMissions == null || data.completedMissions.Count != index
                    || data.checkpointId != definitions[Math.Min(index, definitions.Count - 1)].CheckpointId)
                    throw new ArgumentException("Invalid checkpoint mission order.");
                MissionIds.RequireSeconds(data.currentMissionSeconds);
                if (index == definitions.Count && data.currentMissionSeconds != 0) throw new ArgumentException("A cleared run has no active mission time.");
                var records = new List<MissionTimeRecord>();
                double sum = 0;
                for (int i = 0; i < index; i++)
                {
                    var record = data.completedMissions[i];
                    if (record == null || record.missionId != definitions[i].Id) throw new ArgumentException("Completed mission order mismatch.");
                    MissionIds.RequireSeconds(record.seconds);
                    sum += record.seconds;
                    MissionIds.RequireSeconds(sum);
                    records.Add(new MissionTimeRecord { missionId = record.missionId, seconds = record.seconds });
                }
                var ledger = StageActionLedger.Restore(data.actions);
                for (int i = 0; i < index; i++)
                {
                    var definition = definitions[i];
                    if ((definition.Type == MissionObjectiveType.CollectItems || definition.Type == MissionObjectiveType.EliminateEnemies)
                        && !ObjectiveEvaluatorFactory.Create(definition, ledger).IsComplete)
                        throw new ArgumentException("Completed objective disagrees with action history.");
                    if (definition.Type == MissionObjectiveType.InteractWithTarget && !ledger.WasInteracted(definition.TargetIds[0]))
                        throw new ArgumentException("Completed interaction has no successful action record.");
                }
                var next = index < definitions.Count ? ObjectiveEvaluatorFactory.Create(definitions[index], ledger) : null;
                if (next != null && next.IsComplete) throw new ArgumentException("Snapshot contains an uncommitted completion.");
                // Validate everything before replacing live state. No replay of completion/clear events.
                Actions = ledger;
                completed.Clear();
                completed.AddRange(records);
                CurrentMissionIndex = index;
                CurrentMissionSeconds = data.currentMissionSeconds;
                evaluator = next;
                HasStarted = true;
                IsFailed = false;
                Changed?.Invoke();
            }
            finally { mutating = false; }
        }

        private void PublishTransitions()
        {
            int first = CurrentMissionIndex;
            while (evaluator != null && evaluator.IsComplete)
            {
                completed.Add(new MissionTimeRecord { missionId = definitions[CurrentMissionIndex].Id, seconds = CurrentMissionSeconds });
                CurrentMissionIndex++;
                CurrentMissionSeconds = 0;
                evaluator = CurrentMissionIndex < definitions.Count ? ObjectiveEvaluatorFactory.Create(definitions[CurrentMissionIndex], Actions) : null;
            }
            // Notify only after a complete, consistent transition (including pre-earned objectives).
            Changed?.Invoke();
            for (int i = first; i < CurrentMissionIndex; i++) MissionCompleted?.Invoke(i);
            if (first < definitions.Count && IsCleared) StageCleared?.Invoke();
        }

        private void EnterMutation()
        {
            if (mutating) throw new InvalidOperationException("Do not mutate mission state from its notification callbacks.");
            mutating = true;
        }

        private string MakeSignature()
        {
            var text = new StringBuilder();
            Action<string> append = value => text.Append(value.Length).Append(':').Append(value);
            append(StageId);
            foreach (var m in definitions)
            {
                append(m.Id); append(m.CheckpointId);
                append(((int)m.Type).ToString(CultureInfo.InvariantCulture));
                append(((int)m.CollectionMode).ToString(CultureInfo.InvariantCulture));
                append(m.ItemId); append(m.RequiredQuantity.ToString(CultureInfo.InvariantCulture));
                append(m.TargetIds.Count.ToString(CultureInfo.InvariantCulture));
                foreach (string id in m.TargetIds) append(id);
            }
            return text.ToString();
        }
    }
}
