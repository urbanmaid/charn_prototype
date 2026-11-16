using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 씬 진입 시 아웃게임에서 선택한 StageMetaData의 스테이지 프리팹을 생성한다.
/// </summary>
[DisallowMultipleComponent]
public class StageSceneInitializer : MonoBehaviour
{
    [Header("Fallback")]
    [Tooltip("아웃게임에서 선택된 스테이지가 없을 때 사용할 스테이지입니다.")]
    [SerializeField] private StageMetaData fallbackStageMetaData;

    [Header("Spawn")]
    [Tooltip("비어 있으면 씬의 루트에 생성합니다. 부모가 있어도 스테이지는 월드 원점에 배치됩니다.")]
    [SerializeField] private Transform stageRoot;
    [Tooltip("비어 있으면 씬의 루트에 생성합니다. 미션 개체는 StageMetaData에 지정된 월드 좌표에 배치됩니다.")]
    [SerializeField] private Transform missionObjectRoot;
    [SerializeField] private bool initializeOnAwake = true;

    public StageMetaData ActiveStageMetaData { get; private set; }
    public GameObject SpawnedStage { get; private set; }
    public IReadOnlyList<GameObject> SpawnedMissionObjects => spawnedMissionObjects;

    public event Action<StageMetaData, GameObject> StageInitialized;
    public bool IsStageInitialized { get; private set; }
    // Covers depend on geometry lifetime, including explicit navigation rebuilds.
    public event Action CoverGeometryChanged;

    public void NotifyCoverGeometryChanged() => CoverGeometryChanged?.Invoke();

    private readonly List<GameObject> spawnedMissionObjects = new List<GameObject>();

    private void Awake()
    {
        CommandManager.EnsureActiveManager();
        if (initializeOnAwake)
        {
            InitializeSelectedStage();
        }
    }

    /// <summary>
    /// 아웃게임에서 마지막으로 선택한 스테이지를 생성한다.
    /// 선택 정보가 없으면 인스펙터의 fallbackStageMetaData를 사용한다.
    /// </summary>
    public bool InitializeSelectedStage()
    {
        StageMetaData stageMetaData = StageSelectionManager.SelectedStageMetaData != null
            ? StageSelectionManager.SelectedStageMetaData
            : fallbackStageMetaData;
        return InitializeStage(stageMetaData);
    }

    /// <summary>
    /// 지정한 스테이지 메타데이터의 프리팹을 월드 좌표 원점에 생성한다.
    /// </summary>
    public bool InitializeStage(StageMetaData stageMetaData)
    {
        GameplayDiagnostics.Step("STAGE INITIALIZE BEGIN " + (stageMetaData ? stageMetaData.name : "none"));
        if (stageMetaData == null)
        {
            Debug.LogWarning("생성할 StageMetaData가 없습니다. 선택 스테이지 또는 fallbackStageMetaData를 확인해 주세요.", this);
            return false;
        }

        GameObject stagePrefab = stageMetaData.StageBackgroundPrefab;
        if (stagePrefab == null)
        {
            Debug.LogWarning($"'{stageMetaData.name}'에 스테이지 프리팹이 할당되지 않았습니다.", stageMetaData);
            return false;
        }

        ClearSpawnedStage();

        GameplayDiagnostics.Step("MAP INSTANTIATE BEGIN");
        SpawnedStage = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, stageRoot);
        SpawnedStage.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ActiveStageMetaData = stageMetaData;
        if (stageMetaData.StageId == "vandalism_block")
            VandalismConnectedBridge.RepairPierCollision(SpawnedStage.transform);

        GameplayDiagnostics.Step("MAP INSTANTIATE END; MISSIONS BEGIN");
        SpawnMissionObjects(stageMetaData);
        GameplayDiagnostics.Step("MISSIONS END; STARTPOINT RESOLVE BEGIN");

        // [기존 코드 보관] 배경에 지점이 없으면 씬 전체에서 임의의 지점을 선택했다.
        // PlayerStartpoint playerStartpoint = SpawnedStage.GetComponentInChildren<PlayerStartpoint>(true);
        // if (playerStartpoint == null)
        // {
        //     playerStartpoint = FindAnyObjectByType<PlayerStartpoint>(FindObjectsInactive.Include);
        // }
        // playerStartpoint?.InitializePlayersAndAllies();

        // [수정 의도] 시작 지점을 미션 개체에 두는 설계에 맞춰 이번에 생성한 개체만 검사한다.
        // 지점 누락·중복을 초기화 성공으로 처리하지 않고, 완료 이벤트도 발생시키지 않는다.
        if (!TryResolvePlayerStartpoint(out PlayerStartpoint playerStartpoint))
        {
            Debug.LogError($"'{stageMetaData.name}'의 시작 지점을 하나로 결정할 수 없습니다. 생성된 미션 개체와 배경의 PlayerStartpoint를 확인해 주세요.", this);
            return false;
        }

