using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class MenuSceneManager : MonoBehaviour
{
    public static MenuSceneManager Instance;

    [Serializable]
    public struct ButtonActionPair
    {
        public Button button;
        public UnityEvent action;
    }

    [SerializeField] private ButtonActionPair[] buttonActionPairs;

    [Header("WIP Page")]
    [SerializeField] private RectTransform transformWIPPage;
    [SerializeField] private Button buttonCloseWIPPage;

    [Header("Menu UI Visibility")]
    [SerializeField] private CanvasGroup menuUICanvasGroup;
    [Tooltip("When outer menu is activated, it is hidden.")]
    [SerializeField] private CanvasGroup[] gameplayMenuUICanvasGroup;

    private const float MenuUIHideDuration = 1.5f;
    private bool isMenuUIHidden;
    private int menuUIHiddenFrame = -1;
    private float menuUIInputAllowedAt;
    private Vector3 lastMousePosition;
    private float menuUIAlpha = 1f;
    private bool menuUIInteractable = true;
    private bool menuUIBlocksRaycasts = true;

    private void OnEnable()
    {
        GameplayCursor.SetUIOpen(this, true);
        ReleaseMenuCursor();
    }

    private void Start() => ReleaseMenuCursor();

    private void OnDisable() => GameplayCursor.SetUIOpen(this, false);

    private void OnApplicationFocus(bool focused)
    {
        if (focused) ReleaseMenuCursor();
    }

    private void ReleaseMenuCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameplayDiagnostics.Step("MENU cursor released");
    }

    private void Awake()
    {
        if (GetComponent<ControlsGuideUI>() == null) gameObject.AddComponent<ControlsGuideUI>();
        if (GetComponent<CharacterIntroductionUI>() == null) gameObject.AddComponent<CharacterIntroductionUI>();
        if (GetComponent<MenuControllerNavigation>() == null)
        {
            gameObject.AddComponent<MenuControllerNavigation>();
        }
        if(Instance == null)
        {
            Instance = this;
        }

        if (menuUICanvasGroup == null)
        {
            menuUICanvasGroup = GetComponent<CanvasGroup>();
        }

        HideWIPPage();

        if (buttonCloseWIPPage != null)
        {
            buttonCloseWIPPage.onClick.AddListener(HideWIPPage);
        }

        if (buttonActionPairs == null)
        {
            return;
        }

        foreach (ButtonActionPair pair in buttonActionPairs)
        {
            if (pair.button == null)
            {
                Debug.LogWarning("MenuSceneManager에 버튼이 할당되지 않은 메뉴 쌍이 있습니다.", this);
                continue;
            }

            // 로컬 복사본을 캡처해 각 버튼이 자신의 쌍을 실행하도록 한다.
            ButtonActionPair menuPair = pair;
            if (pair.button.name == "Button_Menu_Loadout") RenameButton(pair.button, "PA관리");
            if (pair.button.name == "Button_Menu_PAManagement") RenameButton(pair.button, "조작키 설명");
            pair.button.onClick.AddListener(() => Execute(menuPair));
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!isMenuUIHidden)
        {
            return;
        }

        // 숨기기 버튼을 누른 같은 프레임의 입력으로 즉시 다시 표시되지 않도록 한다.
        if (Time.frameCount <= menuUIHiddenFrame)
        {
            lastMousePosition = Input.mousePosition;
            return;
        }

        // 숨긴 직후 마우스의 미세한 움직임이나 키 입력은 복원 입력으로 사용하지 않는다.
        if (Time.unscaledTime < menuUIInputAllowedAt)
        {
            lastMousePosition = Input.mousePosition;
            return;
        }

        bool mouseMoved = Input.mousePosition != lastMousePosition;
        lastMousePosition = Input.mousePosition;

        if (mouseMoved || Input.anyKeyDown || MenuControllerNavigation.HasControllerInput())
        {
            ShowMenuUI();
        }
    }

    /// <summary>
    /// 메뉴 UI 전체를 숨기고, 마우스 이동 또는 키 입력이 감지되면 다시 표시한다.
    /// UI 숨기기 버튼의 OnClick 이벤트에 연결해서 사용한다.
    /// </summary>
    public void HideMenuUIUntilInput()
    {
        if (menuUICanvasGroup == null)
        {
            Debug.LogWarning("MenuSceneManager에 메뉴 UI CanvasGroup이 할당되지 않았습니다.", this);
            return;
        }

        if (!isMenuUIHidden)
        {
            menuUIAlpha = menuUICanvasGroup.alpha;
            menuUIInteractable = menuUICanvasGroup.interactable;
            menuUIBlocksRaycasts = menuUICanvasGroup.blocksRaycasts;
        }

        isMenuUIHidden = true;
        menuUIHiddenFrame = Time.frameCount;
        menuUIInputAllowedAt = Time.unscaledTime + MenuUIHideDuration;
        lastMousePosition = Input.mousePosition;

        menuUICanvasGroup.alpha = 0f;
        menuUICanvasGroup.interactable = false;
        menuUICanvasGroup.blocksRaycasts = false;
    }

    private void ShowMenuUI()
    {
        isMenuUIHidden = false;

        if (menuUICanvasGroup == null)
        {
            return;
        }

        menuUICanvasGroup.alpha = menuUIAlpha;
        menuUICanvasGroup.interactable = menuUIInteractable;
        menuUICanvasGroup.blocksRaycasts = menuUIBlocksRaycasts;
    }

    private void Execute(ButtonActionPair pair)
    {
        GameplayDiagnostics.Step("MENU BUTTON dispatched: " + (pair.button ? pair.button.name : "none"));
        if (pair.button != null && pair.button.name == "Button_Menu_Loadout")
        {
            GetComponent<CharacterIntroductionUI>().Open();
            return;
        }
        if (pair.button != null && pair.button.name == "Button_Menu_PAManagement")
        {
            GetComponent<ControlsGuideUI>().Open();
            return;
        }
        if (!HasAssignedAction(pair.action))
        {
            ShowWIPPage();
            return;
        }

        pair.action.Invoke();
    }

    private static void RenameButton(Button button, string label)
    {
        foreach (var text in button.GetComponentsInChildren<Text>(true)) text.text = label;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.text = label;
    }

    private static bool HasAssignedAction(UnityEvent action)
    {
        if (action == null || action.GetPersistentEventCount() == 0)
        {
            return false;
        }

        // UnityEvent는 이벤트 자체가 존재해도 대상 또는 메서드가 비어 있을 수 있다.
        for (int i = 0; i < action.GetPersistentEventCount(); i++)
        {
            if (action.GetPersistentTarget(i) == null ||
                string.IsNullOrEmpty(action.GetPersistentMethodName(i)))
            {
                return false;
            }
        }

        return true;
    }

    public void ShowWIPPage()
    {
        if (transformWIPPage != null)
        {
            transformWIPPage.gameObject.SetActive(true);
            MenuControllerNavigation.SetPanel(transformWIPPage, true, HideWIPPage);
        }
    }

    private void HideWIPPage()
    {
        if (transformWIPPage != null)
        {
            transformWIPPage.gameObject.SetActive(false);
            MenuControllerNavigation.SetPanel(transformWIPPage, false);
        }
    }

    // Show/Hide Ingame UI
    public void SetIngameMenuLayerVisible(bool visible)
    {
        if (gameplayMenuUICanvasGroup == null)
        {
            return;
        }

        foreach(CanvasGroup c in gameplayMenuUICanvasGroup)
        {
            c.alpha = visible ? 1f : 0f;
            c.interactable = visible;
            c.blocksRaycasts = visible;
        }
    }
}
