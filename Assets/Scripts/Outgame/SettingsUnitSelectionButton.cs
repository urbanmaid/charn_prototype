using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingsUnitSelectionButton : MonoBehaviour
{
    [SerializeField] private string categoryTitle;
    [SerializeField] private Image focusImage;

    private Button button;
    private SettingsManager settingsManager;

    public string CategoryTitle => categoryTitle;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(SelectThisSettingsUnit);
        SetFocused(false);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectThisSettingsUnit);
        }
    }

    public void SetManager(SettingsManager manager)
    {
        settingsManager = manager;
    }

    public void SetFocused(bool focused)
    {
        if (focusImage != null)
        {
            focusImage.gameObject.SetActive(focused);
        }
    }

    private void SelectThisSettingsUnit()
    {
        SettingsManager activeManager = settingsManager != null
            ? settingsManager
            : SettingsManager.Instance;

        if (activeManager != null)
        {
            activeManager.SelectSettingsUnit(this);
        }
    }
}
