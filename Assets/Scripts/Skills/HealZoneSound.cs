using UnityEngine;

/// <summary>
/// Sound for Lucia's heal grenade: a resonant chime when it lands and opens the heal zone, then a soft
/// dreamy loop that stays on the spot and fades out as the zone ends. Added at runtime to the grenade.
/// </summary>
public class HealZoneSound : MonoBehaviour
{
    private const float FadeIn = 1f, FadeOut = 1.2f, MaxFlightTime = 3f;

    private float lifetime, age;
    private Rigidbody body;
    private GameObject holder;
    private AudioSource loop;
    private bool landed, ending;

    /// <summary> lifetime: how long the heal zone lasts (the grenade is destroyed then). </summary>
    public void Begin(float zoneLifetime)
    {
        lifetime = zoneLifetime;
        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        age += Time.deltaTime;

        // Same "settled" rule as HealTrigger: kinematic after landing, or barely moving
        if (!landed && (body == null || body.isKinematic || (age > 0.2f && body.linearVelocity.sqrMagnitude < 0.25f) || age > MaxFlightTime))
        {
            landed = true;
            SkillSfx.Play("lucia_heal_burst", transform.position, 0.5f);
            // The loop lives on its own object so the grenade being destroyed doesn't cut it off
            holder = new GameObject("SFX_HealZoneLoop");
            holder.transform.position = transform.position;
            loop = SkillSfx.PlayLoop("lucia_healzone", holder.transform, 0.4f, FadeIn);
        }

        if (landed && !ending && age >= lifetime - FadeOut) StopLoop(FadeOut);
    }

    private void StopLoop(float fade)
    {
        ending = true;
        SkillSfx.Stop(loop, fade);
        loop = null;
        if (holder != null) Destroy(holder);
    }

    private void OnDestroy()
    {
        if (!ending) StopLoop(0.5f);
    }
}
