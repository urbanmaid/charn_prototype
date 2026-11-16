using UnityEngine;

// Implemented by every combatant that belongs to a faction.
// A shared contract lets player and FSM-controlled entities compare teams without tags.
public interface ITeamCode
{
	int TeamCode { get; set; }
	int GetTeamCode();
	void SetTeamCode(int teamCode);
}

public static class TeamCodeUtility
{
	// Finds a team owner on the object that was hit or any of its parents.
	public static bool TryGetTeamCode(Component component, out int teamCode)
	{
		teamCode = default;
		if (component == null)
			return false;

		foreach (MonoBehaviour behaviour in component.GetComponentsInParent<MonoBehaviour>(true))
		{
			if (behaviour is ITeamCode teamOwner)
			{
				teamCode = teamOwner.TeamCode;
				return true;
			}
		}

		return false;
	}

	public static bool IsSameTeam(ITeamCode first, Component second)
	{
		return first != null && TryGetTeamCode(second, out int secondTeamCode) && first.TeamCode == secondTeamCode;
	}
}
