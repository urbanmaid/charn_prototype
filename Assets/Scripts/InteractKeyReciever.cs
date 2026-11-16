using UnityEngine;

/// <summary>
/// Interactive object requires interaction gadget to be functioned.
/// </summary>
public class InteractKeyReciever : IInteractiveObjectController, IInteractable
{
    [Header("Key value and status")]
    [SerializeField] private int value = 0;
    [SerializeField] private bool isPlayerInRange;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            //SetCurrentInteractive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            //SetCurrentInteractive(false);
        }
    }

    void SetCurrentInteractive(bool entered)
    {
        // Only as debug current
        if(entered)
        {
            Debug.Log("Entered interaction area");
        }
    }

    public void Interact(bool asActive)
    {
        ToggleInteractiveObject(true);
    }

    public bool InteractWithGadget(GadgetUseAction gua)
    {
        if (gua is InteractionAction interaction)
        {
            //Debug.Log(value + "/" + interaction.keyValue);

            if(value == interaction.keyValue)
            {
                ToggleInteractiveObject(true);
                return true;
            }
            else
            {
                Debug.LogWarning("Key value of gadget and interactable is not matching");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("Gadget interaction is not valid.");
            return false;
        }
    }
    
    public int GetInteractValue()
    {
        return value;
    }

    public bool IsPlayerInRange()
    {
        return isPlayerInRange;
    }

}
