using System.Collections.Generic;
using UnityEngine;

// Scene-owned bounded pool. Each template/material variant keeps its own configured particles.
public sealed class CombatParticlePool : MonoBehaviour
{
    private static readonly List<CombatParticlePool> entries = new();
    private int key;
    private float end;
    private ParticleSystem[] particles;

    public static bool Replay(int key, Vector3 position, Quaternion rotation, float lifetime)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!entry) { entries.RemoveAt(i); continue; }
            if (entry.key != key || entry.gameObject.activeSelf) continue;
            entry.transform.SetPositionAndRotation(position, rotation);
            entry.gameObject.SetActive(true);
            entry.Begin(lifetime);
            return true;
        }
        return false;
    }

    public static void Register(GameObject root, int key, float lifetime)
    {
        entries.RemoveAll(e => !e);
        if (entries.Count >= 96) {
            int idle = entries.FindIndex(e => !e.gameObject.activeSelf);
            if (idle >= 0) {
                Destroy(entries[idle].gameObject);
                entries.RemoveAt(idle);
            }
        }
        // Never cut off a playing effect. Overflow stays transient.
        if (entries.Count >= 96) { Destroy(root, lifetime); return; }
        var entry = root.AddComponent<CombatParticlePool>();
        entry.key = key;
        foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (script is ParticleSystemAutoDestroy || script is AutoDestroyAfterSeconds) {
                script.CancelInvoke();
                script.enabled = false;
            }
        entry.particles = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in entry.particles) {
            p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.loop = false;
        }
        entries.Add(entry);
        entry.Begin(lifetime);
    }

    private void Begin(float lifetime)
    {
        end = Time.time + lifetime;
        foreach (var p in particles) if (p) {
            p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            p.Play(false);
        }
    }
    private void Update()
    {
        if (Time.time < end) return;
        foreach (var p in particles) if (p) p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }
}
