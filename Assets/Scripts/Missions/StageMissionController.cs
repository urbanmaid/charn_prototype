using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Explicit host boundary: scene initialization/restoration must finish before Begin/Resume.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageMissionDefinition))]
    public sealed class StageMissionController : MonoBehaviour
    {
        public StageMissionRuntime Runtime { get; private set; }
        public bool IsGameplayRunning { get; private set; }
        public event Action ProgressChanged;
        public event Action<int> MissionCompleted;
        public event Action StageCleared;

        public void Begin(double actualPlaySeconds = 0)
        {
            if (Runtime != null) throw new InvalidOperationException("A run already exists on this controller.");
            Attach(GetComponent<StageMissionDefinition>().CreateRuntime(actualPlaySeconds));
            Runtime.Start();
            IsGameplayRunning = !Runtime.IsCleared;
        }

        public void Restore(MissionProgressSnapshot progress, double actualPlaySeconds)
        {
            // Candidate validation cannot partially replace the live run.
            if (Runtime != null && actualPlaySeconds < Runtime.ActualPlaySeconds)
                throw new ArgumentException("Actual play time must not roll back.");
            // Enemy death can remove authored target components. The running stage already owns
            // validated, immutable ID definitions; restoring it must not resolve destroyed objects again.
            var candidate = Runtime == null
                ? GetComponent<StageMissionDefinition>().CreateRuntime(actualPlaySeconds)
                : new StageMissionRuntime(Runtime.StageId, Runtime.Definitions, actualPlaySeconds);
            candidate.RestoreProgress(progress);
            IsGameplayRunning = false;
            Detach();
            Attach(candidate);
            ProgressChanged?.Invoke();
            // Caller restores world/party before explicitly resuming. No completion events are replayed.
        }

        public void SetGameplayRunning(bool running)
        {
            if (running && (Runtime == null || !Runtime.HasStarted || Runtime.IsCleared || Runtime.IsFailed))
                throw new InvalidOperationException("No active mission run.");
            IsGameplayRunning = running;
        }

        private void Update()
        {
            if (IsGameplayRunning && Runtime != null && Time.timeScale > 0f && GameplayCursor.AllowsGameplayInput)
                Runtime.AdvanceActiveTime(Time.unscaledDeltaTime);
        }

        private void Attach(StageMissionRuntime runtime)
        {
            Runtime = runtime;
            Runtime.Changed += OnProgressChanged;
            Runtime.MissionCompleted += OnMissionCompleted;
            Runtime.StageCleared += OnStageCleared;
        }

        private void Detach()
        {
            if (Runtime == null) return;
            Runtime.Changed -= OnProgressChanged;
            Runtime.MissionCompleted -= OnMissionCompleted;
            Runtime.StageCleared -= OnStageCleared;
        }

        private void OnDestroy() => Detach();
        private void OnDisable() => IsGameplayRunning = false;
        private void OnProgressChanged() => ProgressChanged?.Invoke();
        private void OnMissionCompleted(int index) => MissionCompleted?.Invoke(index);
        private void OnStageCleared() { IsGameplayRunning = false; StageCleared?.Invoke(); }
    }
}
