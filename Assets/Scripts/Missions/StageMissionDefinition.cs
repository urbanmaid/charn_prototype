using System;
using System.Collections.Generic;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    [Serializable]
    public sealed class MissionAuthoringEntry
    {
        public string missionId;
        public string title;
        [TextArea] public string description;
        public MissionObjectiveType objectiveType;
        public StageObjectId checkpoint;
        [Tooltip("처치 대상 목록, 특정 수집 물품 목록, 또는 단일 도달/상호작용 대상")]
        public StageObjectId[] targets = Array.Empty<StageObjectId>();
        public MissionCollectionMode collectionMode;
        public MissionItemDefinition item;
        [Min(1)] public int requiredQuantity = 1;
    }

    /// <summary>Attach to the stage interactives root; adding it does not start gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class StageMissionDefinition : MonoBehaviour
    {
        [SerializeField] private string stageId;
        [SerializeField] private MissionAuthoringEntry[] missions = Array.Empty<MissionAuthoringEntry>();
        public string StageId => stageId;

        public StageMissionRuntime CreateRuntime(double actualPlaySeconds = 0)
        {
            // Including inactive children allows enemies, pickups and checkpoint markers to start hidden.
            var registry = new Dictionary<string, StageObjectId>(StringComparer.Ordinal);
            foreach (var identity in GetComponentsInChildren<StageObjectId>(true))
            {
                string id = MissionIds.Require(identity.Id, "stageObjectId");
                if (registry.ContainsKey(id)) throw new ArgumentException("Duplicate stage object ID: " + id);
                registry.Add(id, identity);
            }
            if (missions == null) throw new ArgumentException("Mission list is missing.");
            var definitions = new List<MissionDefinition>();
            var itemDefinitions = new Dictionary<string, MissionItemDefinition>(StringComparer.Ordinal);
            foreach (var entry in missions)
            {
                if (entry == null) throw new ArgumentException("Missing mission entry.");
                string checkpointId = Resolve(entry.checkpoint, registry);
                var targets = new List<string>();
                if (entry.targets != null)
                    foreach (var target in entry.targets) targets.Add(Resolve(target, registry));
                string itemId = entry.item != null ? MissionIds.Require(entry.item.ItemId, "itemId") : "";
                if (entry.item != null)
                {
                    if (itemDefinitions.TryGetValue(itemId, out var previous) && previous != entry.item)
                        throw new ArgumentException("Different item definitions share ID: " + itemId);
                    itemDefinitions[itemId] = entry.item;
                }
                definitions.Add(new MissionDefinition(entry.missionId, entry.title, entry.description, checkpointId,
                    entry.objectiveType, targets, itemId, entry.requiredQuantity, entry.collectionMode));
            }
            return new StageMissionRuntime(stageId, definitions, actualPlaySeconds);
        }

        private static string Resolve(StageObjectId reference, Dictionary<string, StageObjectId> registry)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.Id)
                || !registry.TryGetValue(reference.Id, out var registered) || registered != reference)
                throw new ArgumentException("Targets/checkpoints must belong to this stage root and have a unique ID.");
            return reference.Id;
        }

        [ContextMenu("Validate Mission Configuration")]
        private void ValidateConfiguration()
        {
            try { CreateRuntime(); Debug.Log("미션 정의와 스테이지 ID 검사 통과", this); }
            catch (Exception exception) { Debug.LogError("미션 설정 오류: " + exception.Message, this); }
        }
    }
}
