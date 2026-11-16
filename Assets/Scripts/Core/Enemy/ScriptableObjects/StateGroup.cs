using System.Collections.Generic;
using UnityEngine;

namespace EnemyAI
{
	// A named collection of states that an FSM controller is allowed to use.
	[CreateAssetMenu(menuName = "Scriptable Objects/StateGroups")]
	public class StateGroup : ScriptableObject
	{
		[Tooltip("States that controllers assigned to this group can enter.")]
		public State[] states = new State[0];

		public IReadOnlyList<State> States => states;

		public bool Contains(State state)
		{
			foreach (State groupedState in states)
			{
				if (groupedState == state)
					return true;
			}

			return false;
		}
	}

}
