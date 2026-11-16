using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class InteractDamageTrigger : MonoBehaviour
{
    [SerializeField] List<HealthManager> healthManagers = new();
    [SerializeField] private float damagePerSecond = 50f;
    [SerializeField] private HealthManager.DamageType damageType = HealthManager.DamageType.Environment;
    private readonly Dictionary<Transform, int> overlapCounts = new();
    private readonly Dictionary<Transform, HealthManager> healthByRoot = new();

    void Awake()
    {
        EnsureTriggerColliders();
    }

    void OnValidate()
    {
        EnsureTriggerColliders();
    }

    void OnDisable()
    {
        healthManagers.Clear();
        overlapCounts.Clear();
        healthByRoot.Clear();
    }

    void OnTriggerEnter(Collider col)
    {
        if (!TryGetHealth(col, out Transform root, out HealthManager h))
        {
            return;
        }

        overlapCounts.TryGetValue(root, out int count);
        overlapCounts[root] = count + 1;

        if (count == 0)
        {
            healthByRoot[root] = h;
            healthManagers.Add(h);
        }
    }

    // Update is called once per frame
    void OnTriggerExit(Collider col)
    {
        if (!TryGetHealth(col, out Transform root, out HealthManager h))
        {
            return;
        }

        if (!overlapCounts.TryGetValue(root, out int count))
        {
            return;
        }

        count--;
        if (count > 0)
        {
            overlapCounts[root] = count;
            return;
        }

        overlapCounts.Remove(root);
        if (healthByRoot.TryGetValue(root, out HealthManager trackedHealth))
        {
            healthManagers.Remove(trackedHealth);
            healthByRoot.Remove(root);
        }
        else
        {
            healthManagers.Remove(h);
        }
    }

    void FixedUpdate()
    {
        if(healthManagers.Count <= 0) return;

        for (int i = healthManagers.Count - 1; i >= 0; i--)
        {
            HealthManager h = healthManagers[i];
            if (h == null)
            {
                healthManagers.RemoveAt(i);
                continue;
            }

            GameObject origin = damageType == HealthManager.DamageType.Environment ? null : gameObject;
            h.TakeDamage(h.transform.position, h.transform.forward, damagePerSecond * Time.deltaTime, null, origin, damageType);
        }
    }

    private void EnsureTriggerColliders()
    {
        foreach (Collider triggerCollider in GetComponentsInChildren<Collider>(true))
        {
            triggerCollider.isTrigger = true;
        }

        foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.enabled = false;
        }
    }

    private bool TryGetHealth(Collider col, out Transform root, out HealthManager h)
    {
        root = col.transform.root;
        h = col.GetComponent<HealthManager>();
        if (h == null)
        {
            h = col.GetComponentInParent<HealthManager>();
        }

        return h != null;
    }
}
