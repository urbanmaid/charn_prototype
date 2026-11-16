using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDSkills : MonoBehaviour
{
    [SerializeField] private SkillBehavior skillBehaviorSource;
    private Dictionary<SkillSlot, SkillData> skills;
    private Dictionary<SkillSlot, float> cooldowns;
    private readonly bool[] previousReady = new bool[3];
    private bool readyInitialized;

    private void CheckReady(SkillSlot slot, int index, Image icon, Color color)
    {
        if (!skillBehaviorSource || skills == null || !skills.TryGetValue(slot, out SkillData data) || data == null || data.logic == null) return;
        bool ready = data is GameChangerData gc ? skillBehaviorSource.gameChangerPoint >= gc.requiredPoint
            : cooldowns != null && cooldowns.TryGetValue(slot, out float at) && at <= Time.time;
        if (readyInitialized && ready && !previousReady[index])
        {
            Image baseIcon = index == 0 ? skill1Icon : index == 1 ? skill2Icon : gameChangerIcon;
            CombatReadyPulse.Play(icon && icon.isActiveAndEnabled ? icon : baseIcon, color);
            var shoot = skillBehaviorSource.GetComponent<ShootBehaviour>();
            var weapon = shoot ? shoot.CurrentWeaponForFeedback : null;
            if (weapon) SkillOverlay.Apply(weapon.gameObject, "ready_" + slot, color, 0.4f, 0.7f);
        }
        if (readyInitialized && !ready && previousReady[index])
        {
            Image usedIcon = index == 0 ? skill1Icon : index == 1 ? skill2Icon : gameChangerIcon;
            HUDValueFlash.Play(usedIcon, color);
        }
        previousReady[index] = ready;
    }

    [Header("Skill 1")]
    [SerializeField] private Image skill1Icon;
    [SerializeField] private Image skill1IconReady;
    [SerializeField] private Slider skill1Slider;

    [Header("Skill 2")]
    [SerializeField] private Image skill2Icon;
    [SerializeField] private Image skill2IconReady;
    [SerializeField] private Slider skill2Slider;

    [Header("GameChanger")]
    [SerializeField] private Image gameChangerIcon;
    [SerializeField] private Image gameChangerIconReady;
    [SerializeField] private TextMeshProUGUI gameChangerPercentageText;
    [SerializeField] private Slider gameChangerPercentageSlider;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(SkillBehavior sb)
    {
        CancelReadyPulses();
        HUDValueFlash.Cancel(skill1Icon);
        HUDValueFlash.Cancel(skill2Icon);
        HUDValueFlash.Cancel(gameChangerIcon);
        skillBehaviorSource = sb;
        readyInitialized = false;

        if (!sb)
        {
            skills = null;
            cooldowns = null;
            return;
        }

        skills = sb.GetSkills();
        cooldowns = sb.GetCooldowns();
        SetIconOfSkills();
    }

    void SetIconOfSkills()
    {
        if(skills == null) return;

        if(skills.TryGetValue(SkillSlot.Skill1, out var first) && first != null && first.icon && skill1Icon && skill1IconReady)
        {
            skill1Icon.sprite = skills[SkillSlot.Skill1].icon;
            skill1IconReady.sprite = skills[SkillSlot.Skill1].icon;
        }

        bool hasSkill2 = skills.TryGetValue(SkillSlot.Skill2, out var second) && second != null && second.logic != null;
        if (skill2Icon) skill2Icon.gameObject.SetActive(hasSkill2);
        if (skill2IconReady) skill2IconReady.gameObject.SetActive(hasSkill2);
        if (skill2Slider) skill2Slider.gameObject.SetActive(hasSkill2);
        if(hasSkill2 && second.icon && skill2Icon && skill2IconReady)
        {
            skill2Icon.sprite = skills[SkillSlot.Skill2].icon;
            skill2IconReady.sprite = skills[SkillSlot.Skill2].icon;
        }

        if(skills.TryGetValue(SkillSlot.GameChanger, out var changer) && changer != null && changer.icon && gameChangerIcon && gameChangerIconReady)
        {
            gameChangerIcon.sprite = skills[SkillSlot.GameChanger].icon;
            gameChangerIconReady.sprite = skills[SkillSlot.GameChanger].icon;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(skills != null)
        {
            UpdateSkill1();
            UpdateSkill2();
            UpdateGameChanger();
            CheckReady(SkillSlot.Skill1, 0, skill1IconReady, new Color(0.45f, 0.85f, 1f));
            CheckReady(SkillSlot.Skill2, 1, skill2IconReady, new Color(1f, 0.7f, 0.35f));
            CheckReady(SkillSlot.GameChanger, 2, gameChangerIconReady, new Color(1f, 0.9f, 0.45f));
            readyInitialized = true;
        }
    }

    // Update Skill 1 Cooldown Rate
    void UpdateSkill1()
    {
        if (skills.TryGetValue(SkillSlot.Skill1, out var data) && data is NormalSkillData normal &&
            cooldowns != null && cooldowns.TryGetValue(SkillSlot.Skill1, out float readyAt))
        {
            if(skill1IconReady != null)
            {
                skill1IconReady.fillAmount = 1-((readyAt - Time.time) / Mathf.Max(0.001f, normal.cooldown));
            }
            if(skill1Slider != null)
            {
                skill1Slider.value = 1-((readyAt - Time.time) / Mathf.Max(0.001f, normal.cooldown));
            }
        }
    }

    // Update Skill 2 Cooldown Rate
    void UpdateSkill2()
    {
        if (skills.TryGetValue(SkillSlot.Skill2, out var data) && data is NormalSkillData normal &&
            cooldowns != null && cooldowns.TryGetValue(SkillSlot.Skill2, out float readyAt))
        {
            if(skill2IconReady != null)
            {
                skill2IconReady.fillAmount = 1-((readyAt - Time.time) / Mathf.Max(0.001f, normal.cooldown));
            }
            if(skill2Slider != null)
            {
                skill2Slider.value = 1-((readyAt - Time.time) / Mathf.Max(0.001f, normal.cooldown));
            }
        }
    }

    // Update Game Changer Rate
    void UpdateGameChanger()
    {
        if (skillBehaviorSource && skills.TryGetValue(SkillSlot.GameChanger, out var data) && data is GameChangerData gc)
        {
            float percentage = gc.requiredPoint > 0 ? Mathf.Clamp01((float)skillBehaviorSource.gameChangerPoint / gc.requiredPoint) : 1f;
            if(gameChangerIcon && gameChangerPercentageText)
            {
                gameChangerIcon.fillAmount = percentage;
                if(percentage >= 1f)
                {
                    gameChangerPercentageText.text = "Ready";
                    if (gameChangerIconReady) gameChangerIconReady.gameObject.SetActive(true);
                }
                else
                {
                    gameChangerPercentageText.text = Math.Floor(percentage * 100f) + "%";
                    if (gameChangerIconReady) gameChangerIconReady.gameObject.SetActive(false);
                }
            }
            if(gameChangerPercentageSlider)
            {
                gameChangerPercentageSlider.value = percentage;
            }
        }
    }

    private void OnDisable()
    {
        CancelReadyPulses();
        readyInitialized = false;
        HUDValueFlash.Cancel(skill1Icon);
        HUDValueFlash.Cancel(skill2Icon);
        HUDValueFlash.Cancel(gameChangerIcon);
    }

    private void CancelReadyPulses()
    {
        CombatReadyPulse.Cancel(skill1Icon);
        CombatReadyPulse.Cancel(skill2Icon);
        CombatReadyPulse.Cancel(gameChangerIcon);
        CombatReadyPulse.Cancel(skill1IconReady);
        CombatReadyPulse.Cancel(skill2IconReady);
        CombatReadyPulse.Cancel(gameChangerIconReady);
    }
}
