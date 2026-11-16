using TMPro;
using UnityEngine;

/// <summary>Displays the current mission independently of pause/menu visibility.</summary>
[DisallowMultipleComponent]
public sealed class MissionInfoDisplay : MonoBehaviour
{
    [Header("TextFieldCapsule")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Mission Source")]
    [Tooltip("비어 있으면 같은 씬의 StageSceneInitializer를 찾습니다.")]
    [SerializeField] private StageSceneInitializer stageInitializer;

    private StageMetaData displayedStage;

    private void OnEnable() => Refresh();

    // Another object's Awake may initialize the mission after this object's OnEnable.
    private void Start() => Refresh();

    private void LateUpdate()
    {
        // CanvasGroup visibility does not trigger OnEnable. Keep the capsule current even
        // while hidden or timeScale is zero, including a mission being replaced/cleared.
        StageMetaData current = GetCurrentMission();
        if (current != displayedStage) Display(current);
    }

    public void Refresh()
    {
        if (stageInitializer == null)
        {
            foreach (StageSceneInitializer candidate in FindObjectsByType<StageSceneInitializer>(FindObjectsInactive.Exclude))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                stageInitializer = candidate;
                break;
            }
        }
        Display(GetCurrentMission());
    }

    private StageMetaData GetCurrentMission()
    {
        // The initializer also covers direct gameplay entry using its configured fallback.
        // If it exists but has no active mission, don't show a stale menu selection.
        return stageInitializer != null
            ? stageInitializer.ActiveStageMetaData
            : StageSelectionManager.SelectedStageMetaData;
    }

    private void Display(StageMetaData mission)
    {
        displayedStage = mission;
        if (titleText != null) titleText.text = mission == null ? string.Empty : mission.StageTitle;
        if (descriptionText != null) descriptionText.text = mission == null ? string.Empty : mission.StageDescription;
    }
}
