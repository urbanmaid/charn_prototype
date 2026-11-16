using UnityEngine;

// 적 AttackAction.DoShot()은 hit.transform.GetComponent<HealthManager>()로 데미지를 전달함
// AllyHealthManager는 루트에만 있으므로 자식 bone에 맞으면 null 반환 → 데미지 0
// 이 컴포넌트를 자식 bone에 붙이면 TakeDamage를 루트의 AllyHealthManager로 전달함
[DisallowMultipleComponent]
public class AllyDamageProxy : HealthManager
{
    private AllyHealthManager _rootHealth;

    public override void TakeDamage    
    (
        Vector3 location, 
        Vector3 direction, 
        float damage,
        Collider bodyPart = null, 
        GameObject origin = null, 
        DamageType damageType = DamageType.Normal
    )
    {
        try
        {
            if (_rootHealth == null)
                _rootHealth = transform.root.GetComponent<AllyHealthManager>();
            _rootHealth?.TakeDamage(location, direction, damage, bodyPart, origin, damageType);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }
}
