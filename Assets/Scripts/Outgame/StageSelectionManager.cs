using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectionManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup stageSelectionGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button startStageButton;
    [SerializeField] private StageSceneTransitionManager stageSceneTransitionManager;
    public static StageSelectionManager Instance { get; private set; }
    public static StageMetaData SelectedStageMetaData { get; private set; }

    [Header("Stage List")]
    [SerializeField] private StageSelectionUnitButton stageUnitPrefab;
    [SerializeField] private Transform stageListRoot;
    [SerializeField] private bool usingStageIndexOrder;

    [Header("Stage Details")]
    [SerializeField] private StageDetailedInfoDisplayUnit detailedInfoDisplayUnit;
    [SerializeField] private string[] rankTitles =
    {
        "Achieved",
        "Stunning",
        "Perfect",
        "Outstanding"
    };

    [Header("Save Data")]
    [SerializeField] private SaveDataManager saveDataManager;

    private readonly List<StageSelectionUnitButton> stageUnits = new List<StageSelectionUnitButton>();
    private StageSelectionUnitButton focusedStageUnit;

    public StageMetaData FocusedStage => focusedStageUnit == null ? null : focusedStageUnit.StageMetaData;
    public bool UsingStageIndexOrder
    {
        get => usingStageIndexOrder;
        set => usingStageIndexOrder = value;
    }

    public string[] RankTitles => rankTitles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (stageListRoot != null && stageListRoot.GetComponent<LayoutGroup>() == null)
        {
            Debug.LogWarning("스테이지 목록 오브젝트에 LayoutGroup을 추가해 주세요.", stageListRoot);
        }

        BindActionButtons();
        RefreshStageList();
    }

    private void BindActionButtons()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HideLayer);
        if (startStageButton != null)
            startStageButton.onClick.AddListener(StartFocusedStage);
    }


    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideLayer);
        }

        if (startStageButton != null)
        {
            startStageButton.onClick.RemoveListener(StartFocusedStage);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void HideLayer()
    {
        SetLayerVisible(false);
    }

    /// <summary>
    /// 현재 포커스된 스테이지를 게임플레이 전용 씬에서 시작한다.
    /// </summary>
    public void StartFocusedStage()
    {
        GameplayDiagnostics.Step("START BUTTON clicked; focused=" + (FocusedStage == null ? "none" : FocusedStage.name));
        if (FocusedStage == null)
        {
            Debug.LogWarning("시작할 스테이지를 먼저 선택해 주세요.", this);
            return;
        }

        if (stageSceneTransitionManager == null)
        {
            Debug.LogWarning("StageSelectionManager에 StageSceneTransitionManager가 할당되지 않았습니다.", this);
            return;
        }

        stageSceneTransitionManager.LoadSelectedStageScene();
    }

    public void SetLayerVisible(bool visible)
    {
        if (stageSelectionGroup == null)
        {
            return;
        }

        stageSelectionGroup.alpha = visible ? 1f : 0f;
        stageSelectionGroup.interactable = visible;
        stageSelectionGroup.blocksRaycasts = visible;
        MenuControllerNavigation.SetPanel(stageSelectionGroup.transform, visible, HideLayer);

        if (visible && SaveDataDisplay.Instance != null)
        {
            SaveDataDisplay.Instance.HideAuxMenu();
        }

        if (visible)
        {
            RefreshStageRankDisplays();
        }

        if(MenuSceneManager.Instance)
        {
            MenuSceneManager.Instance.SetIngameMenuLayerVisible(!visible);
        }
    }

    /// <summary>
    /// Resources 내부의 모든 StageMetaData를 읽어 스테이지 선택 목록을 다시 구성한다.
    /// </summary>
    public void RefreshStageList()
    {
        ClearStageList();

        if (stageUnitPrefab == null || stageListRoot == null)
        {
            return;
        }

        StageMetaData[] stages = Resources.LoadAll<StageMetaData>(string.Empty);
        Array.Sort(stages, usingStageIndexOrder ? CompareStagesByStoryIndex : CompareStages);

        foreach (StageMetaData stage in stages)
        {
            StageSelectionUnitButton unit = Instantiate(stageUnitPrefab, stageListRoot);
            unit.Setup(stage);
            stageUnits.Add(unit);
        }

        RefreshStageRankDisplays();
    }

    public void FocusStage(StageSelectionUnitButton unit)
    {
        if (unit == null || unit.StageMetaData == null)
        {
            return;
        }

        if (focusedStageUnit != null)
        {
            focusedStageUnit.SetFocused(false);
        }

        focusedStageUnit = unit;
        focusedStageUnit.SetFocused(true);
        SelectedStageMetaData = unit.StageMetaData;
        detailedInfoDisplayUnit?.Display(unit.StageMetaData);
        SaveDataManager activeSaveDataManager = saveDataManager != null
            ? saveDataManager
            : SaveDataManager.Instance;
        detailedInfoDisplayUnit?.DisplayClearInfo(activeSaveDataManager == null
            ? null
            : activeSaveDataManager.GetStageClearInfo(unit.StageMetaData), rankTitles);
    }

    private void ClearStageList()
    {
        focusedStageUnit = null;

        for (int i = stageUnits.Count - 1; i >= 0; i--)
        {
            if (stageUnits[i] != null)
            {
                Destroy(stageUnits[i].gameObject);
            }
        }

        stageUnits.Clear();
        detailedInfoDisplayUnit?.Display(null);
        detailedInfoDisplayUnit?.DisplayClearInfo(null, rankTitles);
    }

    private void RefreshStageRankDisplays()
    {
        SaveDataManager activeSaveDataManager = saveDataManager != null
            ? saveDataManager
            : SaveDataManager.Instance;

        foreach (StageSelectionUnitButton stageUnit in stageUnits)
        {
            if (stageUnit == null)
            {
                continue;
            }

            StageClearInfo clearInfo = activeSaveDataManager == null
                ? null
                : activeSaveDataManager.GetStageClearInfo(stageUnit.StageMetaData);
            stageUnit.DisplayClearInfo(clearInfo, rankTitles);
        }
    }

    private static int CompareStages(StageMetaData first, StageMetaData second)
    {
        string firstTitle = string.IsNullOrEmpty(first.StageTitle) ? first.name : first.StageTitle;
        string secondTitle = string.IsNullOrEmpty(second.StageTitle) ? second.name : second.StageTitle;
        return string.Compare(firstTitle, secondTitle, StringComparison.OrdinalIgnoreCase);
    }

    private void OnValidate()
    {
        if (rankTitles == null || rankTitles.Length != 4)
        {
            Array.Resize(ref rankTitles, 4);
        }
    }

    private static int CompareStagesByStoryIndex(StageMetaData first, StageMetaData second)
    {
        int indexComparison = first.StoryIndex.CompareTo(second.StoryIndex);
        return indexComparison != 0 ? indexComparison : CompareStages(first, second);
    }
}
