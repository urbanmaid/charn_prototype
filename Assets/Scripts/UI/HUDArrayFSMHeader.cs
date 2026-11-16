using TMPro;
using UnityEngine;

public class HUDArrayFSMHeader : MonoBehaviour
{
    [SerializeField] private string stringNormal = "Current PA";
    [SerializeField] private string stringChangingPA = "Select PA to swap";
    [SerializeField] private TextMeshProUGUI textDisplay;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(textDisplay)
        {
            textDisplay.text = stringNormal;
        }
    }

    public void SwapMode(bool value)
    {
        if(textDisplay)
        {
            textDisplay.text = value ? stringChangingPA : stringNormal;
        }
    }
}
