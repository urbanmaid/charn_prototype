using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Resolves a hit/child to its nearest combat character, not its scene or mission root.
    /// Identity is independent of alive/active state so stale commands can still be cleared.
    /// </summary>
    public static class CombatTargetOwner
    {
        public static Transform Resolve(Transform target)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.TryGetComponent<StateController>(out _) ||
                    current.TryGetComponent<HealthManager>(out _))
                    return current;
            }

            // Keep explicit legacy non-character targets usable, without guessing an
            // unrelated character among a mission parent's children.
            return target;
        }

        public static bool IsSame(Transform first, Transform second)
        {
            return first != null && second != null && Resolve(first) == Resolve(second);
        }
    }
}
