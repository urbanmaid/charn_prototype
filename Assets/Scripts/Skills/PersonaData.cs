using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PersonaData", menuName = "Scriptable Objects/PersonaData")]
// 페르소나의 공통 정의 에셋. 현재 체력·남은 탄약·쿨다운 등 개체별 실행 상태는 컴포넌트가 관리한다.
public class PersonaData : ScriptableObject
{
    [Header("Basic Information")]
    // 표시 이름, 대표 아이콘, 설명문으로 사용하는 기본 정보.
    public string personaName = "";
    public Sprite icon = null;
    public string desc = "";

    //[Header("Ingame Information")]
    // 별도 에셋 참조가 아니라 이 PersonaData 안에 저장되는 기본 능력치 묶음.
    public PersonaDataGenericStat genericStatData;
    //public PersonaDataWeapon personaDataWeapon;
    // 일반 스킬 2개·게임 체인저·패시브를 담은 별도 ScriptableObject 참조.
    public PersonaDataSkill personaDataSkill;
    // 이 에셋에 내장된 외형 설정. 모델/무기 프리팹과 피격/자세 설정 에셋을 연결한다.
    public PersonaDataVisual personaDataVisual;
    //public PersonaDataDialogue personaDataDialogue;
}

/// <summary>
/// 페르소나에 내장되는 능력치. 필드가 존재해도 모든 값이 현재 동작에 반영되는 것은 아니다.
/// </summary>
[Serializable]
public class PersonaDataGenericStat // 별도 ScriptableObject가 아닌 직렬화 클래스.
{
    [Header("Basis")]
    // PlayerHealth/AllyHealthManager가 적용하는 최대 체력. 현재 체력은 각 컴포넌트에서 관리한다.
    public int health = 250;
    // 페르소나 적용 시 제공하는 기본 실드 체력. 현재 실드량과는 별개다.
    public int healthOfPreloadedShield = 0;

    [Header("Move Maneuver")]

    // 플레이어 걷기 속도 입력값. MoveBehaviour가 내부 속도 비율로 변환한다.
    public float moveSpeedMin = 2.4f;
    // 플레이어 달리기 속도 입력값이며, 아군은 NavMeshAgent.speed에 직접 적용한다.
    public float moveSpeedMax = 6.0f;
    // 플레이어 전력 질주 속도 입력값.
    public float moveSpeedSprint = 10f;
    // 이동 가속도용 예약값. 현재 Assets C#에서 이 필드를 읽는 코드는 확인되지 않았다.
    public float moveAcceleration = 1f;

    [Header("Jump Maneuver")]
    // 플레이어 MoveBehaviour에 적용하는 점프 높이.
    public float jumpHeight = 0.6f;
    // 점프 횟수용 예약값. 현재 읽는 코드가 없어 0의 동작 의미는 아직 정의되지 않았다.
    public int jumpAmount = 0;
    // 공중 조작 강도용 예약값. 현재 이 필드를 읽는 코드는 확인되지 않았다.
    public float airControl = 1f;
    // 정면/측면 벽 타기 허용용 예약값. 현재 두 필드를 읽는 코드는 확인되지 않았다.
    public bool useDirectWallclimb = false;
    public bool useSideWallclimb = false;

    [Header("Skill Relation Maneuver")]
    [Range(80f, 100f)]
    // 명령 수락률(%)용 예약값. 현재 읽는 코드가 없어 실제 명령 성공 확률에는 반영되지 않는다.
    public float acceptCommandRate = 97.5f;
    // 장비 수용량용 예약값. 현재 읽는 코드와 수량 단위는 확인되지 않았다.
    public int gadgetSpace = 60;
    // 근접 피해량용 예약값. 현재 이 필드를 읽는 코드는 확인되지 않았다.
    public int damageClose = 30;
}

[Serializable]
public class PersonaDataVisual // PersonaData 내부에 저장하는 외형/장착 정보.
{
    [Header("Basis")]
    // 생존 외형 프리팹. PersonaBehavior가 교체하고 Animator/뼈 참조를 다시 연결한다.
    public GameObject avatarMesh;
    // 빈사/사망 외형용 래그돌 프리팹. 생존용 피격 형상 설정과는 별개다.
    public GameObject avatarMeshRagdoll;
    [Tooltip("FSM 캐릭터의 생존용 부위 피격 설정. 미지정 페르소나는 기존 충돌체 방식을 유지합니다. 플레이어 외형에는 자동 적용하지 않습니다.")]
    // 생존 FSM 외형에 생성할 부위별 충돌체 정의 에셋.
    public PersonaHitboxProfile hitboxProfile;
    [Tooltip("지원 옵션을 켠 아군 FSM의 생존 상체 설정. 플레이어에는 적용하지 않습니다.")]
    // 지원 아군의 고정 조준값과 전용 무기 장착값. 생존 외형 적용 성공 후 채택한다.
    public AllyPoseProfile allyPoseProfile;
    // 무기 프리팹 목록. 플레이어는 목록을 전달받고, EnemyAnimation은 첫 항목만 사용한다.
    public GameObject[] avatarWeapons;
    //public UniVRM10 vrmMesh;
    // BasicBehaviour가 CapsuleCollider.height에 직접 대입하는 Unity 길이 값.
    // 기본값 170을 cm로 자동 환산하지 않으므로 실제 에셋 값과 모델 스케일을 확인해야 한다.
    public float height = 170f;

