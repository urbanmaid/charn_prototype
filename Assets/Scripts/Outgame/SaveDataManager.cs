using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class SaveDataManager : MonoBehaviour
{
    public bool IsManagementVisible => savedataManagementGroup != null && savedataManagementGroup.alpha > 0f;
    [SerializeField] private CanvasGroup savedataManagementGroup;

    [Serializable]
    public class CurrentLevelData
    {
        public int levelIndex = -1;
        public string levelId = string.Empty;
    }

    [Serializable]
    public class SaveData
    {
        public string saveName = string.Empty;
        public string saveId = string.Empty;
        //public string userName = string.Empty;
        public double totalApplicationSeconds;
        public int au;
        public int userLevel = 1;
        public int userExp;
        public string lastPlayedDate = string.Empty;
        public List<StageClearInfo> stageClearInfos = new List<StageClearInfo>();
        public CurrentLevelData currentLevel = new CurrentLevelData();
        public List<Charm.Mechanism.Missions.StageSaveData> stageProgress = new List<Charm.Mechanism.Missions.StageSaveData>();
    }

    [Serializable]
    private class EnvironmentOptions
    {
        public string lastLoadedSaveFileName = string.Empty;
    }

    public static SaveDataManager Instance { get; private set; }

    [Header("Save Data List")]
    [SerializeField] private SaveDataUnitButton saveDataUnitPrefab;
    [SerializeField] private Transform saveDataListRoot;

    [Header("Save Data Actions")]
    [SerializeField] private Button deleteFocusedSaveDataButton;
    [SerializeField] private Button saveCurrentDataButton;
    [SerializeField] private Button loadFocusedSaveDataButton;
    [SerializeField] private Button closeSavedataManagementButton;

    [Header("Current Data")]
    [SerializeField] private SaveData data = new SaveData();

    private const string SaveDirectoryName = "SaveData";
    private const string SaveFilePrefix = "Savedata_";
    private const string EnvironmentOptionsFileName = "_EnvironmentOptions.json";
    private const int SaveIdLength = 8;

    private readonly List<SaveDataUnitButton> saveDataUnits = new List<SaveDataUnitButton>();
    private SaveDataUnitButton focusedSaveDataUnit;
    private EnvironmentOptions environmentOptions = new EnvironmentOptions();

    public SaveData Data => data;
    public SaveDataUnitButton FocusedSaveDataUnit => focusedSaveDataUnit;
    public event Action<SaveData> CurrentSaveDataChanged;

    /// <summary>
    /// 해당 스테이지의 저장된 클리어 정보이다. 데이터가 없으면 아직 플레이하지 않은 상태다.
    /// </summary>
    public StageClearInfo GetStageClearInfo(StageMetaData stageMetaData)
    {
        if (data == null || stageMetaData == null || data.stageClearInfos == null)
        {
            return null;
        }

        return data.stageClearInfos.Find(clearInfo =>
            clearInfo != null && clearInfo.stageId == stageMetaData.StageId);
    }

    /// <summary>
    /// 스테이지 클리어 결과를 현재 세이브 데이터에 기록하거나 갱신한다.
    /// </summary>
    public void SetStageClearInfo(StageMetaData stageMetaData, StageClearInfo clearInfo)
    {
        if (stageMetaData == null || clearInfo == null)
        {
            return;
        }

        if (data == null)
        {
            data = new SaveData();
        }

        if (data.stageClearInfos == null)
        {
            data.stageClearInfos = new List<StageClearInfo>();
        }

        if (clearInfo.rankConditions == null ||
            clearInfo.rankConditions.Length != StageDetailedInfoDisplayUnit.RankConditionCount)
        {
            Array.Resize(ref clearInfo.rankConditions, StageDetailedInfoDisplayUnit.RankConditionCount);
        }

        clearInfo.stageId = stageMetaData.StageId;
        int existingIndex = data.stageClearInfos.FindIndex(savedInfo =>
            savedInfo != null && savedInfo.stageId == clearInfo.stageId);

        if (existingIndex >= 0)
        {
            data.stageClearInfos[existingIndex] = clearInfo;
        }
        else
        {
            data.stageClearInfos.Add(clearInfo);
        }
    }

    private string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveDirectoryName);
    private string EnvironmentOptionsFilePath => Path.Combine(SaveDirectoryPath, EnvironmentOptionsFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        StageSaveSession.Clear();
        Directory.CreateDirectory(SaveDirectoryPath);
        Debug.Log(SaveDirectoryPath);
        LoadEnvironmentOptions();
        LoadLastLoadedSaveData();

        if (saveDataListRoot != null && saveDataListRoot.GetComponent<LayoutGroup>() == null)
        {
            Debug.LogWarning("세이브데이터 목록 오브젝트에 LayoutGroup을 추가해 주세요.", saveDataListRoot);
        }

        BindActionButtons();
        RefreshSaveDataList();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BindActionButtons()
    {
        if (deleteFocusedSaveDataButton != null)
            deleteFocusedSaveDataButton.onClick.AddListener(DeleteFocusedSaveData);
        if (saveCurrentDataButton != null)
            saveCurrentDataButton.onClick.AddListener(SaveCurrentData);
        if (loadFocusedSaveDataButton != null)
            loadFocusedSaveDataButton.onClick.AddListener(LoadFocusedSaveData);
        if (closeSavedataManagementButton != null)
            closeSavedataManagementButton.onClick.AddListener(HideSaveDataManagementLayer);
    }

    /// <summary>
    /// 세이브데이터 관리 레이어의 표시 상태를 반전한다.
    /// 메뉴 버튼의 OnClick 이벤트에 연결해서 사용한다.
    /// </summary>
    public void ToggleSaveDataManagementLayer()
    {
        if (savedataManagementGroup == null)
        {
            Debug.LogWarning("SaveDataManager에 세이브데이터 관리 CanvasGroup이 할당되지 않았습니다.", this);
            return;
        }

        SetSaveDataManagementLayerVisible(savedataManagementGroup.alpha <= 0f);
    }

    public void ShowSaveDataManagementLayer()
    {
        SetSaveDataManagementLayerVisible(true);
    }

    public void HideSaveDataManagementLayer()
    {
        SetSaveDataManagementLayerVisible(false);
    }

    private void SetSaveDataManagementLayerVisible(bool visible)
    {
        if (savedataManagementGroup == null)
        {
            return;
        }

        savedataManagementGroup.alpha = visible ? 1f : 0f;
        savedataManagementGroup.interactable = visible;
        savedataManagementGroup.blocksRaycasts = visible;
        MenuControllerNavigation.SetPanel(savedataManagementGroup.transform, visible, HideSaveDataManagementLayer);

        if (visible && SaveDataDisplay.Instance != null)
        {
            SaveDataDisplay.Instance.HideAuxMenu();
        }

        if(MenuSceneManager.Instance)
        {
            MenuSceneManager.Instance.SetIngameMenuLayerVisible(!visible);
        }
    }

    public void RefreshSaveDataList()
    {
        ClearSaveDataList();

        if (saveDataUnitPrefab == null || saveDataListRoot == null)
        {
            return;
        }

        string[] saveFiles = Directory.GetFiles(SaveDirectoryPath, SaveFilePrefix + "*.json");
        Array.Sort(saveFiles, StringComparer.OrdinalIgnoreCase);

        foreach (string saveFile in saveFiles)
        {
            SaveData savedData = ReadSaveData(saveFile);
            if (savedData == null)
            {
                continue;
            }

            SaveDataUnitButton unit = Instantiate(saveDataUnitPrefab, saveDataListRoot);
            unit.Setup(savedData, saveFile);
            saveDataUnits.Add(unit);
        }
    }

    public void FocusSaveData(SaveDataUnitButton unit)
    {
        if (focusedSaveDataUnit != null)
        {
            focusedSaveDataUnit.SetFocused(false);
        }

        focusedSaveDataUnit = unit;

        if (focusedSaveDataUnit != null)
        {
            focusedSaveDataUnit.SetFocused(true);
        }
    }

    public void DeleteFocusedSaveData()
    {
        if (focusedSaveDataUnit == null)
        {
            return;
        }

        string saveFilePath = focusedSaveDataUnit.SaveFilePath;
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
        }

        if (environmentOptions.lastLoadedSaveFileName == Path.GetFileName(saveFilePath))
        {
            StageSaveSession.Clear();
            environmentOptions.lastLoadedSaveFileName = string.Empty;
            SaveEnvironmentOptions();
        }

        focusedSaveDataUnit = null;
        RefreshSaveDataList();
    }

    public void SaveCurrentData()
    {
        if (data == null)
        {
            data = new SaveData();
        }

        var candidate = new SaveData(); // Start a new game without inheriting the selected slot.
        string saveId = CreateUniqueSaveId();
        candidate.saveId = saveId;
        candidate.saveName = SaveFilePrefix + saveId;
        candidate.lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        string saveFilePath = Path.Combine(SaveDirectoryPath, candidate.saveName + ".json");
        if (!WriteSaveData(candidate, saveFilePath)) return;
        data = candidate;
        SetLastLoadedSaveFile(saveFilePath);
        CurrentSaveDataChanged?.Invoke(data);
        RefreshSaveDataList();

        foreach (SaveDataUnitButton unit in saveDataUnits)
        {
            if (unit.SaveFilePath == saveFilePath)
            {
                FocusSaveData(unit);
                break;
            }
        }
    }

    public void LoadFocusedSaveData()
    {
        if (focusedSaveDataUnit == null)
        {
            return;
        }

        SaveData loadedData = ReadSaveData(focusedSaveDataUnit.SaveFilePath);
        if (loadedData != null)
        {
            data = loadedData;
            SetLastLoadedSaveFile(focusedSaveDataUnit.SaveFilePath);
            CurrentSaveDataChanged?.Invoke(data);
            HideSaveDataManagementLayer();
        }
    }

    public void Save()
    {
        SaveCurrentData();
    }

    public void Load()
    {
        LoadFocusedSaveData();
    }

    /// <summary>
    /// 환경 옵션에 기록된 마지막 세이브 파일을 불러온다.
    /// 파일이 없거나 손상된 경우 현재 기본 데이터를 유지한다.
    /// </summary>
    public bool LoadLastLoadedSaveData()
    {
        if (environmentOptions == null ||
            string.IsNullOrEmpty(environmentOptions.lastLoadedSaveFileName))
        {
            return false;
        }

        string fileName = Path.GetFileName(environmentOptions.lastLoadedSaveFileName);
        string saveFilePath = Path.Combine(SaveDirectoryPath, fileName);
        if (!File.Exists(saveFilePath))
        {
            environmentOptions.lastLoadedSaveFileName = string.Empty;
            SaveEnvironmentOptions();
            return false;
        }

        SaveData loadedData = ReadSaveData(saveFilePath);
        if (loadedData == null)
        {
            environmentOptions.lastLoadedSaveFileName = string.Empty;
            SaveEnvironmentOptions();
            return false;
        }

        data = loadedData;
        if (!string.IsNullOrWhiteSpace(data.saveId)) StageSaveSession.Bind(saveFilePath, data.saveId);
        else StageSaveSession.Clear();
        CurrentSaveDataChanged?.Invoke(data);
        return true;
    }

    private string CreateUniqueSaveId()
    {
        string saveId;
        do
        {
            saveId = Guid.NewGuid().ToString("N").Substring(0, SaveIdLength).ToUpperInvariant();
        }
        while (File.Exists(Path.Combine(SaveDirectoryPath, SaveFilePrefix + saveId + ".json")));

        return saveId;
    }

    private SaveData ReadSaveData(string saveFilePath)
    {
        try
        {
            SaveData loadedData = JsonUtility.FromJson<SaveData>(File.ReadAllText(saveFilePath));
            if (loadedData == null)
            {
                return null;
            }

            if (loadedData.currentLevel == null)
            {
                loadedData.currentLevel = new CurrentLevelData();
            }

            if (loadedData.stageClearInfos == null)
            {
                loadedData.stageClearInfos = new List<StageClearInfo>();
            }
            if (loadedData.stageProgress == null)
                loadedData.stageProgress = new List<Charm.Mechanism.Missions.StageSaveData>();

            if (string.IsNullOrEmpty(loadedData.saveName))
            {
                loadedData.saveName = Path.GetFileNameWithoutExtension(saveFilePath);
            }

            return loadedData;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"세이브 데이터를 읽지 못했습니다: {saveFilePath}\n{exception.Message}", this);
            return null;
        }
    }

    public bool TryUpdateCurrentData()
    {
        return TryCommitCurrentData(data);
    }

    public bool TryBindSelectedSlot()
    {
        if (data == null || string.IsNullOrWhiteSpace(data.saveId) || environmentOptions == null
            || string.IsNullOrWhiteSpace(environmentOptions.lastLoadedSaveFileName)) return false;
        string path = Path.Combine(SaveDirectoryPath, Path.GetFileName(environmentOptions.lastLoadedSaveFileName));
        var disk = File.Exists(path) ? ReadSaveData(path) : null;
        if (disk == null || disk.saveId != data.saveId) return false;
        StageSaveSession.Bind(path, data.saveId);
        return true;
    }

    public Charm.Mechanism.Missions.StageSaveData ReadStageProgress(string stageId)
    {
        var stage = data?.stageProgress?.Find(value => value != null && value.stageId == stageId);
        return stage == null ? null : JsonUtility.FromJson<Charm.Mechanism.Missions.StageSaveData>(JsonUtility.ToJson(stage));
    }

    public bool TrySaveStageProgress(Charm.Mechanism.Missions.StageSaveData stage)
    {
        if (stage == null || string.IsNullOrWhiteSpace(stage.stageId) || stage.schemaVersion != 1
            || double.IsNaN(stage.actualPlaySeconds) || double.IsInfinity(stage.actualPlaySeconds)
            || stage.actualPlaySeconds < 0 || data == null) return false;
        var candidate = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        if (candidate.stageProgress == null)
            candidate.stageProgress = new List<Charm.Mechanism.Missions.StageSaveData>();
        int index = candidate.stageProgress.FindIndex(value => value != null && value.stageId == stage.stageId);
        if (index >= 0 && stage.actualPlaySeconds < candidate.stageProgress[index].actualPlaySeconds) return false;
        var copy = JsonUtility.FromJson<Charm.Mechanism.Missions.StageSaveData>(JsonUtility.ToJson(stage));
        if (index >= 0) candidate.stageProgress[index] = copy;
        else candidate.stageProgress.Add(copy);
        return TryCommitCurrentData(candidate);
    }

    private bool TryCommitCurrentData(SaveData proposed)
    {
        if (data == null || string.IsNullOrEmpty(data.saveId) || environmentOptions == null
            || string.IsNullOrEmpty(environmentOptions.lastLoadedSaveFileName)) return false;
        string fileName = Path.GetFileName(environmentOptions.lastLoadedSaveFileName);
        string path = Path.Combine(SaveDirectoryPath, fileName);
        var existing = File.Exists(path) ? ReadSaveData(path) : null;
        if (existing == null || existing.saveId != data.saveId) return false;
        if (proposed == null || proposed.saveId != data.saveId) return false;
        var candidate = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(proposed));
        candidate.lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (!WriteSaveData(candidate, path)) return false;
        data = candidate;
        CurrentSaveDataChanged?.Invoke(data);
        return true;
    }

    private bool WriteSaveData(SaveData saveData, string saveFilePath)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectoryPath);
            StageSaveSession.MergeApplicationTime(saveData);
            AtomicSaveFile.Write(saveFilePath, JsonUtility.ToJson(saveData, true));
            StageSaveSession.ApplicationTimeSaved(saveData);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"세이브 데이터를 저장하지 못했습니다: {exception.Message}", this);
            return false;
        }
    }

    private void LoadEnvironmentOptions()
    {
        if (!File.Exists(EnvironmentOptionsFilePath))
        {
            environmentOptions = new EnvironmentOptions();
            return;
        }

        try
        {
            environmentOptions = JsonUtility.FromJson<EnvironmentOptions>(
                File.ReadAllText(EnvironmentOptionsFilePath)) ?? new EnvironmentOptions();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"환경 옵션을 불러오지 못했습니다: {exception.Message}", this);
            environmentOptions = new EnvironmentOptions();
        }
    }

    private void SetLastLoadedSaveFile(string saveFilePath)
    {
        if (environmentOptions == null)
        {
            environmentOptions = new EnvironmentOptions();
        }

        environmentOptions.lastLoadedSaveFileName = Path.GetFileName(saveFilePath);
        if (!string.IsNullOrWhiteSpace(data.saveId)) StageSaveSession.Bind(saveFilePath, data.saveId);
        else StageSaveSession.Clear();
        SaveEnvironmentOptions();
    }

    private void SaveEnvironmentOptions()
    {
        try
        {
            Directory.CreateDirectory(SaveDirectoryPath);
            File.WriteAllText(
                EnvironmentOptionsFilePath,
                JsonUtility.ToJson(environmentOptions ?? new EnvironmentOptions(), true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"환경 옵션을 저장하지 못했습니다: {exception.Message}", this);
        }
    }

    private void ClearSaveDataList()
    {
        focusedSaveDataUnit = null;

        for (int i = saveDataUnits.Count - 1; i >= 0; i--)
        {
            if (saveDataUnits[i] != null)
            {
                Destroy(saveDataUnits[i].gameObject);
            }
        }

        saveDataUnits.Clear();
    }
}
