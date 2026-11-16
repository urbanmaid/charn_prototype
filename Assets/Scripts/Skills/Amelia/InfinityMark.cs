using UnityEngine;

/// <summary> Keeps an infinity sign above a character's head, facing the camera. </summary>
public class InfinityMark : MonoBehaviour
{
    public Transform target;

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + Vector3.up * (2.1f + Mathf.Sin(Time.time * 3f) * 0.05f);
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
