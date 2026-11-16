using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a procedural skill effect: animates beam width and light intensity,
/// stops particle emission at stopEmittingAt and destroys itself at lifetime.
/// </summary>
public class SkillVfxRunner : MonoBehaviour
{
    public float lifetime = 2f;
    [Tooltip("Time to stop emitting particles and start fading lights. 0 = never stop before lifetime.")]
    public float stopEmittingAt = 0f;

    private class BeamAnim
    {
        public LineRenderer line;
        public float maxWidth, fadeIn, hold, fadeOut;
        public Transform from, to;
        public Vector3 fromOffset, toOffset;
    }

    private class LightAnim
    {
        public Light light;
        public float maxIntensity, fadeIn, fadeOut;
    }

    [Tooltip("Optional. Effect follows this position without being parented (avoid inheriting scale). When it's destroyed, emission stops.")]
    public Transform followTarget;
    private bool hadFollowTarget;
    private bool snapToGround;
    private static readonly RaycastHit[] groundHits = new RaycastHit[8];

    private readonly List<BeamAnim> beams = new();
    private readonly List<LightAnim> lights = new();
    private ParticleSystem[] particleSystems;
    private float time;
    private bool emissionStopped;

    public void AddBeam(LineRenderer line, float maxWidth, float fadeIn, float hold, float fadeOut,
        Transform from = null, Vector3 fromOffset = default, Transform to = null, Vector3 toOffset = default)
    {
        beams.Add(new BeamAnim
        {
            line = line, maxWidth = maxWidth, fadeIn = fadeIn, hold = hold, fadeOut = fadeOut,
            from = from, to = to, fromOffset = fromOffset, toOffset = toOffset
        });
    }

    public void AddLight(Light light, float maxIntensity, float fadeIn, float fadeOut)
    {
        lights.Add(new LightAnim { light = light, maxIntensity = maxIntensity, fadeIn = fadeIn, fadeOut = fadeOut });
    }

    public void Follow(Transform target, bool snapToGround = false)
    {
        followTarget = target;
        hadFollowTarget = target != null;
        this.snapToGround = snapToGround;
        if (target != null) transform.position = GetFollowPosition();
    }

    // Target position, or the ground right below it (ignoring target's own colliders)
    private Vector3 GetFollowPosition()
    {
        Vector3 origin = followTarget.position;
        if (!snapToGround) return origin;

        int count = Physics.RaycastNonAlloc(origin + Vector3.up * 0.2f, Vector3.down, groundHits, 20f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 result = origin;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].transform.IsChildOf(followTarget.root)) continue;
            if (groundHits[i].rigidbody != null && !groundHits[i].rigidbody.isKinematic) continue;
            if (groundHits[i].distance < best)
            {
                best = groundHits[i].distance;
                result = groundHits[i].point;
            }
        }
        return result;
    }

    public void Play()
    {
        time = 0f;
        emissionStopped = false;
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particleSystems)
            ps.Play(false);
        Animate();
    }

    /// <summary>
    /// Stop emitting now and destroy after remaining particles fade.
    /// </summary>
    public void StopSoon(float fade = 1f)
    {
        if (!emissionStopped) stopEmittingAt = Mathf.Max(0.0001f, time);
        lifetime = Mathf.Min(lifetime, time + fade);
    }

    public float Elapsed => time;

    /// <summary>
    /// Restart timer of looping effect (ex: buff reapplied)
    /// </summary>
    public void Restart(float newStopEmittingAt, float newLifetime)
    {
        stopEmittingAt = newStopEmittingAt;
        lifetime = newLifetime;
        Play();
    }

    private void Update()
    {
        time += Time.deltaTime;

        if (hadFollowTarget)
        {
            if (followTarget != null)
            {
                transform.position = GetFollowPosition();
            }
            else
            {
                // Followed object is gone: fade out remaining particles
                if (!emissionStopped) stopEmittingAt = time;
                lifetime = Mathf.Min(lifetime, time + 1f);
                hadFollowTarget = false;
            }
        }

        if (!emissionStopped && stopEmittingAt > 0f && time >= stopEmittingAt)
        {
            emissionStopped = true;
            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps != null)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        Animate();

        if (time >= lifetime)
            Destroy(gameObject);
    }

    private void Animate()
    {
        foreach (BeamAnim b in beams)
        {
            if (b.line == null) continue;

            float w;
            if (time < b.fadeIn) w = b.fadeIn > 0f ? time / b.fadeIn : 1f;
            else if (time < b.fadeIn + b.hold) w = 1f;
            else w = b.fadeOut > 0f ? 1f - (time - b.fadeIn - b.hold) / b.fadeOut : 0f;
            b.line.widthMultiplier = b.maxWidth * Mathf.Clamp01(w);

            if (b.from != null && b.to != null)
            {
                b.line.SetPosition(0, b.from.position + b.fromOffset);
                b.line.SetPosition(1, b.to.position + b.toOffset);
            }
        }

        float fadeStart = stopEmittingAt > 0f ? stopEmittingAt : 0f;
        foreach (LightAnim l in lights)
        {
            if (l.light == null) continue;

            float k;
            if (time < l.fadeIn) k = l.fadeIn > 0f ? time / l.fadeIn : 1f;
            else if (time < Mathf.Max(l.fadeIn, fadeStart)) k = 1f;
            else k = l.fadeOut > 0f ? 1f - (time - Mathf.Max(l.fadeIn, fadeStart)) / l.fadeOut : 0f;
            l.light.intensity = l.maxIntensity * Mathf.Clamp01(k);
        }
    }
}
