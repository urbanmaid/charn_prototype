using EnemyAI;
using UnityEngine;

[RequireComponent(typeof(AllyStateController))]
public class AllyDebugLogger : MonoBehaviour
{
    public bool enableLogging = true;
    public bool logStateChange = true;
    public float detailInterval = 1f;

    private StateController combatController;
    private State previousState;
    private float detailTimer;

    private void Awake()
    {
        combatController = GetComponent<AllyStateController>()?.CombatController;
    }

    private void Update()
    {
        if (!enableLogging || combatController == null)
            return;

        if (logStateChange && combatController.currentState != previousState)
            previousState = combatController.currentState;

        if (detailInterval <= 0f)
            return;

        detailTimer += Time.deltaTime;
        if (detailTimer >= detailInterval)
            detailTimer = 0f;
    }
}
