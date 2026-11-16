using UnityEngine;

[System.Serializable] 
public class DamageAction : GadgetUseAction
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float throwingPower = 12f;

    protected override bool OnExecute(GameObject target)
    {
        return Throw(target);
    }

    // -------------------------
    // throw
    // -------------------------
    bool Throw(GameObject user)
    {
        if (user == null || projectilePrefab == null || projectilePrefab.GetComponent<Rigidbody>() == null) return false;
        // Get player sight vector
        // Set vector as direction of throwing grenade
        // Instantiate grenade and add force to it

        Transform startPoint = user.transform;
        Quaternion throwingQuat = Quaternion.identity;

        // If Player use this one
        /**/
        if(user.GetComponent<BasicBehaviour>() != null)
        {
            ThirdPersonOrbitCam tpo = user.GetComponentInChildren<ThirdPersonOrbitCam>();
            if (tpo == null) return false;
            startPoint = tpo.transform;
            throwingQuat = tpo.transform.rotation;
        }
        // If FSM use this one
        else
        {
            // TODO
        }

        //Debug.Log("Current sight value:" + throwingQuat + ", Current throwing position: " + context.position);

        if(projectilePrefab != null && projectilePrefab.GetComponent<Rigidbody>() != null && startPoint)
        {
            Vector3 throwingPos = startPoint.position;
            GameObject grenadeInstance = GameObject.Instantiate(projectilePrefab, throwingPos, throwingQuat);

            // Assign origin
            ExplosiveProjectile explosive = grenadeInstance.GetComponent<ExplosiveProjectile>();
            if(explosive != null)
            {
                explosive.SetOrigin(user);
            }

            // Assign rigidbody force
            Rigidbody rb = grenadeInstance.GetComponent<Rigidbody>();
            if(rb != null)
            {
                rb.AddForce(user.transform.forward * throwingPower, ForceMode.VelocityChange);
            }
            return true;
        }
        return false;
    }
    

    // -------------------------
    // single Attack
    // -------------------------
    void AirDrop(GameObject target)
    {
        /*
        Vector3 spawnPos = target.transform.position + Vector3.up * dropHeight;

        GameObject proj = Object.Instantiate(
            projectilePrefab,
            spawnPos,
            Quaternion.identity);

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.down * throwForce;
        }


        SetupProjectile(proj);
        */
    }

    // -------------------------
    // multy attack
    // -------------------------
    void StartAirStrike(GameObject target)
    {
        GameObject runnerObj = new GameObject("AirStrikeRunner");
        var runner = runnerObj.AddComponent<AirStrikeRunner>();
        runner.Run(this, target);
    }

    // -------------------------
    // common Projectile settings
    // -------------------------
    void SetupProjectile(GameObject proj)
    {
        var projectile = proj.GetComponent<SimpleProjectile>();
        if (projectile != null)
        {
            projectile.damageAction = this;
        }
    }

    // -------------------------
    // apply damage
    // -------------------------
    public void ApplyDamage(GameObject target)
    {
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(0);
        }
    }
}