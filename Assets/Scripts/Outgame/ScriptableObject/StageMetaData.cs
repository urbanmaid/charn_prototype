using System;
using UnityEngine;

[Serializable]
public struct StageMissionObjectSpawnData
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 worldPosition;
    [SerializeField] private Vector3 worldEulerAngles;

    public GameObject Prefab => prefab;
    public Vector3 WorldPosition => worldPosition;
    public Vector3 WorldEulerAngles => worldEulerAngles;
}

[CreateAssetMenu(fileName = "StageMetaData", menuName = "Outgame/Stage Meta Data")]
public class StageMetaData : ScriptableObject
{
    [Header("Stage Background")]
    [SerializeField] private GameObject stageBackgroundPrefab;

    [Header("Stage Information")]
    [SerializeField] private string stageId;
    [SerializeField] private Sprite thumbnail;
    [SerializeField] private int storyIndex;
    [SerializeField] private string stageTitle;
    [SerializeField, TextArea(3, 6)] private string stageDescription;
    [SerializeField] private int missionList = 1;

    [Header("Mission Configuration")]
    [Tooltip("미션 구성에 따라 인게임에서 함께 생성할 개체와 월드 배치 정보입니다.")]
    [SerializeField] private StageMissionObjectSpawnData[] missionObjectSpawns;

    public GameObject StageBackgroundPrefab => stageBackgroundPrefab;
    // 비어 있으면 에셋 이름을 사용해 기존 StageMetaData도 별도 설정 없이 식별할 수 있다.
    public string StageId => string.IsNullOrEmpty(stageId) ? name : stageId;
    public Sprite Thumbnail => thumbnail;
    public int StoryIndex => storyIndex;
    public string StageTitle => stageTitle;
    public string StageDescription => stageDescription;

    public int MissionList => missionList;
    public StageMissionObjectSpawnData[] MissionObjectSpawns => missionObjectSpawns;

    // Validate authored entry points before offering a mission or leaving the menu.
    // Mission roots take precedence, matching StageSceneInitializer.
    public bool CanEnterGameplay()
    {
        if (stageBackgroundPrefab == null) return false;
        int count = 0;
        if (missionObjectSpawns != null)
        {
            foreach (var spawn in missionObjectSpawns)
            {
                if (spawn.Prefab == null) return false;
                count += spawn.Prefab.GetComponentsInChildren<PlayerStartpoint>(true).Length;
            }
        }
        return count > 0 ? count == 1
            : stageBackgroundPrefab.GetComponentsInChildren<PlayerStartpoint>(true).Length == 1;
    }

}
