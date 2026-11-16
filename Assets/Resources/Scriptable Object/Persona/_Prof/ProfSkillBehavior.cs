using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ScriptableObject 상속을 제거하고 일반 [System.Serializable] 클래스로 변경합니다.
[Serializable] 
public class ProfSkillGrenade : SkillLogic
{
    [SerializeField] private GameObject grenade;
    [SerializeField] private float throwingPower = 12f;
    public override void OnExecute(SkillContext context)
    {
        // Only if caster or onwer is available..
        if(context.owner != null)
        {
            // Get player sight vector
            // Set vector as direction of throwing grenade
            // Instantiate grenade and add force to it

            Transform startPoint = context.owner.transform;
            Quaternion throwingQuat = Quaternion.identity;

            // If Player use this one
            /**/
            if(context.owner.GetComponent<BasicBehaviour>() != null)
            {
                ThirdPersonOrbitCam tpo = context.owner.GetComponentInChildren<ThirdPersonOrbitCam>();
                startPoint = tpo.transform;
                throwingQuat = tpo.transform.rotation;
            }
            // If FSM use this one
            else
            {
                // TODO
            }

            //Debug.Log("Current sight value:" + throwingQuat + ", Current throwing position: " + context.position);

            if(grenade != null && grenade.GetComponent<Rigidbody>() != null && startPoint)
            {
                Vector3 throwingPos = startPoint.position;
                GameObject grenadeInstance = GameObject.Instantiate(grenade, throwingPos, throwingQuat);

                // Assign origin
                ExplosiveProjectile explosive = grenadeInstance.GetComponent<ExplosiveProjectile>();
                if(explosive != null)
                {
                    explosive.SetOrigin(context.caster);
                }

                // Assign rigidbody force
                Rigidbody rb = grenadeInstance.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    rb.AddForce(context.direction * throwingPower, ForceMode.VelocityChange);
                }
            }
        }
    }
}

[Serializable] 
public class ProfSkillShield : SkillLogic
{
    [SerializeField] private GameObject shieldObj;

    [SerializeReference, SubclassSelector, SerializeField]
    private IEDefenseDamageDamp damageDampBuff; // Cached instance
    private IESpecialShield shieldBuff; // Cached instance

    public override void OnExecute(SkillContext context)
    {
        if(context.caster != null || context.owner != null)
        {
            IngameEffectBehavior iFx = context.owner != null ? context.owner.effectBehavior : context.caster.GetComponent<IngameEffectBehavior>();
            if(iFx)
            {
                // Use cached instance or create it if it doesn't exist
                damageDampBuff = new IEDefenseDamageDamp();
                if (damageDampBuff != null)
                {
                    damageDampBuff.Initialize(
                        "Damage Damp Buff",
                        EffectDetailType.Defense,
                        15f,
                        10f
                    );
                }

                shieldBuff = new IESpecialShield();
                if (shieldBuff != null)
                {
                    shieldBuff.Initialize(
                        "Shield activation",
                        EffectDetailType.Misc,
                        25f,
                        14f
                    );
                }

                // Add to behavior (This will trigger ApplyEffect and the duration Coroutine)
                //iFx.AddIngameEffect(damageDampBuff);
                //iFx.AddIngameEffect(shieldBuff);

                // Only if player's sight has target
                if(context.target)
                {
                    IngameEffectBehavior iFxTarget = context.target.GetComponent<IngameEffectBehavior>();
                    if(iFxTarget)
                    {
                        iFxTarget.AddIngameEffect(damageDampBuff);
                        iFxTarget.AddIngameEffect(shieldBuff);
                    }
                    else
                    {
                        iFx.AddIngameEffect(damageDampBuff);
                        iFx.AddIngameEffect(shieldBuff);
                    }
                }
            }
            else
            {
                Debug.LogWarning("There is no Ingame Effect Behavior in Player Object!");
            }
        }
    }
}


[Serializable]
public class ProfSkillVisor : SkillLogic
{
    [SerializeField] private GameObject visorUI;
    [SerializeField] private LayerMask scanFilterMask;

    [Header("Visor Settings")]
    [SerializeField] private float scanRadius = 50f;
    [SerializeField] private int maxVisorCount = 5;
    [SerializeField] private float duration = 15f;
    private List<GameObject> gameObjectsInVisor = new();

    public override void OnExecute(SkillContext context)
    {
        // Only if caster or onwer is available..
        if(context.caster != null || context.owner != null || context.direction != null || visorUI != null)
        {
            Transform startPoint = context.owner != null ? context.owner.transform : context.caster.transform;
            
            if (context.owner != null && context.owner.GetComponent<BasicBehaviour>() != null)
            {
                ThirdPersonOrbitCam tpo = context.owner.GetComponentInChildren<ThirdPersonOrbitCam>();
                if (tpo != null)
                {
                    startPoint = tpo.transform;
                }
            }

            // Find the list of gameObject which is checked in 'scanFilterMask'
            Collider[] hits = Physics.OverlapSphere(startPoint.position, scanRadius, scanFilterMask);
            int currentVisorCount = 0;

            foreach (Collider hit in hits)
            {
                if (currentVisorCount >= maxVisorCount)
                    break;

                Vector3 directionToTarget = (hit.transform.position - startPoint.position).normalized;
                HealthManager healthManager = hit.GetComponent<HealthManager>();
                if(healthManager != null && !healthManager.dead && visorUI != null)
                {
                    //GameObject ui = GameObject.Instantiate(visorUI);
                    HUDLive.instance.AddObject(hit.gameObject, visorUI);
                    currentVisorCount++;
                    HUDLive.instance.RemoveObject(hit.gameObject, duration);
                }
            }
        }
    }
}