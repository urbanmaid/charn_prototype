using System.Collections;
using UnityEngine;

public class InteractMessegeArea : IInteractiveObjectController
{
    [Header("Content")]
    [SerializeField, TextArea(3, 10)] private string content = "SAMPLE TEXT";
    [SerializeField] private float duration = 4f;
    
    private Coroutine interactionCoroutine;

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if(interactionCoroutine != null) 
            {
                StopCoroutine(interactionCoroutine);
                interactionCoroutine = null;
            }
            ToggleInteractiveObject(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if(interactionCoroutine != null) StopCoroutine(interactionCoroutine);
            interactionCoroutine = StartCoroutine(ToggleExit(duration));
        }
    }

    IEnumerator ToggleExit(float duration)
    {
        yield return new WaitForSeconds(duration);
        ToggleInteractiveObject(false);
        interactionCoroutine = null;
    }

    public override void ToggleInteractiveObject(bool active)
    {
        if(HUDTips.instance)
        {
            HUDTips.instance.ShowTips(active ? content : "");
        }
    }
}