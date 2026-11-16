using TMPro;
using UnityEngine;

public class HUDTips : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textArea;
    [SerializeField] private CanvasGroup textCanvasGroup;
    public static HUDTips instance;
    private string defaultContent = "";
    private Object overrideOwner;
    private string overrideContent;

    public void Start()
    {
        if(instance == null)
        {
            instance = this;
        }

        ShowTips("");
    }

    public void ShowTips(string content)
    {
        defaultContent = content ?? "";
        RenderTips(overrideOwner != null ? overrideContent : defaultContent);
    }

    public void ShowOverrideTips(Object owner, string content)
    {
        if (owner == null) return;
        overrideOwner = owner;
        overrideContent = content ?? "";
        RenderTips(overrideContent);
    }

    public void ClearOverrideTips(Object owner)
    {
        if (overrideOwner != owner) return;
        overrideOwner = null;
        overrideContent = null;
        RenderTips(defaultContent);
    }

    private void RenderTips(string content)
    {
        if(content.Length != 0)
        {
            textCanvasGroup.alpha = 1;
            textArea.text = content;
        }
        else
        {
            textCanvasGroup.alpha = 0;
            textArea.text = "";
        }
    }
}
