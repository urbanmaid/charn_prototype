using System;
using UnityEngine;

public class TriggerDetector : MonoBehaviour
{
    public Action<Collider, Collider> OnDetected;
    public Action<Collider, Collider> OnExited;

    void OnTriggerEnter(Collider other)
    {
        OnDetected?.Invoke(GetComponent<Collider>(), other);
    }

    void OnTriggerExit(Collider other)
    {
        OnExited?.Invoke(GetComponent<Collider>(), other);
    }
}
