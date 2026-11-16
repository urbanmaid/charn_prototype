using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    public DamageAction damageAction;

    [Header("충돌 이펙트")]
    public GameObject hitEffectPrefab;

    private void OnCollisionEnter(Collision collision)
    {
        // 충돌 위치 얻기
        Vector3 hitPoint = collision.contacts[0].point;

        // 이펙트 생성
        if (hitEffectPrefab != null)
        {
            GameObject fx = Instantiate(
                hitEffectPrefab,
                hitPoint,
                Quaternion.identity);

            Destroy(fx, 2f); // 자동 삭제
        }

        // 데미지 적용
        if (damageAction != null)
        {
            damageAction.ApplyDamage(collision.gameObject);
        }

        Destroy(gameObject);
    }
}