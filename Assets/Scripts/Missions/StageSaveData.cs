using System;
using System.Collections.Generic;

namespace Charm.Mechanism.Missions
{
    [Serializable]
    public sealed class PersonaCombatTotals
    {
        public string personaId;
        public long kills, downs, deaths, revives, gadgetUses;
        public double healthDamage, healingReceived, damageDealt;
    }

    [Serializable]
    public sealed class MissionCombatTotals
    {
        public string missionId;
        public List<PersonaCombatTotals> personas = new List<PersonaCombatTotals>();
    }

    [Serializable]
    public sealed class CombatDownSnapshot
    {
        public string personaId, episodeId;
        public bool deathCounted;
    }

    [Serializable]
    public sealed class WorldObjectSnapshot
    {
        public string objectId;
        public string adapterId;
        public int version = 1;
        public string payload;
    }

    // Transport only. A world restore coordinator must validate every adapter before applying this.
    [Serializable]
    public sealed class StageCheckpointSnapshot
    {
        public string checkpointId;
        public string missionId;
        public int checkpointOrder;
        public MissionProgressSnapshot progress;
        public List<WorldObjectSnapshot> world = new List<WorldObjectSnapshot>();
        public List<PersonaCombatTotals> validCombat = new List<PersonaCombatTotals>();
        public List<MissionCombatTotals> missionCombat = new List<MissionCombatTotals>();
        public List<CombatDownSnapshot> downStates = new List<CombatDownSnapshot>();
    }

    [Serializable]
    public sealed class StageAttemptResult
    {
        public int retryCount;
        public string resultId;
        public string runId;
        public string missionAttemptId;
        public string missionId;
        public bool succeeded;
        public bool stageCompleted;
        public bool stageFailed;
        // Elapsed time of the failed mission; never included in successful clear records.
        public double failedMissionSeconds;
        // Cumulative active stage play time at completion; mission record durations are below.
        public double actualSeconds;
        public List<MissionTimeRecord> completedMissions = new List<MissionTimeRecord>();
        // This mission only; final stage totals are separate to prevent summing repeated prefixes.
        public List<PersonaCombatTotals> combat = new List<PersonaCombatTotals>();
        public List<PersonaCombatTotals> stageCombat = new List<PersonaCombatTotals>();
    }

    [Serializable]
    public sealed class StageSaveData : UnityEngine.ISerializationCallbackReceiver
    {
        public int schemaVersion = 1;
        public int retryCount;
        public bool pendingFailedRestart;
        public string stageId;
        public string runId;
        public string missionAttemptId;
        public double actualPlaySeconds;
        public StageCheckpointSnapshot missionStart;
        public StageCheckpointSnapshot latestCheckpoint;
        public List<StageAttemptResult> results = new List<StageAttemptResult>();

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            // JsonUtility materializes null inline classes as empty objects.
            // A checkpoint without an identity is never a resumable snapshot.
            if (missionStart != null && string.IsNullOrWhiteSpace(missionStart.checkpointId)) missionStart = null;
            if (latestCheckpoint != null && string.IsNullOrWhiteSpace(latestCheckpoint.checkpointId)) latestCheckpoint = null;
            if (results == null) results = new List<StageAttemptResult>();
        }

        // Membership is explicit; a future or previous mission cannot advance the resume point.
        public static bool CanAdvanceCheckpoint(string activeMissionId, string candidateMissionId,
            int candidateOrder, StageCheckpointSnapshot current)
        {
            return !string.IsNullOrWhiteSpace(activeMissionId) && activeMissionId == candidateMissionId
                && candidateOrder >= 0 && (current == null || current.missionId != activeMissionId
                    || candidateOrder > current.checkpointOrder);
        }
    }
}
