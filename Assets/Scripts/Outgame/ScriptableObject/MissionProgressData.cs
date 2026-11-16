using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

/// <summary>
/// 인게임 미션의 선형 진행 정보를 보유하고 실행한다.
/// 이 컴포넌트가 붙은 GameObject의 자녀 오브젝트를 미션 오브젝트로 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MissionProgressData : MonoBehaviour
{
    [Serializable]
    public struct MissionProgressUnit
    {
        [Tooltip("플레이어가 진입하면 이 유닛이 시작되는 트리거입니다.")]
        public GameObject triggerObject;

        [Tooltip("트리거 진입 즉시 활성화할 적 그룹 또는 진행 오브젝트입니다.")]
        public GameObject[] progressObjects;

        [Tooltip("지정하면 플레이어가 이 오브젝트에 도달한 뒤 다음 유닛으로 진행합니다.")]
        public GameObject triggerTargetObject;
    }

    private enum ProgressState
    {
        NotStarted,
        WaitingForTrigger,
        WaitingForTarget,
        WaitingForGoal,
        Completed
    }

    private sealed class TriggerBinding
    {
        public TriggerDetector detector;
        public Action<Collider, Collider> callback;
    }

    [Header("Mission Goal")]
    [Tooltip("마지막 진행 이후 플레이어가 도달해야 하는 목표입니다. 비워 두면 마지막 유닛 완료 시 클리어됩니다.")]
    [SerializeField] private GameObject goalObject;

    [Header("Mission Progress")]
    [SerializeField] private MissionProgressUnit[] missionProgressUnits = Array.Empty<MissionProgressUnit>();

    [Header("Mission Completion")]
    [Tooltip("목표 도달 또는 마지막 유닛 완료 시 호출할 게임 클리어 처리입니다.")]
    [SerializeField] private UnityEvent onMissionCompleted;

    private readonly List<TriggerBinding> triggerBindings = new List<TriggerBinding>();
    private ProgressState progressState;
    private int currentUnitIndex = -1;
    private bool runtimeInitialized;

    public GameObject GoalObject => goalObject;
    public IReadOnlyList<MissionProgressUnit> MissionProgressUnits => missionProgressUnits;
    public bool IsMissionCompleted => progressState == ProgressState.Completed;
    public int CurrentUnitIndex => currentUnitIndex;

    /// <summary>
    /// 미션 클리어 시 호출된다.
    /// </summary>
    public event Action<MissionProgressData> MissionCompleted;

    private void Awake()
    {
        InitializeRuntime();
    }

    private void OnDestroy()
    {
        UnregisterTriggerBindings();
    }

    /// <summary>
    /// 미션 진행 상태를 처음부터 다시 시작한다.
    /// 재시작, 씬 재진입, 실패 후 재도전 시 호출할 수 있다.
    /// </summary>
    public void ResetMission()
    {
        if (!runtimeInitialized)
        {
            RegisterTriggerBindings();
            runtimeInitialized = true;
        }

        ResetMissionObjects();
        ActivateFirstProgressUnit();
    }

    private void InitializeRuntime()
    {
        if (!runtimeInitialized)
        {
            RegisterTriggerBindings();
            runtimeInitialized = true;
        }

        ResetMissionObjects();
        ActivateFirstProgressUnit();

        ActivateGoalBeaconSFX();
    }

    private void ResetMissionObjects()
    {
        progressState = ProgressState.NotStarted;
        currentUnitIndex = -1;

        SetActive(goalObject, false);

        if (missionProgressUnits == null)
        {
            return;
        }

        for (int i = 0; i < missionProgressUnits.Length; i++)
        {
            MissionProgressUnit unit = missionProgressUnits[i];
            SetActive(unit.triggerObject, false);
            SetActive(unit.triggerTargetObject, false);

            if (unit.progressObjects == null)
            {
                continue;
            }

            for (int j = 0; j < unit.progressObjects.Length; j++)
            {
                SetActive(unit.progressObjects[j], false);
            }
        }
    }

    private void ActivateFirstProgressUnit()
    {
        if (missionProgressUnits == null || missionProgressUnits.Length == 0)
        {
            ActivateGoalOrComplete();
            return;
        }

        ActivateProgressUnit(0);
    }

    private void ActivateProgressUnit(int index)
    {
        if (missionProgressUnits == null || index < 0 || index >= missionProgressUnits.Length)
        {
            ActivateGoalOrComplete();
            return;
        }

        currentUnitIndex = index;
        progressState = ProgressState.WaitingForTrigger;

        MissionProgressUnit unit = missionProgressUnits[index];
        if (unit.triggerObject == null)
        {
            Debug.LogWarning($"[{nameof(MissionProgressData)}] {name}: {index + 1}번째 유닛에 triggerObject가 없습니다. 유닛을 건너뜁니다.", this);
            CompleteCurrentUnit(index);
            return;
        }

        SetActive(unit.triggerObject, true);
    }

    private void ActivateGoalBeaconSFX()
    {
        if(GoalBeaconSFX.instance)
        {
            GoalBeaconSFX.instance.UpdateCurrentBeacons();
        }
    }

    private void RegisterTriggerBindings()
    {
        UnregisterTriggerBindings();

        RegisterObjectTrigger(goalObject, OnGoalEntered);

        if (missionProgressUnits == null)
        {
            return;
        }

        for (int i = 0; i < missionProgressUnits.Length; i++)
        {
            int unitIndex = i;
            MissionProgressUnit unit = missionProgressUnits[i];
            RegisterObjectTrigger(unit.triggerObject, (myCollider, other) =>
                OnProgressTriggerEntered(unitIndex, myCollider, other));
            RegisterObjectTrigger(unit.triggerTargetObject, (myCollider, other) =>
                OnProgressTargetEntered(unitIndex, myCollider, other));
        }
    }

    private void RegisterObjectTrigger(GameObject target, Action<Collider, Collider> callback)
    {
        if (target == null || callback == null)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            Debug.LogWarning($"[{nameof(MissionProgressData)}] {name}: '{target.name}'에 Collider가 없습니다.", target);
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            TriggerDetector detector = collider.GetComponent<TriggerDetector>();
            if (detector == null)
            {
                detector = collider.gameObject.AddComponent<TriggerDetector>();
            }

            detector.OnDetected += callback;
            triggerBindings.Add(new TriggerBinding
            {
                detector = detector,
                callback = callback
            });
        }
    }

    private void UnregisterTriggerBindings()
    {
        for (int i = 0; i < triggerBindings.Count; i++)
        {
            TriggerBinding binding = triggerBindings[i];
            if (binding?.detector != null)
            {
                binding.detector.OnDetected -= binding.callback;
            }
        }

        triggerBindings.Clear();
    }

    private void OnProgressTriggerEntered(int unitIndex, Collider myCollider, Collider other)
    {
        if (progressState != ProgressState.WaitingForTrigger || currentUnitIndex != unitIndex || !IsPlayer(other))
        {
            return;
        }

        MissionProgressUnit unit = missionProgressUnits[unitIndex];
        ReachGoalObject(unit.triggerObject);
        ActivateProgressObjects(unit.progressObjects);

        if (unit.triggerTargetObject != null)
        {
            progressState = ProgressState.WaitingForTarget;
            SetActive(unit.triggerTargetObject, true);
            return;
        }

        CompleteCurrentUnit(unitIndex);
    }

    private void OnProgressTargetEntered(int unitIndex, Collider myCollider, Collider other)
    {
        if (progressState != ProgressState.WaitingForTarget || currentUnitIndex != unitIndex || !IsPlayer(other))
        {
            return;
        }

        ReachGoalObject(missionProgressUnits[unitIndex].triggerTargetObject);
        CompleteCurrentUnit(unitIndex);
    }

    private void CompleteCurrentUnit(int unitIndex)
    {
        if (currentUnitIndex != unitIndex || progressState == ProgressState.Completed)
        {
            return;
        }

        int nextUnitIndex = unitIndex + 1;
        if (nextUnitIndex < missionProgressUnits.Length)
        {
            ActivateProgressUnit(nextUnitIndex);
            return;
        }

        ActivateGoalOrComplete();
    }

    private void ActivateGoalOrComplete()
    {
        if (goalObject != null)
        {
            progressState = ProgressState.WaitingForGoal;
            SetActive(goalObject, true);
            return;
        }

        CompleteMission();
    }

    private void OnGoalEntered(Collider myCollider, Collider other)
    {
        if (progressState != ProgressState.WaitingForGoal || !IsPlayer(other))
        {
            return;
        }

        CompleteMission();
    }

    private void CompleteMission()
    {
        if (progressState == ProgressState.Completed)
        {
            return;
        }

        Debug.Log("Mission Complete!");

        progressState = ProgressState.Completed;
        ReachGoalObject(goalObject);
        onMissionCompleted?.Invoke();
        MissionCompleted?.Invoke(this);
    }

    private void ActivateProgressObjects(GameObject[] progressObjects)
    {
        if (progressObjects == null)
        {
            return;
        }

        for (int i = 0; i < progressObjects.Length; i++)
        {
            SetActive(progressObjects[i], true);
        }
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }

    private static void ReachGoalObject(GameObject target)
    {
        if (target == null) return;

        if (target.TryGetComponent<MissionGoalAnimator>(out var animator))
        {
            animator.OnGoalReached();
        }
        else
        {
            SetActive(target, false);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private bool IsChildObject(GameObject target)
    {
        return target != null && target.transform != transform && target.transform.IsChildOf(transform);
    }

    private void OnValidate()
    {
        if (goalObject != null && !IsChildObject(goalObject))
        {
            goalObject = null;
        }

        if (missionProgressUnits == null)
        {
            return;
        }

        for (int i = 0; i < missionProgressUnits.Length; i++)
        {
            MissionProgressUnit unit = missionProgressUnits[i];
            if (!IsChildObject(unit.triggerObject))
            {
                unit.triggerObject = null;
            }

            if (!IsChildObject(unit.triggerTargetObject))
            {
                unit.triggerTargetObject = null;
            }

            if (unit.progressObjects != null)
            {
                for (int j = 0; j < unit.progressObjects.Length; j++)
                {
                    if (!IsChildObject(unit.progressObjects[j]))
                    {
                        unit.progressObjects[j] = null;
                    }
                }
            }

            missionProgressUnits[i] = unit;
        }
    }
}
