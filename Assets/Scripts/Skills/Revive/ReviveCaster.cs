using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Channelled cast for revive skills, animated like Overwatch Mercy's resurrect: the gun is put away, the caster
/// gathers light at the chest, then floats up and slowly reaches one hand toward the fallen ally (body turned
/// to it, the other arm trailing behind) and pushes the palm out as the cast completes, while light gathers
/// with beams to targets; the action runs when the cast completes.
/// Player can cancel with the attack button (left click); cancelled also if the caster dies.
/// Shooting is blocked while casting. Added at runtime to the caster.
/// </summary>
public class ReviveCaster : MonoBehaviour
{
    public bool IsCasting { get; private set; }

    // 시전이 끝나도 팔 자세의 복귀가 남아 있으면 파지 보정에 제어권을 넘기지 않는다.
    // LateUpdate의 실제 자세 적용 종료 기준과 동일하게 판단한다.
    public bool ControlsArmPose => isActiveAndEnabled &&
        (IsCasting || Mathf.SmoothStep(0f, 1f, poseWeight) > 0.001f);

    private float castTime, elapsed;
    private float moveMultiplier;
    private Action onComplete, onCancelled;
    private ShootBehaviour shoot;
    private InputAction cancelAction;
    private HealthManager casterHealth;
    private MoveBehaviour move;
    private NavMeshAgent agent;
    private float agentBaseSpeed;
    private Animator animator;
    private float poseWeight;
    private IList<Transform> targets;
    private readonly List<HealthManager> heldCountdowns = new();
    private Transform avatarRoot;
    private Vector3 avatarBase;
    private readonly List<Renderer> hiddenWeapon = new();
    private EnemyAI.EnemyAnimation aiWeapon;
    private const float Lift = 0.15f;             // Rises slightly off the ground while casting
    private GameObject castVfx;
    private AudioSource castSound;

    // Cast bar (player only)
    private GameObject barCanvas;
    private Image barFill;

    /// <summary>
    /// Start casting. moveMultiplier 0.3 = 30% move speed while casting.
    /// </summary>
    public bool Begin(float duration, float moveSpeedMultiplier, IList<Transform> beamTargets, Action complete, Action cancelled = null)
    {
        if (IsCasting) return false;

        casterHealth = GetComponent<HealthManager>();
        move = GetComponent<MoveBehaviour>();
        agent = GetComponent<NavMeshAgent>();
        animator = FindAvatarAnimator();
        targets = beamTargets;
        HideWeapon();
        if (animator != null && animator.transform != transform)
        {
            avatarRoot = animator.transform;
            avatarBase = avatarRoot.localPosition;
        }

        castTime = Mathf.Max(0.05f, duration);
        elapsed = 0f;
        moveMultiplier = Mathf.Clamp(moveSpeedMultiplier, 0.01f, 1f);
        onComplete = complete;
        onCancelled = cancelled;
        IsCasting = true;
        heldCountdowns.Clear();
        if (beamTargets != null)
            foreach (var target in beamTargets)
            {
                var health = target != null ? target.GetComponent<HealthManager>() : null;
                if (health == null || !health.dead || heldCountdowns.Contains(health)) continue;
                ReviveWindow.HoldCountdown(health, this);
                heldCountdowns.Add(health);
            }

        // 런타임에 추가되는 컴포넌트이므로 시작 시 한 번 연결한다. 매 프레임 검색하지 않는다.
        GetComponent<ShootBehaviour>()?.SetRevivePoseSource(this);

        // Player: attack button cancels, and must not shoot while casting
        if (CompareTag("Player"))
        {
            shoot = GetComponent<ShootBehaviour>();
            if (shoot != null) shoot.AddShootBlock();
            PlayerInput input = GetComponent<PlayerInput>();
            cancelAction = input != null ? input.actions.FindAction("Attack") : null;
        }

        // Slow down
        if (move != null) move.AddSpeedModifier(moveMultiplier);
        if (agent != null)
        {
            agentBaseSpeed = agent.speed;
            agent.speed = agentBaseSpeed * moveMultiplier;
        }

        castVfx = SkillVfxFactory.SpawnReviveCast(transform, beamTargets, castTime);
        castSound = SkillSfx.PlayAttached("lucia_revive_cast", transform, 0.6f);   // Choir swell peaking as the cast completes

        if (CompareTag("Player"))
            ShowCastBar();

        return true;
    }

