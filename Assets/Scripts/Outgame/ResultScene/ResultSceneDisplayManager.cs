using System;
using System.Linq;
using Charm.Mechanism.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ResultSceneDisplayManager : MonoBehaviour
{   
    [Header("Clear/Fail Status")]
    [SerializeField] private GameObject layerStageClear;
    [SerializeField] private GameObject layerStageFail;

    [Header("Content")]
    [SerializeField] private TextMeshProUGUI textStage;
    [SerializeField] private TextMeshProUGUI textElapsedTime;
    [SerializeField] private string timeFormat = "00:00:00.00";
    [SerializeField] private TextMeshProUGUI textDamageGiven, textDamageRecieved, textRetryCount, textUsedGadgets;

    [Serializable]
    public struct ButtonActionPair
    {
        public Button button;
        public UnityEvent action;
    }
    [Header("Navigator")]
    [SerializeField] private ButtonActionPair[] buttonActionPairs;

    private const string ResultScenePath = "Assets/Scenes/Outgame/Result.unity";

    private sealed class ResultContext
    {
        public StageAttemptResult Result;
        public string StageTitle;
        public string MenuPath;
        public string GameplayPath;
    }

    private static ResultContext pendingResult;
    private ResultContext context;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingResult() => pendingResult = null;

    public static bool TryOpen(StageAttemptResult result, StageMetaData stage, string stageId,
        string menuPath, string gameplayPath)
    {
        if (result == null || (!result.stageCompleted && !result.stageFailed)) return false;
        if (!Application.CanStreamedLevelBeLoaded(ResultScenePath))
        {
            Debug.LogWarning("Result 씬을 불러올 수 없습니다. 기존 결과 UI에서 메뉴로 나갈 수 있습니다.");
            return false;
        }

        pendingResult = new ResultContext
        {
            Result = result,
            StageTitle = stage != null && !string.IsNullOrWhiteSpace(stage.StageTitle) ? stage.StageTitle : stageId,
            MenuPath = menuPath,
            GameplayPath = gameplayPath
        };
        PauseUIController.LoadMenuScene(ResultScenePath);
        return true;
    }

    private void Start()
    {
        context = pendingResult;
        pendingResult = null;
        UpdateMissionClearStatus();

        var configured = new System.Collections.Generic.HashSet<Button>();
        if (buttonActionPairs != null)
        {
            foreach (var pair in buttonActionPairs)
            {
                if (pair.button == null || pair.action == null || pair.action.GetPersistentEventCount() == 0 ||
                    !configured.Add(pair.button)) continue;
                pair.button.onClick.AddListener(pair.action.Invoke);
            }
        }

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button.gameObject.scene != gameObject.scene || configured.Contains(button) ||
                button.onClick.GetPersistentEventCount() > 0) continue;
            switch (button.name)
            {
                case "Button_Menu": button.onClick.AddListener(ReturnToMenu); break;
                case "Button_Restart":
                    button.interactable = context != null && Application.CanStreamedLevelBeLoaded(context.GameplayPath);
                    button.onClick.AddListener(RestartStage);
                    break;
                case "Button_MissionLog": button.interactable = false; break;
            }
        }
    }

    public void ReturnToMenu()
        => PauseUIController.LoadMenuScene(context?.MenuPath ?? PauseUIController.DefaultMenuScenePath);

    public void RestartStage()
    {
        if (context != null) PauseUIController.LoadMenuScene(context.GameplayPath);
    }

    public void UpdateMissionClearStatus()
    {
        var result = context?.Result;
        if (layerStageClear != null) layerStageClear.SetActive(result?.stageCompleted == true);
        if (layerStageFail != null) layerStageFail.SetActive(result?.stageFailed == true);

        SetText(textStage, context?.StageTitle ?? "—");
        var combat = result?.stageCombat;
        SetText(textDamageGiven, combat == null ? "—" : combat.Sum(entry => entry.damageDealt).ToString("0.##"));
        SetText(textRetryCount, result == null ? "—" : result.retryCount.ToString());
        SetText(textDamageRecieved, combat == null ? "—" : combat.Sum(entry => entry.healthDamage).ToString("0.##"));
        SetText(textUsedGadgets, combat == null ? "—" : combat.Sum(entry => entry.gadgetUses).ToString());
        double? seconds = result == null ? null :
            (result.completedMissions?.Sum(mission => mission.seconds) ?? 0d) + result.failedMissionSeconds;
        SetText(textElapsedTime, seconds.HasValue ? FormatTime(seconds.Value) : "—");
    }

    private string FormatTime(double seconds)
    {
        var elapsed = TimeSpan.FromSeconds(seconds);
        if (string.IsNullOrEmpty(timeFormat) || timeFormat == "00:00:00.00")
            return $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 10:00}";
        try { return elapsed.ToString(timeFormat); }
        catch (FormatException) { return elapsed.ToString(@"hh\:mm\:ss\.ff"); }
    }

    private static void SetText(TextMeshProUGUI field, string value)
    {
        if (field != null) field.text = value;
    }
}
