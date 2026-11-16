using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public bool IsManagementVisible => settingsManagementGroup != null && settingsManagementGroup.alpha > 0f;
    [Serializable]
    public struct SettingsUnit
    {
        [SerializeField] private SettingsUnitSelectionButton selectionButton;
        [SerializeField] private RectTransform settingsPanel;

        public SettingsUnitSelectionButton SelectionButton => selectionButton;
        public RectTransform SettingsPanel => settingsPanel;
    }

    public static SettingsManager Instance { get; private set; }

    [Header("Settings Layer")]
    [SerializeField] private CanvasGroup settingsManagementGroup;
    [SerializeField] private Button closeSettingsButton;

    [Header("Settings Categories")]
    [SerializeField] private SettingsUnit[] settingsUnits;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI categoryTitleText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideAllSettingsPanels();

        if (settingsUnits != null)
        {
            foreach (SettingsUnit settingsUnit in settingsUnits)
            {
                if (settingsUnit.SelectionButton != null)
                {
                    settingsUnit.SelectionButton.SetManager(this);
                    settingsUnit.SelectionButton.SetFocused(false);
                }
            }
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.AddListener(HideSettingsManagementLayer);
        }
    }

    private void OnDestroy()
    {
        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(HideSettingsManagementLayer);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ToggleSettingsManagementLayer()
    {
        if (settingsManagementGroup == null)
        {
            Debug.LogWarning("SettingsManager에 설정 관리 CanvasGroup이 할당되지 않았습니다.", this);
            return;
        }

        SetSettingsManagementLayerVisible(settingsManagementGroup.alpha <= 0f);
    }

    public void ShowSettingsManagementLayer()
    {
        SetSettingsManagementLayerVisible(true);
    }

    public void HideSettingsManagementLayer()
    {
        SetSettingsManagementLayerVisible(false);
    }

    private void SetSettingsManagementLayerVisible(bool visible)
    {
        if (settingsManagementGroup == null)
        {
            return;
        }

        bool wasVisible = settingsManagementGroup.alpha > 0f;
        settingsManagementGroup.alpha = visible ? 1f : 0f;
        settingsManagementGroup.interactable = visible;
        settingsManagementGroup.blocksRaycasts = visible;
        MenuControllerNavigation.SetPanel(settingsManagementGroup.transform, visible, HideSettingsManagementLayer);

        if (visible && SaveDataDisplay.Instance != null)
        {
            SaveDataDisplay.Instance.HideAuxMenu();
        }

        if (visible && !wasVisible && settingsUnits != null && settingsUnits.Length > 0)
        {
            SelectSettingsUnit(0);
        }

        if (MenuSceneManager.Instance != null)
        {
            MenuSceneManager.Instance.SetIngameMenuLayerVisible(!visible);
        }
    }

    public void SelectSettingsUnit(SettingsUnitSelectionButton selectedButton)
    {
        if (settingsUnits == null)
        {
            return;
        }

        int index = Array.FindIndex(settingsUnits, unit => unit.SelectionButton == selectedButton);
        if (index < 0)
        {
            Debug.LogWarning("SettingsManager에 등록되지 않은 설정 카테고리 버튼입니다.", selectedButton);
            return;
        }

        SelectSettingsUnit(index);
    }

    public void SelectSettingsUnit(int index)
    {
        if (settingsUnits == null || index < 0 || index >= settingsUnits.Length)
        {
            Debug.LogWarning("설정 카테고리 인덱스를 확인해 주세요.", this);
            return;
        }

        HideAllSettingsPanels();

        foreach (SettingsUnit settingsUnit in settingsUnits)
        {
            if (settingsUnit.SelectionButton != null)
            {
                settingsUnit.SelectionButton.SetFocused(false);
            }
        }

        SettingsUnit selectedUnit = settingsUnits[index];
        if (selectedUnit.SettingsPanel != null)
        {
            selectedUnit.SettingsPanel.gameObject.SetActive(true);
        }

        if (selectedUnit.SelectionButton != null)
        {
            selectedUnit.SelectionButton.SetFocused(true);

            if (categoryTitleText != null)
            {
                categoryTitleText.text = selectedUnit.SelectionButton.CategoryTitle;
            }
        }
    }

    private void HideAllSettingsPanels()
    {
        if (settingsUnits == null)
        {
            return;
        }

        foreach (SettingsUnit settingsUnit in settingsUnits)
        {
            if (settingsUnit.SettingsPanel != null)
            {
                settingsUnit.SettingsPanel.gameObject.SetActive(false);
            }
        }

        if (categoryTitleText != null)
        {
            categoryTitleText.text = string.Empty;
        }
    }
}
