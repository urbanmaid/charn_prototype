using System.Collections;
using UnityEngine;

public class AirStrikeRunner : MonoBehaviour
{
    public void Run(DamageAction action, GameObject user)
    {
        StartCoroutine(AirStrikeCoroutine(action, user));
    }

    IEnumerator AirStrikeCoroutine(DamageAction action, GameObject user)
    {
        /*
        Transform t = user.transform;

        // �÷��̾� �� �߽� ��ġ
        Vector3 center = t.position + t.forward * action.forwardDistance;

        float timer = 0f;

        while (timer < action.duration)
        {
            SpawnOne(action, center);

            yield return new WaitForSeconds(action.dropInterval);
            timer += action.dropInterval;
        }

        Destroy(gameObject);
        */

        yield return null;
    }

    void SpawnOne(DamageAction action, Vector3 center)
    {
        /*
        // ���� ��ġ
        Vector2 rand = Random.insideUnitCircle * action.radius;
        Vector3 targetPos = center + new Vector3(rand.x, 0, rand.y);

        // �ϴ� ��ġ
        Vector3 spawnPos = targetPos + Vector3.up * action.dropHeight;

        // ��ź ����
        GameObject proj = Instantiate(
            action.projectilePrefab,
            spawnPos,
            Quaternion.identity);

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.down * action.throwForce;
        }

        var projectile = proj.GetComponent<SimpleProjectile>();
        if (projectile != null)
        {
            projectile.damageAction = action;
        }
        */
    }
}