    private void Update()
    {
        // Pose blends in while casting and out afterwards; the gun comes back once the arms are down
        poseWeight = Mathf.MoveTowards(poseWeight, IsCasting ? 1f : 0f, Time.deltaTime * (IsCasting ? 5f : 3f));
        if (!IsCasting && poseWeight <= 0.25f) ShowWeapon();

        if (!IsCasting) return;

        if (casterHealth != null && casterHealth.dead)
        {
            Finish(false);
            return;
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            Cancel();
            return;
        }

        elapsed += Time.deltaTime;
        if (barFill) barFill.fillAmount = Mathf.Clamp01(elapsed / castTime);

        if (elapsed >= castTime)
            Finish(true);
    }

    /// <summary> Cancel current cast (runs the cancelled callback, ex: refund). </summary>
    public void Cancel()
    {
        if (!IsCasting) return;
        Finish(false);
        if (CompareTag("Player")) SkillToast.Show("REVIVE CANCELLED", Color.white);
        SkillSfx.Play("skill_cancel", transform.position, 0.7f);
    }

    private void Finish(bool completed)
    {
        IsCasting = false;

        if (move != null) move.RemoveSpeedModifier(moveMultiplier);
        if (shoot != null) shoot.RemoveShootBlock();
        shoot = null;
        cancelAction = null;
        if (agent != null) agent.speed = agentBaseSpeed;
        if (barCanvas) Destroy(barCanvas);
        if (!completed && castVfx) Destroy(castVfx);
        if (!completed) SkillSfx.Stop(castSound, 0.25f);   // On success the swell rings out into the revive
        castSound = null;

        Action action = completed ? onComplete : onCancelled;
        onComplete = null;
        onCancelled = null;
        try { action?.Invoke(); }
        finally
        {
            foreach (var health in heldCountdowns) ReviveWindow.ReleaseCountdown(health, this);
            heldCountdowns.Clear();
        }
    }

    private void OnDisable()
    {
        if (IsCasting) Finish(false);
        poseWeight = 0f;
        ShowWeapon();
        if (avatarRoot != null) avatarRoot.localPosition = avatarBase;
    }

