using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StageClearInfo
{
    public string stageId = string.Empty;
    public float clearTimeSeconds = -1f;
    public bool[] rankConditions = new bool[StageDetailedInfoDisplayUnit.RankConditionCount];
    public bool achievedSpecialGoal;

    public bool IsCleared => rankConditions != null &&
                             rankConditions.Length > 0 &&
                             rankConditions[0];

    public bool GetRankCondition(int index)
    {
        return rankConditions != null &&
               index >= 0 &&
               index < rankConditions.Length &&
               rankConditions[index];
    }
}

public class StageDetailedInfoDisplayUnit : MonoBehaviour
{
    public const int RankConditionCount = 3;

    [Header("Stage Information")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Stage Clear Information")]
    [SerializeField] private TextMeshProUGUI clearTimeText;
    [SerializeField] private Image[] rankConditionImages = new Image[RankConditionCount];
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private GameObject rankTextBox;

    public void Display(StageMetaData stageMetaData)
    {
        if (descriptionText != null)
        {
            descriptionText.text = stageMetaData == null ? string.Empty : stageMetaData.StageDescription;
        }
    }

    /// <summary>
    /// 클리어 시간과 3개의 랭크 조건 달성 상태를 상세 UI에 표시한다.
    /// rankConditions[0]은 스테이지 클리어 여부이고, 나머지는 추가 클리어 조건이다.
    /// </summary>
    public void DisplayClearInfo(StageClearInfo clearInfo)
    {
        DisplayClearInfo(clearInfo, null);
    }

    /// <summary>
    /// 클리어 시간과 랭크 조건, 설정된 랭크 칭호를 상세 UI에 표시한다.
    /// rankTitles는 Achieved, Stunning, Perfect, Outstanding 순서로 전달한다.
    /// </summary>
    public void DisplayClearInfo(StageClearInfo clearInfo, string[] rankTitles)
    {
        bool isCleared = clearInfo != null && clearInfo.IsCleared;

        for (int i = 0; i < RankConditionCount; i++)
        {
            bool conditionMet = isCleared && clearInfo.GetRankCondition(i);

            if (rankConditionImages != null && i < rankConditionImages.Length && rankConditionImages[i] != null)
            {
                rankConditionImages[i].enabled = conditionMet;
            }
        }

        if (clearTimeText != null)
        {
            clearTimeText.text = isCleared && clearInfo.clearTimeSeconds >= 0f
                ? $"{FormatClearTime(clearInfo.clearTimeSeconds)}"
                : "--:--.---";
        }

        if (rankText != null)
        {
            rankText.text = isCleared ? GetRankTitle(clearInfo, rankTitles) : string.Empty;
        }

        if (rankTextBox != null)
        {
            rankTextBox.SetActive(isCleared);
        }
    }

    private void OnValidate()
    {
        if (rankConditionImages == null || rankConditionImages.Length != RankConditionCount)
        {
            Array.Resize(ref rankConditionImages, RankConditionCount);
        }
    }

    public static string GetRankTitle(StageClearInfo clearInfo, string[] rankTitles)
    {
        if (clearInfo == null || !clearInfo.IsCleared)
        {
            return string.Empty;
        }

        if (clearInfo.achievedSpecialGoal)
        {
            return GetRankTitleAt(rankTitles, 3);
        }

        int achievedConditionCount = 0;
        for (int i = 0; i < RankConditionCount; i++)
        {
            if (clearInfo.GetRankCondition(i))
            {
                achievedConditionCount++;
            }
        }

        int titleIndex = Mathf.Clamp(achievedConditionCount - 1, 0, 2);
        return GetRankTitleAt(rankTitles, titleIndex);
    }

    private static string GetRankTitleAt(string[] rankTitles, int index)
    {
        return rankTitles != null && index >= 0 && index < rankTitles.Length
            ? rankTitles[index]
            : string.Empty;
    }

    private static string FormatClearTime(float clearTimeSeconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(clearTimeSeconds);
        return $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:00}";
    }
}
