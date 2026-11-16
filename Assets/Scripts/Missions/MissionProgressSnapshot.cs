using System;
using System.Collections.Generic;

namespace Charm.Mechanism.Missions
{
    // Value DTOs only. These are the mission portion of a future world checkpoint, NOT a disk save service.
    [Serializable]
    public sealed class MissionPickupRecord
    {
        public string objectId;
        public string itemId;
        public int quantity;
        public int sequence;
    }

    [Serializable]
    public sealed class MissionItemBalance
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class StageActionSnapshot
    {
        public List<string> defeatedEnemyIds = new List<string>();
        public List<string> interactedTargetIds = new List<string>();
        public List<MissionPickupRecord> pickups = new List<MissionPickupRecord>();
        public List<MissionItemBalance> balances = new List<MissionItemBalance>();
    }

    [Serializable]
    public sealed class MissionTimeRecord
    {
        public string missionId;
        public double seconds;
    }

    [Serializable]
    public sealed class MissionProgressSnapshot
    {
        public int schemaVersion = 1;
        public string stageId;
        public string definitionSignature;
        public string checkpointId;
        public int currentMissionIndex;
        public double currentMissionSeconds;
        public List<MissionTimeRecord> completedMissions = new List<MissionTimeRecord>();
        public StageActionSnapshot actions = new StageActionSnapshot();
        // Actual accumulated play time deliberately does not belong to a rollback snapshot.
    }
}
