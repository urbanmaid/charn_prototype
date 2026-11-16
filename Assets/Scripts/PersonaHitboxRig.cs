using System.Collections.Generic;
using UnityEngine;

/// <summary>이 인스턴스가 생성한 피격 충돌체의 소유권과 준비 상태를 관리한다.</summary>
public sealed class PersonaHitboxRig : MonoBehaviour
{
    public PersonaHitboxProfile Profile { get; private set; }
    private Collider[] hitboxes;
    private static readonly HumanBodyBones[] RequiredBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg
    };

    public bool IsReady
    {
        get
        {
            if (!gameObject.activeInHierarchy || hitboxes == null || hitboxes.Length == 0) return false;
            foreach (Collider col in hitboxes)
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) return false;
            return true;
        }
    }

    public static bool TryCreate(GameObject model, Animator animator, PersonaHitboxProfile profile,
        out string failure)
    {
        failure = null;
        if (profile == null || profile.parts == null || profile.parts.Length == 0 || profile.hitboxLayer < 0 || profile.hitboxLayer > 31)
        {
            failure = "피격 설정이 비어 있습니다.";
            return false;
        }
        // 일부만 생성한 뒤 실패하지 않도록 전체 설정/뼈를 먼저 검사한다.
        var bones = new HashSet<HumanBodyBones>();
        foreach (var part in profile.parts)
        {
            if (part == null || (int)part.bone < 0 || part.bone >= HumanBodyBones.LastBone ||
                !bones.Add(part.bone) || !ValidShape(part))
            {
                failure = "잘못된 크기·형상·뼈 또는 중복 뼈가 있습니다.";
                return false;
            }
            Transform bone = animator.GetBoneTransform(part.bone);
            if (bone == null || !bone.IsChildOf(model.transform) || bone.GetComponent<Collider>() != null)
            {
                failure = $"{part.bone}: 뼈가 없거나 이미 충돌체가 있습니다.";
                return false;
            }
        }
        foreach (var required in RequiredBones)
            if (!bones.Contains(required))
            {
                failure = $"필수 피격 부위 {required}가 없습니다.";
                return false;
            }

        var rig = model.AddComponent<PersonaHitboxRig>();
        rig.Profile = profile;
        rig.hitboxes = new Collider[profile.parts.Length];
        for (int i = 0; i < profile.parts.Length; i++)
        {
            var part = profile.parts[i];
            GameObject bone = animator.GetBoneTransform(part.bone).gameObject;
            bone.layer = profile.hitboxLayer;
            Collider col;
            switch (part.shape)
            {
                case PersonaHitboxProfile.Shape.Box:
                    var box = bone.AddComponent<BoxCollider>();
                    box.center = part.center; box.size = part.size; col = box;
                    break;
                case PersonaHitboxProfile.Shape.Sphere:
                    var sphere = bone.AddComponent<SphereCollider>();
                    sphere.center = part.center; sphere.radius = part.radius; col = sphere;
                    break;
                default:
                    var capsule = bone.AddComponent<CapsuleCollider>();
                    capsule.center = part.center; capsule.radius = part.radius;
                    capsule.height = part.height; capsule.direction = part.direction; col = capsule;
                    break;
            }
            // Unity 6의 접촉 레이어 제외는 Raycast와 별개다. 팔다리가 벽/동료를 밀지 않게 한다.
            // Rigidbody/Joint 및 DamageProxy를 추가하지 않는다. 피해 수신자는 루트 하나다.
            col.isTrigger = false;
            col.includeLayers = 0;
            col.excludeLayers = ~0;
            col.layerOverridePriority = 64;
            rig.hitboxes[i] = col;
        }
        return true;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private static bool ValidShape(PersonaHitboxProfile.Part part)
    {
        if (!Finite(part.center)) return false;
        switch (part.shape)
        {
            case PersonaHitboxProfile.Shape.Box:
                return Finite(part.size) && part.size.x > 0 && part.size.y > 0 && part.size.z > 0;
            case PersonaHitboxProfile.Shape.Sphere:
                return float.IsFinite(part.radius) && part.radius > 0;
            case PersonaHitboxProfile.Shape.Capsule:
                return float.IsFinite(part.radius) && part.radius > 0 && float.IsFinite(part.height) &&
                    part.height >= part.radius * 2 && part.direction >= 0 && part.direction <= 2;
            default: return false;
        }
    }
}
