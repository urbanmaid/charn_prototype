using UnityEngine;

public class MissionGoalBeacon : MonoBehaviour
{
    private void OnEnable()
    {
        if (GoalBeaconSFX.instance != null)
            GoalBeaconSFX.instance.RegisterBeacon(this);
    }

    private void OnDisable()
    {
        if (GoalBeaconSFX.instance != null)
            GoalBeaconSFX.instance.UnregisterBeacon(this);
    }

    public void DisableThisBeacon()
    {
        if (GoalBeaconSFX.instance != null)
            GoalBeaconSFX.instance.UnregisterBeacon(this);
    }
}
