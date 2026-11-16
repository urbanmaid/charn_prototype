using UnityEngine;
using System.Collections.Generic;

public class HealTrigger : MonoBehaviour
{
    [SerializeField] private float healPerSecond = 25f;
    [SerializeField] private bool healOnlyAllies = true;

    [Header("Barrier (0 = no barrier)")]
    [SerializeField] private float barrierAmount = 0f;
    [SerializeField] private float barrierDuration = 6f;
    [SerializeField] private bool playBarrierEffect = true;
    [SerializeField] private Color barrierColor = new Color(1f, 0.86f, 0.45f, 1f);

    private readonly Dictionary<Transform, HealthManager> healthByRoot = new();
    // Barrier is given once per target for this zone
    private readonly HashSet<Transform> barrierGiven = new();

    public void Configure(float healAmountPerSecond, bool alliesOnly = true)
    {
        healPerSecond = healAmountPerSecond;
        healOnlyAllies = alliesOnly;
    }

    public void ConfigureBarrier(float amount, float duration, bool playEffect, Color color)
    {
        barrierAmount = amount;
        barrierDuration = duration;
        playBarrierEffect = playEffect;
        barrierColor = color;
    }

    void OnTriggerEnter(Collider col)
    {
        if (TryGetHealth(col, out Transform root, out HealthManager h))
        {
            healthByRoot[root] = h;
        }
    }

    private Rigidbody zoneBody;

    private bool IsSettled()
    {
        if (zoneBody == null) zoneBody = GetComponentInParent<Rigidbody>();
        return zoneBody == null || zoneBody.isKinematic || zoneBody.linearVelocity.sqrMagnitude < 0.25f;
    }

    private void TryGiveBarrier(Transform root, HealthManager health)
    {
        if (barrierAmount <= 0f || health.dead || barrierGiven.Contains(root))
            return;

        IngameEffectBehavior iFx = health.GetComponent<IngameEffectBehavior>();
        if (iFx == null)
        {
            iFx = health.gameObject.AddComponent<IngameEffectBehavior>();
            iFx.RefreshComponentReferences();
        }

        // IESpecialShield gives intensity * 10 shield and takes it back after duration
        IESpecialShield barrier = new();
        barrier.Initialize("Heal Zone Barrier", EffectDetailType.Defense, barrierAmount / 10f, barrierDuration);
        iFx.AddIngameEffect(barrier);
        barrierGiven.Add(root);

        if (playBarrierEffect)
        {
            SkillVfxFactory.SpawnBarrier(health.transform, barrierDuration, barrierColor);
            SkillSfx.Play("lucia_barrier", health.transform.position, 0.4f, 1f, 0.03f);
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (TryGetHealth(col, out Transform root, out _))
        {
            healthByRoot.Remove(root);
        }
    }

    void Update()
    {
        if (healthByRoot.Count <= 0) return;

        List<Transform> staleRoots = null;
        foreach (var pair in healthByRoot)
        {
            HealthManager health = pair.Value;
            if (health == null)
            {
                staleRoots ??= new List<Transform>();
                staleRoots.Add(pair.Key);
                continue;
            }

            health.HealDamage(healPerSecond * Time.deltaTime);

            // Barrier only after the grenade has landed (not while flying past the thrower)
            if (IsSettled())
                TryGiveBarrier(pair.Key, health);
        }

        if (staleRoots == null) return;

        foreach (Transform root in staleRoots)
        {
            healthByRoot.Remove(root);
        }
    }

    private bool TryGetHealth(Collider col, out Transform root, out HealthManager health)
    {
        root = col.transform.root;
        health = root.GetComponent<HealthManager>();
        if (health == null)
        {
            health = col.GetComponentInParent<HealthManager>();
        }

        if (health == null || (healOnlyAllies && !IsAlly(health)))
        {
            return false;
        }

        root = health.transform.root;
        return true;
    }

    private bool IsAlly(HealthManager health)
    {
        return health is PlayerHealth
            || health is AllyHealthManager
            || health.GetComponent<AllyTargetTag>() != null;
    }
}
