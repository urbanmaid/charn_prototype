using UnityEngine;

/// <summary>총알 전용 검사. 이동 캡슐과 호출자가 지정한 발사자만 제외한다.</summary>
public static class ShotRaycast
{
    private static readonly RaycastHit[] Hits = new RaycastHit[32];

    public static bool Cast(Ray ray, out RaycastHit hit, float distance, int mask) =>
        Cast(ray, out hit, distance, mask, null);

    public static bool Cast(Ray ray, out RaycastHit hit, float distance, int mask, Transform ignoredRoot)
    {
        // 일반 적/지형은 기존 첫 충돌 검사 그대로 처리한다.
        if (!Physics.Raycast(ray, out hit, distance, mask)) return false;
        if (!ShouldIgnore(hit.collider, ignoredRoot)) return true;

        int count = Physics.RaycastNonAlloc(ray, Hits, distance, mask);
        // 결과가 잘리면 가까운 벽/부위를 놓칠 수 있으므로 포화 시 전체 결과를 사용한다.
        RaycastHit[] candidates = count == Hits.Length ? Physics.RaycastAll(ray, distance, mask) : Hits;
        if (candidates != Hits) count = candidates.Length;
        bool found = false;
        float nearest = float.PositiveInfinity;
        hit = default;
        for (int i = 0; i < count; i++)
        {
            var candidate = candidates[i];
            if (candidate.distance >= nearest || ShouldIgnore(candidate.collider, ignoredRoot)) continue;
            nearest = candidate.distance;
            hit = candidate;
            found = true;
        }
        return found;
    }

    // 몸 안쪽에서 시작하는 NPC 근접탄만 자기 팔/몸을 건너뛴다. 다른 캐릭터와 벽은 보존한다.
    private static bool ShouldIgnore(Collider collider, Transform ignoredRoot) =>
        (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot)) || IsMovementCapsule(collider);

    private static bool IsMovementCapsule(Collider collider) =>
        collider is CapsuleCollider && collider.TryGetComponent<PersonaBehavior>(out var persona) &&
        persona.UsesBodyHitboxes;
}
