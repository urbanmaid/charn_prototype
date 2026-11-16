using System;
using System.IO;
using Charm.Mechanism.Missions;
using UnityEngine;

/// <summary>Selected slot identity survives scene unloading; no menu GameObject is retained.</summary>
public static class StageSaveSession
{
    private static string selectedPath, selectedId;
    public static bool HasSelection => !string.IsNullOrEmpty(selectedPath) && !string.IsNullOrEmpty(selectedId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear() { FlushApplicationTime(); selectedPath = null; selectedId = null; }

    public static void Bind(string path, string saveId)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("A committed save slot is required.");
        string full = Path.GetFullPath(path);
        if (selectedPath == full && selectedId == saveId) return;
        FlushApplicationTime();
        selectedPath = full; selectedId = saveId;
        clockStart = Time.realtimeSinceStartupAsDouble;
        if (TryReadSlot(out var selected, out _)) clockBase = selected.totalApplicationSeconds;
        else clockBase = 0;
        SlotApplicationClock.Ensure();
    }

    public static bool TryReadStage(string stageId, out StageSaveData stage, out string error)
    {
        stage = null;
        if (!TryReadSlot(out var slot, out error)) return false;
        stage = slot.stageProgress?.Find(value => value != null && value.stageId == stageId);
        if (stage != null && stage.schemaVersion != 1) { error = "Unsupported stage save version."; stage = null; return false; }
        return true;
    }

    public static bool TryWriteStage(StageSaveData stage, out string error)
    {
        if (!TryReadSlot(out var slot, out error)) return false;
        if (stage == null || string.IsNullOrWhiteSpace(stage.stageId) || stage.schemaVersion != 1
            || double.IsNaN(stage.actualPlaySeconds) || double.IsInfinity(stage.actualPlaySeconds) || stage.actualPlaySeconds < 0)
        { error = "Invalid stage snapshot."; return false; }
        try
        {
            if (slot.stageProgress == null) slot.stageProgress = new System.Collections.Generic.List<StageSaveData>();
            int index = slot.stageProgress.FindIndex(value => value != null && value.stageId == stage.stageId);
            if (index >= 0 && stage.actualPlaySeconds < slot.stageProgress[index].actualPlaySeconds)
            { error = "Actual play time cannot roll back."; return false; }
            var copy = JsonUtility.FromJson<StageSaveData>(JsonUtility.ToJson(stage));
            if (index >= 0) slot.stageProgress[index] = copy; else slot.stageProgress.Add(copy);
            slot.currentLevel = slot.currentLevel ?? new SaveDataManager.CurrentLevelData();
            slot.currentLevel.levelId = stage.stageId;
            slot.lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            MergeApplicationTime(slot);
            AtomicSaveFile.Write(selectedPath, JsonUtility.ToJson(slot, true));
            ApplicationTimeSaved(slot);
            return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
    }

    // Discard only this stage's resumable attempt, retaining results and cumulative totals.
    // Unsupported outer schemas are not rewritten: their result format is not known here.
    public static bool TryRestartStage(string stageId, double actualPlaySeconds, out string error)
    {
        if (!TryReadStage(stageId, out var stage, out error)) return false;
        stage = stage ?? new StageSaveData { stageId = stageId };
        stage.actualPlaySeconds = Math.Max(stage.actualPlaySeconds, actualPlaySeconds);
        if (stage.latestCheckpoint != null || stage.pendingFailedRestart) stage.retryCount++;
        stage.pendingFailedRestart = false;
        stage.latestCheckpoint = null;
        stage.missionStart = null;
        stage.runId = null;
        stage.missionAttemptId = null;
        return TryWriteStage(stage, out error);
    }

    public static bool TryReadSlot(out SaveDataManager.SaveData slot, out string error)
    {
        slot = null; error = null;
        if (!HasSelection) { error = "No save slot selected."; return false; }
        try
        {
            if (!File.Exists(selectedPath)) { error = "Selected save slot was deleted."; return false; }
            slot = JsonUtility.FromJson<SaveDataManager.SaveData>(File.ReadAllText(selectedPath));
            if (slot == null || slot.saveId != selectedId) { error = "Selected save identity changed."; slot = null; return false; }
            return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
    }
    private static double clockStart, clockBase, capturedAt;
    public static double TotalApplicationSeconds => HasSelection ? clockBase + Math.Max(0, Time.realtimeSinceStartupAsDouble - clockStart) : 0;
    public static void MergeApplicationTime(SaveDataManager.SaveData slot)
    {
        if (HasSelection && slot.saveId == selectedId)
        {
            capturedAt = Time.realtimeSinceStartupAsDouble;
            slot.totalApplicationSeconds = Math.Max(slot.totalApplicationSeconds, clockBase + Math.Max(0, capturedAt - clockStart));
        }
    }
    public static void ApplicationTimeSaved(SaveDataManager.SaveData slot)
    {
        if (!HasSelection || slot.saveId != selectedId) return;
        clockBase = Math.Max(clockBase, slot.totalApplicationSeconds);
        clockStart = capturedAt;
    }
    public static bool FlushApplicationTime()
    {
        if (!HasSelection) return true;
        if (!TryReadSlot(out var slot, out string error)) { Debug.LogWarning(error); return false; }
        try
        {
            MergeApplicationTime(slot);
            AtomicSaveFile.Write(selectedPath, JsonUtility.ToJson(slot, true));
            ApplicationTimeSaved(slot);
            return true;
        }
        catch (Exception exception) { Debug.LogWarning(exception.Message); return false; }
    }
}
