using System;

namespace Charm.Mechanism.Missions
{
    public enum StageActionKind { EnemyDefeated = 0, ItemCollected = 1, LocationReached = 2, InteractionSucceeded = 3 }

    public interface IObjectiveEvaluator
    {
        int Current { get; }
        int Required { get; }
        bool IsComplete { get; }
        void Observe(StageActionKind kind, string targetId);
    }

    public static class ObjectiveEvaluatorFactory
    {
        public static bool Supports(MissionObjectiveType type) => type == MissionObjectiveType.EliminateEnemies
            || type == MissionObjectiveType.ReachLocation || type == MissionObjectiveType.CollectItems
            || type == MissionObjectiveType.InteractWithTarget;

        public static IObjectiveEvaluator Create(MissionDefinition definition, StageActionLedger ledger)
        {
            if (definition == null || ledger == null) throw new ArgumentNullException();
            switch (definition.Type)
            {
                case MissionObjectiveType.EliminateEnemies: return new TargetListEvaluator(definition, ledger.WasDefeated);
                case MissionObjectiveType.CollectItems:
                    return definition.CollectionMode == MissionCollectionMode.SpecificObjects
                        ? (IObjectiveEvaluator)new TargetListEvaluator(definition, ledger.WasCollected)
                        : new QuantityEvaluator(definition, ledger);
                case MissionObjectiveType.ReachLocation: return new ActionEvaluator(definition, StageActionKind.LocationReached);
                case MissionObjectiveType.InteractWithTarget: return new ActionEvaluator(definition, StageActionKind.InteractionSucceeded);
                default: throw new NotSupportedException("Survival/health preservation rules must be agreed before this objective can run.");
            }
        }

        private sealed class TargetListEvaluator : IObjectiveEvaluator
        {
            private readonly MissionDefinition definition;
            private readonly Func<string, bool> contains;
            public TargetListEvaluator(MissionDefinition definition, Func<string, bool> contains) { this.definition = definition; this.contains = contains; }
            public int Current
            {
                get { int n = 0; foreach (string id in definition.TargetIds) if (contains(id)) n++; return n; }
            }
            public int Required => definition.TargetIds.Count;
            public bool IsComplete => Current >= Required;
            public void Observe(StageActionKind kind, string targetId) { }
        }

        private sealed class QuantityEvaluator : IObjectiveEvaluator
        {
            private readonly MissionDefinition definition;
            private readonly StageActionLedger ledger;
            public QuantityEvaluator(MissionDefinition definition, StageActionLedger ledger) { this.definition = definition; this.ledger = ledger; }
            public int Current => Math.Min(Required, ledger.AcquiredQuantity(definition.ItemId));
            public int Required => definition.RequiredQuantity;
            public bool IsComplete => Current >= Required;
            public void Observe(StageActionKind kind, string targetId) { }
        }

        private sealed class ActionEvaluator : IObjectiveEvaluator
        {
            private readonly string target;
            private readonly StageActionKind expected;
            public ActionEvaluator(MissionDefinition definition, StageActionKind expected) { target = definition.TargetIds[0]; this.expected = expected; }
            public int Current { get; private set; }
            public int Required => 1;
            public bool IsComplete => Current == 1;
            public void Observe(StageActionKind kind, string targetId) { if (kind == expected && targetId == target) Current = 1; }
        }
    }
}
