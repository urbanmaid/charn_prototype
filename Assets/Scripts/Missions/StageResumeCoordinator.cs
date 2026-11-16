using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnemyAI;

namespace Charm.Mechanism.Missions
{
    [DisallowMultipleComponent, RequireComponent(typeof(StageCheckpointDetector))]
    public sealed class StageResumeCoordinator : MonoBehaviour
    {
        [SerializeField] private SavedPersonaReference[] personas = Array.Empty<SavedPersonaReference>();
        private StageMissionDirector director;
        private StageWorldSnapshot world;
        public StageCombatStatistics Statistics { get; private set; }
        private StageSaveData saved;
        public StageAttemptResult CompletedResult => State == RecoveryState.Ended && saved?.results != null
            ? saved.results.FindLast(r => r.stageCompleted || r.stageFailed) : null;
        private StageCheckpointDetector detector;
        private readonly Dictionary<StateController,bool> aiStates = new Dictionary<StateController,bool>();
        public bool CaptureCombatEnabled(StateController ai) => !IsReady && aiStates.TryGetValue(ai,out bool enabled) ? enabled : ai.enabled;
        public void RestoreSquadCombatEnabled(StateController ai, bool enabled)
        {
            aiStates[ai] = enabled;
        }
        public enum RecoveryState { Waiting, Restoring, Playing, RecoveryRequired, CompletionSaveFailed, ExitSaveFailed, Exiting, Ended }
        public RecoveryState State { get; private set; } = RecoveryState.Waiting;
        public string Warning { get; private set; }
        public bool CanRestart => saved != null && (!IsReady || State == RecoveryState.Ended);
        private string dismissedWarning;
        private string writeError;
        private string requestedMenuPath;
        private bool ownsPause;
        private float previousTimeScale = 1f;
        private bool transitionPending;
        private StageSaveData terminalCandidate;
        private bool terminalCombatSuspended;
        private readonly List<int> completedIndices = new List<int>();
        public bool IsSuspended { get; private set; }
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        private void AcquirePause()
        {
            if (!ownsPause)
            {
                var pause = FindAnyObjectByType<PauseUIController>();
                previousTimeScale = pause != null ? pause.ResumeTimeScale : Time.timeScale;
                if (previousTimeScale <= 0f) previousTimeScale = 1f;
                ownsPause = true;
            }
            IsSuspended = true;
            Time.timeScale = 0f;
            GameplayCursor.SetUIOpen(this, true);
            if (director != null && director.Controller != null) director.Controller.SetGameplayRunning(false);
        }

        private void ReleasePause()
        {
            IsSuspended = false;
            if (ownsPause)
            {
                var pause = FindAnyObjectByType<PauseUIController>();
                Time.timeScale = pause != null && pause.IsPaused ? 0f : previousTimeScale;
                ownsPause = false;
            }
            GameplayCursor.SetUIOpen(this, false);
        }

        public void ReportInitializationFailure(Exception exception)
        {
            if (State == RecoveryState.Exiting) return;
            director = GetComponent<StageMissionDirector>();
            Error = exception.Message;
            State = RecoveryState.RecoveryRequired;
            AcquirePause();
            Debug.LogException(exception, this);
        }

