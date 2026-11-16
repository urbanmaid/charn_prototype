using UnityEngine;
using EnemyAI;

[CreateAssetMenu(menuName = "Ally AI/Decisions/AllyCoverNotFound")]
public class AllyCoverNotFoundDecision : Decision
{
    public override bool Decide(StateController controller)
    {
        bool notFound = Vector3.Equals(controller.CoverSpot, Vector3.positiveInfinity);
#if UNITY_EDITOR
        if (notFound)
            Debug.Log($"[AllyCoverNotFound:{controller.name}] " +
                      $"personalTarget={controller.personalTarget:F1} | " +
                      $"viewRadius={controller.viewRadius} | " +
                      $"coverMask={controller.generalStats.coverMask.value} | " +
                      $"coverLookup={controller.coverLookup != null}");
#endif
        return notFound;
    }
}
