using System;
using UnityEngine;

public class InteractIntegratedProgressGroup : IInteractiveObjectController
{
    [Header("Progress Objects")]
    [Tooltip("Sequential groups of interactive objects input - output pair.")]
    public InteractiveObjectGroup[] interactiveObjectGroups;
    [Serializable]
    public struct InteractiveObjectGroup
    {
        public IInteractiveObjectController inputController;
        public InteractiveStageObject[] outputObject;
    }
    public Collider progressStartCollider;
    public int currentIndex = 0;
    [SerializeField] Status currentStatus = Status.Standby;

    void Start()
    {
        if(interactiveObjectGroups == null || interactiveObjectGroups.Length == 0)
        {
            return;
        }

        RegisterStartTrigger();

        int controllerWithinProgress = 0;
        for(int i = 0; i < interactiveObjectGroups.Length; i++)
        {
            IInteractiveObjectController ioc = interactiveObjectGroups[i].inputController; 
            if(ioc != null)
            {
                ioc.OnInteractActivated += ActivateInteractionProgress;
                controllerWithinProgress++;
            }
        }
        Debug.LogFormat("Count of object within integrated progress {0}: {1}", gameObject, controllerWithinProgress);
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
            ToggleInteractiveObjectOfIndex(true, currentIndex); // Set as 0 at initial action
            currentStatus = Status.Checking;
        }
    }

    public void ActivateInteractionProgress()
    {
        // Deactivate Prior
        if(currentIndex != 0 || true)
        {
            ToggleInteractiveObjectOfIndex(false, currentIndex);
        }

        currentIndex++;

        // Activate current or last one
        if(currentIndex < interactiveObjectGroups.Length) // If it is on progress, not finished
        {
            ToggleInteractiveObjectOfIndex(true, currentIndex);
        }
        else // If it is the last of interaction
        {
            ToggleInteractiveObject(true);
            currentStatus = Status.Finished;
        }
    }

    public void ToggleInteractiveObjectOfIndex(bool active, int index)
	{
		if(interactiveObjectGroups[index].outputObject.Length != 0 && interactiveObjectGroups[index].outputObject != null)
		{
			foreach(InteractiveStageObject iso in interactiveObjectGroups[index].outputObject)
			{
				if(iso & iso.gameObject.activeSelf) iso.Interact(active);
			}
		}
	}
}
