using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDLive : MonoBehaviour
{
    public static HUDLive instance;
    private Canvas instanceCanvas;
    [SerializeField] private List<HUDObjectLocation> hUDObjects = new List<HUDObjectLocation>();
    [SerializeField] private Camera viewpoint;

    void Awake()
    {
        if (viewpoint == null)
        {
            viewpoint = Camera.main;
        }

        instanceCanvas = GetComponent<Canvas>();
        instance = this;
    }

    void Update()
    {
        if (viewpoint == null) return;

        for (int i = hUDObjects.Count - 1; i >= 0; i--)
        {
            // declare HUD data, 
            HUDObjectLocation hudData = hUDObjects[i];
            if (hudData.target == null || hudData.uiElement == null)
            {
                hUDObjects.RemoveAt(i);
                continue;
            }

            /*
            Remove when:
            - cannot find target or ui element
            - target have health manager and dead, not estimated if not having health manager
            */
            bool targetDead = hudData.healthManager ? hudData.healthManager.dead : false;
            if(targetDead)
            {
                RemoveObjectHUD(hudData.target);
            }

            // Set world coor to screen coor
            Vector3 screenPos = viewpoint.WorldToScreenPoint(hudData.target.transform.position);

            // If object is only in front of camera, update
            if (screenPos.z > 0)
            {
                if (!hudData.uiElement.activeSelf) hudData.uiElement.SetActive(true);

                // Apply screen coor (and apply offset if needed)
                hudData.uiElement.transform.position = new Vector3(screenPos.x, screenPos.y, 0);
                hudData.location = new Vector2(screenPos.x, screenPos.y);
            }
            else
            {
                // If target is beside the camera, hide
                if (hudData.uiElement.activeSelf) hudData.uiElement.SetActive(false);
            }
        }
    }

    public void AddObject(GameObject target, GameObject uiElementInput)
    {
        if (target != null && uiElementInput != null)
        {
            if (hUDObjects.Exists(x => x.target == target))
            {
                Debug.LogWarning($"[HUDLive] 이미 등록된 타겟입니다: {target.name}");
                return;
            }

            // Instantiate new UI element
            HealthManager hm = target.GetComponent<HealthManager>();
            HUDObjectLocation newHud = new HUDObjectLocation
            {
                target = target,
                healthManager = hm ? hm : null,
                uiElement = Instantiate(uiElementInput),
                location = Vector2.zero
            };
            newHud.uiElement.transform.SetParent(instanceCanvas.transform);

            hUDObjects.Add(newHud);
        }
    }

    public void RemoveObject(GameObject target, float duration = 0f)
    {
        if (target != null)
        {
            if(duration == 0f)
            {
                RemoveObjectHUD(target);
            }
            else
            {
                StartCoroutine(RemoveObjectCo(target, duration));
            }
        }
    }

    IEnumerator RemoveObjectCo(GameObject target, float duration)
    {
        yield return new WaitForSeconds(duration);

        RemoveObjectHUD(target);
    }

    void RemoveObjectHUD(GameObject target)
    {
        HUDObjectLocation targetHud = hUDObjects.Find(x => x.target == target);
        if (targetHud != null)
        {
            Destroy(targetHud.uiElement);
            hUDObjects.Remove(targetHud);
        }
    }
}

[Serializable]
public class HUDObjectLocation
{
    public GameObject target;
    public HealthManager healthManager;
    public GameObject uiElement;
    public Vector2 location;
}