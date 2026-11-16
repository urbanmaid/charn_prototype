using UnityEngine;

public class HUDHighlight : MonoBehaviour
{
    [SerializeField] private GameObject transformToShow;

    public void Show(bool value)
    {
        if(transformToShow != null)
        {
            transformToShow.SetActive(value);
        }
    }
}
