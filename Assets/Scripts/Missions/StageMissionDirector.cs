using System;
using System.Collections;
using System.Collections.Generic;
using EnemyAI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Charm.Mechanism.Missions
{
    [Serializable]
    public sealed class MissionEnemyGroup
    {
        public string missionId;
        public GameObject group;
    }

    /// <summary>Owns authored encounter activation and world adapters; no procedural enemy placement.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(StageMissionController))]
    public sealed class StageMissionDirector : MonoBehaviour
    {
        [SerializeField] private StageRoutePoint[] points = Array.Empty<StageRoutePoint>();
        [SerializeField] private MissionEnemyGroup[] enemyGroups = Array.Empty<MissionEnemyGroup>();
        [SerializeField] private string openingMissionId;
        [SerializeField] private Transform openingDestination;
        [SerializeField] private bool showMissionHud = true;
        public bool UsingExternalHUD = false;
        private HUDTips externalHud;
        [SerializeField] private bool showObjectiveMarkers;
        [SerializeField, Tooltip("스테이지 종료 후 결과 씬을 표시합니다.")]
        private bool showResultScene = true;
        private StageMetaData resultStage;
        private bool resultTransitionStarted;
        [Header("Encounter preparation")]
        [SerializeField] private bool activateEncountersByDistanceOnly;
        [SerializeField, Min(0)] private int enemyActivationLeadMissions;
        [SerializeField, Min(0)] private float enemyPrewarmDistance;
        private readonly Dictionary<EnemyHealth, string> enemies = new Dictionary<EnemyHealth, string>();
        private readonly Dictionary<string, int> indices = new Dictionary<string, int>();
        private readonly Dictionary<GameObject, Vector3[]> encounterPositions = new Dictionary<GameObject, Vector3[]>();
        private readonly HashSet<GameObject> activatedGroups = new HashSet<GameObject>();
        private float nextProximityCheck;
        private GUIStyle hudTitleStyle, hudBodyStyle;
        private GUIStyle markerStyle;
        private bool pending;
        private StageRoutePoint candidate;
        private BasicBehaviour player;
        private InputAction interact;
        public StageMissionController Controller { get; private set; }
        public string OpeningMissionId => openingMissionId;
        public Transform OpeningDestination => openingDestination;
        public IReadOnlyList<StageRoutePoint> Points => points;

        private void Awake()
        {
            Controller = GetComponent<StageMissionController>();
            // Suppress encounters before their first Start; activation is owned exclusively here.
            foreach (var binding in enemyGroups)
            {
                if (binding.group == null) continue;
                var actors = binding.group.GetComponentsInChildren<EnemyHealth>(true);
                var positions = new Vector3[actors.Length];
                for (int i = 0; i < actors.Length; i++) positions[i] = actors[i].transform.position;
                encounterPositions[binding.group] = positions;
                binding.group.SetActive(false);
            }
        }

        private IEnumerator Start()
        {
            // Manually advance nested initialization iterators so exceptions reach the recovery UI.
            var work = new Stack<IEnumerator>();
            work.Push(StartStage());
            while (work.Count > 0)
            {
                object next = null;
                bool moved = false;
                Exception failure = null;
                try { moved = work.Peek().MoveNext(); if (moved) next = work.Peek().Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    var recovery = GetComponent<StageResumeCoordinator>();
                    if (recovery != null) recovery.ReportInitializationFailure(failure);
                    else Debug.LogException(failure, this);
                    yield break;
                }
                if (!moved) { (work.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) work.Push(nested);
                else yield return next;
            }
        }

        private IEnumerator StartStage()
        {
            var initializer = FindAnyObjectByType<StageSceneInitializer>();
            if (initializer == null) throw new InvalidOperationException("StageMissionDirector needs StageSceneInitializer.");
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!initializer.IsStageInitialized)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException("Stage initialization timed out (30 seconds).");
                yield return null;
            }
            if (initializer.ActiveStageMetaData.StageId != GetComponent<StageMissionDefinition>().StageId)
                throw new InvalidOperationException("Mission stage ID mismatch.");
            resultStage = initializer.ActiveStageMetaData;
            var definitions = GetComponent<StageMissionDefinition>().CreateRuntime().Definitions;
            for (int i = 0; i < definitions.Count; i++) indices.Add(definitions[i].Id, i);
            foreach (var binding in enemyGroups)
                if (binding.group == null || !indices.ContainsKey(binding.missionId))
                    throw new InvalidOperationException("Missing encounter group or mission ID: " + binding.missionId);
            foreach (var health in GetComponentsInChildren<EnemyHealth>(true))
            {
                var id = health.GetComponent<StageObjectId>();
                if (id == null) continue;
                enemies.Add(health, id.Id);
                health.DeathConfirmed += OnDeath;
            }
            Controller.ProgressChanged += ScheduleReconcile;
            var persistence = GetComponent<StageResumeCoordinator>();
            if (persistence != null && StageSaveSession.HasSelection)
            {
                yield return persistence.Initialize(this);
                if (!persistence.IsReady) yield break;
            }
            if (Controller.Runtime == null) Controller.Begin();
            // A supplied restored runtime is kept paused until the caller restores the world and resumes.
            pending = true;
        }

        private bool TryShowResult(StageResumeCoordinator persistence)
        {
            if (resultTransitionStarted) return true;
            var runtime = Controller?.Runtime;
            if (!showResultScene || resultStage == null || runtime == null ||
                !runtime.IsCleared) return false;

            // A checkpoint session, when present, must finish its save before unloading the stage.
            if (persistence != null && StageSaveSession.HasSelection &&
                persistence.State != StageResumeCoordinator.RecoveryState.Ended) return false;

            var progress = runtime.CaptureProgress();
            int finalMission = runtime.IsCleared ? runtime.Definitions.Count - 1 : runtime.CurrentMissionIndex;
            var result = persistence?.CompletedResult ?? new StageAttemptResult
            {
                missionId = runtime.Definitions[finalMission].Id,
                succeeded = runtime.IsCleared,
                stageCompleted = runtime.IsCleared,
                stageFailed = runtime.IsFailed,
                actualSeconds = runtime.ActualPlaySeconds,
                failedMissionSeconds = runtime.IsFailed ? runtime.CurrentMissionSeconds : 0,
                completedMissions = progress.completedMissions,
                combat = persistence?.Statistics?.CaptureMission(finalMission),
                stageCombat = persistence?.Statistics?.CaptureValid()
            };
            var pause = FindAnyObjectByType<PauseUIController>();
            string menuPath = pause != null ? pause.MenuScenePath : PauseUIController.DefaultMenuScenePath;
            resultTransitionStarted = true;
            // If the result scene is unavailable, keep the existing result UI usable.
            ResultSceneDisplayManager.TryOpen(result, resultStage, runtime.StageId, menuPath, gameObject.scene.path);
            return true;
        }

        private void ScheduleReconcile() => pending = true;
        private void OnDeath(EnemyHealth health)
        {
            if (Controller.Runtime != null && enemies.TryGetValue(health, out string id))
                Controller.Runtime.RecordEnemyDefeated(id);
        }

        private void LateUpdate()
        {
            var persistence = GetComponent<StageResumeCoordinator>();
            if (TryShowResult(persistence)) return;
            UpdateExternalHud();
            if (persistence != null && persistence.IsSuspended) return;
            if (Controller == null || Controller.Runtime == null || indices.Count == 0) return;
            // Check infrequently against cached spawn positions, including when missions do not advance.
            bool proximityDue = Controller.IsGameplayRunning && enemyPrewarmDistance > 0f
                && Time.timeScale > 0f && Time.unscaledTime >= nextProximityCheck;
            if (!pending && !proximityDue) return;
            if (proximityDue) nextProximityCheck = Time.unscaledTime + .25f;
            var controlled = proximityDue ? PlayerControlResolver.Resolve(null) : null;
            pending = false;
            // Reconcile from current state, including Restore; never rely on a missed enter event.
            foreach (var binding in enemyGroups)
            {
                bool enabled = activatedGroups.Contains(binding.group)
                    || (!activateEncountersByDistanceOnly && indices[binding.missionId] <= Controller.Runtime.CurrentMissionIndex + Mathf.Max(0, enemyActivationLeadMissions));
                if (!enabled && controlled != null && encounterPositions.TryGetValue(binding.group, out var positions))
                {
                    float radiusSquared = enemyPrewarmDistance * enemyPrewarmDistance;
                    foreach (var position in positions)
                        if ((position - controlled.transform.position).sqrMagnitude <= radiusSquared) { enabled = true; break; }
                }
                // Once visible, an encounter stays present even if the player moves away.
                if (enabled) activatedGroups.Add(binding.group);
                if (binding.group.activeSelf != enabled) SetEncounterActive(binding.group, enabled);
            }
            foreach (var point in points)
                if (point != null) point.RefreshPresentation(Controller.Runtime, IsCurrentTarget(point));
        }

        private bool IsCurrentTarget(StageRoutePoint point)
        {
            var run = Controller.Runtime;
            if (run == null || !run.HasStarted || run.IsCleared) return false;
            foreach (var id in run.Definitions[run.CurrentMissionIndex].TargetIds) if (point.Id == id) return true;
            return false;
        }

        public List<SavedEncounter> CaptureEncounters()
        {
            var result = new List<SavedEncounter>();
            var ids = new HashSet<string>();
            foreach(var binding in enemyGroups)
            {
                var identity = binding.group.GetComponent<StageObjectId>();
                if(identity == null || string.IsNullOrWhiteSpace(identity.Id) || !ids.Add(identity.Id))
                    throw new InvalidOperationException("Missing or duplicate encounter ID.");
                result.Add(new SavedEncounter { id=identity.Id, entered=activatedGroups.Contains(binding.group) || binding.group.activeSelf });
            }
            return result;
        }

        public string EncounterId(Transform enemy)
        {
            string id=null;
            foreach(var binding in enemyGroups)
                if(enemy.IsChildOf(binding.group.transform))
                {
                    if(id!=null)throw new InvalidOperationException("Nested encounter membership.");
                    id=binding.group.GetComponent<StageObjectId>()?.Id;
                }
            return id ?? throw new InvalidOperationException("Enemy has no encounter group.");
        }

        public void RestoreEncounters(List<SavedEncounter> saved)
        {
            var current=CaptureEncounters();
            var states=new bool[current.Count];
            for(int i=0;i<states.Length;i++)
            {
                var value=saved.Find(g=>g.id==current[i].id);
                if(value==null)throw new InvalidOperationException("Encounter identity changed.");
                states[i]=value.entered;
            }
            RestoreEncounterStates(states);
        }

        public bool[] CaptureEncounterStates()
        {
            var result = new bool[enemyGroups.Length];
            for (int i=0;i<result.Length;i++) result[i]=enemyGroups[i].group.activeSelf;
            return result;
        }

        public void RestoreEncounterStates(bool[] states)
        {
            if(states==null || states.Length!=enemyGroups.Length) throw new ArgumentException("Encounter layout changed.");
            activatedGroups.Clear();
            for(int i=0;i<states.Length;i++)
            {if(enemyGroups[i].group.activeSelf!=states[i])SetEncounterActive(enemyGroups[i].group,states[i]);if(states[i])activatedGroups.Add(enemyGroups[i].group);}
            pending=true;
        }

        private static void SetEncounterActive(GameObject group, bool active)
        {
            group.SetActive(active);
            if (!active) return;

        }

        private void Update()
        {
            candidate = null;
            if (Controller == null || Controller.Runtime == null || Controller.Runtime.IsCleared) return;
            if (!Controller.IsGameplayRunning || !GameplayCursor.AllowsGameplayInput || Time.timeScale <= 0) return;
            var current = PlayerControlResolver.Resolve(null);
            if (current == null) return;
            if (current != player)
            {
                player = current;
                var input = player.GetComponent<PlayerInput>();
                interact = input != null ? input.actions.FindAction("Interact", false) : null;
            }
            var active = Controller.Runtime.Definitions[Controller.Runtime.CurrentMissionIndex];
            float nearest = float.PositiveInfinity;
            foreach (var point in points)
            {
                if (point == null || !point.isActiveAndEnabled || !point.Contains(player.transform.position)) continue;
                bool target = point.ObjectiveType == MissionObjectiveType.CollectItems && !Controller.Runtime.Actions.WasCollected(point.Id);
                foreach (var id in active.TargetIds) if (id == point.Id) target = true;
                float distance = (point.transform.position - player.transform.position).sqrMagnitude;
                if (target && distance < nearest) { candidate = point; nearest = distance; }
            }
            if (candidate != null && candidate.Apply(Controller.Runtime, player.transform.position, interact != null && interact.WasPressedThisFrame())
                && candidate.ObjectiveType == MissionObjectiveType.CollectItems && !candidate.NeedsInput)
                GetComponent<StageCheckpointDetector>()?.Evaluate(player.transform.position);
        }

        private void OnDestroy()
        {
            ReleaseExternalHud();
            if (Controller != null) Controller.ProgressChanged -= ScheduleReconcile;
            foreach (var pair in enemies) if (pair.Key != null) pair.Key.DeathConfirmed -= OnDeath;
        }

        private void OnGUI()
        {
            if (Controller == null || Controller.Runtime == null) return;
            if (showObjectiveMarkers) DrawObjectiveMarkers();
            if (!showMissionHud || UsingExternalHUD) return;

            // Get style
            var run = Controller.Runtime;
            if (hudTitleStyle == null)
            {
                hudTitleStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold };
                hudBodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }

            // Get scale and location
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), .75f, 2f);
            hudTitleStyle.fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * scale));
            hudBodyStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * scale));

            // Declare safezone
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            float margin = 8f * scale, padding = 8f * scale;
            float width = Mathf.Min(330f * scale, safe.width - margin * 2f);
            if (width <= padding * 2f) return;

            // Set Content
            string title, body;
            if (run.IsCleared) { title = "스테이지 클리어"; body = "기록 시간: " + run.StageRecordSeconds.ToString("F1") + "초"; }
            else
            {
                var mission = run.Definitions[run.CurrentMissionIndex];
                title = (run.CurrentMissionIndex + 1) + "/" + run.Definitions.Count + "  " + mission.Title;
                body = candidate != null && candidate.NeedsInput ? "F : " + candidate.Prompt
                    : mission.Description + "  " + run.ObjectiveCurrent + "/" + run.ObjectiveRequired;
            }
            
            // Set content layout
            float innerWidth = width - padding * 2f;
            float titleHeight = hudTitleStyle.CalcHeight(new GUIContent(title), innerWidth);
            float bodyHeight = hudBodyStyle.CalcHeight(new GUIContent(body), innerWidth);
            float height = Mathf.Min(titleHeight + bodyHeight + padding * 2f + 2f * scale, safe.height - margin * 2f);

            // Start Drawing
            Rect panel = new Rect(safe.xMin + margin, Screen.height - safe.yMax + margin, width, height);
            GUI.Box(panel, GUIContent.none); // Clip only on exceptionally small windows; ordinary text wraps and grows the panel.
            GUI.BeginGroup(panel);
            GUI.Label(new Rect(padding, padding, innerWidth, titleHeight), title, hudTitleStyle);
            GUI.Label(new Rect(padding, padding + titleHeight + 2f * scale, innerWidth, bodyHeight), body, hudBodyStyle);
            GUI.EndGroup();
        }

        private void OnDisable() => ReleaseExternalHud();

        private void ReleaseExternalHud()
        {
            if (externalHud != null) externalHud.ClearOverrideTips(this);
            externalHud = null;
        }

        private void UpdateExternalHud()
        {
            var hud = HUDTips.instance;
            if (!UsingExternalHUD || !showMissionHud || Controller == null || Controller.Runtime == null || hud == null)
            {
                ReleaseExternalHud();
                return;
            }
            if (externalHud != hud)
            {
                ReleaseExternalHud();
                externalHud = hud;
            }
            var run = Controller.Runtime;
            string content;
            if (run.IsCleared)
                content = "스테이지 클리어\n기록 시간: " + run.StageRecordSeconds.ToString("F1") + "초";
            else
            {
                var mission = run.Definitions[run.CurrentMissionIndex];
                string targetCount = (run.ObjectiveRequired == 1) ? "" : run.ObjectiveCurrent + "/" + run.ObjectiveRequired;
                string body = 
                (
                    candidate != null && candidate.NeedsInput && IsCurrentTarget(candidate)
                    ? "F : " + candidate.Prompt
                    : mission.Description + "  " + targetCount
                );
                content = 
                (
                    run.CurrentMissionIndex + 1 + "/" + run.Definitions.Count + 
                    "  " + mission.Title + 
                    "\n" + body
                );
            }
            externalHud.ShowOverrideTips(this, content);
        }

        private void DrawObjectiveMarkers()
        {
            if (Controller.Runtime.IsCleared) return;
            var view = player != null && player.GetCamScript != null ? player.GetCamScript.GetComponent<Camera>() : null;
            if (view == null) view = Camera.main;
            if (view == null) return;
            if (markerStyle == null)
                markerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12 };
            foreach (var point in points)
            {
                if (point == null || !point.isActiveAndEnabled || !point.NeedsInput || !IsCurrentTarget(point)) continue;
                var screen = view.WorldToScreenPoint(point.transform.position + Vector3.up * 1.6f);
                if (screen.z <= 0f) continue;
                float x = Mathf.Clamp(screen.x, 75, Mathf.Max(75, Screen.width - 75));
                float y = Mathf.Clamp(Screen.height - screen.y, 18, Mathf.Max(18, Screen.height - 65));
                Color previous = GUI.color;
                GUI.color = new Color(1f, .12f, .1f);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(45, new Vector2(x, y));
                GUI.DrawTexture(new Rect(x - 5, y - 5, 10, 10), Texture2D.whiteTexture);
                GUI.matrix = matrix;
                GUI.color = previous;
                float distance = Vector3.Distance(player != null ? player.transform.position : view.transform.position, point.transform.position);
                var label = new Rect(x - 72, y + 9, 144, 40);
                GUI.Box(label, GUIContent.none);
                GUI.Label(label, point.Prompt + "\n" + Mathf.RoundToInt(distance) + " m", markerStyle);
            }
        }
    }
}
