using System;
using UnityEngine;

public class IInteractiveObjectController : MonoBehaviour
{   
	[Header("Interactive Stuff")]
	public InteractiveStageObject[] interactiveStageObject;
	public AudioClip activateSound;
    public AudioSource audioSource;
	public event Action OnInteractActivated;

    protected enum Status
    {
        Preparing, 	// When controller is not ready to be activated
		Standby,	// Ready to address the player, but not activated
		Checking,	// Activated and be switched as Finished or Failed
		Finished,	// Finished interaction and activate the interactive object
		Failed		// Player failed the address of interaction, returning Standby
    }

    public virtual void ToggleInteractiveObject(bool active)
	{
		if(interactiveStageObject.Length != 0 && interactiveStageObject != null)
		{
			foreach(InteractiveStageObject iso in interactiveStageObject)
			{
				if(iso) iso.Interact(active);
			}
		}

		// Invoke integrated group to next progress(if applied)
		OnInteractActivated?.Invoke();
	}

	public void PlayClip()
	{
		if(activateSound == null) return;
		
		if(audioSource)
		{
			audioSource.PlayOneShot(activateSound);
		}
		else
		{
			Debug.LogWarningFormat("{0} has no audio source applied.", gameObject.name);
			AudioSource.PlayClipAtPoint(activateSound, transform.position);
		}
	}
}