    #region Pose
    private Animator FindAvatarAnimator()
    {
        BasicBehaviour basic = GetComponent<BasicBehaviour>();
        Animator anim = basic != null ? basic.GetAnim : null;
        if (anim == null || !anim.isHuman) anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            foreach (Animator a in GetComponentsInChildren<Animator>())
                if (a.isHuman && a.avatar != null) { anim = a; break; }
        }
        return anim != null && anim.isHuman ? anim : null;
    }

    // Put the gun away: player weapon renderers are hidden, AI uses its weapon toggle
    private void HideWeapon()
    {
        ShowWeapon();
        ShootBehaviour gun = GetComponent<ShootBehaviour>();
        InteractiveWeapon weapon = gun != null && gun.GunMuzzle != null ? gun.GunMuzzle.GetComponentInParent<InteractiveWeapon>() : null;
        if (weapon != null)
        {
            foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenWeapon.Add(r); }
        }
        else if (TryGetComponent(out aiWeapon))
        {
            aiWeapon.SetPersonaWeaponActive(false);
        }
    }

    private void ShowWeapon()
    {
        foreach (Renderer r in hiddenWeapon)
            if (r != null) r.enabled = true;
        hiddenWeapon.Clear();
        if (aiWeapon != null) aiWeapon.SetPersonaWeaponActive(true);
        aiWeapon = null;
    }

    // Applied after the animation every frame, blended by poseWeight.
    // Cast progress drives the motion: gather at chest -> reach toward the ally while floating -> push the palm out.
    private void LateUpdate()
    {
        float progress = IsCasting ? Mathf.Clamp01(elapsed / castTime) : 1f;
        float w = Mathf.SmoothStep(0f, 1f, poseWeight);

        if (avatarRoot != null)
        {
            float bob = IsCasting ? Mathf.Sin(Time.time * 3f) * 0.015f : 0f;
            avatarRoot.localPosition = avatarBase + Vector3.up * (Lift * Mathf.SmoothStep(0.1f, 0.8f, progress) + bob) * w;
        }
        if (animator == null || w <= 0.001f) return;

        Vector3 up = Vector3.up, forward = transform.forward, right = transform.right;
        Transform target = FirstTarget();

        // Direction to the ally (from the right shoulder), a little upward
        Vector3 reach = forward;
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (target != null && shoulder != null)
        {
            Vector3 to = target.position + up * 0.8f - shoulder.position;
            if (to.sqrMagnitude > 0.01f) reach = to.normalized;
        }
        reach = (reach + up * 0.2f).normalized;

        float extend = Mathf.SmoothStep(0.2f, 0.75f, progress);      // Hand leaves the chest and reaches out
        float push = Mathf.SmoothStep(0.85f, 1f, progress);          // Palm pushes out at the end

        // Upper body turns toward the ally and leans in slightly (a bit more on the push)
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest != null)
        {
            Vector3 flat = Vector3.ProjectOnPlane(reach, up);
            float yaw = flat.sqrMagnitude > 0.01f ? Mathf.Clamp(Vector3.SignedAngle(forward, flat, up), -35f, 35f) : 0f;
            Quaternion turn = Quaternion.AngleAxis(yaw * 0.6f * extend, up) * Quaternion.AngleAxis((6f + 6f * push) * extend, right);
            chest.rotation = Quaternion.Slerp(chest.rotation, turn * chest.rotation, w);
        }

        // Right arm: tucked at the chest -> reaching toward the ally
        Vector3 tuckUpper = (-up * 0.8f + forward * 0.35f + right * 0.25f).normalized;
        Vector3 tuckLower = (up * 0.55f + forward * 0.45f - right * 0.55f).normalized;
        Vector3 reachUpper = Vector3.Slerp(reach, (reach - up * 0.15f).normalized, 1f - push);
        Vector3 reachLower = (reach + up * 0.08f * (1f - push)).normalized;   // Soft elbow, straight on the push
        PointArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            Vector3.Slerp(tuckUpper, reachUpper, extend), Vector3.Slerp(tuckLower, reachLower, extend), w);

        // Left arm trails down and back, relaxed
        Vector3 trail = (-up * 0.9f - forward * 0.35f - right * 0.3f).normalized;
        PointArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            trail, (trail + forward * 0.25f).normalized, w * 0.8f);

        // Look toward the ally
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null && target != null)
        {
            Vector3 look = (target.position + up * 0.5f - head.position).normalized;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.FromToRotation(transform.forward, look) * head.rotation, w * 0.4f);
        }
    }

    // Swing the upper arm along upperDir and the forearm along lowerDir
    private void PointArm(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 upperDir, Vector3 lowerDir, float w)
    {
        Transform upper = animator.GetBoneTransform(upperBone);
        Transform lower = animator.GetBoneTransform(lowerBone);
        Transform hand = animator.GetBoneTransform(handBone);
        if (upper == null || lower == null || hand == null) return;

        Aim(upper, lower.position, upperDir, w);
        Aim(lower, hand.position, lowerDir, w);
    }

    private static void Aim(Transform bone, Vector3 childPosition, Vector3 dir, float w)
    {
        Vector3 current = childPosition - bone.position;
        if (current.sqrMagnitude < 1e-6f) return;
        Quaternion turn = Quaternion.FromToRotation(current.normalized, dir);
        bone.rotation = Quaternion.Slerp(bone.rotation, turn * bone.rotation, w);
    }

    private Transform FirstTarget()
    {
        if (targets == null) return null;
        foreach (Transform t in targets)
            if (t != null) return t;
        return null;
    }
    #endregion

    private void ShowCastBar()
    {
        barCanvas = new GameObject("ReviveCastBar");
        Canvas canvas = barCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        RectTransform frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(barCanvas.transform, false);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.3f);
        frame.sizeDelta = new Vector2(260f, 14f);
        Image back = frame.gameObject.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.55f);

        barFill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        barFill.transform.SetParent(frame, false);
        RectTransform fr = barFill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);
        // Filled needs a sprite; use a 1px white one
        Texture2D white = Texture2D.whiteTexture;
        barFill.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillAmount = 0f;
        barFill.color = SkillVfxFactory.ReviveGold;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            Text label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(frame, false);
            label.font = font;
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.LowerCenter;
            label.color = SkillVfxFactory.ReviveGold;
            label.text = "REVIVING";
            RectTransform lr = label.rectTransform;
            lr.anchoredPosition = new Vector2(0f, 18f);
            lr.sizeDelta = new Vector2(200f, 20f);

            // Cancel hint under the bar
            Text hint = new GameObject("Hint", typeof(RectTransform)).AddComponent<Text>();
            hint.transform.SetParent(frame, false);
            hint.font = font;
            hint.fontSize = 13;
            hint.alignment = TextAnchor.UpperCenter;
            hint.color = new Color(1f, 1f, 1f, 0.85f);
            hint.text = "[LMB]  CANCEL";
            RectTransform hr = hint.rectTransform;
            hr.anchoredPosition = new Vector2(0f, -18f);
            hr.sizeDelta = new Vector2(200f, 20f);
        }
    }
}
