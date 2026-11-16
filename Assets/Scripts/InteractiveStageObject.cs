using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Individual object that responds with IInteractiveObjectController and inherited ones.
/// </summary>
public class InteractiveStageObject : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // Start
    public Transform[] objectsIntereacted;
    private Vector3[] objectsIntereactedInitVectorPos;
    private Quaternion[] objectsIntereactedInitVectorRot;

    // Goal
    public Transform[] objectsIntereactedTargetTransform;
    public float duration = 2f;
    private int countObj = 0;
    private bool currentStatus = false;

    // Status
    public bool activatedAsStart = false;

    void Start()
    {
        countObj = objectsIntereacted.Length;

        if(objectsIntereacted != null)
        {
            objectsIntereactedInitVectorPos = new Vector3[countObj];
            objectsIntereactedInitVectorRot = new Quaternion[countObj];

            for(int i = 0; i < countObj; i++)
            {
                objectsIntereactedInitVectorPos[i] = objectsIntereacted[i].localPosition;
                objectsIntereactedInitVectorRot[i] = objectsIntereacted[i].localRotation;
            }
            
            if(activatedAsStart)
            {
                Interact(true);
            }
        }
    }

    public void Interact(bool asActive)
    {
        // If current value is not changing, don't do anything
        if(currentStatus != asActive)
        {
            currentStatus = asActive;
            StartCoroutine(InteractCo(asActive));
        }
    }

    IEnumerator InteractCo(bool asActive)
    {
        float current = 0f;        

        // Test
        while(current <= 1f)
        {
            current += 1/duration * Time.deltaTime;

            for(int i = 0; i < countObj; i++)
            {
                if(objectsIntereacted[i] == null || objectsIntereactedInitVectorPos[i] == null)
                {
                    continue;
                }

                // Apply transform of targetted object with same order
                Vector3 originP = asActive ? objectsIntereactedInitVectorPos[i] : objectsIntereactedTargetTransform[i].localPosition;
                Vector3 targetP = asActive ? objectsIntereactedTargetTransform[i].localPosition : objectsIntereactedInitVectorPos[i];
                Quaternion originR = asActive ? objectsIntereactedInitVectorRot[i] : objectsIntereactedTargetTransform[i].localRotation;
                Quaternion targetR = asActive ? objectsIntereactedTargetTransform[i].localRotation : objectsIntereactedInitVectorRot[i];

                objectsIntereacted[i].localPosition = Vector3.Lerp(originP, targetP, current);
                objectsIntereacted[i].localRotation = Quaternion.Lerp(originR, targetR, current);
            }

            yield return null;
        }
    }
}
