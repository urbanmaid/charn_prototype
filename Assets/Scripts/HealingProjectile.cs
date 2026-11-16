using UnityEngine;

public class HealingProjectile : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float healRatio = 0.2f;
    [SerializeField] private bool destroyOnAnyCollision = true;
    [SerializeField, Min(0f)] private float collisionActivationDelay = 0.5f;
    [SerializeField, Min(0f)] private float targetSearchRadius = 0.7f;
    [SerializeField] LayerMask ignoringLayers;

    private GameObject origin;
    private Collider[] colliders;
    private bool collisionArmed;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        ResetCollisionActivation();
    }

    public void SetOrigin(GameObject originObject)
    {
        origin = originObject;
    }

    /*
    public void SetIgnoringLayerMask(LayerMask lm)
    {
        ignoringLayers = lm;
    }
    */

    public void Configure(float ratio)
    {
        Configure(ratio, collisionActivationDelay, targetSearchRadius);
    }

    public void Configure(float ratio, float activationDelay)
    {
        Configure(ratio, activationDelay, targetSearchRadius);
    }

    public void Configure(float ratio, float activationDelay, float searchRadius)
    {
        healRatio = Mathf.Clamp01(ratio);
        collisionActivationDelay = Mathf.Max(0f, activationDelay);
        targetSearchRadius = Mathf.Max(0f, searchRadius);
        ResetCollisionActivation();
    }

    /*
    void OnTriggerEnter(Collider other)
    {
        if (!collisionArmed || IsOriginCollider(other))
        {
            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        bool healed = TryHealNear(hitPoint);
        if (destroyOnAnyCollision || healed)
        {
            Destroy(gameObject);
        }
    }
    */

    void OnCollisionEnter(Collision collision)
    {
        Collider contactCollider = collision.collider;
        if (!collisionArmed || IsOriginCollider(contactCollider))
        {
            return;
        }

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        bool healed = TryHealNear(hitPoint);
        if (destroyOnAnyCollision || healed)
        {
            Destroy(gameObject);
        }
    }

    private bool TryHealNear(Vector3 hitPoint)
    {
        AllyHealthManager target = FindBestAllyTarget(hitPoint);
        if (target == null)
        {
            return false;
        }

        target.ReviveWithHealthRatio(healRatio);
        Destroy(gameObject);
        return true;
    }

    private AllyHealthManager FindBestAllyTarget(Vector3 center)
    {
        AllyHealthManager bestTarget = null;
        float bestDistanceSqr = float.MaxValue;
        float searchRadiusSqr = targetSearchRadius * targetSearchRadius;

        foreach (AllyHealthManager candidate in Object.FindObjectsByType<AllyHealthManager>(FindObjectsInactive.Include))
        {
            if (candidate == null || candidate.gameObject == origin)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - center).sqrMagnitude;
            if (distanceSqr > searchRadiusSqr)
            {
                continue;
            }

            if (bestTarget == null)
            {
                bestTarget = candidate;
                bestDistanceSqr = distanceSqr;
                continue;
            }

            if (candidate.dead != bestTarget.dead)
            {
                if (candidate.dead)
                {
                    bestTarget = candidate;
                    bestDistanceSqr = distanceSqr;
                }

                continue;
            }

            if (distanceSqr < bestDistanceSqr)
            {
                bestTarget = candidate;
                bestDistanceSqr = distanceSqr;
            }
        }

        return bestTarget;
    }

    private bool IsOriginCollider(Collider col)
    {
        return origin != null && col.transform.root.gameObject == origin;
    }

    private void ResetCollisionActivation()
    {
        CancelInvoke(nameof(ArmCollision));
        collisionArmed = collisionActivationDelay <= 0f;
        SetColliderEnabled(collisionArmed);

        if (!collisionArmed)
        {
            Invoke(nameof(ArmCollision), collisionActivationDelay);
        }
    }

    private void ArmCollision()
    {
        collisionArmed = true;
        SetColliderEnabled(true);
    }

    private void SetColliderEnabled(bool value)
    {
        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }

        foreach (Collider projectileCollider in colliders)
        {
            if (projectileCollider != null)
            {
                projectileCollider.enabled = value;
            }
        }
    }

}