        public IEnumerator Initialize(StageMissionDirector host)
        {
            director=host;detector=GetComponent<StageCheckpointDetector>();
            State = RecoveryState.Restoring;
            AcquirePause();
            aiStates.Clear();
            foreach(var ai in FindObjectsByType<StateController>(FindObjectsInactive.Include))
                if(ai.gameObject.scene==gameObject.scene){aiStates.Add(ai,ai.enabled);ai.enabled=false;}
            yield return null; // Scene squad components finish ordinary Start without waking future encounters.
            SavedWorld restored=null;
            try
            {
                world=new StageWorldSnapshot(host,personas);
                if(!StageSaveSession.TryReadStage(GetComponent<StageMissionDefinition>().StageId,out saved,out string error))throw new InvalidOperationException(error);
                saved=saved??new StageSaveData{stageId=GetComponent<StageMissionDefinition>().StageId};
                host.Controller.Begin(saved.actualPlaySeconds);host.Controller.SetGameplayRunning(false);
                Statistics = new StageCombatStatistics(host, this, world.SquadActors, personas, saved);
                if(saved.latestCheckpoint!=null)
                {
                    restored=world.Validate(saved.latestCheckpoint.world);
                    var candidate=new StageMissionRuntime(host.Controller.Runtime.StageId,host.Controller.Runtime.Definitions,saved.actualPlaySeconds);
                    candidate.RestoreProgress(saved.latestCheckpoint.progress);
                    world.PrepareRestore(restored);
                    // Permit Start, but block FSM Update using the existing owner-based guard.
                    foreach(var ai in host.GetComponentsInChildren<StateController>())
                    {
                        ai.SetAnimationResetPending(this,true);
                        ai.enabled=true;
                    }
                }
            }
            catch(Exception exception){ReportInitializationFailure(exception);}
            if(Error!=null)yield break;
            if(restored!=null)yield return null;
            try
            {
                foreach(var ai in host.GetComponentsInChildren<StateController>())ai.enabled=false;
                if(restored!=null)
                {
                    host.Controller.Restore(saved.latestCheckpoint.progress,saved.actualPlaySeconds);
                    world.Restore(restored);
                }
                else
                {
                    if (saved.pendingFailedRestart) saved.retryCount++;
                    saved.pendingFailedRestart = false;
                    saved.runId=Guid.NewGuid().ToString("N");saved.missionAttemptId=Guid.NewGuid().ToString("N");
                    var cp=FindCheckpoint(host.Controller.Runtime.Definitions[0].CheckpointId);
                    saved.missionStart=MakeSnapshot(cp);saved.latestCheckpoint=saved.missionStart;
                    if(!Persist(saved))throw new InvalidOperationException(writeError);
                }
                foreach(var pair in aiStates)
                    if(pair.Key){pair.Key.SetAnimationResetPending(this,false);if(!pair.Key.GetComponent<HealthManager>().dead)pair.Key.enabled=pair.Value;}
                detector.Initialize(saved,Persist,point=>world.Capture(point.ResumePoint),()=>Statistics.CaptureValid(), snapshot=>Statistics.WriteCheckpoint(snapshot));
                detector.SaveFailed+=OnSaveFailed;
                detector.CheckpointSaved+=OnCheckpointSaved;
                host.Controller.MissionCompleted+=OnMissionCompleted;
                Statistics.StartObserving(saved.runId);
                IsReady=true;State=RecoveryState.Playing;Error=null;ReleasePause();
                if(!host.Controller.Runtime.IsCleared)host.Controller.SetGameplayRunning(true);
            }
            catch(Exception exception){ReportInitializationFailure(exception);}
        }

        private StageCheckpoint FindCheckpoint(string id)
        {
            foreach(var point in GetComponentsInChildren<StageCheckpoint>(true))if(point.Id==id)return point;
            throw new InvalidOperationException("Checkpoint not configured: "+id);
        }
        private StageCheckpointSnapshot MakeSnapshot(StageCheckpoint point)
        {
            var runtime=director.Controller.Runtime;
            var mission=runtime.Definitions[Math.Min(runtime.CurrentMissionIndex,runtime.Definitions.Count-1)].Id;
            point.TryGetOrder(mission,runtime.Actions,out int order);
            var snapshot = new StageCheckpointSnapshot{checkpointId=point.Id,missionId=mission,checkpointOrder=order,
                progress=runtime.CaptureProgress(),world=world.Capture(point.ResumePoint)};
            Statistics?.WriteCheckpoint(snapshot);
            return snapshot;
        }
        private bool Persist(StageSaveData value)
        {

            if(!StageSaveSession.TryWriteStage(value,out writeError))return false;
            saved=JsonUtility.FromJson<StageSaveData>(JsonUtility.ToJson(value));Warning=null;dismissedWarning=null;return true;
        }
        private void OnSaveFailed(string error)
        {
            string message=writeError ?? error;
            if(message==dismissedWarning)return;
            Warning=message;Debug.LogWarning(Warning,this);
        }
        public void DismissWarning(){dismissedWarning=Warning;Warning=null;}
        private void OnCheckpointSaved(string id){Warning=null;dismissedWarning=null;writeError=null;}
        private void OnMissionCompleted(int index)
        {
            detector?.DiscardPending();
            Warning=null;dismissedWarning=null;
            // Pre-earned collection/elimination objectives can complete together in one event.
            if (!completedIndices.Contains(index)) completedIndices.Add(index);
            transitionPending=true;
        }
        private void SuspendTerminalCombat()
        {
            AcquirePause();
            if (terminalCombatSuspended) return;
            terminalCombatSuspended = true;
            foreach (var ai in aiStates.Keys) if (ai) ai.SetAnimationResetPending(this, true);
        }

