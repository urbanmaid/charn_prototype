using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Test integration of Kevin Iglesias' HumanF@ThrowBall01_R. Keep the original controller asset intact.
[DefaultExecutionOrder(600)]
public sealed class AmeliaGrenadeThrowMotion : MonoBehaviour
{
    public bool IsThrowing { get; private set; }
    public bool IsPreparingRelease => IsThrowing && !released;
    public Vector3 PreviewOrigin => prop ? prop.transform.position : transform.position + Vector3.up * 1.4f;
    public Vector3 PreviewTarget => throwTarget;
    private Animator animator;
    private ShootBehaviour shoot;
    private PlayableGraph graph;
    private AnimatorControllerPlayable controller;
    private AnimationLayerMixerPlayable mixer;
    private AnimationClipPlayable motion;
    private AvatarMask mask;
    private AvatarMask gripMask;
    private bool hasGrip;
    private Transform hand;
    private GameObject prop;
    private Action<Vector3> release;
    private float elapsed, length, releaseTime;
    private const float PlaybackSpeed = 1.65f;
    private const float ClipStart = .08f, ClipEnd = .85f, ClipRelease = .48f;
    private bool released, rootMotion;
    private readonly List<Renderer> hidden = new();
    private AnimatorStateInfo[] states;
    private float[] weights;
    private Vector3 throwTarget;

    public static bool Active(GameObject owner) =>
        owner && owner.TryGetComponent<AmeliaGrenadeThrowMotion>(out var component) && component.IsThrowing;

    public static bool Play(GameObject owner, Vector3 target, Action<Vector3> onRelease)
    {
        var basic = owner.GetComponent<BasicBehaviour>();
        var anim = basic ? basic.GetAnim : owner.GetComponentInChildren<Animator>();
        AnimationClip clip = null;
        foreach (var candidate in Resources.LoadAll<AnimationClip>("AmeliaThrowTest"))
            if (!candidate.name.StartsWith("__preview__")) { clip = candidate; break; }
        if (!anim || !anim.isHuman || !anim.runtimeAnimatorController || !clip) return false;
        var component = owner.GetComponent<AmeliaGrenadeThrowMotion>();
        if (!component) component = owner.AddComponent<AmeliaGrenadeThrowMotion>();
        if (component.IsThrowing) return false;
        component.throwTarget = target;
        component.Begin(anim, clip, onRelease);
        return true;
    }

