//using HeneGames.DialogueSystem;
using UnityEngine;
//using static HeneGames.DialogueSystem.DialogueManager;

public class Character : MonoBehaviour
{
    [SerializeField] MoveBehaviour movement;
    [SerializeField] GameObject questManager;

    public void StopPlayer()
    {
        movement.enabled = false;
    }

    public void ResumePlayer()
    {
        movement.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        /*questManager.GetComponent<QuestManager>()?.SetDialogPawn(other.gameObject);*/
    }
}