        private void LateUpdate()
        {
            if (!IsReady || State == RecoveryState.Exiting || State == RecoveryState.Ended) return;
            Statistics?.PollDeaths();
            var activeRun = director.Controller.Runtime;
            // Resolve after this frame's combat/goal callbacks. An already confirmed clear wins.
            if (State == RecoveryState.Playing && !activeRun.IsCleared
                && !activeRun.IsFailed && world.IsEntireSquadDown())
            {
                activeRun.Fail();
                detector.Suspend();
                transitionPending = true;
                SuspendTerminalCombat();
            }
            if (!transitionPending) return;
            transitionPending=false;
            try
            {
                var run=director.Controller.Runtime;
                var candidate=JsonUtility.FromJson<StageSaveData>(JsonUtility.ToJson(saved));candidate.actualPlaySeconds=run.ActualPlaySeconds;
                if (terminalCandidate != null) candidate = terminalCandidate;
                else
                {
                    var completedTimes=run.CaptureProgress().completedMissions;
                    foreach (int index in completedIndices)
                    {
                        var result=new StageAttemptResult{retryCount=candidate.retryCount,resultId=candidate.missionAttemptId,runId=candidate.runId,missionAttemptId=candidate.missionAttemptId,
                            missionId=run.Definitions[index].Id,succeeded=true,
                            stageCompleted=run.IsCleared && index==run.Definitions.Count-1,
                            actualSeconds=run.ActualPlaySeconds,completedMissions=completedTimes.GetRange(0,index+1),
                            combat=Statistics.CaptureMission(index),stageCombat=Statistics.CaptureValid()};
                        if(!candidate.results.Exists(r=>r.resultId==result.resultId))candidate.results.Add(result);
                        candidate.missionAttemptId=Guid.NewGuid().ToString("N");
                    }
                    if (run.IsFailed)
                    {
                        var result = new StageAttemptResult
                        {
                            retryCount = candidate.retryCount,
                            resultId = candidate.missionAttemptId, runId = candidate.runId,
                            missionAttemptId = candidate.missionAttemptId,
                            missionId = run.Definitions[run.CurrentMissionIndex].Id,
                            succeeded = false, stageFailed = true, actualSeconds = run.ActualPlaySeconds,
                            failedMissionSeconds = run.CurrentMissionSeconds, completedMissions = completedTimes,
                            combat = Statistics.CaptureMission(run.CurrentMissionIndex), stageCombat = Statistics.CaptureValid()
                        };
                        if (!candidate.results.Exists(r => r.resultId == result.resultId)) candidate.results.Add(result);
                    }
                    if (run.IsCleared || run.IsFailed)
                    {
                        candidate.pendingFailedRestart = run.IsFailed;
                        if (run.IsCleared) candidate.retryCount = 0;
                        candidate.latestCheckpoint = null;
                        candidate.missionStart = null;
                        candidate.runId = null;
                        candidate.missionAttemptId = null;
                        detector.Suspend();
                        terminalCandidate = candidate; // Immutable retry payload, including result identities.
                        SuspendTerminalCombat();
                    }
                    else
                    {
                        int completedIndex=completedIndices[completedIndices.Count-1];
                        StageCheckpoint next=null;int lastOrder=-1;
                        foreach(var point in GetComponentsInChildren<StageCheckpoint>(true))
                            if(point.TryGetOrder(run.Definitions[completedIndex].Id,out int order)&&order>lastOrder){lastOrder=order;next=point;}
                        if(next==null)throw new InvalidOperationException("Completed mission has no exit checkpoint.");
                        candidate.missionStart=MakeSnapshot(next);candidate.latestCheckpoint=candidate.missionStart;
                    }
                }
                if(!Persist(candidate))throw new InvalidOperationException(writeError);
                completedIndices.Clear();
                if (run.IsCleared || run.IsFailed)
                {
                    terminalCandidate = null;
                    Error = null;
                    State = RecoveryState.Ended;
                    AcquirePause();
                    return;
                }
                Error=null;
                State=RecoveryState.Playing;
                ReleasePause();
                if(!run.IsCleared)director.Controller.SetGameplayRunning(true);
                if(!run.IsCleared)detector.Initialize(saved,Persist,point=>world.Capture(point.ResumePoint),()=>Statistics.CaptureValid(), snapshot=>Statistics.WriteCheckpoint(snapshot));
            }
            catch(Exception exception)
            {
                Error=exception.Message;State=RecoveryState.CompletionSaveFailed;
                AcquirePause();Debug.LogException(exception,this);
            }
        }
        public bool SaveBeforeExit()
        {
            // Leaving before initialization has touched the world requires no new snapshot.
            if(State==RecoveryState.Waiting)return true;
            if(!StageSaveSession.HasSelection)return State!=RecoveryState.RecoveryRequired;
            if (State == RecoveryState.Playing) LateUpdate();
            if (State == RecoveryState.Ended) return true;
            if(completedIndices.Count>0 || terminalCandidate != null || director?.Controller?.Runtime?.IsFailed == true)
            {transitionPending=true;LateUpdate();}
            if (State == RecoveryState.Ended) return true;
            if(!IsReady || State==RecoveryState.CompletionSaveFailed)return false;
            if(detector.HasPending && !detector.RetryPending())
            {
                Error=Warning ?? "Checkpoint retry failed.";
                State=RecoveryState.ExitSaveFailed;AcquirePause();return false;
            }
            var candidate=JsonUtility.FromJson<StageSaveData>(JsonUtility.ToJson(saved));
            candidate.actualPlaySeconds=Math.Max(candidate.actualPlaySeconds,director.Controller.Runtime.ActualPlaySeconds);
            if(!Persist(candidate))
            {
                Error=writeError;State=RecoveryState.ExitSaveFailed;AcquirePause();return false;
            }
            Error=null;State=RecoveryState.Playing;ReleasePause();
            if(!director.Controller.Runtime.IsCleared)director.Controller.SetGameplayRunning(true);
            return true;
        }

