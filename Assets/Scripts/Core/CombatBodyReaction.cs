using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public sealed class CombatBodyReaction : MonoBehaviour
{
    private enum Part { Head, Torso, LeftArm, RightArm }

    private sealed class BonePose
    {
        public Transform bone;
        public Vector3 bend;
        public Quaternion applied, baseRotation;
        public bool poseApplied;
    }

    private readonly Dictionary<Transform, BonePose> poses = new Dictionary<Transform, BonePose>();

    public static void Hit(Animator animator, Vector3 direction, float strength, Vector3 contact, Collider bodyPart, bool confirmedHeadshot)
    {
        if (!animator || !animator.isHuman || !animator.enabled) return;
        var reaction = animator.GetComponent<CombatBodyReaction>();
        if (!reaction) reaction = animator.gameObject.AddComponent<CombatBodyReaction>();
        var latePass = animator.GetComponent<CombatPoseLatePass>();
        if (!latePass) latePass = animator.gameObject.AddComponent<CombatPoseLatePass>();
        latePass.reaction = reaction;
        reaction.enabled = true;

        Part part = ResolvePart(animator, bodyPart, contact, confirmedHeadshot);
        Transform bone = BoneFor(animator, part);
        if (!bone)
        {
            part = Part.Torso;
            bone = BoneFor(animator, part);
        }
        if (!bone) return;

        if (!reaction.poses.TryGetValue(bone, out BonePose pose))
        {
            pose = new BonePose { bone = bone };
            reaction.poses.Add(bone, pose);
        }
        Restore(pose);

        Vector3 local = animator.transform.InverseTransformDirection(direction.normalized);
        float side = Mathf.Clamp(animator.transform.InverseTransformPoint(contact).x * 2f, -1f, 1f);
        Vector3 bend = part switch
        {
            Part.Head => new Vector3(local.z * 7f, local.x * 6f, -local.x * 5f),
            Part.LeftArm => new Vector3(local.z * 4f, local.x * 3f, -7f - Mathf.Abs(local.x) * 2f),
            Part.RightArm => new Vector3(local.z * 4f, local.x * 3f, 7f + Mathf.Abs(local.x) * 2f),
            _ => new Vector3(local.z * 5f, local.x * 2f, -local.x * 4f - side * 3f)
        };
        pose.bend = Vector3.ClampMagnitude(pose.bend + bend * Mathf.Clamp(strength, 0.3f, 1f), part == Part.Torso ? 7f : 10f);
    }

    private static Part ResolvePart(Animator animator, Collider bodyPart, Vector3 contact, bool confirmedHeadshot)
    {
        if (confirmedHeadshot || IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.Head))) return Part.Head;
        if (IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)) ||
            IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm)) ||
            IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.LeftHand))) return Part.LeftArm;
        if (IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.RightUpperArm)) ||
            IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.RightLowerArm)) ||
            IsUnder(bodyPart, animator.GetBoneTransform(HumanBodyBones.RightHand))) return Part.RightArm;

        Vector3 local = animator.transform.InverseTransformPoint(contact);
        if (local.y > 1.45f) return Part.Head;
        if (local.y > 0.75f && Mathf.Abs(local.x) > 0.38f)
            return local.x < 0f ? Part.LeftArm : Part.RightArm;
        return Part.Torso;
    }

    private static bool IsUnder(Collider collider, Transform bone) =>
        collider && bone && (collider.transform == bone || collider.transform.IsChildOf(bone));

    private static Transform BoneFor(Animator animator, Part part)
    {
        switch (part)
        {
            case Part.Head: return animator.GetBoneTransform(HumanBodyBones.Head);
            case Part.LeftArm: return animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            case Part.RightArm: return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            default:
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                return chest ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }

    private void Update()
    {
        bool moving = false;
        foreach (BonePose pose in poses.Values)
        {
            Restore(pose);
            pose.bend *= Mathf.Exp(-16f * Time.deltaTime);
            if (pose.bend.sqrMagnitude < .0001f) pose.bend = Vector3.zero;
            else moving = true;
        }
        if (!moving) enabled = false;
    }

    public void ApplyPose()
    {
        foreach (BonePose pose in poses.Values)
        {
            if (!pose.bone) continue;
            pose.baseRotation = pose.bone.localRotation;
            pose.applied = pose.baseRotation * Quaternion.Euler(pose.bend);
            pose.bone.localRotation = pose.applied;
            pose.poseApplied = true;
        }
    }

    private static void Restore(BonePose pose)
    {
        if (pose.poseApplied && pose.bone && Quaternion.Angle(pose.bone.localRotation, pose.applied) < 0.01f)
            pose.bone.localRotation = pose.baseRotation;
        pose.poseApplied = false;
    }

    private void RestoreAll()
    {
        foreach (BonePose pose in poses.Values)
        {
            Restore(pose);
            pose.bend = Vector3.zero;
        }
    }

    private void OnDisable() => RestoreAll();
    public void StopBeforeRagdoll() { RestoreAll(); enabled = false; }
}
