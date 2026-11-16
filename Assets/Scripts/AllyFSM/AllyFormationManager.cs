using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-90)]
public class AllyFormationManager : MonoBehaviour
{
    public static AllyFormationManager Instance;

    public Transform player;
    public float spacing = 1f;

    private static readonly Vector3[] SlotOffsets =
    {
        Vector3.zero,
        new(-1f, 0f, -1f),
        new(1f, 0f, -1f),
        new(-1.5f, 0f, -2f),
        new(1.5f, 0f, -2f),
    };

    private readonly Dictionary<AllyStateController, int> slotMap = new();

    void Awake()
    {
        Instance = this;
        ResolvePlayer();
    }

    public void RecalculateSlots(List<AllyStateController> allies)
    {
        ResolvePlayer(true);
        slotMap.Clear();
        for (int i = 0; i < allies.Count && i < SlotOffsets.Length - 1; i++)
        {
            AssignSlot(allies[i], i + 1);
        }
    }

    public Vector3 GetFormationPosition(AllyStateController ally)
    {
        if (ally == null) return Vector3.zero;

        ResolvePlayer();
        if (player == null) return ally.transform.position;

        if (!slotMap.ContainsKey(ally))
            AssignSlot(ally, slotMap.Count + 1);

        Quaternion yawOnly = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
        int slot = slotMap[ally];
        Vector3 worldTarget = player.position + yawOnly * (SlotOffsets[slot] * spacing);

        if (NavMesh.SamplePosition(worldTarget, out NavMeshHit hit, spacing, NavMesh.AllAreas))
            return hit.position;

        return worldTarget;
    }

    private void ResolvePlayer()
    {
        ResolvePlayer(false);
    }

    public void ForceResolvePlayer()
    {
        ResolvePlayer(true);
    }

    private void ResolvePlayer(bool force)
    {
        if (AllySquadManager.Instance != null)
        {
            var slot = AllySquadManager.Instance.CurrentPlayer;
            player = slot != null ? slot.transform : null;
            return;
        }

        // Compatibility for scenes without a squad manager.
        if (player != null &&
            PlayerControlResolver.IsAvailable(player.GetComponent<BasicBehaviour>())) return;
        var fallback = PlayerControlResolver.Resolve(null);
        player = fallback != null ? fallback.transform : null;
    }

    private void AssignSlot(AllyStateController ally, int slot)
    {
        if (ally == null) return;

        slot = Mathf.Clamp(slot, 1, SlotOffsets.Length - 1);
        slotMap[ally] = slot;
    }
}
