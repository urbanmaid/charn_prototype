using System;
using UnityEngine;

/// <summary>목표 도달 시 지정된 오브젝트의 활성 상태를 전환한다.</summary>
[DisallowMultipleComponent]
public sealed class MissionGoalAnimator : MonoBehaviour
{
    public GameObject[] DisabledObjectsWhenGoalReached = Array.Empty<GameObject>();
    public GameObject[] EnabledObjectsWhenGoalReached = Array.Empty<GameObject>();

    [Header("SFX")]
    [SerializeField] AudioClip clipOnReach;

    public void OnGoalReached()
    {
        SetObjectsActive(DisabledObjectsWhenGoalReached, false);
        SetObjectsActive(EnabledObjectsWhenGoalReached, true);

        PlayClipOnReach();
        DisableBeacon();
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject target in objects)
        {
            if (target != null) target.SetActive(active);
        }
    }

    private void PlayClipOnReach()
    {
        AudioSource aus = gameObject.GetComponent<AudioSource>();
        if(aus != null)
        {
            aus.PlayOneShot(clipOnReach);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clipOnReach, transform.position);
        }
    }

    private void DisableBeacon()
    {
        MissionGoalBeacon beacon = GetComponent<MissionGoalBeacon>();
        if(beacon != null)
        {
            beacon.DisableThisBeacon();
        }
    }
}