    [Header("기본 무기 장착")]
    [InspectorName("무기 위치")]
    [Tooltip("플레이어와 기본 FSM 경로가 사용하는 장착 뼈 기준 로컬 위치입니다. 지원 아군에서 AllyPoseProfile의 장착 덮어쓰기를 켜면 아군 전용값이 우선합니다.")]
    // 장착 덮어쓰기를 사용하는 경우의 무기 로컬 위치. 지원 아군의 전용 설정이 우선할 수 있다.
    public Vector3 weaponPositionOverride;
    [InspectorName("무기 회전")]
    [Tooltip("장착 뼈 기준 무기 로컬 회전(도)입니다. 왼팔·손목 파지 보정과는 별개이며 지원 아군은 전용 장착값을 사용할 수 있습니다.")]
    // 같은 장착 기준의 로컬 오일러 회전(도). 플레이어와 FSM이 각 장착 경로에서 사용한다.
    public Vector3 weaponRotationOverride;

    [Header("Aim - 플레이어 조준·사격 파지")]
    [InspectorName("왼손목 회전")]
    [Tooltip("플레이어 LONG 조준·사격 중 왼손목에 더하는 로컬 회전(도)입니다. 엄폐 중 조준에도 사용하며, 아군 IK에는 적용하지 않습니다. 0이면 추가 회전이 없습니다.")]
    // 손 위치나 팔 길이는 바꾸지 않는다. 손바닥 방향만 미세 조정하는 선택형 보정값.
    public Vector3 playerLeftWristAimEuler;

    [InspectorName("왼쪽 상완 회전")]
    [Tooltip("플레이어 LONG 조준·사격 중 위팔에 더하는 로컬 회전(도)입니다. 손바닥 위치를 맞추는 고정값이며 실시간 IK는 사용하지 않습니다.")]
    public Vector3 playerLeftUpperArmAimEuler;
    [InspectorName("왼쪽 전완 회전")]
    [Tooltip("플레이어 LONG 조준·사격 중 아래팔에 더하는 로컬 회전(도)입니다. 뼈 길이를 바꾸지 않고 팔을 회전시켜 손 위치를 조정합니다.")]
    public Vector3 playerLeftForearmAimEuler;
    [InspectorName("왼손가락 굽힘")]
    [Tooltip("플레이어 LONG 조준 중 왼손 검지~소지에 더하는 굽힘(도). X=첫 마디, Y=둘째 마디, Z=끝 마디. 각 뼈의 로컬 Z축에 적용하며 엄지는 유지합니다.")]
    public Vector3 playerLeftFingerCurl;

    [Header("Cover - 플레이어 엄폐 파지")]
    [InspectorName("엄폐 파지 사용")]
    [Tooltip("플레이어 LONG 무기의 웅크린 엄폐 파지를 아래 값으로 보정합니다. 끄면 엄폐 전용 보정만 해제하며 Aim의 조준·사격 설정은 유지합니다.")]
    public bool playerCoverGripEnabled;
    [InspectorName("왼쪽 상완 회전")]
    [Tooltip("웅크린 엄폐 자세의 위팔에 더하는 로컬 회전(도)입니다. 엄폐 파지 사용이 켜져 있을 때 적용합니다.")]
    // 현재 엄폐 애니메이션에 더하는 왼팔/손목 로컬 회전. 조준용 값과 별도로 작성한다.
    public Vector3 playerLeftUpperArmCoverEuler;
    [InspectorName("왼쪽 전완 회전")]
    [Tooltip("웅크린 엄폐 자세의 아래팔에 더하는 로컬 회전(도)입니다. 조준용 전완 설정과 별개입니다.")]
    public Vector3 playerLeftForearmCoverEuler;
    [InspectorName("왼손목 회전")]
    [Tooltip("웅크린 엄폐 자세의 왼손목에 더하는 로컬 회전(도)입니다. 조준용 손목 설정과 별개입니다.")]
    public Vector3 playerLeftWristCoverEuler;
    [InspectorName("왼손가락 굽힘")]
    [Tooltip("엄폐 중 검지~소지 굽힘(도). X=첫 마디, Y=둘째 마디, Z=끝 마디. 엄지는 유지합니다.")]
    public Vector3 playerLeftCoverFingerCurl;

    [Header("FSM 기본 총구 축")]
    [InspectorName("총열 전방 축")]
    [Tooltip("FSM이 실제 총열 전방으로 읽는 총구 로컬 방향입니다. 영벡터는 +Z를 사용하며 지원 아군의 전용 장착 설정에 유효한 축이 있으면 그 축이 우선합니다. 플레이어 파지값이 아닙니다.")]
    // FSM이 muzzle의 로컬 축 중 실제 총열 전방으로 해석할 방향. 영벡터는 +Z로 대체한다.
    // 지원 아군의 AllyPoseProfile 장착 설정에 유효한 축이 있으면 그 축이 우선한다.
    public Vector3 weaponMuzzleForwardAxis = Vector3.forward;
}