        // 위 검사에서 단일 지점이 확인된 경우에만 플레이어와 아군을 합류 위치로 이동한다.
        GameplayDiagnostics.Step("PLAYERS INITIALIZE BEGIN");
        playerStartpoint.InitializePlayersAndAllies();
        GameplayDiagnostics.Step("PLAYERS INITIALIZE END");
        IsStageInitialized = true;
        if (stageMetaData.StageId == "vandalism_block")
        {
            Charm.Mechanism.Missions.StageMissionDirector director = null;
            foreach (var root in spawnedMissionObjects)
                if (root != null && root.TryGetComponent(out Charm.Mechanism.Missions.StageMissionDirector found)) { director = found; break; }
            // Authored missions own enemy placement. Keep legacy maps on their existing procedural path.
            if (director == null)
            {
                var patrols = SpawnedStage.AddComponent<VandalismRoutePatrols>();
                patrols.Configure(stageMetaData, playerStartpoint.transform.position, spawnedMissionObjects);
            }
            // Share the tested presentation with the riverside approach encounter.
            var airstrike = SpawnedStage.GetComponent<VandalismBridgeAirstrike>();
            if (!airstrike) airstrike = SpawnedStage.AddComponent<VandalismBridgeAirstrike>();
            if (director != null && director.OpeningDestination != null)
                airstrike.ConfigureMission(playerStartpoint.transform.position, director.OpeningDestination.position,
                    director.GetComponent<Charm.Mechanism.Missions.StageMissionController>(), director.OpeningMissionId);
            else airstrike.Configure(playerStartpoint.transform.position, spawnedMissionObjects);
        }
        if (stageMetaData.StageId == "vandalism_block" && !FindAnyObjectByType<BackgroundSound>())
        {
            var music = new GameObject("Vandalism background music");
            music.AddComponent<AudioSource>();
            music.AddComponent<BackgroundSound>();
        }
        NotifyCoverGeometryChanged();
        GameplayDiagnostics.Step("STAGE READY EVENT BEGIN");
        StageInitialized?.Invoke(ActiveStageMetaData, SpawnedStage);
        GameplayDiagnostics.Step("STAGE READY EVENT END");
        return true;
    }

    // [추가 의도] 미션 지점을 우선하되, 미션에 지점이 없는 기존 맵은 배경 지점으로 호환한다.
    // 전역 탐색을 사용하지 않아 다른 씬이나 이전 맵의 지점을 잘못 선택하지 않는다.
    private bool TryResolvePlayerStartpoint(out PlayerStartpoint startpoint)
    {
        startpoint = null;
        int count = 0;
        foreach (GameObject missionObject in spawnedMissionObjects)
        {
            if (missionObject == null) continue;
            // 표시용 오브젝트가 비활성이어도 시작 위치를 정의하는 마커는 검사에 포함한다.
            foreach (PlayerStartpoint candidate in missionObject.GetComponentsInChildren<PlayerStartpoint>(true))
            {
                startpoint = candidate;
                count++;
            }
        }

        // 미션에 여러 지점이 있으면 배경으로 우회하지 않고 설정 중복으로 실패 처리한다.
        if (count > 0) return count == 1;

        // 시작 지점을 배경에 두었던 기존 스테이지와의 호환 경로.
        PlayerStartpoint[] backgroundPoints = SpawnedStage.GetComponentsInChildren<PlayerStartpoint>(true);
        // 배경 역시 정확히 하나일 때만 사용해 누락이나 모호한 선택을 방지한다.
        if (backgroundPoints.Length != 1) return false;
        startpoint = backgroundPoints[0];
        return true;
    }

    private void SpawnMissionObjects(StageMetaData stageMetaData)
    {
        StageMissionObjectSpawnData[] missionObjectSpawns = stageMetaData.MissionObjectSpawns;
        if (missionObjectSpawns == null)
        {
            return;
        }

        for (int i = 0; i < missionObjectSpawns.Length; i++)
        {
            StageMissionObjectSpawnData spawnData = missionObjectSpawns[i];
            if (spawnData.Prefab == null)
            {
                Debug.LogWarning($"'{stageMetaData.name}'의 미션 개체 #{i + 1}에 프리팹이 할당되지 않았습니다.", stageMetaData);
                continue;
            }

            GameObject missionObject = Instantiate(
                spawnData.Prefab,
                spawnData.WorldPosition,
                Quaternion.Euler(spawnData.WorldEulerAngles),
                missionObjectRoot);
            spawnedMissionObjects.Add(missionObject);
        }
    }

    /// <summary>
    /// 이 초기화 컴포넌트가 생성한 스테이지 프리팹을 제거한다.
    /// </summary>
    public void ClearSpawnedStage()
    {
        IsStageInitialized = false;
        NotifyCoverGeometryChanged();
        for (int i = spawnedMissionObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedMissionObjects[i] != null)
            {
                spawnedMissionObjects[i].SetActive(false);
                Destroy(spawnedMissionObjects[i]);
            }
        }

        spawnedMissionObjects.Clear();

        if (SpawnedStage != null)
        {
            SpawnedStage.SetActive(false);
            Destroy(SpawnedStage);
        }

        SpawnedStage = null;
        ActiveStageMetaData = null;
    }
}
