using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SaveDataDisplay : MonoBehaviour
{
    private const float AuxMenuHideDelay = 1f;

    public static SaveDataDisplay Instance { get; private set; }

    [Header("Save Data Display")]
    [SerializeField] private TextMeshProUGUI saveDataNameText;
    [SerializeField] private TextMeshProUGUI auText;

    [Header("Auxiliary Menu")]
    [SerializeField] private Button openAuxMenuButton;
    [SerializeField] private CanvasGroup auxMenuGroup;

    private RectTransform auxMenuRectTransform;
    private float hideAuxMenuAt = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        auxMenuRectTransform = auxMenuGroup == null ? null : auxMenuGroup.transform as RectTransform;

        if (openAuxMenuButton == null)
        {
            openAuxMenuButton = GetComponent<Button>();
        }

        if (openAuxMenuButton != null)
        {
            openAuxMenuButton.onClick.AddListener(ShowAuxMenu);
        }

        SetAuxMenuVisible(false);
    }

    private void Start()
    {
        // OnEnable이 SaveDataManager의 Awake보다 먼저 실행된 경우를 보완한다.
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CurrentSaveDataChanged -= SetSaveData;
            SaveDataManager.Instance.CurrentSaveDataChanged += SetSaveData;
        }

        RefreshFromSaveDataManager();
    }

    private void OnEnable()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CurrentSaveDataChanged += SetSaveData;
        }
    }

    private void OnDisable()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.CurrentSaveDataChanged -= SetSaveData;
        }
    }

    private void OnDestroy()
    {
        if (openAuxMenuButton != null)
        {
            openAuxMenuButton.onClick.RemoveListener(ShowAuxMenu);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsAuxMenuVisible())
        {
            return;
        }

        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        bool hasFocus = selected != null && selected.transform.IsChildOf(auxMenuGroup.transform);
        if (hasFocus || IsPointerOverRect(auxMenuRectTransform))
        {
            hideAuxMenuAt = -1f;
            return;
        }

        if (hideAuxMenuAt < 0f)
        {
            hideAuxMenuAt = Time.unscaledTime + AuxMenuHideDelay;
            return;
        }

        if (Time.unscaledTime >= hideAuxMenuAt)
        {
            SetAuxMenuVisible(false);
        }
    }

    /// <summary>
    /// 보조 메뉴를 연다. 버튼의 OnClick에도 직접 연결할 수 있다.
    /// </summary>
    public void ShowAuxMenu()
    {
        hideAuxMenuAt = -1f;
        SetAuxMenuVisible(true);
    }

    /// <summary>
    /// 보조 메뉴를 즉시 닫는다.
    /// 다른 관리 UI가 열릴 때 호출한다.
    /// </summary>
    public void HideAuxMenu()
    {
        SetAuxMenuVisible(false);
    }

    /// <summary>
    /// 사용 중인 세이브데이터의 이름과 AU를 표시한다.
    /// </summary>
    public void SetSaveData(SaveDataManager.SaveData saveData)
    {
        if (saveDataNameText != null)
        {
            saveDataNameText.text = saveData == null ? string.Empty : saveData.saveName;
        }

        if (auText != null)
        {
            auText.text = saveData == null ? "AU: -" : $"AU: {saveData.au:N0}";
        }
    }

    /// <summary>
    /// 현재 SaveDataManager가 보유한 세이브데이터를 다시 표시한다.
    /// </summary>
    public void RefreshFromSaveDataManager()
    {
        SetSaveData(SaveDataManager.Instance == null ? null : SaveDataManager.Instance.Data);
    }

    private bool IsAuxMenuVisible()
    {
        return auxMenuGroup != null && auxMenuGroup.alpha > 0f;
    }

    private void SetAuxMenuVisible(bool visible)
    {
        if (auxMenuGroup == null)
        {
            return;
        }

        auxMenuGroup.alpha = visible ? 1f : 0f;
        auxMenuGroup.interactable = visible;
        auxMenuGroup.blocksRaycasts = visible;
        MenuControllerNavigation.SetPanel(auxMenuGroup.transform, visible, HideAuxMenu);

        if (!visible)
        {
            hideAuxMenuAt = -1f;
        }
    }

    private bool IsPointerOverRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return false;
        }

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            Input.mousePosition,
            eventCamera);
    }
}
