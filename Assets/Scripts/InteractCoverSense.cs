using System;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Interactive object that responds with access of player with behavior or cover. 
/// It lets player cover in some area and pass it with keeping coverage. 
/// If cover is disabled or passing cover area without coverage, it sets failed.
/// </summary>
public class InteractCoverSense : IInteractiveObjectController
{
    [Header("Cover Sense Area")]
    [SerializeField] Collider startCollider;
    [SerializeField] Collider[] senseCollider;
    [SerializeField] Collider finishCollider;

	[Header("Status")]
    [SerializeField] Status status = Status.Standby;
    [SerializeField] GameObject playerObject;
    private CoverBehaviour coverBehaviourPlayer;

    //[SerializeField] 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TriggerDetector d = startCollider.gameObject.AddComponent<TriggerDetector>();
        d.OnDetected = SenseStart;
        d = finishCollider.gameObject.AddComponent<TriggerDetector>();
        d.OnDetected = SenseFinish;

        foreach (var col in senseCollider)
        {
            var detector = col.gameObject.AddComponent<TriggerDetector>();
            detector.OnDetected = Sense;
        }
    }

    void SenseStart(Collider myCol, Collider other)
    {
        coverBehaviourPlayer = other.GetComponent<CoverBehaviour>();

        if(other.CompareTag("Player") && (coverBehaviourPlayer != null))
        {
            playerObject = other.gameObject;
        }
    }


    void Sense(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player") && (coverBehaviourPlayer != null))
        {
            status = Status.Checking;
        }
    }

    void SenseFinish(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player") && (coverBehaviourPlayer != null))
        {
            status = Status.Finished;
            ToggleInteractiveObject(true);
        }    
    }

    void Update()
    {
        if(status == Status.Checking)
        {
            if(coverBehaviourPlayer.isCovering == false)
            {
                Debug.Log("Covering failed! Go back and restart..");
                status = Status.Standby;
            }
        }
    }
}
