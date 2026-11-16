using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SaveDataUnitButton : MonoBehaviour
{
    [Header("Focus")]
    [SerializeField] private Image focusedBackground;

    [Header("Save Data Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI auText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI lastPlayedDateText;

    private Button button;
    private SaveDataManager.SaveData saveData;
    private string saveFilePath;

    public SaveDataManager.SaveData SaveData => saveData;
    public string SaveFilePath => saveFilePath;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(FocusThisSaveData);
        SetFocused(false);
    }

    public void Setup(SaveDataManager.SaveData data, string filePath)
    {
        saveData = data;
        saveFilePath = filePath;

        if (saveData == null)
        {
            return;
        }

        if (nameText != null) nameText.text = saveData.saveName;
        if (auText != null) auText.text = $"AU: {saveData.au:N0}";
        if (levelText != null) levelText.text = $"Lv. {saveData.userLevel}";
        if (expText != null) expText.text = $"EXP: {saveData.userExp:N0}";
        if (lastPlayedDateText != null)
        {
            lastPlayedDateText.text = string.IsNullOrEmpty(saveData.lastPlayedDate)
                ? "Last Played: -"
                : $"Last Played: {saveData.lastPlayedDate}";
        }
    }

    private void FocusThisSaveData()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.FocusSaveData(this);
        }
    }

    public void SetFocused(bool focused)
    {
        if (focusedBackground != null)
        {
            focusedBackground.gameObject.SetActive(focused);
        }
    }
}
