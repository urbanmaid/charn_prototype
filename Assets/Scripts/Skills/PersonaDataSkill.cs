
using UnityEngine;

[CreateAssetMenu(fileName = "PersonaDataSkill", menuName = "Scriptable Objects/PersonaDataSkill")]
// PersonaData가 참조하는 스킬 구성 에셋. 각 슬롯 데이터는 이 에셋 내부에 직렬화된다.
// 남은 쿨다운과 현재 게임 체인저 포인트는 SkillBehavior가 개체별로 관리한다.
public class PersonaDataSkill: ScriptableObject
{
    // 첫 번째 일반 스킬: 공통 정보/실행 로직과 재사용 대기시간(초).
    public NormalSkillData skill1;
    // 두 번째 일반 스킬: 첫 번째와 독립된 슬롯 및 쿨다운을 사용한다.
    public NormalSkillData skill2;
    // 포인트를 모아 사용하는 스킬: 공통 정보/실행 로직과 필요 포인트.
    public GameChangerData gameChanger;
    // 페르소나 스킬 적용 시 실행하는 패시브. 지속 효과의 구현은 연결된 로직이 담당한다.
    public PassiveSkillData passive;
}

/// <summary>
/// 스킬 적용·입력·조회에서 구분하는 슬롯 식별자. 에셋 참조 자체는 아니다.
/// </summary>
public enum SkillSlot
{
    Skill1,
    Skill2,
    GameChanger,
    Passive
}
