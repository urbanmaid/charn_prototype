using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StageSelectionUnitButton : MonoBehaviour
{
    [Header("Focus")]
    [SerializeField] private Image focusedBackground;

    [Header("Stage Information")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Rank Information")]
    [SerializeField] private Image[] rankConditionImages = new Image[StageDetailedInfoDisplayUnit.RankConditionCount];
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private GameObject rankTextBox;

    private Button button;
    private StageMetaData stageMetaData;

    public StageMetaData StageMetaData => stageMetaData;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(FocusThisStage);
        SetFocused(false);
    }

    public void Setup(StageMetaData data)
    {
        stageMetaData = data;

        if (stageMetaData == null)
        {
            return;
        }

        if (thumbnailImage != null) thumbnailImage.sprite = stageMetaData.Thumbnail;
        if (titleText != null) titleText.text = stageMetaData.StageTitle;
    }

    /// <summary>
    /// 목록 버튼에 현재 세이브데이터의 스테이지 랭크를 표시한다.
    /// </summary>
    public void DisplayClearInfo(StageClearInfo clearInfo, string[] rankTitles)
    {
        bool isCleared = clearInfo != null && clearInfo.IsCleared;

        for (int i = 0; i < StageDetailedInfoDisplayUnit.RankConditionCount; i++)
        {
            if (rankConditionImages != null && i < rankConditionImages.Length && rankConditionImages[i] != null)
            {
                rankConditionImages[i].enabled = isCleared && clearInfo.GetRankCondition(i);
            }
        }

        if (rankText != null)
        {
            rankText.text = isCleared
                ? StageDetailedInfoDisplayUnit.GetRankTitle(clearInfo, rankTitles)
                : string.Empty;
        }

        if (rankTextBox != null)
        {
            rankTextBox.SetActive(isCleared);
        }
    }

    public void SetFocused(bool focused)
    {
        if (focusedBackground != null)
        {
            focusedBackground.gameObject.SetActive(focused);
        }
    }

    private void OnValidate()
    {
        if (rankConditionImages == null ||
            rankConditionImages.Length != StageDetailedInfoDisplayUnit.RankConditionCount)
        {
            Array.Resize(ref rankConditionImages, StageDetailedInfoDisplayUnit.RankConditionCount);
        }
    }

    private void FocusThisStage()
    {
        if (StageSelectionManager.Instance != null)
        {
            StageSelectionManager.Instance.FocusStage(this);
        }
    }
}
