using System.Collections;
using UnityEngine;

/// <summary>
/// Interactive object requires protection of a specific entity from death for a certain period of time.
/// </summary>
public class InteractTargetObserver : IInteractiveObjectController
{
    [Header("Status and Onboard interaction")]
    [SerializeField] Status status = Status.Standby;
    private bool isPlayerInStartCollider;

    [Tooltip("The object when activated on testing. It will be set inactive when statut got complete.")]
	public InteractiveStageObject[] interactiveStageObjectWhenTesting;

    [Header("Target")]
    [SerializeField, Tooltip("The object on this parameter should be alive after the duration.")] TargetHealth target;
    [SerializeField, Tooltip("If target is alive after this duration, interactive object will be toggled.")] float duration;

    [Header("Control")]
    [SerializeField, Tooltip("Start of testing target.")] Collider startCollider;
    [SerializeField] float retryDuration = 2.5f;
    [SerializeField, Tooltip("If using Retry Collider, Check. If not, timer based retry system will be applied.")] bool usingRetryCollider = true;
    [SerializeField, Tooltip("Start of retrying target preservation after death.")] Collider retryCollider;

    /// <summary>
    /// Assign each functions of both colliders which has collider with trigger action
    /// </summary>
    void Start()
    {
        TriggerDetector d = startCollider.gameObject.AddComponent<TriggerDetector>();
        d.OnDetected = TestStartTrigger;
        d.OnExited = TestStartExitTrigger;

        if(usingRetryCollider)
        {
            d = retryCollider.gameObject.AddComponent<TriggerDetector>();
            d.OnDetected = TestRetryTrigger;
        }
    }

    /// <summary>
    /// Assign in 'startCollider'
    /// </summary>
    void TestStartTrigger(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player"))
        {
            isPlayerInStartCollider = true;
            TestStart();
        }
    }

    /// <summary>
    /// Assign in 'startCollider'
    /// </summary>
    void TestStartExitTrigger(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player"))
        {
            isPlayerInStartCollider = false;
        }
    }

    /// <summary>
    /// Assign in 'retryCollider'
    /// </summary>
    void TestRetryTrigger(Collider myCol, Collider other)
    {
        if(other.CompareTag("Player"))
        {
            TestRetry();
        }
    }

    void TestStart()
    {
        if(status == Status.Standby)
        {
            status = Status.Checking;
            target.Revive();
            StartCoroutine(TestStartCo());
            ToggleInteractiveObjectWhenTesting(true);
        }
    }

    void TestRetry()
    {
        if(status == Status.Failed)
        {
            status = Status.Standby;
            TestStart();
        }
    }

    IEnumerator TestStartCo()
    {
        yield return new WaitForSeconds(duration);

        if(!target.dead)
        {
            status = Status.Finished;
            ToggleInteractiveObject(true);
            ToggleInteractiveObjectWhenTesting(false);
            yield break;
        }

        status = Status.Failed;
        ToggleInteractiveObjectWhenTesting(false);

        if(usingRetryCollider)
        {
            yield break;
        }

        yield return new WaitForSeconds(retryDuration);

        if(status == Status.Failed)
        {
            status = Status.Standby;
            if(isPlayerInStartCollider)
            {
                TestStart();
            }
        }
    }

    public virtual void ToggleInteractiveObjectWhenTesting(bool active)
	{
		if(interactiveStageObjectWhenTesting.Length != 0 && interactiveStageObjectWhenTesting != null)
		{
			foreach(InteractiveStageObject iso in interactiveStageObjectWhenTesting)
			{
				if(iso) iso.Interact(active);
			}
		}
	}
}
