using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDPersona : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI personaName;
    //[SerializeField] private TextMeshProUGUI personaProfile;
    [SerializeField] private Image personaIcon;
    [SerializeField] private bool hideIconWhenEmpty = true;
    [SerializeField, Range(0f, 1f)] private float deadIconAlpha = 0.5f;
    private bool deadVisualActive;

    // Subscribe one of the persona data and initialize elements
    public void Init(PersonaBehavior personaBehavior)
    {
        PersonaData data = personaBehavior != null ? personaBehavior.GetPersona() : null;
        Show(PersonaHUDData.FromPersona(data));
    }

    public void Init(PersonaHUDData data)
    {
        Show(data);
    }

    public void Show(PersonaHUDData data)
    {
        UpdateName(data.Name);
        //UpdateProfile(data.Profile);
        UpdateIcon(data.Icon);

        if(!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Clear()
    {
        UpdateName("");
        //UpdateProfile("");
        UpdateIcon(null);
    }

    private void UpdateName(string value)
    {
        if(personaName != null)
        {
            personaName.text = value;
        }
    }

    /*
    private void UpdateProfile(string value)
    {
        if(personaProfile != null)
        {
            personaProfile.text = value;
        }
    }
    */

    private void UpdateIcon(Sprite sprite)
    {
        if(personaIcon != null)
        {
            personaIcon.sprite = sprite;
            if(hideIconWhenEmpty)
            {
                personaIcon.enabled = sprite != null;
            }
            ApplyIconAlpha();
        }
    }

    public void SetDeadVisual(bool value)
    {
        deadVisualActive = value;
        ApplyIconAlpha();
    }

    private void ApplyIconAlpha()
    {
        if(personaIcon == null)
        {
            return;
        }

        Color color = personaIcon.color;
        color.a = deadVisualActive ? deadIconAlpha : 1f;
        personaIcon.color = color;
    }
}

public readonly struct PersonaHUDData
{
    public readonly string Name;
    //public readonly string Profile;
    public readonly Sprite Icon;

    public PersonaHUDData(string name, Sprite icon)
    {
        Name = name;
        //Profile = profile;
        Icon = icon;
    }

    public static PersonaHUDData FromPersona(PersonaData data)
    {
        if(data == null)
        {
            return new PersonaHUDData("", null);
        }

        string displayName = string.IsNullOrWhiteSpace(data.personaName) ? data.name : data.personaName;
        return new PersonaHUDData(displayName, data.icon);
    }
}
