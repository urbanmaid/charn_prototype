using UnityEngine;

public class PersonaBehavior : MonoBehaviour
{
    [SerializeField] private PersonaData personaData;
    [SerializeField] private HUDPersona hUDPersona;
    [SerializeField] private bool applyPersonaMesh = true;
    [SerializeField, Tooltip("Projectile or throwable spawn point. If empty, the left hand bone is used.")]
    private Transform throwingPoint;

    private GameObject avatarInstance;
    private GameObject currentAvatarMeshPrefab;
    private Transform cachedDefaultThrowingPoint;
    private bool usingRagdollAvatar;
    private PersonaHitboxRig hitboxRig;
    // Commit only after a successful visual application, not merely SetPersona data reception.
    public AllyPoseProfile AppliedAllyPoseProfile { get; private set; }

    // 준비가 끝난 생존 외형에만 총알의 루트 캡슐 제외를 허용한다.
    public bool UsesBodyHitboxes => !usingRagdollAvatar && hitboxRig != null && hitboxRig.IsReady;
    public bool HasAppliedAliveAvatar => avatarInstance != null && !usingRagdollAvatar;

    public Transform ThrowingPoint => throwingPoint != null ? throwingPoint : ResolveDefaultThrowingPoint();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(personaData)
        {
            ApplyPersonaRuntime();
        }
        else
        {
            Debug.LogWarning("There is no persona applied in this player object!");
        }
    }

    /// <summary>
    /// Method of inserting persona data into player object
    /// </summary>
    public void InsertPersona(PersonaData pd)
    {
        if(pd == null) return;

        personaData = pd;
    }

    public void EnablePersonaVisuals() { applyPersonaMesh = true; }

    public void SetPersona(PersonaData pd)
    {
        InsertPersona(pd);
        ApplyPersonaRuntime();
    }

    public PersonaData GetPersona()
    {
        return personaData;
    }

    public void ApplyPersonaRuntime()
    {
        if(personaData == null) return;

        bool visualReady = false;
        if(applyPersonaMesh)
        {
            visualReady = TryApplyAliveAvatar();
            if (visualReady) RefreshPersonaVisualReferences();
        }

        foreach (IPersonaReceiver r in GetComponents<IPersonaReceiver>()) 
        {
            // A companion can receive gameplay persona data (health, movement,
            // target-command adapter) without replacing the weapon that its FSM
            // owns.  EnemyAnimation is a visual receiver: applying it replaces
            // the hand weapon and its muzzle reference.  Do not run that path
            // when this PersonaBehavior is configured not to apply an avatar.
            if (!visualReady && r is IPersonaVisualReceiver)
            {
                continue;
            }

            r.ApplyPersona(personaData);
        }

        ShowPersonaHUD();
        RefreshPersonaVisualReferences();
    }

    public void ShowPersonaHUD()
    {
        if(hUDPersona == null || personaData == null)
        {
            return;
        }

        hUDPersona.Show(PersonaHUDData.FromPersona(personaData));
    }

    public void ApplyAliveAvatar()
    {
        TryApplyAliveAvatar();
    }

    private bool TryApplyAliveAvatar()
    {
        if(personaData == null || personaData.personaDataVisual == null)
        {
            return false;
        }

        if (!ApplyPersonaAvatar(personaData.personaDataVisual.avatarMesh, false)) return false;
        usingRagdollAvatar = false;
        AppliedAllyPoseProfile = personaData.personaDataVisual.allyPoseProfile;
        return true;
    }

    public void ApplyRagdollAvatar(bool copyCurrentPose)
    {
        if(personaData == null || personaData.personaDataVisual == null)
        {
            return;
        }

        GameObject ragdollPrefab = personaData.personaDataVisual.avatarMeshRagdoll;
        if(ragdollPrefab == null)
        {
            return;
        }

        if (ApplyPersonaAvatar(ragdollPrefab, copyCurrentPose, true)) usingRagdollAvatar = true;
    }

    private bool ApplyPersonaAvatar(GameObject nextAvatarMeshPrefab, bool copyPoseFromCurrentAvatar, bool isRagdoll = false)
    {
        Animator targetAnimator = GetTargetAnimator();
        if(nextAvatarMeshPrefab == null || targetAnimator == null)
        {
            return false;
        }

        PersonaHitboxProfile profile = !isRagdoll && GetComponent<EnemyAI.StateController>() != null
            ? personaData.personaDataVisual.hitboxProfile : null;
        bool sameProfile = profile == null ? hitboxRig == null : hitboxRig != null && hitboxRig.Profile == profile && hitboxRig.IsReady;
        if(avatarInstance != null && currentAvatarMeshPrefab == nextAvatarMeshPrefab && targetAnimator.avatar != null && sameProfile)
        {
            DisableNestedPersonaAnimator();
            return true;
        }

        GameObject previousAvatarInstance = avatarInstance;
        // 새 모델 검증 전에는 현재 모델/무기를 제거하지 않는다.
        GameObject candidate = Instantiate(nextAvatarMeshPrefab, transform);
        Animator candidateAnimator = candidate.GetComponentInChildren<Animator>(true);
        if (candidateAnimator == null || candidateAnimator.avatar == null ||
            !candidateAnimator.avatar.isValid || !candidateAnimator.avatar.isHuman)
        {
            candidate.SetActive(false);
            Destroy(candidate);
            Debug.LogWarning("페르소나 외형 적용 실패: 유효한 Humanoid Avatar가 필요합니다. 기존 외형을 유지합니다.", this);
            return false;
        }

        if (profile != null && !PersonaHitboxRig.TryCreate(candidate, candidateAnimator, profile, out string failure))
        {
            candidate.SetActive(false);
            Destroy(candidate);
            Debug.LogWarning($"페르소나 피격 설정 실패: {failure} 기존 외형을 유지합니다.", this);
            return false;
        }

        if(copyPoseFromCurrentAvatar)
        {
            CopyAvatarPose(targetAnimator, candidateAnimator);
        }

        // FSM의 초기 모델만 자동 추적한다. 플레이어의 HUD/장비 등 임의 자식은 제거하지 않는다.
        var retiredRoots = new System.Collections.Generic.List<Transform>();
        if (previousAvatarInstance != null) retiredRoots.Add(previousAvatarInstance.transform);
        else if (GetComponent<EnemyAI.StateController>() != null && targetAnimator.isHuman)
        {
            Transform oldHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform skeletonRoot = GetDirectVisualRoot(oldHips);
            if (skeletonRoot != null) retiredRoots.Add(skeletonRoot);
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.transform.IsChildOf(candidate.transform) || skeletonRoot == null ||
                    skin.rootBone == null || !skin.rootBone.IsChildOf(skeletonRoot)) continue;
                Transform meshRoot = GetDirectVisualRoot(skin.transform);
                if (meshRoot != null && !retiredRoots.Contains(meshRoot)) retiredRoots.Add(meshRoot);
            }
        }

        Avatar oldAvatar = targetAnimator.avatar;
        var oldActive = new System.Collections.Generic.List<bool>();
        foreach (Transform root in retiredRoots)
        {
            oldActive.Add(root.gameObject.activeSelf);
            root.gameObject.SetActive(false);
            // Rebind가 비활성인 옛 뼈까지 찾지 않도록 잠시 계층 밖으로 분리한다.
            root.SetParent(null, true);
        }
        foreach (Animator nested in candidate.GetComponentsInChildren<Animator>(true)) nested.enabled = false;
        if (!copyPoseFromCurrentAvatar)
        {
            targetAnimator.avatar = candidateAnimator.avatar;
            targetAnimator.Rebind();
            targetAnimator.Update(0f);
            Transform hand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hand == null || hips == null || !hand.IsChildOf(candidate.transform) || !hips.IsChildOf(candidate.transform))
            {
                candidate.SetActive(false);
                candidate.transform.SetParent(null, true);
                Destroy(candidate);
                for (int i = 0; i < retiredRoots.Count; i++)
                {
                    retiredRoots[i].SetParent(transform, true);
                    retiredRoots[i].gameObject.SetActive(oldActive[i]);
                }
                targetAnimator.avatar = oldAvatar;
                targetAnimator.Rebind();
                targetAnimator.Update(0f);
                Debug.LogWarning("페르소나 뼈 연결 실패: 기존 외형을 복원합니다.", this);
                return false;
            }
        }
        avatarInstance = candidate;
        if (isRagdoll)
        {
            // Animator가 꺼진 래그돌은 가져온 메시의 오래된 bounds 때문에 몸이 사라질 수 있다.
            // 물리로 움직이는 뼈에 맞춰 표시 범위를 갱신한다. 생존 외형의 컬링 설정은 유지한다.
            foreach (SkinnedMeshRenderer skin in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
        }
        hitboxRig = candidate.GetComponent<PersonaHitboxRig>();
        currentAvatarMeshPrefab = nextAvatarMeshPrefab;
        foreach (Transform root in retiredRoots) Destroy(root.gameObject);
        return true;
    }

    private Transform GetDirectVisualRoot(Transform boneOrMesh)
    {
        if (boneOrMesh == null || boneOrMesh == transform || !boneOrMesh.IsChildOf(transform)) return null;
        while (boneOrMesh.parent != transform) boneOrMesh = boneOrMesh.parent;
        return boneOrMesh;
    }

    private void CopyAvatarPose(Animator sourceAnimator, Animator targetAnimator)
    {
        if(sourceAnimator == null || targetAnimator == null)
        {
            return;
        }

        if(sourceAnimator.isHuman && targetAnimator.isHuman)
        {
            CopyHumanoidPose(sourceAnimator, targetAnimator);
            return;
        }

        CopyTransformPoseByPath(sourceAnimator.transform, targetAnimator.transform);
    }

    private void CopyHumanoidPose(Animator sourceAnimator, Animator targetAnimator)
    {
        foreach(HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if(bone == HumanBodyBones.LastBone)
            {
                continue;
            }

            Transform sourceBone = sourceAnimator.GetBoneTransform(bone);
            Transform targetBone = targetAnimator.GetBoneTransform(bone);
            if(sourceBone == null || targetBone == null)
            {
                continue;
            }

            targetBone.SetPositionAndRotation(sourceBone.position, sourceBone.rotation);
            targetBone.localScale = sourceBone.localScale;
        }
    }

    private void CopyTransformPoseByPath(Transform sourceRoot, Transform targetRoot)
    {
        foreach(Transform source in sourceRoot.GetComponentsInChildren<Transform>(true))
        {
            string path = GetRelativePath(sourceRoot, source);
            Transform target = string.IsNullOrEmpty(path) ? targetRoot : targetRoot.Find(path);
            if(target == null)
            {
                continue;
            }

            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = source.localScale;
        }
    }

    private string GetRelativePath(Transform root, Transform target)
    {
        if(root == target)
        {
            return string.Empty;
        }

        System.Collections.Generic.Stack<string> names = new();
        Transform current = target;
        while(current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private void DisableNestedPersonaAnimator()
    {
        if(avatarInstance == null)
        {
            return;
        }

        Animator targetAnimator = GetTargetAnimator();
        foreach(Animator animator in avatarInstance.GetComponentsInChildren<Animator>(true))
        {
            if(animator != null && animator != targetAnimator)
            {
                animator.enabled = false;
            }
        }
    }

    private void RefreshPersonaVisualReferences()
    {
        cachedDefaultThrowingPoint = null;
        if(usingRagdollAvatar)
        {
            return;
        }

        foreach(IPersonaVisualRefreshReceiver receiver in GetComponents<IPersonaVisualRefreshReceiver>())
        {
            receiver.RefreshPersonaVisualReferences();
        }
    }

    private Transform ResolveDefaultThrowingPoint()
    {
        if(cachedDefaultThrowingPoint != null)
        {
            return cachedDefaultThrowingPoint;
        }

        Animator targetAnimator = GetTargetAnimator();
        if(targetAnimator != null && targetAnimator.avatar != null && targetAnimator.isHuman)
        {
            cachedDefaultThrowingPoint = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        return cachedDefaultThrowingPoint != null ? cachedDefaultThrowingPoint : transform;
    }

    private Animator GetTargetAnimator()
    {
        return GetComponent<Animator>();
    }
}

/// <summary>
/// Interface of application of PersonaData into controlable player object
/// </summary>
public interface IPersonaReceiver
{
    /// <summary>
    /// Method of applying PersonaData into area handled by each class
    /// </summary>
    public void ApplyPersona(PersonaData data);
}

/// <summary>
/// Marks persona receivers whose work mutates avatar-owned visual objects.
/// PersonaBehavior skips these receivers when avatar application is disabled,
/// allowing gameplay-only persona injection on an FSM companion.
/// </summary>
public interface IPersonaVisualReceiver
{
}

public interface IPersonaVisualRefreshReceiver
{
    public void RefreshPersonaVisualReferences();
}
