using UnityEngine;

// 프리팹 루트에 추가하면 Awake 시 모든 자식 Collider에 AllyDamageProxy를 자동 부착
// 루트 자체는 AllyHealthManager가 직접 처리하므로 건너뜀
[DefaultExecutionOrder(-80)]
public class AllyDamageAutoSetup : MonoBehaviour, IPersonaVisualRefreshReceiver
{
    void Awake()
    {
        RebuildDamageProxies();
    }

    public void RefreshPersonaVisualReferences()
    {
        RebuildDamageProxies();
    }

    public void RebuildDamageProxies()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.transform == transform) continue;
            // 새 피격 계층은 부모 HealthManager로 직접 전달한다. Proxy를 추가하면
            // 플레이어의 SendMessageUpwards가 루트와 Proxy 양쪽에 피해를 전달할 수 있다.
            if (col.GetComponentInParent<PersonaHitboxRig>() != null) continue;
            if (col.GetComponent<AllyDamageProxy>() == null)
                col.gameObject.AddComponent<AllyDamageProxy>();
        }
    }
}
