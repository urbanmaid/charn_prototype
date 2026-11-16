using System;
using UnityEngine;

[Serializable]
// PersonaDataSkill의 각 슬롯에 내장되는 공통 데이터. 자체 ScriptableObject 에셋은 아니다.
public class SkillData// : ScriptableObject
{
    // 스킬 표시 이름.
    public string skillName;
    // 스킬 UI에 전달할 아이콘 이미지 참조.
    public Sprite icon;
    //public float cooldown;
    // 스킬 설명문.
    public string desc;
    
    // Set as abstract to override with Another Data
    //public abstract void Execute(SkillContext context);
    // 구체 SkillLogic 파생형을 선택해 이 슬롯 내부에 저장한다. 별도 SO 참조가 아니다.
    // 에셋을 사용하는 개체들이 로직 인스턴스를 공유하므로 개체별 상태는 컴포넌트에 둔다.
    [SerializeReference, SubclassSelector] public SkillLogic logic; 
    public virtual void Execute(SkillContext context)
    {
        if(logic != null) logic.OnExecute(context);
    }
}

[Serializable]
// cooldown: 일반 스킬의 재사용 대기시간(초). 남은 시간은 SkillBehavior에서 별도 관리한다.
public class NormalSkillData : SkillData { public float cooldown = 10f; }

[Serializable]
// requiredPoint: 게임 체인저 사용에 필요한 포인트. 현재 누적 포인트와는 별개다.
public class GameChangerData : SkillData { public int requiredPoint = 1000; }

[Serializable]
// 추가 데이터 없이 공통 이름/아이콘/설명/로직만 갖는 패시브 슬롯 데이터.
public class PassiveSkillData : SkillData { }

[Serializable]
public abstract class SkillLogic
{
    public abstract void OnExecute(SkillContext context);

    /// <summary>
    /// Checked before execution. Returning false cancels the skill without consuming cooldown or point.
    /// Can also fill context (ex: resolve target).
    /// </summary>
    public virtual bool CanExecute(SkillContext context) { return true; }

    // Called when the skill is assigned to / removed from a SkillBehavior (ex: to attach a targeting indicator)
    // Logic instance is shared by every user of the persona asset, so keep runtime state on owner's components.
    public virtual void OnEquip(SkillBehavior owner, SkillSlot slot) { }
    public virtual void OnUnequip(SkillBehavior owner, SkillSlot slot) { }
}

// 스킬 실행마다 전달하는 실행 문맥. 페르소나 에셋에 저장되는 설정 데이터가 아니다.
public class SkillContext
{
    [Tooltip("The Subject who used the skill")]
    // 시전자 개체.
    public GameObject caster;
    [Tooltip("The Object who will be affected by the skill")]
    // 대상 개체. 로직에 따라 실행 가능 여부 검사에서 결정되거나 비어 있을 수 있다.
    public GameObject target;
    [Tooltip("Charge of Usage to present more powerful usage"), Range(0f, 1f)]
    // 충전 비율(0~1)을 전달하기 위한 값. 실제 사용 여부는 구체 스킬 로직에 따른다.
    public float charge;

    [Tooltip("Position of Usage")]
    // 실행 위치. 좌표의 해석 및 설정 여부는 호출부와 스킬 로직의 계약에 따른다.
    public Vector3 position;
    [Tooltip("Direction of Usage")]
    // 실행 방향. 모든 호출이 자동으로 정규화하거나 채우는 값은 아니다.
    public Vector3 direction;

    [Tooltip("For component access")]
    // 실행을 관리하는 SkillBehavior. 패시브 적용 등 일부 경로에서는 지정하지 않는다.
    public SkillBehavior owner;
}
