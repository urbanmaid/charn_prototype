using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDWeapon : MonoBehaviour
{
    [SerializeField] private ShootBehaviour weaponInfoSource;
    [SerializeField] private InteractiveWeapon interactiveWeaponCurrent;

    [SerializeField] private Image weaponImage;
    [SerializeField] private TextMeshProUGUI magazineLoadCurrent;
    [SerializeField] private TextMeshProUGUI magazineLoadMax;
    [SerializeField] private Slider magazineLoadSlider;

    [Header("Layers")]
    [SerializeField] bool isArmed = false;
    [SerializeField] private RectTransform armedLayer;
    [SerializeField] private RectTransform unarmedLayer;

    private int previousAmmo = -1;
    private float pulse;
    private bool completed;
    private Color baseColor;
    private bool colorCaptured;

    private void Update()
    {
        if (!magazineLoadCurrent) return;
        if (!colorCaptured) { baseColor = magazineLoadCurrent.color; colorCaptured = true; }
        pulse = Mathf.Max(0f, pulse - Time.deltaTime);
        magazineLoadCurrent.color = Color.Lerp(baseColor, completed ? new Color(0.55f, 1f, 0.75f) : new Color(1f, 0.6f, 0.25f), Mathf.Clamp01(pulse / 0.25f));
    }

    public void ReloadCompleted() { completed = true; pulse = 0.3f; }
    private void OnDisable() { if (colorCaptured && magazineLoadCurrent) magazineLoadCurrent.color = baseColor; pulse = 0f; previousAmmo = -1; }

    public void UpdateHUD(InteractiveWeapon weapon)
    {
        if(weapon != null)
        {
            isArmed = true;
            UpdateArmed();

            if (interactiveWeaponCurrent != weapon)
            {
                if (previousAmmo >= 0) HUDValueFlash.Play(weaponImage, new Color(0.65f, 0.9f, 1f));
                previousAmmo = -1; pulse = 0f;
            }
            interactiveWeaponCurrent = weapon;

            if (weaponImage) weaponImage.sprite = interactiveWeaponCurrent.sprite;
            int cur = interactiveWeaponCurrent.GetMag();
            int max = interactiveWeaponCurrent.GetFullMag();
            if (cur < previousAmmo && max > 0 && (float)cur / max <= .2f) { completed = false; pulse = 0.25f; }
            previousAmmo = cur;
            if (magazineLoadCurrent) magazineLoadCurrent.text = cur.ToString();
            if (magazineLoadMax) magazineLoadMax.text = "/" + max.ToString();
            if (magazineLoadSlider) magazineLoadSlider.value = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
        }
        else if(weapon == null)
        {
            // Todo
            isArmed = false;
            previousAmmo = -1;
            pulse = 0f;
            UpdateArmed();
        }
    }

    void UpdateArmed()
    {
        if (armedLayer) armedLayer.gameObject.SetActive(isArmed);
        if (unarmedLayer) unarmedLayer.gameObject.SetActive(!isArmed);
    }
}
