using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Skill sound effects. Put clips in Resources/SFX/Skills/<name>. A missing clip plays nothing.
/// One-shots are played at a position with a temporary AudioSource; loops are attached to a transform.
/// </summary>
public static class SkillSfx
{
    /// <summary> Optional mixer group for skill sounds (ex: SFX group of Master.mixer). </summary>
    public static AudioMixerGroup MixerGroup;

    /// <summary> Overall volume of every skill sound. </summary>
    public static float MasterVolume = 0.7f;

    private static readonly Dictionary<string, AudioClip> clips = new();

    public static AudioClip Clip(string name)
    {
        if (clips.TryGetValue(name, out AudioClip cached)) return cached;
        // Missing clip = silent (sounds are added one by one after review)
        AudioClip clip = Resources.Load<AudioClip>("SFX/Skills/" + name);
        clips[name] = clip;
        return clip;
    }

    /// <summary> One-shot at position. pitchJitter randomizes pitch a bit for repeated sounds. </summary>
    public static AudioSource Play(string name, Vector3 position, float volume = 1f, float pitch = 1f, float pitchJitter = 0f, float delay = 0f)
    {
        AudioClip clip = Clip(name);
        if (clip == null) return null;

        GameObject go = new GameObject("SFX_" + name);
        go.transform.position = position;
        AudioSource source = Setup(go.AddComponent<AudioSource>(), clip, volume, pitch + Random.Range(-pitchJitter, pitchJitter));
        if (delay > 0f) source.PlayDelayed(delay); else source.Play();
        Object.Destroy(go, delay + clip.length / Mathf.Max(0.05f, Mathf.Abs(source.pitch)) + 0.1f);
        return source;
    }

    /// <summary> Sound that follows a transform. Loop sounds must be stopped with Stop(). </summary>
    public static AudioSource PlayAttached(string name, Transform parent, float volume = 1f, float pitch = 1f, bool loop = false)
    {
        AudioClip clip = Clip(name);
        if (clip == null || parent == null) return null;

        GameObject go = new GameObject("SFX_" + name);
        go.transform.SetParent(parent, false);
        AudioSource source = Setup(go.AddComponent<AudioSource>(), clip, volume, pitch);
        source.loop = loop;
        source.Play();
        if (!loop) Object.Destroy(go, clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.1f);
        return source;
    }

    /// <summary> Looping sound that follows a transform and fades in. Stop it with Stop(source, fade). </summary>
    public static AudioSource PlayLoop(string name, Transform parent, float volume = 1f, float fadeIn = 0.5f)
    {
        AudioSource source = PlayAttached(name, parent, volume, 1f, true);
        if (source == null) return null;
        float target = source.volume;
        source.volume = 0f;
        source.time = Random.Range(0f, source.clip.length);   // Different start each cast
        source.gameObject.AddComponent<SkillSfxFader>().Begin(source, fadeIn, target, false);
        return source;
    }

    /// <summary> Loop that follows parent for duration seconds: fades in, then fades out by the end. </summary>
    public static AudioSource PlayLoopFor(string name, Transform parent, float volume, float duration, float fadeIn = 0.5f, float fadeOut = 1f)
    {
        AudioSource source = PlayLoop(name, parent, volume, fadeIn);
        if (source != null)
            source.gameObject.AddComponent<SkillSfxTimedStop>().Begin(source, Mathf.Max(0f, duration - fadeOut), fadeOut);
        return source;
    }

    /// <summary> Fade out, then destroy. Keeps playing where it is even if the owner goes away. </summary>
    public static void Stop(AudioSource source, float fade = 0.15f)
    {
        if (source == null) return;
        foreach (SkillSfxFader running in source.GetComponents<SkillSfxFader>())
            Object.Destroy(running);
        source.transform.SetParent(null, true);
        source.gameObject.AddComponent<SkillSfxFader>().Begin(source, fade, 0f, true);
    }

    private static AudioSource Setup(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        source.clip = clip;
        source.volume = volume * MasterVolume;
        source.pitch = pitch;
        source.playOnAwake = false;
        source.spatialBlend = 0.7f;           // Mostly 3D, still audible when far
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 70f;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = MixerGroup;
        return source;
    }
}

/// <summary> Stops a timed loop (PlayLoopFor) with a fade once its time is up. </summary>
public class SkillSfxTimedStop : MonoBehaviour
{
    private AudioSource source;
    private float delay, fade;

    public void Begin(AudioSource target, float stopAfter, float fadeOut)
    {
        source = target;
        delay = stopAfter;
        fade = fadeOut;
    }

    private void Update()
    {
        delay -= Time.deltaTime;
        if (delay > 0f) return;
        SkillSfx.Stop(source, fade);
        Destroy(this);
    }
}

/// <summary> Fades a skill sound's volume; a fade-out destroys the sound when done. </summary>
public class SkillSfxFader : MonoBehaviour
{
    private AudioSource source;
    private float duration, startVolume, endVolume, time;
    private bool destroyWhenDone;

    public void Begin(AudioSource target, float fade, float toVolume, bool destroy)
    {
        source = target;
        duration = Mathf.Max(0.01f, fade);
        startVolume = target.volume;
        endVolume = toVolume;
        destroyWhenDone = destroy;
    }

    private void Update()
    {
        if (source == null) { Destroy(this); return; }
        time += Time.deltaTime;
        float u = Mathf.Clamp01(time / duration);
        source.volume = Mathf.Lerp(startVolume, endVolume, u * u * (3f - 2f * u));   // Smooth ease
        if (u < 1f) return;
        if (destroyWhenDone) Destroy(gameObject);
        else Destroy(this);
    }
}
