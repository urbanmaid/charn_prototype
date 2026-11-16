using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScreenWidthHeight : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    Resolution[] resolutions;

    void Start()
    {
        resolutions = Screen.resolutions;
        dropdown.ClearOptions();

        int currentIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        dropdown.value = currentIndex;
        dropdown.RefreshShownValue();
    }

    public void ChangeResolution(int index)
    {
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }
}