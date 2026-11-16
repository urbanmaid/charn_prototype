using EnemyAI;
using UnityEngine;

// Keeps a state-controller companion near the player while it is in the follow state.
[CreateAssetMenu(menuName = "Enemy AI/Actions/Follow Player")]
public class FollowPlayerAction : Action
{
	[Tooltip("Team code assigned to the player the companion should follow.")]
	public int playerTeamCode = 0;
	[Tooltip("Distance at which the companion stops moving toward the player.")]
	[Min(0f)] public float followDistance = 3f;
	[Tooltip("Movement speed used while following the player.")]
	[Min(0f)] public float followSpeed = 5f;

	public override void Act(StateController controller)
	{
		Transform target = controller.HasSquadFollowSpacing
            ? controller.ResolveSquadFollowTarget()
            : FindPlayer(playerTeamCode, controller.transform)?.transform;
		if (target == null)
        {
            if (controller.HasSquadFollowSpacing) controller.UpdateFollowMovement();
            return;
        }
        if (controller.nav == null || !controller.nav.enabled || !controller.nav.isOnNavMesh)
			return;

		controller.ConfigureFollow(this, target);
		controller.UpdateFollowMovement();
	}

	private static BasicBehaviour FindPlayer(int teamCode, Transform self)
	{
		foreach (BasicBehaviour player in FindObjectsByType<BasicBehaviour>())
		{
			if (player.transform != self && player.TeamCode == teamCode)
				return player;
		}

		return null;
	}
}
