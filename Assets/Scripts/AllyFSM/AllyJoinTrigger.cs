using UnityEngine;

// 플레이어가 이 Collider(IsTrigger=true)에 진입하면 아군을 팀에 합류시킨다.
// targets 비워두면 AllySquadManager에 등록된 전체 아군 합류.
// targets에 특정 AllyStateController만 지정하면 해당 아군만 합류.
public class AllyJoinTrigger : MonoBehaviour
{
    [Tooltip("합류시킬 아군 목록. 비워두면 전체 아군 합류.")]
    public AllyStateController[] targets;

    [Tooltip("트리거를 발동시키는 태그")]
    public string activatorTag = "Player";

    private bool triggered = false;

    //kimdong if triggered Setactive(true)
    [Header("PA UI")]
    [SerializeField] private GameObject[] PAUIs;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(activatorTag)) return;

        triggered = true;

        if (targets != null && targets.Length > 0)
        {
            foreach (var ally in targets)
                if (ally != null && ally.CombatController != null) ally.CombatController.enabled = true;

            foreach (var ui in PAUIs)
                ui.SetActive(true);
        }
        else
        {
            AllySquadManager.Instance?.JoinAllAllies();
        }
    }
}
