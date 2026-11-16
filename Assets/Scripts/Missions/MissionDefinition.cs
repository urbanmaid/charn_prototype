using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Charm.Mechanism.Missions
{
    // Serialized values are persistent. Never reorder or reuse a removed value.
    public enum MissionObjectiveType
    {
        EliminateEnemies = 0,
        ReachLocation = 1,
        CollectItems = 2,
        InteractWithTarget = 3,
        SurviveDuration = 4,
        PreserveAlliedHealth = 5
    }

    public enum MissionCollectionMode { ItemTypeAndQuantity = 0, SpecificObjects = 1 }
    public enum MissionDisplayState { Locked = 0, Active = 1, Completed = 2 }

    /// <summary>Immutable runtime definition. Scene references are resolved to authored IDs before use.</summary>
    public sealed class MissionDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string CheckpointId { get; }
        public MissionObjectiveType Type { get; }
        public MissionCollectionMode CollectionMode { get; }
        public string ItemId { get; }
        public int RequiredQuantity { get; }
        public IReadOnlyList<string> TargetIds { get; }

        public MissionDefinition(string id, string title, string description, string checkpointId,
            MissionObjectiveType type, IEnumerable<string> targets = null, string itemId = "",
            int requiredQuantity = 1, MissionCollectionMode collectionMode = MissionCollectionMode.ItemTypeAndQuantity)
        {
            Id = MissionIds.Require(id, nameof(id));
            CheckpointId = MissionIds.Require(checkpointId, nameof(checkpointId));
            if (!Enum.IsDefined(typeof(MissionObjectiveType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(MissionCollectionMode), collectionMode)) throw new ArgumentOutOfRangeException(nameof(collectionMode));
            Title = title ?? "";
            Description = description ?? "";
            Type = type;
            CollectionMode = collectionMode;
            ItemId = itemId ?? "";
            RequiredQuantity = requiredQuantity;
            var copy = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (targets != null)
                foreach (string target in targets)
                {
                    MissionIds.Require(target, nameof(targets));
                    if (!unique.Add(target)) throw new ArgumentException("Duplicate objective target: " + target);
                    copy.Add(target);
                }
            TargetIds = new ReadOnlyCollection<string>(copy);
            if (type == MissionObjectiveType.EliminateEnemies && copy.Count == 0)
                throw new ArgumentException("An elimination mission needs explicit enemies.");
            if ((type == MissionObjectiveType.ReachLocation || type == MissionObjectiveType.InteractWithTarget) && copy.Count != 1)
                throw new ArgumentException("A reach/interaction mission needs exactly one target.");
            if (type == MissionObjectiveType.CollectItems)
            {
                if (collectionMode == MissionCollectionMode.SpecificObjects && copy.Count == 0)
                    throw new ArgumentException("A specific collection mission needs placed objects.");
                if (collectionMode == MissionCollectionMode.ItemTypeAndQuantity)
                {
                    MissionIds.Require(ItemId, nameof(itemId));
                    if (requiredQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
                }
            }
        }
    }

    internal static class MissionIds
    {
        public static string Require(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                throw new ArgumentException("A non-empty, trimmed stable ID is required.", parameter);
            return value;
        }

        public static void RequireSeconds(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
