using UnityEngine;

/// <summary>Refreshable damage-over-time state applied by Fiona's venom rounds.</summary>
public class FionaVenomPoison : MonoBehaviour
{
    private const float TickInterval = 1f;

    private HealthManager target;
    private GameObject source;
    private Charm.Mechanism.Missions.CombatSource combatSource;
    private float damagePerSecond;
    private float elapsed;
    private float expiresAt;
    private float nextTick = TickInterval;

    public void Apply(float damagePerSecond, float duration, GameObject source)
    {
        target = GetComponent<HealthManager>();
        this.source = source;
        combatSource = Charm.Mechanism.Missions.CombatTelemetry.Capture(source);
        this.damagePerSecond = Mathf.Max(0f, damagePerSecond);
        expiresAt = Mathf.Max(expiresAt, elapsed + duration);
    }

    private void Update()
    {
        if (target == null || target.dead || damagePerSecond <= 0f)
        {
            Destroy(this);
            return;
        }

        elapsed += Time.deltaTime;
        while (nextTick <= elapsed && nextTick <= expiresAt)
        {
            using (Charm.Mechanism.Missions.CombatTelemetry.UseSource(combatSource))
            target.TakeDamage(target.transform.position + Vector3.up, Vector3.up,
                damagePerSecond * TickInterval, null, source, HealthManager.DamageType.Skill);
            nextTick += TickInterval;
        }

        if (elapsed >= expiresAt) Destroy(this);
    }
}
