using System.Collections;
using UnityEngine;

/// <summary>
/// This class is interactive object that responds with access of player, and resets after moments.
/// </summary>
public class InteractArea : IInteractiveObjectController
{
    [SerializeField] private float duration = 4f;

    // Update is called once per frame
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            ToggleInteractiveObject(true);
            PlayClip();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            StartCoroutine(ToggleExit(duration));
        }
    }

    IEnumerator ToggleExit(float duration)
    {
        yield return new WaitForSeconds(duration);
        ToggleInteractiveObject(false);
    }
}