        public bool RequestMenuExit(string menuPath, bool discardUnsaved)
        {
            if(!Application.CanStreamedLevelBeLoaded(menuPath))
            {
                requestedMenuPath=null;
                CancelMenuExit();
                Warning="메뉴 씬을 불러올 수 없습니다: "+menuPath;
                return false;
            }
            requestedMenuPath=menuPath;
            if(!discardUnsaved && !SaveBeforeExit())return false;
            State=RecoveryState.Exiting;
            detector?.Suspend();
            ReleasePause();
            PauseUIController.LoadMenuScene(menuPath);
            return true;
        }

        public bool CancelMenuExit()
        {
            // A partially restored world or uncommitted mission completion cannot resume safely.
            if(State!=RecoveryState.ExitSaveFailed || !IsReady)return false;
            requestedMenuPath=null;
            Error=null;
            State=RecoveryState.Playing;
            ReleasePause();
            var pause=FindAnyObjectByType<PauseUIController>();
            if(pause!=null)pause.ResumeGame();
            if(!director.Controller.Runtime.IsCleared)director.Controller.SetGameplayRunning(true);
            return true;
        }

        public bool RetryCheckpoint()
        {
            writeError=null;dismissedWarning=null;
            return detector!=null && detector.RetryPending();
        }

        public void RetryFailure()
        {
            if(State==RecoveryState.RecoveryRequired){ReloadStage();return;}
            if(!string.IsNullOrEmpty(requestedMenuPath)){RequestMenuExit(requestedMenuPath,false);return;}
            if(State==RecoveryState.CompletionSaveFailed){transitionPending=true;LateUpdate();return;}
            if(State==RecoveryState.ExitSaveFailed)SaveBeforeExit();
        }

        public bool RestartStage()
        {
            if(!CanRestart)return false;
            if(State==RecoveryState.Ended)return ReloadStage();
            string scene=gameObject.scene.path;
            if(!Application.CanStreamedLevelBeLoaded(scene)){Error="플레이 씬을 다시 불러올 수 없습니다.";return false;}
            double actual=director?.Controller?.Runtime?.ActualPlaySeconds ?? saved.actualPlaySeconds;
            if(!StageSaveSession.TryRestartStage(saved.stageId,actual,out string error)){Error=error;return false;}
            return ReloadStage();
        }

        private bool ReloadStage()
        {
            string scene=gameObject.scene.path;
            if(!Application.CanStreamedLevelBeLoaded(scene)){Error="플레이 씬을 다시 불러올 수 없습니다.";return false;}
            State=RecoveryState.Exiting;
            detector?.Suspend();
            ReleasePause();
            var pause=FindAnyObjectByType<PauseUIController>();
            if(pause!=null)pause.ResumeGame();
            Time.timeScale=1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            return true;
        }

