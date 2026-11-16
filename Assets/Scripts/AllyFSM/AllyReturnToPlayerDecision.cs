using UnityEngine;
using EnemyAI;

[CreateAssetMenu(menuName = "Ally AI/Decisions/AllyReturnToPlayer")]
public class AllyReturnToPlayerDecision : Decision
{
    [SerializeField] float returningDistance = -1f;

    public override bool Decide(StateController controller)
    {
        // Return-to-formation used to be injected by the AllyStateController
        // subclass. The adapter deliberately owns no FSM transitions.
        return false;
    }
}
