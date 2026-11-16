using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class AllySquadManager : MonoBehaviour
{
    public static AllySquadManager Instance;

    [Header("PA Swap")]
    [SerializeField, Tooltip("플레이어 조종 슬롯. 미지정이면 활성 BasicBehaviour가 하나일 때 자동 선택합니다.")]
    private BasicBehaviour controlledPlayer;
    private BasicBehaviour resolvedPlayer;
    private BasicBehaviour lastAssignedPlayer;
    private int playerResolutionFrame = -1;
    private bool warnedUnresolvedPlayer;
    [SerializeField] private List<GameObject> allPAOnField = new();
    [SerializeField] private bool autoCollectPAOnField = true;
    [SerializeField] private float longPressThreshold = 0.35f;
    [SerializeField, Tooltip("조종 중인 캐릭터가 빈사가 되면 분대 순서의 다음 생존 팀원으로 자동 교환합니다.")]
    private bool autoSwapOnDowned = true;
    private PlayerHealth pendingDownedPlayer;
    private PersonaData pendingDownedPersona;
    private Charm.Mechanism.Missions.StageResumeCoordinator pendingAutoSwapStage;
    private int downedRequestFrame;

    [Header("UI")]
    [SerializeField] private HUDHighlight playerHUDHighlight;
    [SerializeField] private HUDArrayFSM hUDArrayFSM;

    public PersonaData dataCaster;
    public PersonaData dataTarget;
    public CurrentStatus statusCaster;
    public CurrentStatus statusTarget;

    private readonly List<AllyStateController> allies = new();
    private PlayerInput boundPlayerInput;
    private InputAction accessSwapAction;
    private InputAction scrollAction;
    private InputAction regroupAction, spreadAction;
    private float nextSpacingInput;
    private bool swapModeActive;
    private bool accessSwapPressed;
    private bool wasSwapModeActiveBeforePress;
    private bool warnedMissingAccessSwap;
    private bool warnedMissingScroll;
    private float accessSwapPressedAt;
    private int focusedPAIndex = -1;
    // 물리 복원과 마지막 Rebind까지 하나의 교환으로 관리한다.
    private GameObject[] pendingSwapTargets;
    private SwapPhysicsSuspension[] pendingSwapPhysics;
    private Coroutine pendingSwapCompletion;

    private int followSpacingMode = 1;
    public int FollowSpacingMode => followSpacingMode;

    // Spread is peaceful-only. Regroup can supersede combat after safe action completion.
    public bool TryChangeFollowSpacing(bool spread)
    {
        if (!isActiveAndEnabled || !GameplayCursor.AllowsGameplayInput || swapModeActive ||
            pendingSwapTargets != null || CurrentPlayer == null || IsDeadPA(CurrentPlayer.gameObject)) return false;
        var stage = FindAnyObjectByType<Charm.Mechanism.Missions.StageResumeCoordinator>();
        if (stage != null && (!stage.IsReady || stage.IsSuspended ||
            stage.State != Charm.Mechanism.Missions.StageResumeCoordinator.RecoveryState.Playing)) return false;
        int count = 0;
        bool combatRegroup = false;
        foreach (var member in allPAOnField)
        {
            if (member == null || member == CurrentPlayer.gameObject || !member.activeInHierarchy || IsDeadPA(member)) continue;
            if (!member.TryGetComponent(out AllyStateController ally) || ally.CombatController == null) return false;
            if (!ally.CombatController.CanAdjustFollowSpacing)
            {
                if (spread || !ally.CombatController.CanAcceptCombatRegroup) return false;
                combatRegroup = true;
            }
            count++;
        }
        if (count == 0) return false;
        if (combatRegroup)
            foreach (var member in allPAOnField)
            {
                if (member == null || member == CurrentPlayer.gameObject || !member.activeInHierarchy || IsDeadPA(member)) continue;
                if (!member.GetComponent<AllyStateController>().CombatController.CanAcceptCombatRegroup) return false;
            }
        followSpacingMode = combatRegroup ? 0 : Mathf.Clamp(followSpacingMode + (spread ? 1 : -1), 0, 2);
        int ordinal = 0;
        foreach (var member in allPAOnField)
        {
            if (member == null || member == CurrentPlayer.gameObject || !member.activeInHierarchy || IsDeadPA(member)) continue;
            var controller = member.GetComponent<AllyStateController>().CombatController;
            if (combatRegroup) controller.RequestCombatRegroup(this, ordinal++);
            else controller.SetSquadFollowSpacing(this, followSpacingMode, ordinal++);
        }
        return true;
    }

    private void CancelFollowSpacingMovement(bool clear = false)
    {
        foreach (var ally in allies)
        {
            if (ally == null || ally.CombatController == null) continue;
            if (clear) ally.CombatController.ClearSquadFollowSpacing();
            else ally.CombatController.CancelSquadFollowMovement();
        }
        if (clear) followSpacingMode = 1;
    }


    private void OnDisable()
    {
        UnbindInputActions();
        CancelFollowSpacingMovement(true);
        ClearAutoSwapRequest();
        if (pendingSwapCompletion != null) StopCoroutine(pendingSwapCompletion);
        CompletePendingSwap();
    }

    public IReadOnlyList<GameObject> AllPAOnField => allPAOnField;
    public BasicBehaviour CurrentPlayer
    {
        get
        {
            if (playerResolutionFrame != Time.frameCount ||
                !ReferenceEquals(lastAssignedPlayer, controlledPlayer) ||
                (!ReferenceEquals(resolvedPlayer, null) && !PlayerControlResolver.IsAvailable(resolvedPlayer)))
            {
                playerResolutionFrame = Time.frameCount;
                lastAssignedPlayer = controlledPlayer;
                resolvedPlayer = PlayerControlResolver.Resolve(controlledPlayer);
            }
            return resolvedPlayer;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInScene()
    {
        if (FindAnyObjectByType<BasicBehaviour>() == null) return;
        if (FindAnyObjectByType<AllySquadManager>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject(nameof(AllySquadManager)).AddComponent<AllySquadManager>();
    }

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshPAList();
        BindInputToCurrentPlayer();
        UpdateSelectedPAHighlight();
    }

    private void Update()
    {
        RefreshPAList();
        BindInputToCurrentPlayer();
        ProcessAutoSwapRequest();
        UpdateSelectedPAHighlight();
    }

    private void OnDestroy()
    {
        UnbindInputActions();
        if (Instance == this)
        {
            Instance = null;
        }

        ClearPAHighlights();
    }

    // Kill의 나머지 처리가 새 페르소나에 적용되지 않도록 다음 프레임에 처리한다.
    public void RequestAutoSwapOnDowned(PlayerHealth player)
    {
        if (!autoSwapOnDowned || !isActiveAndEnabled || player == null || !player.dead ||
            CurrentPlayer == null || CurrentPlayer.gameObject != player.gameObject) return;
        var persona = player.GetComponent<PersonaBehavior>()?.GetPersona();
        if (pendingDownedPlayer == player && pendingDownedPersona == persona) return;
        pendingDownedPlayer = player;
        pendingDownedPersona = persona;
        pendingAutoSwapStage = FindAnyObjectByType<Charm.Mechanism.Missions.StageResumeCoordinator>();
        downedRequestFrame = Time.frameCount;
    }

    private void ClearAutoSwapRequest()
    {
        pendingDownedPlayer = null;
        pendingDownedPersona = null;
        pendingAutoSwapStage = null;
    }

    private void ProcessAutoSwapRequest()
    {
        if (pendingDownedPlayer == null) return;
        if (!autoSwapOnDowned || CurrentPlayer == null ||
            CurrentPlayer.gameObject != pendingDownedPlayer.gameObject || !pendingDownedPlayer.dead ||
            pendingDownedPlayer.GetComponent<PersonaBehavior>()?.GetPersona() != pendingDownedPersona)
        {
            ClearAutoSwapRequest(); // 수동 교환 또는 부활로 이미 해결된 요청.
            return;
        }

        var stage = pendingAutoSwapStage;
        if (stage != null && (!stage.IsReady ||
            stage.State != Charm.Mechanism.Missions.StageResumeCoordinator.RecoveryState.Playing))
        {
            ClearAutoSwapRequest(); // 복원/종료 중의 빈사 상태를 새 자동 교환으로 이어가지 않는다.
            return;
        }
        if (Time.frameCount <= downedRequestFrame || Time.timeScale <= 0f ||
            (stage != null && stage.IsSuspended) || pendingSwapTargets != null) return;

        // 자동 교환 뒤 눌러 두었던 수동 교환 키를 놓아 재교환하는 것을 막는다.
        accessSwapPressed = false;
        EndSwapMode(false);
        ClearAutoSwapRequest(); // 실패/전원 빈사도 한 번만 처리하고 무한 재시도하지 않는다.
        for (int i = 1; i < allPAOnField.Count; i++)
        {
            var target = allPAOnField[i];
            if (!IsSelectableSwapTarget(target) || target.GetComponent<PersonaBehavior>()?.GetPersona() == null)
                continue;
            try
            {
                if (TrySwapPA(target)) return;
            }
            catch (System.Exception exception)
            {
                // 부분 적용 후 다른 대상을 연속 교환하지 않는다. 기존 교환의 정리 경로를 따른다.
                Debug.LogException(exception, this);
                return;
            }
        }
    }

    public void RegisterAlly(AllyStateController ally)
    {
        if (allies.Contains(ally)) return;
        allies.Add(ally);
        AddPAIfMissing(ally.gameObject);
        AllyFormationManager.Instance?.RecalculateSlots(allies);
    }

    public void UnregisterAlly(AllyStateController ally)
    {
        if (!allies.Remove(ally)) return;
        if (ally != null && ally.CombatController != null) ally.CombatController.ClearSquadFollowSpacing();
        allPAOnField.Remove(ally.gameObject);
        AllyFormationManager.Instance?.RecalculateSlots(allies);
    }

    public void JoinAllAllies()
    {
        foreach (var ally in allies)
        {
            if (ally != null && !IsDeadPA(ally.gameObject))
            {
                if (ally.CombatController != null)
                    ally.CombatController.enabled = true;
            }
        }
    }

    private void RefreshPAList()
    {
        allPAOnField.RemoveAll(pa => pa == null);
        allies.RemoveAll(ally => ally == null || IsDeadPA(ally.gameObject));

        if (autoCollectPAOnField)
        {
            foreach (var ally in FindObjectsByType<AllyStateController>(FindObjectsInactive.Exclude))
            {
                if (IsDeadPA(ally.gameObject))
                {
                    AddPAIfMissing(ally.gameObject);
                    continue;
                }

                if (!allies.Contains(ally))
                {
                    allies.Add(ally);
                }
                AddPAIfMissing(ally.gameObject);
            }
        }

        BasicBehaviour slot = CurrentPlayer;
        if (slot != null)
        {
            warnedUnresolvedPlayer = false;
            GameObject player = slot.gameObject;
            allPAOnField.Remove(player);
            allPAOnField.Insert(0, player);
        }
        else
        {
            if (!warnedUnresolvedPlayer)
            {
                Debug.LogWarning("[AllySquadManager] 플레이어 조종 슬롯을 확인할 수 없습니다. 활성 BasicBehaviour를 하나만 두거나 Controlled Player를 지정하세요.", this);
                warnedUnresolvedPlayer = true;
            }
            UnbindInputActions();
            swapModeActive = false;
            accessSwapPressed = false;
            focusedPAIndex = -1;
        }
    }

    private void AddPAIfMissing(GameObject pa)
    {
        if (pa != null && !allPAOnField.Contains(pa))
        {
            allPAOnField.Add(pa);
        }
    }

    private void BindInputToCurrentPlayer()
    {
        var player = CurrentPlayer;
        PlayerInput playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
        if (playerInput == null || playerInput.actions == null)
        {
            UnbindInputActions();
            return;
        }
        if (playerInput == boundPlayerInput)
        {
            return;
        }

        UnbindInputActions();
        boundPlayerInput = playerInput;
        accessSwapAction = boundPlayerInput.actions.FindAction("AccessSwap", false);
        scrollAction = boundPlayerInput.actions.FindAction("Scroll", false);
        regroupAction = boundPlayerInput.actions.FindAction("SquadRegroup", false);
        spreadAction = boundPlayerInput.actions.FindAction("SquadSpread", false);
        if (regroupAction != null) { regroupAction.performed += OnRegroupPerformed; regroupAction.Enable(); }
        if (spreadAction != null) { spreadAction.performed += OnSpreadPerformed; spreadAction.Enable(); }

        if (accessSwapAction != null)
        {
            accessSwapAction.started += OnAccessSwapStarted;
            accessSwapAction.canceled += OnAccessSwapCanceled;
            accessSwapAction.Enable();
        }
        else if (!warnedMissingAccessSwap)
        {
            warnedMissingAccessSwap = true;
            Debug.LogWarning("[AllySquadManager] Input action 'AccessSwap' is missing.");
        }

        if (scrollAction != null)
        {
            scrollAction.performed += OnScrollPerformed;
            scrollAction.Enable();
        }
        else if (!warnedMissingScroll)
        {
            warnedMissingScroll = true;
            Debug.LogWarning("[AllySquadManager] Input action 'Scroll' is missing.");
        }
    }

    private void UnbindInputActions()
    {
        if (regroupAction != null) regroupAction.performed -= OnRegroupPerformed;
        if (spreadAction != null) spreadAction.performed -= OnSpreadPerformed;
        regroupAction = spreadAction = null;
        if (accessSwapAction != null)
        {
            accessSwapAction.started -= OnAccessSwapStarted;
            accessSwapAction.canceled -= OnAccessSwapCanceled;
        }

        if (scrollAction != null)
        {
            scrollAction.performed -= OnScrollPerformed;
        }

        accessSwapAction = null;
        scrollAction = null;
        boundPlayerInput = null;
    }

    private void OnAccessSwapStarted(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f) return;
        RefreshPAList();
        if (CurrentPlayer == null || allPAOnField.Count < 2)
        {
            return;
        }

        accessSwapPressed = true;
        accessSwapPressedAt = Time.unscaledTime;
        wasSwapModeActiveBeforePress = swapModeActive;

        if (!swapModeActive)
        {
            EnterSwapMode();
        }
    }

    private void OnAccessSwapCanceled(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f)
        {
            accessSwapPressed = false;
            EndSwapMode(false);
            return;
        }
        if (!accessSwapPressed)
        {
            return;
        }

        accessSwapPressed = false;
        float heldTime = Time.unscaledTime - accessSwapPressedAt;

        if (heldTime >= longPressThreshold || wasSwapModeActiveBeforePress)
        {
            EndSwapMode(true);
        }
    }

    private void OnScrollPerformed(InputAction.CallbackContext context)
    {
        if (!isActiveAndEnabled || !GameplayCursor.AllowsGameplayInput) return;
        if (!swapModeActive)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed))
            {
                float delta = GetScrollDelta(context);
                if (Mathf.Abs(delta) >= .01f) RequestSpacingInput(delta > 0f);
            }
            return;
        }

        float scrollDelta = GetScrollDelta(context);
        if (Mathf.Abs(scrollDelta) < 0.01f)
        {
            return;
        }

        CycleFocusedPA(scrollDelta > 0f ? -1 : 1);
    }

    private void OnRegroupPerformed(InputAction.CallbackContext context) => RequestSpacingInput(false);
    private void OnSpreadPerformed(InputAction.CallbackContext context) => RequestSpacingInput(true);
    private void RequestSpacingInput(bool spread)
    {
        if (!isActiveAndEnabled || Time.unscaledTime < nextSpacingInput) return;
        // Shared debounce for brackets and wheel; rejected commands are never queued.
        if (TryChangeFollowSpacing(spread)) nextSpacingInput = Time.unscaledTime + .5f;
    }

    private float GetScrollDelta(InputAction.CallbackContext context)
    {
        object value = context.ReadValueAsObject();
        if (value is Vector2 vector)
        {
            return vector.y;
        }
        if (value is float scalar)
        {
            return scalar;
        }

        return 0f;
    }

    private void EnterSwapMode()
    {
        RefreshPAList();
        if (CurrentPlayer == null) return;
        focusedPAIndex = FindNextSelectablePAIndex(0, 1);
        if(focusedPAIndex < 0)
        {
            swapModeActive = false;
            UpdateSelectedPAHighlight();
            return;
        }

        swapModeActive = true;
        LogFocusedPA();
        UpdateSelectedPAHighlight();
    }

    private void EndSwapMode(bool commit)
    {
        if (!swapModeActive)
        {
            return;
        }

        if (commit)
        {
            TrySwapToFocusedPA();
        }

        swapModeActive = false;
        focusedPAIndex = -1;
        UpdateSelectedPAHighlight();
    }

    private void CycleFocusedPA(int direction)
    {
        RefreshPAList();
        if (CurrentPlayer == null || allPAOnField.Count < 2)
        {
            focusedPAIndex = -1;
            return;
        }

        int nextIndex = FindNextSelectablePAIndex(focusedPAIndex, direction);
        if(nextIndex < 0)
        {
            focusedPAIndex = -1;
            UpdateSelectedPAHighlight();
            return;
        }

        focusedPAIndex = nextIndex;
        LogFocusedPA();
        UpdateSelectedPAHighlight();
    }

    public void UpdateSelectedPAHighlight()
    {
        ResolvePAHighlightUI();

        GameObject selectedPA = GetHighlightedPA();
        bool hasSelection = swapModeActive && selectedPA != null;

        if(playerHUDHighlight != null)
        {
            GameObject playerPA = CurrentPlayer != null ? CurrentPlayer.gameObject : null;
            playerHUDHighlight.Show(hasSelection && selectedPA == playerPA);
        }

        if(hUDArrayFSM != null)
        {
            hUDArrayFSM.SetHighlightedPA(selectedPA, hasSelection);
            hUDArrayFSM.SetSwapMode(swapModeActive);
        }
    }

    public void ClearPAHighlights()
    {
        ResolvePAHighlightUI();

        if(playerHUDHighlight != null)
        {
            playerHUDHighlight.Show(false);
        }

        if(hUDArrayFSM != null)
        {
            hUDArrayFSM.ClearHighlights();
        }
    }

    private GameObject GetHighlightedPA()
    {
        if(!swapModeActive || focusedPAIndex < 0 || focusedPAIndex >= allPAOnField.Count)
        {
            return null;
        }

        GameObject highlightedPA = allPAOnField[focusedPAIndex];
        return IsSelectableSwapTarget(highlightedPA) ? highlightedPA : null;
    }

    private void ResolvePAHighlightUI()
    {
        if(hUDArrayFSM == null)
        {
            hUDArrayFSM = FindAnyObjectByType<HUDArrayFSM>(FindObjectsInactive.Include);
        }

        if(playerHUDHighlight != null)
        {
            return;
        }

        foreach(HUDHealth hudHealth in FindObjectsByType<HUDHealth>(FindObjectsInactive.Include))
        {
            if(hudHealth == null || hudHealth.GetComponentInParent<HUDArrayFSM>(true) != null)
            {
                continue;
            }

            playerHUDHighlight = hudHealth.GetComponentInChildren<HUDHighlight>(true);
            if(playerHUDHighlight != null)
            {
                break;
            }
        }
    }

    private void LogFocusedPA()
    {
        if (focusedPAIndex <= 0 || focusedPAIndex >= allPAOnField.Count || allPAOnField[focusedPAIndex] == null)
        {
            return;
        }

        Debug.LogFormat("[AllySquadManager] Swap focus: {0}", allPAOnField[focusedPAIndex].name);
    }

    public bool TrySwapToFocusedPA()
    {
        RefreshPAList();
        if (focusedPAIndex <= 0 || focusedPAIndex >= allPAOnField.Count)
        {
            return false;
        }

        return TrySwapPA(allPAOnField[focusedPAIndex]);
    }

    public bool TrySwapPA(GameObject target)
    {
        // 같은 초기화 구간에 중첩 교환이 들어와 잠금/물리 원본을 덮어쓰지 않게 한다.
        if (!isActiveAndEnabled || pendingSwapTargets != null) return false;
        RefreshPAList();
        if (CurrentPlayer == null || allPAOnField.Count == 0 ||
            allPAOnField[0] != CurrentPlayer.gameObject || target == null)
        {
            return false;
        }

        GameObject caster = allPAOnField[0];
        if (caster == target)
        {
            return false;
        }

        int targetIndex = allPAOnField.IndexOf(target);
        if (targetIndex <= 0)
        {
            return false;
        }

        if(!IsSelectableSwapTarget(target))
        {
            return false;
        }

        PersonaBehavior casterPersona = caster.GetComponent<PersonaBehavior>();
        PersonaBehavior targetPersona = target.GetComponent<PersonaBehavior>();
        if (casterPersona == null || targetPersona == null)
        {
            Debug.LogWarning("[AllySquadManager] PA swap requires PersonaBehavior on both caster and target.");
            return false;
        }

        dataCaster = casterPersona.GetPersona();
        dataTarget = targetPersona.GetPersona();
        statusCaster = CaptureCurrentStatus(caster);
        statusTarget = CaptureCurrentStatus(target);
        List<IngameEffectRuntimeState> casterEffects = CaptureTransferableEffects(caster);
        List<IngameEffectRuntimeState> targetEffects = CaptureTransferableEffects(target);
        statusCaster.ingameEffectStatus = casterEffects;
        statusTarget.ingameEffectStatus = targetEffects;
        // 빈사 시각도 페르소나와 함께 이전한다. 교환으로 부활 제한 시간이 다시 시작되지 않는다.
        HealthManager casterHealth = caster.GetComponent<HealthManager>();
        HealthManager targetHealth = target.GetComponent<HealthManager>();
        float? casterDeathTime = ReviveWindow.CaptureDeathTime(casterHealth);
        float? targetDeathTime = ReviveWindow.CaptureDeathTime(targetHealth);
        CancelFollowSpacingMovement();
        pendingSwapTargets = new[] { caster, target };
        foreach (GameObject pa in pendingSwapTargets)
        {
            var controller = pa.GetComponent<EnemyAI.StateController>();
            if (controller != null) controller.SetAnimationResetPending(this, true);
        }
        try
        {
            ClearRuntimeEffects(caster);
            ClearRuntimeEffects(target);

            Debug.LogFormat(
                "[AllySquadManager] Swap {0}({1}) <-> {2}({3}), distance {4:F2}",
                caster.name,
                dataCaster != null ? dataCaster.personaName : "No Persona",
                target.name,
                dataTarget != null ? dataTarget.personaName : "No Persona",
                Vector3.Distance(caster.transform.position, target.transform.position));

            Vector3 casterPosition = caster.transform.position;
            Quaternion casterRotation = caster.transform.rotation;
            Vector3 targetPosition = target.transform.position;
            Quaternion targetRotation = target.transform.rotation;
            pendingSwapPhysics = new SwapPhysicsSuspension[2];
            pendingSwapPhysics[0] = SuspendSwapPhysics(caster);
            pendingSwapPhysics[1] = SuspendSwapPhysics(target);

            SetWorldPose(caster, targetPosition, targetRotation);
            SetWorldPose(target, casterPosition, casterRotation);
            Physics.SyncTransforms();

            casterPersona.SetPersona(dataTarget);
            targetPersona.SetPersona(dataCaster);

            RestoreCurrentStatus(caster, statusTarget);
            RestoreCurrentStatus(target, statusCaster);
            ReviveWindow.RestoreDeathTime(casterHealth, targetDeathTime);
            ReviveWindow.RestoreDeathTime(targetHealth, casterDeathTime);
            ReorderPAListAfterSwap(targetIndex);
            ReorderAlliesFromPAList();
            RefreshAllyFollowState();
            ResetAnimatorAfterSwap(caster);
            ResetAnimatorAfterSwap(target);
            pendingSwapCompletion = StartCoroutine(CompleteSwapAfterFixedUpdate());

            return true;
        }
        catch
        {
            // 예외로 중단된 경우에도 교환 잠금이 영구히 남지 않게 한다.
            CompletePendingSwap();
            throw;
        }
    }

    /// <summary>
    /// Applies a changed checkpoint persona while simulation is suspended. Unlike an input swap,
    /// this does not exchange slots/status or wait for FixedUpdate; the caller restores saved state next.
    /// Returns false for an unchanged persona, without touching its animator, weapons or effects.
    /// </summary>
    public bool RestoreCheckpointPersona(GameObject actor, PersonaData persona)
    {
        if (actor == null || persona == null)
            throw new System.ArgumentException("Checkpoint actor and persona are required.");
        var behavior = actor.GetComponent<PersonaBehavior>();
        if (behavior == null)
            throw new System.InvalidOperationException("Checkpoint actor has no PersonaBehavior.");
        if (behavior.GetPersona() == persona) return false;
        if (Time.timeScale > 0f || pendingSwapTargets != null)
            throw new System.InvalidOperationException("Checkpoint persona restore requires suspended simulation and no pending swap.");

        CancelFollowSpacingMovement();
        pendingSwapTargets = new[] { actor };
        var controller = actor.GetComponent<EnemyAI.StateController>();
        if (controller != null) controller.SetAnimationResetPending(this, true);
        try
        {
            pendingSwapPhysics = new[] { SuspendSwapPhysics(actor) };
            behavior.SetPersona(persona);
        }
        finally
        {
            // FixedUpdate cannot run at timeScale=0. Reuse the ordinary swap finalizer now,
            // including physics restoration, final Rebind and release of our reset ownership.
            CompletePendingSwap();
            // Rebind resets Aim/Strafe even on legacy visuals without a persona pose profile.
            actor.GetComponent<EnemyAI.EnemyAnimation>()?.RestoreEncounterAnimationState();
        }
        return true;
    }

    private CurrentStatus CaptureCurrentStatus(GameObject pa)
    {
        CurrentStatus status = new();

        // Get health status
        if (pa.TryGetComponent(out PlayerHealth playerHealth))
        {
            status.currentHealth = playerHealth.health;
            status.currentShieldHealth = playerHealth.shieldHealth;
            status.totalShieldHealth = playerHealth.TotalShieldHealth;
        }
        else if (pa.TryGetComponent(out AllyHealthManager allyHealthManager))
        {
            status.currentHealth = allyHealthManager.health;
            status.currentShieldHealth = allyHealthManager.shieldHealth;
            status.totalShieldHealth = allyHealthManager.TotalShieldHealth;
        }

        // Get ammos and weapons status
        if (pa.TryGetComponent(out ShootBehaviour shootBehaviour))
        {
            status.ammoStatus = shootBehaviour.CaptureAmmoStatus();
            status.currentMagazine = status.ammoStatus.currentMagazine;
            status.reserveBullets = status.ammoStatus.reserveBullets;
        }
        else if (pa.TryGetComponent(out AllyStateController allyStateController))
        {
            status.ammoStatus = allyStateController.CaptureAmmoStatus();
            status.currentMagazine = status.ammoStatus.currentMagazine;
            status.reserveBullets = status.ammoStatus.reserveBullets;
        }

        // Get gadget status
        if (pa.TryGetComponent(out PlayerGadgetManager playerGadgetManager))
        {
            status.gadgets = playerGadgetManager.CaptureGadgetObjects();
        }
        else if (pa.TryGetComponent(out TeamGadgetManager teamGadgetManager))
        {
            status.gadgets = teamGadgetManager.CaptureGadgetObjects();
        }
        else if (pa.TryGetComponent<AllyStateController>(out var gadgetOwner)) status.gadgets = gadgetOwner.CaptureGadgetObjects();

        // Get skill status
        if (pa.TryGetComponent(out SkillBehavior skillBehavior))
        {
            status.skillStatus = skillBehavior.CaptureRuntimeStatus();
        }
        else if (pa.TryGetComponent(out AllyStateController skillStorage))
        {
            // 실제 스킬 컴포넌트를 우선하고, 없는 아군은 보관한 상태를 읽는다.
            status.skillStatus = skillStorage.CaptureSkillStatus();
        }

        // Out
        return status;
    }

    private void RestoreCurrentStatus(GameObject pa, CurrentStatus status)
    {
        if (pa.TryGetComponent(out IngameEffectBehavior effectBehavior))
        {
            effectBehavior.RestoreRuntimeStatus(status.ingameEffectStatus);
        }

        // Set health status after effects are restored so temporary shield values keep their consumed amount.
        if (pa.TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.RestoreRuntimeHealth(status.currentHealth, status.currentShieldHealth, status.totalShieldHealth);
        }
        else if (pa.TryGetComponent(out AllyHealthManager allyHealthManager))
        {
            allyHealthManager.RestoreRuntimeHealth(status.currentHealth, status.currentShieldHealth, status.totalShieldHealth);
        }

        // Set ammos and weapons status
        if (pa.TryGetComponent(out ShootBehaviour shootBehaviour))
        {
            AmmoRuntimeStatus ammoStatus = status.ammoStatus;
            if (!ammoStatus.hasWeapon && status.currentMagazine > 0)
            {
                ammoStatus = new AmmoRuntimeStatus
                {
                    hasWeapon = true,
                    activeWeapon = 1,
                    currentMagazine = status.currentMagazine,
                    reserveBullets = -1
                };
            }

            shootBehaviour.RestoreAmmoStatus(ammoStatus);
        }
        else if (pa.TryGetComponent(out AllyStateController allyStateController))
        {
            allyStateController.RestoreAmmoStatus(status.ammoStatus);
        }

        // Set gadget status
        if (pa.TryGetComponent(out PlayerGadgetManager playerGadgetManager))
        {
            playerGadgetManager.RestoreGadgetObjects(status.gadgets);
        }
        else if (pa.TryGetComponent(out TeamGadgetManager teamGadgetManager))
        {
            teamGadgetManager.RestoreGadgetObjects(status.gadgets);
        }
        else if (pa.TryGetComponent<AllyStateController>(out var gadgetOwner)) gadgetOwner.RestoreGadgetObjects(status.gadgets);

        // Set skill status
        if (pa.TryGetComponent(out SkillBehavior skillBehavior))
        {
            skillBehavior.RestoreRuntimeStatus(status.skillStatus);
        }
        else if (pa.TryGetComponent(out AllyStateController skillStorage))
        {
            skillStorage.RestoreSkillStatus(status.skillStatus);
        }

    }

    private void ClearRuntimeEffects(GameObject pa)
    {
        if (pa != null && pa.TryGetComponent(out IngameEffectBehavior effectBehavior))
        {
            effectBehavior.ClearRuntimeEffects();
        }
    }

    private List<IngameEffectRuntimeState> CaptureTransferableEffects(GameObject pa)
    {
        if (pa != null && pa.TryGetComponent(out IngameEffectBehavior effectBehavior))
        {
            return effectBehavior.CaptureRuntimeStatus(false);
        }

        return null;
    }

    private void SetWorldPose(GameObject pa, Vector3 position, Quaternion rotation)
    {
        if (pa.TryGetComponent(out NavMeshAgent navAgent) && navAgent.enabled && navAgent.gameObject.activeInHierarchy && navAgent.isOnNavMesh)
        {
            Vector3 warpPosition = position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                warpPosition = hit.position;
            }

            navAgent.Warp(warpPosition);
            pa.transform.rotation = rotation;
            return;
        }

        if (pa.TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody.position = position;
            rigidbody.rotation = rotation;
            // 교환 일시정지로 Kinematic이 된 몸체에는 속도를 기록하지 않는다.
            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            return;
        }

        pa.transform.SetPositionAndRotation(position, rotation);
    }

    private SwapPhysicsSuspension SuspendSwapPhysics(GameObject pa)
    {
        if (pa == null || pa.GetComponent<AllyHealthManager>() == null)
        {
            return null;
        }

        SwapPhysicsSuspension suspension = new(pa);
        suspension.Suspend();
        return suspension;
    }

    private IEnumerator CompleteSwapAfterFixedUpdate()
    {
        yield return new WaitForFixedUpdate();
        CompletePendingSwap();
    }

    private void CompletePendingSwap()
    {
        if (pendingSwapTargets == null) return;
        try
        {
            Physics.SyncTransforms();
            if (pendingSwapPhysics != null)
                foreach (SwapPhysicsSuspension suspension in pendingSwapPhysics)
                    suspension?.Restore();
            foreach (GameObject target in pendingSwapTargets)
                ResetAnimatorAfterSwap(target);
            Physics.SyncTransforms();
        }
        finally
        {
            // 최종 초기화 후 기존 FSM을 재개한다. 엄폐/추종의 정지 소유권은 건드리지 않는다.
            foreach (GameObject target in pendingSwapTargets)
            {
                if (target == null) continue;
                var controller = target.GetComponent<EnemyAI.StateController>();
                if (controller != null) controller.SetAnimationResetPending(this, false);
            }
            pendingSwapTargets = null;
            pendingSwapPhysics = null;
            pendingSwapCompletion = null;
        }
    }

    private void ResetAnimatorAfterSwap(GameObject target)
    {
        if (target == null || IsDeadPA(target))
        {
            return;
        }

        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            bool wasEnabled = animator.enabled;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
            animator.enabled = wasEnabled || !IsDeadPA(target);
        }

        // EnemyAnimation도 이 인터페이스를 구현하므로 여기서 한 번만 갱신한다.
        foreach (IPersonaVisualRefreshReceiver receiver in target.GetComponents<IPersonaVisualRefreshReceiver>())
        {
            receiver?.RefreshPersonaVisualReferences();
        }
    }

    private void ReorderPAListAfterSwap(int targetIndex)
    {
        if (targetIndex <= 0 || targetIndex >= allPAOnField.Count)
        {
            return;
        }

        GameObject playerObject = allPAOnField[0];
        GameObject swappedOutPlayerObject = allPAOnField[targetIndex];
        List<GameObject> reordered = new() { playerObject, swappedOutPlayerObject };

        for (int i = targetIndex + 1; i < allPAOnField.Count; i++)
        {
            reordered.Add(allPAOnField[i]);
        }

        for (int i = 1; i < targetIndex; i++)
        {
            reordered.Add(allPAOnField[i]);
        }

        allPAOnField = reordered;
    }

    private void ReorderAlliesFromPAList()
    {
        allies.Clear();
        for (int i = 1; i < allPAOnField.Count; i++)
        {
            if (allPAOnField[i] != null && !IsDeadPA(allPAOnField[i]) && allPAOnField[i].TryGetComponent(out AllyStateController ally))
            {
                allies.Add(ally);
            }
        }
    }

    private void RefreshAllyFollowState()
    {
        AllyFormationManager.Instance?.ForceResolvePlayer();
        AllyFormationManager.Instance?.RecalculateSlots(allies);

        foreach (var ally in allies)
        {
            if (ally == null || IsDeadPA(ally.gameObject))
            {
                continue;
            }

            if (ally.CombatController != null)
                ally.CombatController.enabled = true;
        }
    }

    private int FindNextSelectablePAIndex(int startIndex, int direction)
    {
        if(allPAOnField.Count < 2)
        {
            return -1;
        }

        int normalizedDirection = direction >= 0 ? 1 : -1;
        int index = startIndex;
        for(int i = 0; i < allPAOnField.Count - 1; i++)
        {
            index += normalizedDirection;
            if(index < 1)
            {
                index = allPAOnField.Count - 1;
            }
            else if(index >= allPAOnField.Count)
            {
                index = 1;
            }

            if(IsSelectableSwapTarget(allPAOnField[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsSelectableSwapTarget(GameObject pa)
    {
        if(pa == null)
        {
            return false;
        }

        return pa.activeInHierarchy && !IsDeadPA(pa);
    }

    private bool IsDeadPA(GameObject pa)
    {
        if (pa != null && pa.TryGetComponent(out AllyHealthManager allyHealth))
            return allyHealth.dead || allyHealth.health <= 0f;
        if (pa != null && pa.TryGetComponent(out PlayerHealth playerHealth))
            return playerHealth.dead || playerHealth.health <= 0f;
        HealthManager healthManager = pa != null ? pa.GetComponent<HealthManager>() : null;
        return healthManager != null && healthManager.dead;
    }
}

[System.Serializable]
public struct CurrentStatus
{
    // Health
    public float currentHealth;
    public float currentShieldHealth;
    public float totalShieldHealth;

    // Weapons
    public int currentMagazine;
    public int reserveBullets;
    public AmmoRuntimeStatus ammoStatus;

    // Gadgets
    public List<GameObject> gadgets;

    // Skills
    public SkillRuntimeStatus skillStatus;

    // Ingame Effects
    public List<IngameEffectRuntimeState> ingameEffectStatus;

}

public class SwapPhysicsSuspension
{
    private readonly AllyHealthManager ownerHealth;
    private readonly RigidbodyState[] rigidbodyStates;
    private readonly ColliderState[] colliderStates;

    public SwapPhysicsSuspension(GameObject root)
    {
        ownerHealth = root.GetComponent<AllyHealthManager>();
        Rigidbody rootRigidbody = root.GetComponent<Rigidbody>();
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        List<RigidbodyState> capturedRigidbodies = new();
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != null)
            {
                capturedRigidbodies.Add(new RigidbodyState(rb));
            }
        }
        rigidbodyStates = capturedRigidbodies.ToArray();

        Collider rootCollider = root.GetComponent<Collider>();
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        List<ColliderState> capturedColliders = new();
        foreach (Collider collider in colliders)
        {
            if (collider == null || collider == rootCollider || collider.isTrigger)
            {
                continue;
            }

            Rigidbody attachedRigidbody = collider.attachedRigidbody;
            if (attachedRigidbody == null || attachedRigidbody == rootRigidbody)
            {
                continue;
            }

            capturedColliders.Add(new ColliderState(collider));
        }
        colliderStates = capturedColliders.ToArray();
    }

    public void Suspend()
    {
        foreach (RigidbodyState state in rigidbodyStates)
        {
            state.Suspend();
        }

        foreach (ColliderState state in colliderStates)
        {
            state.Suspend();
        }
    }

    public void Restore()
    {
        bool downed = ownerHealth != null && (ownerHealth.dead || ownerHealth.health <= 0f);
        foreach (RigidbodyState state in rigidbodyStates)
        {
            state.Restore(downed);
        }

        if (downed) return;
        foreach (ColliderState state in colliderStates)
        {
            state.Restore();
        }
    }

    private readonly struct RigidbodyState
    {
        private readonly Rigidbody rigidbody;
        private readonly bool isKinematic;
        private readonly RigidbodyConstraints constraints;
        private readonly Vector3 linearVelocity;
        private readonly Vector3 angularVelocity;

        public RigidbodyState(Rigidbody rigidbody)
        {
            this.rigidbody = rigidbody;
            isKinematic = rigidbody.isKinematic;
            constraints = rigidbody.constraints;
            linearVelocity = rigidbody.linearVelocity;
            angularVelocity = rigidbody.angularVelocity;
        }

        public void Suspend()
        {
            if (rigidbody == null)
            {
                return;
            }

            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            rigidbody.isKinematic = true;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        public void Restore(bool downed)
        {
            if (rigidbody == null)
            {
                return;
            }

            rigidbody.constraints = constraints;
            if (downed) return;
            rigidbody.isKinematic = isKinematic;
            // 원래 Dynamic이었던 몸체만 저장한 속도를 복원한다.
            if (!isKinematic)
            {
                rigidbody.linearVelocity = linearVelocity;
                rigidbody.angularVelocity = angularVelocity;
            }
        }
    }

    private readonly struct ColliderState
    {
        private readonly Collider collider;
        private readonly bool enabled;

        public ColliderState(Collider collider)
        {
            this.collider = collider;
            enabled = collider.enabled;
        }

        public void Suspend()
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        public void Restore()
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }
    }
}
