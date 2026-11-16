using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GoalBeaconSFX : MonoBehaviour
{
    public static GoalBeaconSFX instance;
    public List<MissionGoalBeacon> beacons = new();
    private GameObject playerObject = null;
    private Transform nearestBeacon = null;
    private float distLerp = 0;

    [Header("SFX Parameters")]
    [SerializeField] float beaconSoundStartDist = 80f;
    [SerializeField] float beaconSoundEndDist = 5f;
    [SerializeField] float beaconSoundIntervalStart = 2f, beaconSoundIntervalEnd = 0.25f;
    [SerializeField] AudioClip beaconClip;
    [SerializeField] AudioSource audioSource;
    private float beaconInterval = 0;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        UpdateCurrentBeacons();
    }

    private void OnDisable()
    {
        beaconInterval = 0;
        nearestBeacon = null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        // Find player object
        playerObject = GameObject.FindGameObjectWithTag("Player");
        UpdateCurrentBeacons();
    }

    // Also supports explicit refreshes from mission initialization.
    public void UpdateCurrentBeacons()
    {
        beacons = FindObjectsByType<MissionGoalBeacon>()
            .Where(beacon => beacon.isActiveAndEnabled).ToList();
        beaconInterval = 0;
        nearestBeacon = null;
    }

    public void RegisterBeacon(MissionGoalBeacon beacon)
    {
        if (beacon == null || !beacon.isActiveAndEnabled) return;
        beacons ??= new List<MissionGoalBeacon>();
        if (!beacons.Contains(beacon)) beacons.Add(beacon);
    }

    public void UnregisterBeacon(MissionGoalBeacon beacon)
    {
        beacons?.Remove(beacon);
        if (beacon != null && nearestBeacon == beacon.transform)
        {
            nearestBeacon = null;
            beaconInterval = 0;
        }
    }

    // Main update func
    void FixedUpdate()
    {
        if(
            beacons == null || 
            beacons.Count == 0 ||
            playerObject == null
        )
        {
            beaconInterval = 0;
            nearestBeacon = null;
            return;
        }

        // Find nearest beacon
        float dist = float.PositiveInfinity;
        Transform previousBeacon = nearestBeacon;
        nearestBeacon = null;
        foreach (MissionGoalBeacon mgb in beacons)
        {
            if (mgb == null || !mgb.isActiveAndEnabled) continue;
            float distCur = Vector3.Distance(mgb.transform.position, playerObject.transform.position);
            if (distCur < dist)
            {
                nearestBeacon = mgb.transform;
                dist = distCur;
            }
        }

        if (nearestBeacon != previousBeacon) beaconInterval = 0;

        // if nearest distance of beacon is in range?
        if( nearestBeacon != null && 
            dist <= beaconSoundStartDist &&
            dist >= beaconSoundEndDist &&
            audioSource != null && beaconClip != null)
        {
            beaconInterval += Time.fixedDeltaTime;
            // Set interval by dist
            distLerp = Mathf.InverseLerp(beaconSoundStartDist, beaconSoundEndDist, dist);
            if(beaconInterval >= Mathf.Lerp(beaconSoundIntervalStart, beaconSoundIntervalEnd, distLerp))
            {
                beaconInterval = 0;
                audioSource.PlayOneShot(beaconClip);
            }
        }
        else
        {
            beaconInterval = 0;
        }
    }
}
