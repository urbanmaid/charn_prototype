using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Move speed multiplier for NavMeshAgent characters (enemy / ally AI).
/// FSM actions keep overwriting nav.speed, so the multiplier is re-applied whenever the base speed changes.
/// Added on demand by ingame effects (ex: IEMoveSpeed slow).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavSpeedModifier : MonoBehaviour
{
    private readonly List<float> multipliers = new();
    private NavMeshAgent agent;
    private float baseSpeed = -1f, appliedSpeed = -1f;

    public float Multiplier { get; private set; } = 1f;

    public static NavSpeedModifier Get(GameObject target)
    {
        if (target.GetComponent<NavMeshAgent>() == null) return null;
        NavSpeedModifier modifier = target.GetComponent<NavSpeedModifier>();
        return modifier != null ? modifier : target.AddComponent<NavSpeedModifier>();
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void Add(float multiplier)
    {
        multipliers.Add(multiplier);
        Recalculate();
    }

    public void Remove(float multiplier)
    {
        if (multipliers.Remove(multiplier))
            Recalculate();
    }

    private void Recalculate()
    {
        float total = 1f;
        foreach (float m in multipliers) total *= m;
        Multiplier = Mathf.Clamp(total, 0.05f, 5f);
        Apply(true);
    }

    private void LateUpdate()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        if (agent == null) return;

        // FSM changed speed since our last write: take it as new base speed
        if (baseSpeed < 0f || !Mathf.Approximately(agent.speed, appliedSpeed))
            baseSpeed = agent.speed;
        else if (!force)
            return;

        appliedSpeed = baseSpeed * Multiplier;
        agent.speed = appliedSpeed;
    }

    private void OnDisable()
    {
        if (agent != null && baseSpeed >= 0f && Mathf.Approximately(agent.speed, appliedSpeed))
            agent.speed = baseSpeed;
    }
}
