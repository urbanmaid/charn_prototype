using EnemyAI;
using UnityEngine;
using UnityEngine.AI;

// Compatibility action for legacy Ally state assets. It delegates all FSM
// data to the regular StateController and only consults the ally adapter for
// an optional formation position.
[CreateAssetMenu(menuName = "Ally AI/Actions/AllyFollow")]
public class AllyFollowAction : Action
{
    public float runThresholdDistance = 4f;
    public float walkSpeed = 0f;
    public float runSpeed = 0f;

    public override void Act(StateController controller)
    {
        if (!IsNavReady(controller))
            return;

        AllyStateController ally = controller.GetComponent<AllyStateController>();
        Vector3 target = ally != null && AllyFormationManager.Instance != null
            ? AllyFormationManager.Instance.GetFormationPosition(ally)
            : GetPlayerPosition(controller.transform.position);

        float distance = Vector3.Distance(controller.transform.position, target);
        if (distance <= 1f)
        {
            controller.nav.isStopped = true;
            return;
        }

        bool shouldRun = distance > runThresholdDistance;
        controller.nav.speed = shouldRun
            ? (runSpeed > 0f ? runSpeed : controller.generalStats.chaseSpeed)
            : (walkSpeed > 0f ? walkSpeed : controller.generalStats.patrolSpeed);
        controller.nav.isStopped = false;
        controller.nav.SetDestination(target);
    }

    public override void OnEnableAction(StateController controller)
    {
        if (controller.aimTarget == null)
            controller.aimTarget = controller.transform;

        controller.focusSight = false;
        controller.Aiming = false;
        controller.Strafing = false;
        controller.enemyAnimation.AbortPendingAim();
        controller.enemyAnimation.anim.SetBool("Crouch", false);
        controller.personalTarget = controller.transform.position + controller.transform.forward * 5f;
        if (IsNavReady(controller))
            controller.nav.isStopped = false;
    }

    private static bool IsNavReady(StateController controller)
    {
        return controller != null && controller.nav != null && controller.nav.enabled
            && controller.nav.gameObject.activeInHierarchy && controller.nav.isOnNavMesh;
    }

    private static Vector3 GetPlayerPosition(Vector3 fallback)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform.position : fallback;
    }
}
