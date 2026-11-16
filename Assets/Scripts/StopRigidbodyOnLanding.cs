using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class StopRigidbodyOnLanding : MonoBehaviour
{
    [SerializeField] private float minLandingNormalY = 0.45f;
    [SerializeField] private float landingGraceTime = 0.1f;
    [SerializeField] private bool makeKinematicOnLanding = true;
    [SerializeField] private bool freezeRotationOnLanding = true;

    private Rigidbody rb;
    private float enabledAt;
    private bool landed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        enabledAt = Time.time;
        landed = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        TryStopOnLanding(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryStopOnLanding(collision);
    }

    private void TryStopOnLanding(Collision collision)
    {
        if (landed || Time.time - enabledAt < landingGraceTime)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= minLandingNormalY)
            {
                StopRigidbody();
                return;
            }
        }
    }

    private void StopRigidbody()
    {
        landed = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (freezeRotationOnLanding)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        if (makeKinematicOnLanding)
        {
            rb.isKinematic = true;
        }
    }
}
