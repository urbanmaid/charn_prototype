using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Charm.Mechanism.Missions
{
    [Serializable]
    public sealed class RouteActivation
    {
        [Min(0)] public int missionIndex;
        public GameObject[] groups = Array.Empty<GameObject>();
    }

    /// <summary>Opt-in, self-contained review host. Does not change the original stage's runtime setup.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageMissionController))]
    public sealed class StageRoutePreview : MonoBehaviour
    {
        [SerializeField] private StageRoutePoint[] route = Array.Empty<StageRoutePoint>();
        [SerializeField] private RouteActivation[] activations = Array.Empty<RouteActivation>();
        [SerializeField] private bool showReviewHud = true;
        private StageMissionController controller;
        private bool[] activated;
        private StageRoutePoint candidate;
        private PlayerInput currentInput;
        private BasicBehaviour currentPlayer;
        private InputAction interact;
        public StageMissionController Controller => controller;
        public System.Collections.Generic.IReadOnlyList<StageRoutePoint> Route => route;

        private IEnumerator Start()
        {
            controller = GetComponent<StageMissionController>();
            StageSceneInitializer initializer = FindAnyObjectByType<StageSceneInitializer>();
            if (initializer == null) { Debug.LogError("검토 미션은 메뉴를 통해 MainGameplay에서 시작하세요.", this); yield break; }
            while (!initializer.IsStageInitialized) yield return null;
            if (initializer.ActiveStageMetaData == null || initializer.ActiveStageMetaData.StageId != GetComponent<StageMissionDefinition>().StageId)
            { Debug.LogError("검토 프리팹과 선택한 스테이지 ID가 다릅니다.", this); yield break; }
            // The review stage has a separate ID so the original procedural enemy relocation/airstrike does not run.
            // Retain the existing pier collision correction, which does not move any authored enemy.
            VandalismConnectedBridge.RepairPierCollision(initializer.SpawnedStage.transform);
            initializer.NotifyCoverGeometryChanged();
            activated = new bool[activations.Length];
            controller.ProgressChanged += ApplyActivations;
            controller.Begin();
            ApplyActivations();
        }

        private void OnDestroy()
        {
            if (controller != null) controller.ProgressChanged -= ApplyActivations;
        }

        private void ApplyActivations()
        {
            if (activated == null || controller.Runtime == null) return;
            for (int i = 0; i < activations.Length; i++)
            {
                if (activated[i] || controller.Runtime.CurrentMissionIndex < activations[i].missionIndex) continue;
                activated[i] = true;
                foreach (var group in activations[i].groups) if (group != null) group.SetActive(true);
            }
        }

        private void Update()
        {
            candidate = null;
            if (controller == null || controller.Runtime == null || controller.Runtime.IsCleared) return;
            bool allowed = GameplayCursor.AllowsGameplayInput;
            controller.SetGameplayRunning(allowed);
            if (!allowed) return;
            var player = PlayerControlResolver.Resolve(null);
            if (player == null) return;
            if (currentPlayer != player)
            {
                currentPlayer = player;
                currentInput = player.GetComponent<PlayerInput>();
                interact = currentInput != null ? currentInput.actions.FindAction("Interact", false) : null;
            }
            int index = controller.Runtime.CurrentMissionIndex;
            var active = controller.Runtime.Definitions[index];
            float nearest = float.PositiveInfinity;
            foreach (var point in route)
            {
                if (point == null || !point.isActiveAndEnabled || !point.Contains(player.transform.position)) continue;
                bool target = false;
                foreach (string id in active.TargetIds) if (id == point.Id) target = true;
                if (point.ObjectiveType == MissionObjectiveType.CollectItems && !controller.Runtime.Actions.WasCollected(point.Id)) target = true;
                if (!target) continue;
                float distance = (point.transform.position - player.transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; candidate = point; }
            }
            // At most one action per frame; held input cannot cascade across successive objectives.
            if (candidate != null) candidate.Apply(controller.Runtime, player.transform.position, interact != null && interact.WasPressedThisFrame());
        }

        private void OnGUI()
        {
            if (!showReviewHud || controller == null || controller.Runtime == null) return;
            var run = controller.Runtime;
            GUI.Box(new Rect(16, 80, 430, 110), "반달리즘 · 동선 검토");
            if (run.IsCleared) GUI.Label(new Rect(28, 110, 405, 65), "검토 동선 완료\n기록 시간: " + run.StageRecordSeconds.ToString("F1") + "초");
            else
            {
                var definition = run.Definitions[run.CurrentMissionIndex];
                GUI.Label(new Rect(28, 108, 405, 26), (run.CurrentMissionIndex + 1) + "/" + run.Definitions.Count + "  " + definition.Title);
                GUI.Label(new Rect(28, 135, 405, 44), candidate != null && candidate.NeedsInput ? "F : " + candidate.Prompt : definition.Description);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            for (int i = 1; i < route.Length; i++)
                if (route[i - 1] != null && route[i] != null) Gizmos.DrawLine(route[i - 1].transform.position, route[i].transform.position);
        }
    }
}
