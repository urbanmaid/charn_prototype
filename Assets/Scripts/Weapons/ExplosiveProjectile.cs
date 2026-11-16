using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static HealthManager;

/// <summary>
/// Explosive projectile such as grenade or rocket launcher projectile. Apply this script on object which has rigidbody and collider.
/// </summary>
public class ExplosiveProjectile : MonoBehaviour
{
    [SerializeField] private bool explodingWhenTouchedImmediately = false;
    [SerializeField] private bool ignoreDistance = false;
    [SerializeField] private float explodingDelay = 4f;
    [SerializeField] private DamageType damageTypeClassification;

    [Header("Explosion")]
    [SerializeField] private float damage = 80f;
    [SerializeField] private float damageRange = 6f; 

    [Header("FX")]
    [SerializeField] private AudioClip[] explosionClip;
    [SerializeField] private GameObject explosionVFX;

    private GameObject origin;
    private Charm.Mechanism.Missions.CombatSource combatSource;
    private bool exploded;

    void OnEnable()
    {
        exploded = false;
        if(!explodingWhenTouchedImmediately)
        {
            StartCoroutine(ExplodingCountdown(explodingDelay));
        }
    }

    public void SetOrigin(GameObject gameObject)
    {
        origin = gameObject;
        combatSource = Charm.Mechanism.Missions.CombatTelemetry.Capture(gameObject);
    }

    IEnumerator ExplodingCountdown(float dur)
    {
        yield return new WaitForSeconds(dur);

        ExplodeOnce();
    }

    private void OnDisable() => StopAllCoroutines();

    private void ExplodeOnce()
    {
        // Destroy is deferred: multiple collision callbacks can arrive this frame.
        if (exploded) return;
        exploded = true;
        CheckTargetAndDamage();
        ShowFX();
        Destroy(gameObject);
    }

    void CheckTargetAndDamage()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, damageRange);
        var damaged = new HashSet<HealthManager>();
        //Debug.Log("Count of collided object: " + colliders.Length);

        foreach (Collider hit in colliders)
        {
            HealthManager targetHealth = hit.GetComponent<HealthManager>();
            
            if (targetHealth != null && !damaged.Contains(targetHealth))
            {
                // Check out the direction of target to raycast
                Vector3 directionToTarget = hit.bounds.center - transform.position;
                float distanceToTarget = directionToTarget.magnitude;

                // If the object collided into raycast is not target, don't check health manager
                if (Physics.Raycast(transform.position, directionToTarget, out RaycastHit rayHit, distanceToTarget))
                {
                    // check once again
                    if (rayHit.collider == hit)
                    {
                        damaged.Add(targetHealth);
                        // set the damage with dist
                        float proximity = ignoreDistance ? ((damageRange - distanceToTarget) / damageRange) : 1;
                        float finalDamage = proximity * damage;

                        using (Charm.Mechanism.Missions.CombatTelemetry.UseSource(combatSource))
                        targetHealth.TakeDamage
                        (
                            transform.position, 
                            hit.transform.position - transform.position, 
                            Mathf.Max(0, finalDamage), 
                            rayHit.collider, 
                            origin, 
                            damageTypeClassification
                        );
                        //Debug.Log($"{hit.name}에게 {finalDamage}의 데미지를 입혔습니다.");
                    }
                }
            }
        }
    }

    void ShowFX()
    {
        CombatCameraFeedback.Explosion(transform.position, Mathf.Max(6f, damageRange * 1.5f));
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }

        if (explosionClip != null && explosionClip.Length > 0)
        {
            int randomIndex = Random.Range(0, explosionClip.Length);
            AudioClip clipToPlay = explosionClip[randomIndex];
            if(clipToPlay) AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if(explodingWhenTouchedImmediately)
        {
            ExplodeOnce();
        }
    }
}
