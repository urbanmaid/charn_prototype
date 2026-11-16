using EnemyAI;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class PlayerCommander : MonoBehaviour
{
    public String singleSelectedUnitName;
    public List<String> selectedUnitNames;
    public Transform enemyTarget;
    public AimBehaviour PlayerAimBehaviour;

    public Transform focusedTarget;
    public GameObject targetMarkPrefab;
    public GameObject currentMark;
    public float markDuration = 2f;
    private bool targetSwitchFlag = false;

    private ControlsTutorial controlsTutorial;
    private PiUIManager manager;
    //public CommandHUD commandHUD;
    //private bool isOpenCommandHUD=false;


    
    private void Start()
    {
        PlayerAimBehaviour = GameObject.FindWithTag("Player").GetComponent<AimBehaviour>();
        selectedUnitNames = new List<String>();

        controlsTutorial = FindAnyObjectByType<ControlsTutorial>();
        manager = FindAnyObjectByType<PiUIManager>();        
    }

    void Update()
    {
        if (focusedTarget != null)
        {           

            //이미 마크 있으면 아무것도 안함
            if (currentMark != null && !targetSwitchFlag)
            {
                return;
            }
            else
            {
                Destroy(currentMark);
                targetSwitchFlag = false;
            }               

            currentMark = Instantiate(targetMarkPrefab);
            currentMark.transform.SetParent(focusedTarget);
            currentMark.transform.localPosition = Vector3.up * 2.5f;            
            currentMark.transform.LookAt(Camera.main.transform);

            StartCoroutine(ClearTargetAfterTime(markDuration));
        }        

        return;
        // Disabled this area...

        //명령패널 열기 (PA 선택)
        if (Input.GetKeyDown(KeyCode.C))
        {
            /*
            Debug.Log("C 눌림");

            manager.ChangeMenuState("HeroCommandPannel", new Vector2(Screen.width - 200f, 200f));
            if (controlsTutorial)
            {
                controlsTutorial.isUIOpen = !controlsTutorial.isUIOpen;
            }*/
           

            Debug.Log("=== C Key Pressed ===");

            Debug.Log($"manager : {manager}");
            Debug.Log($"controlsTutorial : {controlsTutorial}");

            if (manager == null)
            {
                Debug.LogError("PiUIManager가 NULL 입니다!");
                return;
            }

            Debug.Log("ChangeMenuState 호출 전");

            try
            {
                manager.ChangeMenuState(
                    "HeroCommandPannel",
                    new Vector2(Screen.width - 200f, 200f));

                Debug.Log("ChangeMenuState 호출 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"ChangeMenuState 예외 발생 : {e}");
            }

            if (controlsTutorial)
            {
                controlsTutorial.isUIOpen = !controlsTutorial.isUIOpen;
                Debug.Log($"isUIOpen : {controlsTutorial.isUIOpen}");
            }

            Debug.Log("=== End ===");
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UseSkillOnSelected(SkillSlot.Skill1);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            UseSkillOnSelected(SkillSlot.Skill2);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            UseSkillOnSelected(SkillSlot.GameChanger);
        }
    }

    IEnumerator ClearTargetAfterTime(float time)
    {
        yield return new WaitForSeconds(time);

        if (currentMark != null)
            Destroy(currentMark);

        focusedTarget = null;
    }
    void UseSkillOnSelected(SkillSlot slot)
    {
        if (string.IsNullOrEmpty(singleSelectedUnitName))
        {
            Debug.LogWarning("선택된 캐릭터 없음");
            return;
        }

        GameObject[] objs = GameObject.FindGameObjectsWithTag("PA");

        foreach (GameObject obj in objs)
        {
            var persona = obj.GetComponent<PersonaBehavior>()?.GetPersona();
            if (persona == null) continue;

            if (persona.personaName == singleSelectedUnitName)
            {                
                var skill = obj.GetComponent<AllySkillBehavior>();

                if (skill != null)
                {
                    skill.UseSkill(slot);
                }
                else
                {
                    Debug.LogWarning("AllySkillBehavior 없음");
                }

                return;
            }
        }
    }
}
