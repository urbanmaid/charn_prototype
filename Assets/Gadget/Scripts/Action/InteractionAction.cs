using UnityEngine;

[System.Serializable] 
public class InteractionAction : GadgetUseAction
{
    [Tooltip("Will be overriden for environmental interaction unit.")]
    public int keyValue = 0;

    public InteractionAction DeepCopy(int value)
    {
        // For only an circumstances that can be used as runtime creation
        InteractionAction ia = new();
        Debug.Log("Deep copying interaction with overriden value: " + value);
        ia.keyValue = value;

        return ia;
    }

    protected override bool OnExecute(GameObject target)
    {
        // Set all object which has tag of interactible.
        GameObject[] interactablesObject = GameObject.FindGameObjectsWithTag("Interactable");
        
        IInteractable closestInteractable = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = target.transform.position;

        if(interactablesObject == null || interactablesObject.Length == 0)
        {
            Debug.LogWarning("There is no interactible objects");
            return false;
        }
        foreach (GameObject obj in interactablesObject)
        {
            if (obj.TryGetComponent<IInteractable>(out var interactable))
            {
                Vector3 diff = obj.transform.position - currentPos;
                float distSqr = diff.sqrMagnitude;

                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestInteractable = interactable;
                }
            }
        }

        // Only if player is in range..
        if (closestInteractable != null && closestInteractable.IsPlayerInRange())
        {
            return closestInteractable.InteractWithGadget(this);
        }
        else
        {
            return false;
        }
    }
}