using System;
using UnityEngine;

/// <summary>모델과 분리해 보관하는 생존용 피격 형상. 좌표는 연결한 Humanoid 뼈 기준이다.</summary>
[CreateAssetMenu(fileName = "PersonaHitboxes", menuName = "Scriptable Objects/Persona Hitbox Profile")]
public sealed class PersonaHitboxProfile : ScriptableObject
{
    [Range(0, 31), Tooltip("피격 부위의 물리 레이어 번호. Lucia 래그돌의 Default(0)를 사용합니다. 루트의 적/아군 분류 레이어와 별개이며, 공격자의 사격 마스크에 포함되어야 합니다.")]
    // 생성할 각 뼈 충돌체의 레이어 번호. 비트 마스크 값이 아니다.
    public int hitboxLayer;

    // 생성할 Collider 종류. 선택한 종류에 해당하는 크기 필드만 사용한다.
    public enum Shape { Box, Sphere, Capsule }

    [Serializable]
    // 프로필 내부의 부위 한 개 정의. 별도 ScriptableObject나 피해 배율 데이터가 아니다.
    public class Part
    {
        [Tooltip("피격 충돌체를 직접 붙일 Humanoid 뼈입니다.")]
        // Animator의 Humanoid 뼈 식별자. 같은 프로필에서 중복 지정할 수 없다.
        public HumanBodyBones bone;
        // Box는 size, Sphere는 radius, Capsule은 radius/height/direction을 사용한다.
        public Shape shape;
        [Tooltip("뼈 로컬 좌표의 중심. 크기와 함께 씬에서 실제 외형에 맞게 조정합니다.")]
        // 뼈 로컬 좌표의 충돌체 중심. 모든 형상에 공통으로 적용한다.
        public Vector3 center;
        // 박스의 전체 폭/높이/깊이. 반지름이나 반쪽 크기가 아니며 각 축이 양수여야 한다.
        public Vector3 size = Vector3.one * .1f;
        // 구 또는 캡슐의 로컬 반지름. 뼈/모델 Transform 스케일의 영향을 받는다.
        [Min(.001f)] public float radius = .05f;
        // 캡슐 양 끝을 포함한 전체 길이. 유효성 검사에서 반지름의 2배 이상을 요구한다.
        [Min(.001f)] public float height = .2f;
        [Range(0, 2), Tooltip("캡슐 길이 방향: 0=X, 1=Y, 2=Z")]
        // 캡슐의 길이 축: 0=X, 1=Y, 2=Z. Box/Sphere에서는 사용하지 않는다.
        public int direction = 1;
    }

    [Tooltip("머리·골반·척추·양쪽 위/아래 팔·다리를 포함해야 합니다. 같은 뼈의 중복 항목은 허용하지 않습니다.")]
    // 생존 외형에 생성할 충돌체 목록. PersonaHitboxRig가 뼈/중복/형상 유효성을 검사한다.
    public Part[] parts = Array.Empty<Part>();
}
