using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDHealth : MonoBehaviour
{
    [SerializeField] private HealthManager targetHealthManager;

    [Header("Value")]
    [SerializeField] private int valueHealthBasic;    // Max health of basic
    [SerializeField] private int valueHealthShield;         // Max health of basic+shield

    [SerializeField] private int valueHealthCurrentBasic;
    [SerializeField] private int valueHealthCurrentShield;

    [Header("Expressions")]
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI healthTextCurrent;
    [SerializeField] Slider sliderHealthBasic, sliderHealthShield; 
    [SerializeField] Image basicFillImage;
    private bool feedbackInitialized;
    private float nextFeedback;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Demo(400, 200);
    }

    void Demo(int healthValue, int healthValueShield = 0)
    {
        UpdateHealth(healthValue, healthValueShield);

        UpdateHealthCurrent((int)(healthValue * 0.15f), (int)(healthValueShield * 0.6f));
    }

    public void Init()
    {
        
    }

    public void Init(HealthManager healthManager)
    {
        feedbackInitialized = false;
        nextFeedback = 0f;
        HUDValueFlash.Cancel(healthTextCurrent);
        targetHealthManager = healthManager;
        RefreshFromTarget();
    }

    private void Update()
    {
        RefreshFromTarget();
    }

    private void RefreshFromTarget()
    {
        if(targetHealthManager == null)
        {
            return;
        }

        if (targetHealthManager is TargetHealth target && target.useAsDummy)
        {
            UpdateHealth(target.DummyDisplayedDamage);
            UpdateHealthCurrent(target.DummyDisplayedDamage);
            return;
        }

        int maxHealth;
        int maxShield;
        GetMaxHealthValues(targetHealthManager, out maxHealth, out maxShield);

        int currentHealth;
        int currentShield;
        GetCurrentHealthValues(targetHealthManager, out currentHealth, out currentShield);

        UpdateHealth(maxHealth, maxShield);
        UpdateHealthCurrent(currentHealth, currentShield);
    }

    private void GetMaxHealthValues(HealthManager healthManager, out int maxHealth, out int maxShield)
    {
        maxHealth = 0;
        maxShield = 0;

        int currentHealth;
        int currentShield;
        GetCurrentHealthValues(healthManager, out currentHealth, out currentShield);

        if(healthManager is PlayerHealth playerHealth
            && (playerHealth.TotalHealth > 0f || playerHealth.TotalShieldHealth > 0f))
        {
            maxHealth = Mathf.CeilToInt(playerHealth.TotalHealth);
            maxShield = Mathf.CeilToInt(Mathf.Max(playerHealth.TotalShieldHealth, currentShield));
            return;
        }

        if(healthManager is AllyHealthManager allyHealthManager
            && (allyHealthManager.TotalHealth > 0f || allyHealthManager.TotalShieldHealth > 0f))
        {
            maxHealth = Mathf.CeilToInt(allyHealthManager.TotalHealth);
            maxShield = Mathf.CeilToInt(Mathf.Max(allyHealthManager.TotalShieldHealth, currentShield));
            return;
        }

        PersonaBehavior personaBehavior = healthManager.GetComponent<PersonaBehavior>();
        PersonaData personaData = personaBehavior != null ? personaBehavior.GetPersona() : null;
        if(personaData != null && personaData.genericStatData != null)
        {
            maxHealth = personaData.genericStatData.health;
            maxShield = Mathf.Max(personaData.genericStatData.healthOfPreloadedShield, currentShield);
            return;
        }

        maxHealth = currentHealth;
        maxShield = currentShield;
    }

    private void GetCurrentHealthValues(HealthManager healthManager, out int currentHealth, out int currentShield)
    {
        currentHealth = 0;
        currentShield = 0;

        if(healthManager is PlayerHealth playerHealth)
        {
            currentHealth = Mathf.CeilToInt(playerHealth.health);
            currentShield = Mathf.CeilToInt(playerHealth.shieldHealth);
            return;
        }

        if(healthManager is AllyHealthManager allyHealthManager)
        {
            currentHealth = Mathf.CeilToInt(allyHealthManager.health);
            currentShield = Mathf.CeilToInt(allyHealthManager.shieldHealth);
        }
    }

    /// <summary>
    /// Update max health of player
    /// </summary>
    public void UpdateHealth(int healthValue, int healthValueShield = 0)
    {
        valueHealthBasic = healthValue;
        valueHealthShield = healthValueShield;

        if(healthText != null)
        {
            healthText.text = "/" + (healthValue + healthValueShield).ToString();
        }
        UpdateHealthSlider();
    }

    /// <summary>
    /// Update current health of player
    /// </summary>
    public void UpdateHealthCurrent(int healthValue, int healthValueShield = 0)
    {
        if (feedbackInitialized)
        {
            bool recovered = healthValue > valueHealthCurrentBasic || healthValueShield > valueHealthCurrentShield;
            bool shieldDepleted = valueHealthCurrentShield > 0 && healthValueShield <= 0;
            if (shieldDepleted || (recovered && Time.time >= nextFeedback))
            {
                if (shieldDepleted) HUDValueFlash.Cancel(healthTextCurrent);
                HUDValueFlash.Play(healthTextCurrent, shieldDepleted
                    ? new Color(0.45f, 0.8f, 1f) : new Color(0.55f, 1f, 0.75f));
                nextFeedback = Time.time + 0.8f;
            }
        }
        feedbackInitialized = true;
        valueHealthCurrentBasic = healthValue;
        valueHealthCurrentShield = healthValueShield;

        if(healthTextCurrent != null)
        {
            healthTextCurrent.text = (healthValue + healthValueShield).ToString();
        }
        UpdateHealthSlider();
    }

    void UpdateHealthSlider()
    {
        int currentTotal = valueHealthCurrentBasic + valueHealthCurrentShield;
        int healthTotal = Math.Max(valueHealthBasic + valueHealthShield, currentTotal);
        if (sliderHealthBasic) sliderHealthBasic.value = healthTotal > 0 ? Mathf.Clamp01((float)Math.Min(valueHealthCurrentBasic, valueHealthBasic) / healthTotal) : 0f;
        if (sliderHealthShield) sliderHealthShield.value = healthTotal > 0 ? Mathf.Clamp01((float)currentTotal / healthTotal) : 0f;

        if (basicFillImage) basicFillImage.color = (valueHealthBasic * 0.2f > valueHealthCurrentBasic) ? Color.red : Color.white;
    }

    private void OnDisable()
    {
        feedbackInitialized = false;
        nextFeedback = 0f;
        HUDValueFlash.Cancel(healthTextCurrent);
    }
}
