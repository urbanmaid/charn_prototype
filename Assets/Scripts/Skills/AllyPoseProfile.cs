using UnityEngine;

public interface IUpperBodyPoseCorrection
{
    // 목표 방향과 작성된 보정값으로 추가 회전만 계산한다. 총구 자세는 입력받지 않는다.
    Quaternion Evaluate(Vector3 localDirection, bool aiming);
    float DegreesPerSecond { get; }
}

[CreateAssetMenu(fileName = "AllyPoseProfile", menuName = "Persona/Ally Pose Profile")]
// PersonaDataVisual에서 참조하는 아군 전용 자세/장착 에셋. 플레이어 조작 자세에는 적용하지 않는다.
public class AllyPoseProfile : ScriptableObject, IUpperBodyPoseCorrection
{
    [Tooltip("이 설정을 연결한 지원 아군의 상체 보정을 사용합니다.")]
    // 상체 보정 사용 여부. 아래 무기 장착 덮어쓰기 옵션과는 독립적이다.
    public bool correctionEnabled = true;
    // 목표 방향에 고정값을 더한 최종 좌우 각도의 대칭 제한(도).
    [Range(0f, 90f)] public float maxYaw = 60f;
    // 목표 방향에 고정값을 더한 최종 상하 각도의 대칭 제한(도).
    [Range(0f, 90f)] public float maxPitch = 45f;
    // 초당 회전량(도/초). 0이면 보간이 멈추며 즉시 적용을 뜻하지 않는다.
    [Min(0f)] public float degreesPerSecond = 180f;
    [Tooltip("캐릭터별 고정 조준 보정값(도). X는 상하, Y는 좌우입니다. 총구 움직임에 따라 자동 변경하지 않습니다.")]
    // X=상하, Y=좌우 오일러 각도(도). 목표 방향의 각도에 더한 뒤 제한한다.
    // 런타임에서 총구 오차를 따라 수정하지 않는 작성값이다.
    public Vector2 fixedAimOffset;

    [Header("아군 무기 장착")]
    [Tooltip("지원 아군만 아래 장착값을 사용합니다. 플레이어와 미지원 아군은 PersonaData의 기존 값을 사용합니다.")]
    // true이면 지원 아군의 PersonaDataVisual 공통 장착값 대신 아래 값을 사용한다.
    public bool overrideWeaponAttachment;
    // 무기 부모(장착 뼈) 기준 로컬 위치. 월드 좌표가 아니다.
    public Vector3 weaponLocalPosition;
    // 동일 장착 기준의 로컬 오일러 회전(도).
    public Vector3 weaponLocalEulerAngles;
    [Tooltip("muzzle Transform 로컬 기준 실제 총열 방향. 영벡터는 기존 PersonaData 축으로 대체합니다.")]
    // 실제 총열 전방을 나타내는 muzzle 로컬 방향. 정렬 측정용이며 동적 보정 입력이 아니다.
    public Vector3 muzzleForwardAxis = Vector3.forward;

    [Header("아군 양손 파지")]
    [Tooltip("조준 중 왼팔/왼손과 양손 손가락에 작성된 추가 회전을 적용합니다. 장전 중에는 해제합니다.")]
    public bool gripEnabled;
    [Min(.01f), Tooltip("파지 보정이 완전히 적용되거나 해제되는 데 걸리는 시간(초).")]
    public float gripBlendSeconds = .15f;
    [System.Serializable]
    public struct GripBoneOffset
    {
        // 총을 자식으로 가진 오른손/오른팔과 상체는 허용하지 않아 총열 방향을 보존한다.
        public HumanBodyBones bone;
        [Tooltip("현재 애니메이션 회전에 더할 뼈 로컬 오일러 회전(도). 모델별로 작성합니다.")]
        public Vector3 localEulerOffset;
    }
    [Tooltip("왼팔/왼손 및 양손 손가락만 지원합니다. 같은 뼈는 첫 항목만 적용합니다.")]
    public GripBoneOffset[] gripOffsets = System.Array.Empty<GripBoneOffset>();

    [Header("왼손 받침 IK")]
    [Tooltip("무기 기준 받침점에 왼팔만 맞춥니다. 왼팔/손목의 고정 회전은 대체하고 손가락 보정은 유지합니다.")]
    public bool leftHandIKEnabled;
    [Tooltip("무기 프리팹 루트 로컬 기준 왼손 뼈의 목표 위치. 손바닥 중심 좌표가 아닙니다.")]
    public Vector3 leftHandGripPosition;
    [Tooltip("무기 루트 기준 왼손 뼈의 목표 오일러 회전(도).")]
    public Vector3 leftHandGripEuler;

    public static bool IsGripBone(HumanBodyBones bone)
    {
        return bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.LeftLowerArm ||
            bone == HumanBodyBones.LeftHand ||
            (bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.LeftLittleDistal) ||
            (bone >= HumanBodyBones.RightThumbProximal && bone <= HumanBodyBones.RightLittleDistal);
    }

    public float DegreesPerSecond => Mathf.Max(0f, degreesPerSecond);

    public Quaternion Evaluate(Vector3 localDirection, bool aiming)
    {
        if (!aiming || !float.IsFinite(localDirection.x) || !float.IsFinite(localDirection.y) ||
            !float.IsFinite(localDirection.z) || localDirection.sqrMagnitude < .0001f)
            return Quaternion.identity;
        float yawLimit = Mathf.Clamp(maxYaw, 0f, 90f);
        float pitchLimit = Mathf.Clamp(maxPitch, 0f, 90f);
        float yawOffset = float.IsFinite(fixedAimOffset.y) ? fixedAimOffset.y : 0f;
        float pitchOffset = float.IsFinite(fixedAimOffset.x) ? fixedAimOffset.x : 0f;
        float yaw = Mathf.Clamp(Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg + yawOffset, -yawLimit, yawLimit);
        float pitch = Mathf.Clamp(-Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg + pitchOffset, -pitchLimit, pitchLimit);
        return Quaternion.Euler(pitch, yaw, 0f);
    }

}
