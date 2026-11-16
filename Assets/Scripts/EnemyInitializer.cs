using EnemyAI;
using UnityEngine;

public class EnemyInitializer : MonoBehaviour
{
    [Header("Initialize Target")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private StateController[] aimTargetReciever;

    [Header("Initialize Target")]
    [SerializeField] private Transform waypointGroup;
    private Transform[] entityWaypointGroupArray;
    [SerializeField] private bool mixOrderOfWaypoints;
    [SerializeField] private StateController[] waypointReciever;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ApplyAimTargets();
        ApplyWaypoint();
    }

    // Update is called once per frame
    void ApplyAimTargets()
    {
        if(aimTargetReciever.Length == 0 || aimTargetReciever == null)
        {
            return;
        }

        foreach(StateController sc in aimTargetReciever)
        {
            if(sc != null)
            {
                sc.SetAimTarget(aimTarget);
            }
        }
    }

    void ApplyWaypoint()
    {
        if(waypointReciever.Length == 0 || waypointReciever == null)
        {
            return;
        }

        OrganizeWaypoint();

        foreach(StateController sc in waypointReciever)
        {
            if(sc != null)
            {
                sc.SetWaypoints(mixOrderOfWaypoints ? GenerateRandomizedWaypoints() : entityWaypointGroupArray);
            }
        }
    }

    void OrganizeWaypoint()
    {
        if(waypointGroup == null)
        {
            Debug.LogWarning("waypointGroup == null");
            return;
        }

        int l = waypointGroup.childCount;
        if(l == 0)
        {
            Debug.LogWarning("waypointGroup.childCount == null");
            return;
        }

        entityWaypointGroupArray = new Transform[l];

        for(int i = 0; i < l; i++)
        {
            entityWaypointGroupArray[i] = waypointGroup.GetChild(i);
        }
    }

    Transform[] GenerateRandomizedWaypoints()
    {
        Transform[] rwp = (Transform[])entityWaypointGroupArray.Clone();

        // Using Fisher-Yates algorithm to mix up order of waypoints
        System.Random rand = new();
        for (int i = rwp.Length - 1; i > 0; i--)
        {
            int randomIndex = rand.Next(0, i + 1);
            (rwp[randomIndex], rwp[i]) = (rwp[i], rwp[randomIndex]);
        }

        return rwp;
    }
}
