using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Keep this root enabled; its CanvasGroup controls the pause screen visibility.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[DefaultExecutionOrder(-200)]
public sealed class PauseUIController : MonoBehaviour
{
    [Header("Pause Screen")]
    [SerializeField] private CanvasGroup pauseGroup;
    [Tooltip("일시정지 중 숨길 UI입니다. 재개하면 각 UI의 기존 활성 상태를 복원합니다.")]
    [SerializeField] private GameObject[] uiToHide = new GameObject[0];

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button statisticsButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private string menuScenePath = "Assets/Scenes/Outgame/Menu.unity";

    private readonly Dictionary<GameObject, bool> hiddenUIStates = new Dictionary<GameObject, bool>();
    private float previousTimeScale;
    private GameObject previousSelection;
    private int lastToggleFrame = -1;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public const string DefaultMenuScenePath = "Assets/Scenes/Outgame/Menu.unity";
    public string MenuScenePath => menuScenePath;
    public float ResumeTimeScale => IsPaused ? previousTimeScale : Time.timeScale;
    public bool IsPaused { get; private set; }
    private static bool RecoverySuspended => FindAnyObjectByType<Charm.Mechanism.Missions.StageResumeCoordinator>() is { IsSuspended: true };

    private void Awake()
    {
        if (pauseGroup == null) pauseGroup = GetComponent<CanvasGroup>();
        SetVisible(false);
        Bind(resumeButton, ResumeGame);
        Bind(statisticsButton, ShowStatistics);
        Bind(settingsButton, ShowSettings);
        Bind(returnToMenuButton, ReturnToMenu);
    }

    private void OnEnable() => GameplayCursor.EscapeRequested += TogglePause;

    public void ConfigureForSkillTest(Canvas screen)
    {
        if (screen == null) return;
        // Awake hid the serialized wrapper. Reset it before handing visibility to
        // the Canvas group: parent alpha multiplies child alpha, even across canvases.
        var previousGroup = pauseGroup;
        pauseGroup = screen.GetComponent<CanvasGroup>();
        if (pauseGroup == null) pauseGroup = screen.gameObject.AddComponent<CanvasGroup>();
        if (previousGroup != null && previousGroup != pauseGroup)
        {
            previousGroup.alpha = 1f;
            previousGroup.interactable = true;
            previousGroup.blocksRaycasts = true;
        }
        screen.gameObject.SetActive(true);
        screen.enabled = true;
        screen.renderMode = RenderMode.ScreenSpaceOverlay;
        screen.sortingOrder = 200;
        if (screen.GetComponent<GraphicRaycaster>() == null) screen.gameObject.AddComponent<GraphicRaycaster>();
        var hidden = new List<GameObject>();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (canvas == screen || screen.transform.IsChildOf(canvas.transform) ||
                canvas.gameObject.scene != gameObject.scene || !canvas.isRootCanvas ||
                canvas.transform.IsChildOf(transform) || transform.IsChildOf(canvas.transform)) continue;
            hidden.Add(canvas.gameObject);
        }
        uiToHide = hidden.ToArray();
        var background = screen.transform.Find("Image");
        if (background != null && background.TryGetComponent<Image>(out var dimmer))
        {
            dimmer.color = new Color(.015f, .025f, .045f, .72f);
            dimmer.raycastTarget = true;
        }
        var panel = screen.transform.Find("Layer_ProfileManagement") as RectTransform;
        if (panel != null)
        {
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(40, 20);
        }
        SetVisible(IsPaused);
    }

    private void OnDisable()
    {
        GameplayCursor.EscapeRequested -= TogglePause;
        ResumeGame();
    }

    private void OnDestroy()
    {
        Unbind(resumeButton, ResumeGame);
        Unbind(statisticsButton, ShowStatistics);
        Unbind(settingsButton, ShowSettings);
        Unbind(returnToMenuButton, ReturnToMenu);
    }

    private void Update()
    {
        // The local check also supports scenes where the gameplay camera is temporarily absent.
        // A frame guard prevents the camera's EscapeRequested event toggling a second time.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (lastToggleFrame == Time.frameCount || !isActiveAndEnabled) return;
        lastToggleFrame = Time.frameCount;
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (IsPaused || !isActiveAndEnabled || RecoverySuspended) return;
        IsPaused = true;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        hiddenUIStates.Clear();
        foreach (GameObject ui in uiToHide)
        {
            // Disabling this root (or an ancestor) would also disable the Escape listener.
            if (ui == null || transform.IsChildOf(ui.transform) || ui.transform.IsChildOf(transform) ||
                (pauseGroup != null && pauseGroup.transform.IsChildOf(ui.transform))) continue;
            if (!hiddenUIStates.ContainsKey(ui)) hiddenUIStates.Add(ui, ui.activeSelf);
            ui.SetActive(false);
        }

        Time.timeScale = 0f;
        GameplayCursor.SetUIOpen(this, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetVisible(true);
        if (EventSystem.current != null && resumeButton != null)
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;
        SetVisible(false);
        Time.timeScale = RecoverySuspended ? 0f : previousTimeScale;
        foreach (KeyValuePair<GameObject, bool> ui in hiddenUIStates)
            if (ui.Key != null) ui.Key.SetActive(ui.Value);
        hiddenUIStates.Clear();
        GameplayCursor.SetUIOpen(this, false);
        Cursor.lockState = RecoverySuspended ? CursorLockMode.None : previousCursorLock;
        Cursor.visible = RecoverySuspended || previousCursorVisible;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                ? previousSelection : null);
    }

    public void ShowStatistics() => Debug.Log("[PauseUI] 통계 화면을 열었습니다. (미구현)", this);
    public void ShowSettings() => Debug.Log("[PauseUI] 설정 화면을 열었습니다. (미구현)", this);

    public void ReturnToMenu()
    {
        var checkpoint = FindAnyObjectByType<Charm.Mechanism.Missions.StageResumeCoordinator>();
        if (checkpoint != null)
        {
            checkpoint.RequestMenuExit(menuScenePath, false);
            return;
        }
        LoadMenuScene(menuScenePath);
    }

    public static void LoadMenuScene(string menuScenePath)
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScenePath))
        {
            Debug.LogError("[PauseUI] 메뉴 씬을 불러올 수 없습니다. Build Settings를 확인해 주세요: " + menuScenePath);
            return;
        }
        var pause = FindAnyObjectByType<PauseUIController>();
        if (pause != null) pause.ResumeGame();
        Time.timeScale = 1f;
        // The outgame scene must start with a free cursor even if the gameplay camera unloads later.
        GameplayCursor.SetCaptured(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(menuScenePath, LoadSceneMode.Single);
    }

    private void SetVisible(bool visible)
    {
        if (pauseGroup == null) return;
        pauseGroup.alpha = visible ? 1f : 0f;
        pauseGroup.interactable = visible;
        pauseGroup.blocksRaycasts = visible;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
