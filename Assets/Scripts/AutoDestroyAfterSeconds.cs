using UnityEngine;

public class AutoDestroyAfterSeconds : MonoBehaviour
{
    [SerializeField] private float lifetime = 6f;

    public void Configure(float seconds)
    {
        lifetime = seconds;
        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), lifetime);
    }

    void OnEnable()
    {
        Invoke(nameof(DestroySelf), lifetime);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
