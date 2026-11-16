using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AllySkillBehavior : SkillBehavior
{
    private readonly HashSet<SkillSlot> notifiedSlots = new HashSet<SkillSlot>(); //한 번만 출력용

    protected override void InitiaizeInput()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");        

        if (player == null)
        {            
            return;
        }
        //Make fake InputActionSystem
        //Disable Input(Key Q,E,X) in SkillBehavior and able in PlayerCommander.cs
        skill1Button = new InputAction();
        skill2Button = new InputAction();
        gameChangerButton = new InputAction();        
        Debug.Log("InitiaizeInput called");    
    }
    
    protected override void OnStart()
    {
        StartCoroutine(CheckCooldowns());
    }

    private IEnumerator CheckCooldowns()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);

            if (cooldowns == null) continue;

            foreach (var pair in cooldowns)
            {
                SkillSlot slot = pair.Key;
                float endTime = pair.Value;

                //아직 한 번도 안 쓴 스킬은 무시
                if (endTime == 0f) continue;

                //쿨타임 끝났고 아직 알림 안 보냈을 때만
                if (endTime <= Time.time && !notifiedSlots.Contains(slot))
                {
                    notifiedSlots.Add(slot);
                    FeedBackCanSkill(slot);
                }
            }
        }
    }

    void FeedBackCanSkill(SkillSlot slot)
    {
        PersonaData personaData = GetComponent<PersonaBehavior>()?.GetPersona();
        if (CommandManager.Instance != null && personaData != null)
            CommandManager.Instance.FeedBackByMessage($"{slot} ready!", personaData.icon, personaData.personaName);
    }
    protected override void OnSkillUsed(SkillSlot slot)
    {
        notifiedSlots.Remove(slot);
    }
}