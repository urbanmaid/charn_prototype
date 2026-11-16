using UnityEngine;

// Persona swaps exchange character data, not the BasicBehaviour control slot.
public static class PlayerControlResolver
{
    public static BasicBehaviour Resolve(BasicBehaviour assigned)
    {
        // A disabled/destroyed explicit slot must not silently select someone else.
        if (!ReferenceEquals(assigned, null))
            return IsAvailable(assigned) ? assigned : null;

        BasicBehaviour result = null;
        foreach (var candidate in Object.FindObjectsByType<BasicBehaviour>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (result != null) return null;
            result = candidate;
        }
        return result;
    }

    public static bool IsAvailable(BasicBehaviour player)
        => player != null && player.gameObject.activeInHierarchy;
}
