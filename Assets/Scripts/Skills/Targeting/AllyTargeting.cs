using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helpers to find ally targets for supportive skills.
/// </summary>
public static class AllyTargeting
{
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public static bool IsAlly(HealthManager health)
    {
        return health is PlayerHealth
            || health is AllyHealthManager
            || health.GetComponent<AllyTargetTag>() != null;
    }

    public static bool IsValidAlly(HealthManager health, Transform casterRoot)
    {
        return health != null
            && health.isActiveAndEnabled
            && !health.dead
            && health.transform.root != casterRoot
            && IsAlly(health);
    }

    public static Vector3 GetAimPoint(HealthManager health)
    {
        return health.transform.position + Vector3.up * 1.1f;
    }

    public static void CollectAllies(List<HealthManager> result)
    {
        result.Clear();
        foreach (HealthManager health in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Exclude))
        {
            if (IsAlly(health))
                result.Add(health);
        }
    }

    /// <summary>
    /// Ally closest to the center of view, within range and angle (like Ana's ultimate).
    /// </summary>
    public static HealthManager FindAllyInSight(Transform casterRoot, Transform view, IList<HealthManager> candidates,
        float range, float maxAngle, bool requireLineOfSight)
    {
        HealthManager best = null;
        float bestAngle = maxAngle;

        foreach (HealthManager health in candidates)
        {
            if (!IsValidAlly(health, casterRoot))
                continue;

            if ((health.transform.position - casterRoot.position).sqrMagnitude > range * range)
                continue;

            Vector3 aimPoint = GetAimPoint(health);
            float angle = Vector3.Angle(view.forward, aimPoint - view.position);
            if (angle > bestAngle)
                continue;

            if (requireLineOfSight && !HasLineOfSight(view.position, aimPoint, casterRoot, health.transform.root))
                continue;

            best = health;
            bestAngle = angle;
        }

        return best;
    }

    /// <summary>
    /// Target for AI caster: player first, then nearest ally.
    /// </summary>
    public static HealthManager FindAllyForAI(Transform casterRoot, float range)
    {
        HealthManager best = null;
        float bestDistance = float.MaxValue;

        foreach (HealthManager health in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Exclude))
        {
            if (!IsValidAlly(health, casterRoot))
                continue;

            float distance = (health.transform.position - casterRoot.position).sqrMagnitude;
            if (distance > range * range)
                continue;

            if (health is PlayerHealth)
                return health;

            if (distance < bestDistance)
            {
                best = health;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static bool HasLineOfSight(Vector3 from, Vector3 to, Transform casterRoot, Transform targetRoot)
    {
        Vector3 direction = to - from;
        int count = Physics.RaycastNonAlloc(from, direction.normalized, hitBuffer, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);

        // Nearest blocking collider that is neither caster nor target
        for (int i = 0; i < count; i++)
        {
            Transform root = hitBuffer[i].transform.root;
            if (root == casterRoot || root == targetRoot)
                continue;
            if (hitBuffer[i].collider.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast"))
                continue;
            return false;
        }

        return true;
    }
}
