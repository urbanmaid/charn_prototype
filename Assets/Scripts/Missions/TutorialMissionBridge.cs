using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Observes existing tutorial interactions; never opens doors or consumes gadgets itself.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageMissionController))]
    public sealed class TutorialMissionBridge : MonoBehaviour
    {
        [Header("01 · Cover completion (either existing route)")]
        [SerializeField] private InteractCoverSense[] coverSensors;
        [SerializeField] private StageObjectId coverObjective;
        [Header("03 · Shooting range (not skill dummies)")]
        [SerializeField] private InteractiveSwitch shootingSwitch;
        [SerializeField] private TargetHealth[] shootingTargets;
        [Header("04–05 · Existing personal gadget and receiver")]
        [SerializeField] private Gadget accessCard;
        [SerializeField] private StageObjectId cardIdentity;
        [SerializeField] private InteractKeyReciever keyReceiver;
        [SerializeField] private StageObjectId doorObjective;
        private StageMissionController controller;
        private bool coverSucceeded, shootingSucceeded, keyUsed;
        private string cardId;
        private bool[] targetWasAlive;
        private const string KeyItemId = "tutorial_access_key";

        private void Awake()
        {
            controller = GetComponent<StageMissionController>();
            cardId = cardIdentity.Id;
            targetWasAlive = new bool[shootingTargets.Length];
            foreach (var sensor in coverSensors) sensor.OnInteractActivated += OnCover;
            shootingSwitch.OnInteractActivated += OnShooting;
            keyReceiver.OnInteractActivated += OnKey;
        }
        private void OnDestroy()
        {
            foreach (var sensor in coverSensors) if (sensor) sensor.OnInteractActivated -= OnCover;
            if (shootingSwitch) shootingSwitch.OnInteractActivated -= OnShooting;
            if (keyReceiver) keyReceiver.OnInteractActivated -= OnKey;
        }
        private void OnCover() => coverSucceeded = true;
        private void OnShooting() => shootingSucceeded = true;
        private void OnKey() => keyUsed = true;
        private void Update()
        {
            var run = controller.Runtime;
            if (run == null || !controller.IsGameplayRunning) return;
            if (coverSucceeded && run.CurrentMissionIndex == 0)
                run.RecordInteractionSucceeded(coverObjective.Id);
            for (int i = 0; i < shootingTargets.Length; i++)
            {
                var target = shootingTargets[i];
                if (!target || !target.gameObject.activeInHierarchy) continue;
                if (!target.IsDead) targetWasAlive[i] = true;
                // Targets begin folded down. Initial dead is not a completed shooting exercise.
                if ((targetWasAlive[i] && target.IsDead) || shootingSucceeded)
                    run.RecordEnemyDefeated(target.GetComponent<StageObjectId>().Id);
            }
            if (run.CurrentMissionIndex == 3 || run.CurrentMissionIndex == 4)
            {
                bool held = keyUsed;
                var player = AllySquadManager.Instance != null ? AllySquadManager.Instance.CurrentPlayer : null;
                if (!held && player && accessCard)
                {
                    var inventory = player.GetComponent<PlayerGadgetManager>();
                    held = inventory && inventory.CaptureGadgetObjects().Contains(accessCard.gameObject);
                }
                if (held) run.RecordItemCollected(cardId, KeyItemId, 1);
                if (keyUsed && run.CurrentMissionIndex == 4)
                    run.RecordInteractionSucceeded(doorObjective.Id);
            }
        }
    }
}


