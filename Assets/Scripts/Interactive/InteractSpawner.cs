using EnemyAI;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class InteractSpawner : IInteractiveObjectController
{
    [Header("Entity objects and spawn position")]
    [SerializeField] private GameObject[] entity;
    [SerializeField] private Transform[] entitySpawnPos;
    [SerializeField] private Transform waypointGroup;
    private Transform[] entityWaypointGroupArray;

    [Header("Quantity control")]
    [SerializeField] private int maxEntityCountInRuntime = 5;
    [SerializeField] private Transform currentEntities;

    [Header("End Control")]
    [SerializeField, Tooltip("If the number of summoned entities exceeds the specified argument value, summoning stops. If this value is set as 0, It continues infinitely.")] private int countOfEntitySpawning = 0;
    private int countCurrent = 0;
    private Coroutine spawningCo;
    [SerializeField] Status currentStatus = Status.Standby;

    [Header("Action Control")]
    [SerializeField] private bool mixOrderOfWaypoints = false;
    [SerializeField] private float Interval = 4f;
    [SerializeField] private bool spawning = true;

    public Collider progressStartCollider;

    void Start()
    {
        OrganizeWaypoint();
        //spawningCo = StartCoroutine(SpawnEntityContinuously());

        RegisterStartTrigger();
    }

    void RegisterStartTrigger()
    {
        if(progressStartCollider == null) return;

        TriggerDetector d = progressStartCollider.gameObject.AddComponent<TriggerDetector>();
        d.OnDetected = StartInteractionProgress;
    }

    public void StartInteractionProgress(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player") && currentStatus == Status.Standby)
        {
            spawningCo = StartCoroutine(SpawnEntityContinuously());
        }
    }

    void FixedUpdate()
    {
        if(countCurrent > 1 && currentEntities.childCount == 0 && currentStatus == Status.Finished)
        {
            if(spawningCo != null)
            {
                StopCoroutine(spawningCo);
            }
            
            Debug.Log("Entities all removed!");
            currentStatus = Status.Standby;
            ToggleInteractiveObject(true);
        }
    }

    IEnumerator SpawnEntityContinuously()
    {
        currentStatus = Status.Checking;

        if(countOfEntitySpawning != 0)
        {
            countCurrent = 0;
        }

        while(countCurrent < countOfEntitySpawning || countOfEntitySpawning == 0)
        {
            yield return new WaitForSeconds(Interval);
            SpawnEntity();
            countCurrent++;
        }

        // After countCurrent >= countOfEntitySpawning (if(countOfEntitySpawning == 0, this code doesn't reach))
        SetActivated(false);
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

    void SpawnEntity()
    {
        if(!spawning)
        {
            return;
        }

        if(entity == null || entity.Length == 0 || entitySpawnPos == null || entitySpawnPos.Length == 0 || currentEntities == null)
        {
            Debug.LogWarning("entity == null or entitySpawnPos == null or currentEntities == null");
            return;
        }

        if(maxEntityCountInRuntime <= currentEntities.childCount)
        {
            Debug.LogWarning("maxEntityCount <= currentEntities.Count");
            return;
        }

        int e = UnityEngine.Random.Range(0, entity.Length);
        int esp = UnityEngine.Random.Range(0, entitySpawnPos.Length);

        GameObject g = Instantiate(entity[e], entitySpawnPos[esp].position, entitySpawnPos[esp].rotation);
        g.transform.SetParent(currentEntities);

        ApplyWaypoint(g);
    }

    void ApplyWaypoint(GameObject g)
    {
        StateController sc = g.GetComponent<StateController>();
        if(sc == null)
        {
            Debug.LogWarningFormat("StateController not found in {0}", g);
            return;
        }
        else
        {
            sc.SetWaypoints(mixOrderOfWaypoints ? GenerateRandomizedWaypoints() : entityWaypointGroupArray);
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
    //    public UnityEvent OnStopRequest;

    public void SetActivated(bool value)
    {
        currentStatus = Status.Finished;
        spawning = value;
    }
}
