using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatAudioFocus : MonoBehaviour
{
    private sealed class Bed { public AudioSource source; public float baseline, applied; }
    private readonly List<Bed> beds = new List<Bed>();
    private float until, gain = 1f;
    private static readonly List<AudioSource> registered = new();
    private static readonly List<AudioSource> threatVoices = new();
    private static readonly float[] voiceStarted = new float[24];

    public static void RegisterBackground(AudioSource source)
    {
        registered.RemoveAll(s => !s);
        if (source && !registered.Contains(source)) registered.Add(source);
    }
    public static void UnregisterBackground(AudioSource source) => registered.Remove(source);
    private void OnEnable() {
        SceneManager.sceneLoaded += SceneLoaded;
        ScanBackgrounds();
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => ScanBackgrounds();
    private void ScanBackgrounds() {
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include)) {
            string group = source.outputAudioMixerGroup ? source.outputAudioMixerGroup.name : "";
            if (source.GetComponent<BackgroundSound>() || (source.loop && (group == "BGM" || group == "Background")))
                RegisterBackground(source);
        }
    }

    public static void Notify(Vector3 point)
    {
        Camera camera = Camera.main;
        if (!camera || Vector3.Distance(camera.transform.position, point) > 18f) return;
        var focus = camera.GetComponent<CombatAudioFocus>();
        if (!focus) focus = camera.gameObject.AddComponent<CombatAudioFocus>();
        focus.until = Time.time + 0.4f;
    }

    public static void PlayThreat(AudioClip clip, Vector3 point, float volume)
    {
        if (!clip) return;
        Notify(point);
        threatVoices.RemoveAll(s => !s);
        int slot = -1;
        for (int i = 0; i < threatVoices.Count; i++)
            if (!threatVoices[i].isPlaying) { slot = i; break; }
        if (slot < 0 && threatVoices.Count < 24) {
            var go = new GameObject("CombatThreatAudio pooled");
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            threatVoices.Add(voice);
            slot = threatVoices.Count - 1;
        }
        if (slot < 0) {
            slot = 0;
            for (int i = 1; i < threatVoices.Count; i++)
                if (threatVoices[i].priority > threatVoices[slot].priority ||
                    (threatVoices[i].priority == threatVoices[slot].priority && voiceStarted[i] < voiceStarted[slot])) slot = i;
            // Distant fire must not evict an important nearby shot.
            int priority = Camera.main && Vector3.Distance(Camera.main.transform.position, point) <= 18f ? 64 : 160;
            if (priority > threatVoices[slot].priority) return;
        }
        var audio = threatVoices[slot];
        audio.Stop();
        audio.transform.position = point;
        voiceStarted[slot] = Time.time;
        audio.clip = clip;
        audio.volume = volume;
        audio.spatialBlend = 1f;
        audio.priority = Camera.main && Vector3.Distance(Camera.main.transform.position, point) <= 18f ? 64 : 160;
        audio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        audio.Play();

    }

    private void Update()
    {
        for (int i = registered.Count - 1; i >= 0; i--) {
            var source = registered[i];
            if (!source) { registered.RemoveAt(i); continue; }
            bool known = false;
            foreach (var bed in beds) if (bed.source == source) { known = true; break; }
            if (!known) beds.Add(new Bed { source = source, baseline = source.volume, applied = source.volume });
        }
        gain = Mathf.MoveTowards(gain, Time.time < until && Time.timeScale > 0f ? 0.72f : 1f, Time.unscaledDeltaTime * 3f);
        for (int i = beds.Count - 1; i >= 0; i--)
        {
            Bed bed = beds[i];
            if (!bed.source) { beds.RemoveAt(i); continue; }
            if (!registered.Contains(bed.source)) {
                if (Mathf.Approximately(bed.source.volume, bed.applied)) bed.source.volume = bed.baseline;
                beds.RemoveAt(i); continue;
            }
            if (!Mathf.Approximately(bed.source.volume, bed.applied)) bed.baseline = bed.source.volume;
            bed.applied = bed.baseline * gain;
            bed.source.volume = bed.applied;
        }
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        foreach (Bed bed in beds) if (bed.source && Mathf.Approximately(bed.source.volume, bed.applied)) bed.source.volume = bed.baseline;
        beds.Clear();
    }
}