        private void OnDestroy()
        {
            Statistics?.Dispose();
            if(director && director.Controller)director.Controller.MissionCompleted-=OnMissionCompleted;
            if(detector){detector.SaveFailed-=OnSaveFailed;detector.CheckpointSaved-=OnCheckpointSaved;}
            foreach(var pair in aiStates)if(pair.Key)pair.Key.SetAnimationResetPending(this,false);
            ReleasePause();
        }

        private void OnGUI()
        {
            if (State == RecoveryState.Ended)
            {
                float resultWidth = Mathf.Min(440, Screen.width - 20);
                GUILayout.BeginArea(new Rect((Screen.width - resultWidth) / 2, 60, resultWidth,
                    Mathf.Min(280, Screen.height - 70)), GUI.skin.box);
                var run = director.Controller.Runtime;
                GUILayout.Label(run.IsCleared ? "스테이지 클리어" : "스테이지 실패 — 분대 전원 빈사");
                GUILayout.Label("결과를 저장했습니다. 재도전은 스테이지 시작부터 진행합니다.");
                GUILayout.Label(" ");
                GUILayout.Label(" ");
                //GUILayout.Label("완료 미션 기록 시간: " + run.StageRecordSeconds.ToString("F1") + "초");
                //GUILayout.Label("실제 누적 플레이 시간: " + run.ActualPlaySeconds.ToString("F1") + "초");
                if (!string.IsNullOrEmpty(Warning)) GUILayout.Label(Warning);
                if (!string.IsNullOrEmpty(Error)) GUILayout.Label(Error);
                if (GUILayout.Button("재도전")) RestartStage();
                if (GUILayout.Button("나가기"))
                {
                    var pause = FindAnyObjectByType<PauseUIController>();
                    RequestMenuExit(pause != null ? pause.MenuScenePath : PauseUIController.DefaultMenuScenePath, false);
                }
                GUILayout.EndArea();
                return;
            }
            if(string.IsNullOrEmpty(Error) && string.IsNullOrEmpty(Warning))return;
            // Existing IMGUI only; keep the whole panel usable on smaller displays.
            float width=Mathf.Min(620,Screen.width-20);
            GUILayout.BeginArea(new Rect(10,60,width,Mathf.Min(340,Screen.height-70)),GUI.skin.box);
            if(string.IsNullOrEmpty(Error))
            {
                GUILayout.Label("이전 저장은 유지됩니다. 경고를 닫고 플레이를 계속할 수 있습니다.");
                GUILayout.Label(Warning);
                if(detector!=null && detector.HasPending && GUILayout.Button("체크포인트 저장 재시도"))RetryCheckpoint();
                if(GUILayout.Button("경고 닫기"))DismissWarning();
            }
            else
            {
                GUILayout.Label("진행 데이터를 저장/복원하지 못했습니다.");
                GUILayout.Label(Error);
                if(!string.IsNullOrEmpty(Warning))
                {
                    GUILayout.Label(Warning);
                    if(GUILayout.Button("경고 닫기"))DismissWarning();
                }
                if(State==RecoveryState.ExitSaveFailed && GUILayout.Button("취소하고 계속 플레이"))CancelMenuExit();
                if(GUILayout.Button("재시도"))RetryFailure();
                if(CanRestart)
                {
                    GUILayout.Label("스테이지 재시작: 진행 스냅샷은 초기화하고 완료 기록·슬롯 총 실행 시간·다른 스테이지 저장은 유지합니다.");
                    if(GUILayout.Button("진행을 초기화하고 스테이지 재시작"))RestartStage();
                }
                GUILayout.Label("저장 없이 나가면 저장하지 못한 진행과 대기 중인 미션 성공·실패 결과가 사라집니다. 마지막 정상 저장은 유지됩니다.");
                if(GUILayout.Button("저장 없이 메뉴로 나가기"))
                {
                    var pause=FindAnyObjectByType<PauseUIController>();
                    RequestMenuExit(requestedMenuPath ?? (pause!=null ? pause.MenuScenePath : PauseUIController.DefaultMenuScenePath),true);
                }
            }
            GUILayout.EndArea();
        }
    }
}