    private void Begin(Animator anim, AnimationClip clip, Action<Vector3> onRelease)
    {
        animator = anim;
        hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        release = onRelease;
        elapsed = 0f; released = false;
        // Trim the long anticipation/recovery of the ball throw for a quick combat toss.
        length = clip.length * (ClipEnd - ClipStart) / PlaybackSpeed;
        releaseTime = clip.length * (ClipRelease - ClipStart) / PlaybackSpeed;
        rootMotion = anim.applyRootMotion;
        states = new AnimatorStateInfo[anim.layerCount];
        weights = new float[anim.layerCount];
        for (int i = 0; i < states.Length; i++) {
            states[i] = anim.GetCurrentAnimatorStateInfo(i);
            weights[i] = anim.GetLayerWeight(i);
        }
        shoot = GetComponent<ShootBehaviour>();
        if (shoot) {
            shoot.AddShootBlock();
            var weapon = shoot.CurrentWeaponForFeedback;
            if (weapon) foreach (var r in weapon.GetComponentsInChildren<Renderer>())
                if (r.enabled) { hidden.Add(r); r.enabled = false; }
        }
        IsThrowing = true;
        anim.applyRootMotion = false;
        graph = PlayableGraph.Create("Amelia Grenade Throw");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        controller = AnimatorControllerPlayable.Create(graph, anim.runtimeAnimatorController);
        for (int i = 0; i < states.Length; i++) {
            controller.Play(states[i].fullPathHash, i, states[i].normalizedTime);
            controller.SetLayerWeight(i, weights[i]);
        }
        mixer = AnimationLayerMixerPlayable.Create(graph, 3);
        motion = AnimationClipPlayable.Create(graph, clip);
        motion.SetTime(clip.length * ClipStart);
        motion.SetSpeed(PlaybackSpeed);
        motion.SetApplyFootIK(false);
        motion.SetApplyPlayableIK(false);
        graph.Connect(controller, 0, mixer, 0);
        graph.Connect(motion, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        mixer.SetLayerMaskFromAvatarMask(1, mask);
        hasGrip = false;
        foreach (var clipGrip in Resources.LoadAll<AnimationClip>("AmeliaGrenadeGrip")) {
            if (clipGrip.name.StartsWith("__preview__")) continue;
            var grip = AnimationClipPlayable.Create(graph, clipGrip);
            grip.SetSpeed(0);
            grip.SetTime(clipGrip.length * .5f);
            graph.Connect(grip, 0, mixer, 2);
            gripMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                gripMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            gripMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mixer.SetLayerMaskFromAvatarMask(2, gripMask);
            mixer.SetInputWeight(2, 1f);
            hasGrip = true;
            break;
        }
        AnimationPlayableOutput.Create(graph, "Throw", anim).SetSourcePlayable(mixer);
        prop = new GameObject("Held Cluster Grenade");
        prop.transform.SetParent(hand ? hand : transform, false);
        // A scaled character rig must not enlarge the grenade in the hand.
        Vector3 parentScale = prop.transform.parent.lossyScale;
        prop.transform.localScale = new Vector3(
            1f / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
            1f / Mathf.Max(.001f, Mathf.Abs(parentScale.y)),
            1f / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
        prop.transform.localPosition = new Vector3(0f, -.015f, .045f);
        AmeliaGrenadeVfx.BuildShell(prop.transform, false, held: true);
        // Fit the prop between the palm and proximal fingers, independent of hand bone axes.
        var middle = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        var index = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        var pinky = anim.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand && middle && index && pinky) {
            Vector3 along = (middle.position - hand.position).normalized;
            Vector3 across = (index.position - pinky.position).normalized;
            Vector3 palm = Vector3.Cross(along, across).normalized;
            prop.transform.position = Vector3.Lerp(hand.position, middle.position, .65f) + palm * .025f;
            prop.transform.rotation = Quaternion.LookRotation(palm, along);
        }
        graph.Play();
    }

    private void Update()
    {
        if (!IsThrowing) return;
        var health = GetComponent<HealthManager>();
        if (!animator || !animator.isActiveAndEnabled || (health && health.dead)) { Finish(); return; }
        elapsed += Time.deltaTime;
        FaceThrowDirection();
        float fade = Mathf.Min(Mathf.SmoothStep(0f, 1f, elapsed / .12f),
            Mathf.SmoothStep(0f, 1f, (length - elapsed) / .18f));
        mixer.SetInputWeight(1, fade * .85f);
        if (hasGrip) mixer.SetInputWeight(2, released ? Mathf.Clamp01(1f - (elapsed - releaseTime) / .09f) : 1f);
        foreach (var p in animator.parameters) {
            if (p.type == AnimatorControllerParameterType.Float) controller.SetFloat(p.nameHash, animator.GetFloat(p.nameHash));
            else if (p.type == AnimatorControllerParameterType.Bool) controller.SetBool(p.nameHash, animator.GetBool(p.nameHash));
            else if (p.type == AnimatorControllerParameterType.Int) controller.SetInteger(p.nameHash, animator.GetInteger(p.nameHash));
        }
    }

    private void LateUpdate()
    {
        if (!IsThrowing) return;
        FaceThrowDirection();
        // Release stays aligned to the same source pose when trimming or changing playback speed.
        if (!released && elapsed >= releaseTime) {
            released = true;
            Vector3 origin = prop ? prop.transform.position : transform.position + Vector3.up * 1.4f;
            if (prop) Destroy(prop);
            release?.Invoke(origin);
            release = null;
        }
        if (elapsed >= length) Finish();
    }

    private int lastTurnFrame = -1;
    private void FaceThrowDirection()
    {
        Vector3 direction = throwTarget - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .001f) return;
        direction.Normalize();
        var basic = GetComponent<BasicBehaviour>();
        if (basic) basic.SetLastDirection(direction);
        // Keep the throw aligned even if movement/aim updates rotation later in the frame.
        if (lastTurnFrame != Time.frameCount) {
            lastTurnFrame = Time.frameCount;
            throwRotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 900f * Time.deltaTime);
        }
        transform.rotation = throwRotation;
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.rotation = throwRotation;
    }
    private Quaternion throwRotation;

    private void Finish()
    {
        if (!IsThrowing) return;
        IsThrowing = false;
        if (graph.IsValid()) {
            // Resume the live locomotion state instead of rewinding to the throw's starting frame.
            for (int i = 0; i < states.Length; i++)
                states[i] = controller.GetCurrentAnimatorStateInfo(i);
            graph.Destroy();
        }
        if (animator) {
            animator.applyRootMotion = rootMotion;
            for (int i = 0; i < states.Length && i < animator.layerCount; i++) {
                animator.Play(states[i].fullPathHash, i, states[i].normalizedTime);
                animator.SetLayerWeight(i, weights[i]);
            }
        }
        if (mask) Destroy(mask);
        if (gripMask) Destroy(gripMask);
        if (prop) Destroy(prop);
        foreach (var r in hidden) if (r) r.enabled = true;
        hidden.Clear();
        if (shoot) shoot.RemoveShootBlock();
        release = null;
    }
    private void OnDisable() => Finish();
    private void OnDestroy() => Finish();
}
