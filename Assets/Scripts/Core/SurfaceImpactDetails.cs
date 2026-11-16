using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class SurfaceImpactDetails
{
    private static readonly Dictionary<SurfaceImpact.Kind, AudioClip> clips = new();
    private static readonly Dictionary<SurfaceImpact.Kind, Material> materials = new();
    private static readonly Queue<GameObject> marks = new();
    private static readonly AudioSource[] voices = new AudioSource[12];
    private static int voice;

    public static void Play(SurfaceImpact.Kind kind, Vector3 point)
    {
        if (!clips.TryGetValue(kind, out var clip) || !clip)
        {
            const int rate = 22050;
            float duration = kind == SurfaceImpact.Kind.Metal ? .65f : .3f;
            var samples = new float[(int)(duration * rate)];
            var rng = new System.Random(811 + (int)kind);
            float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2 - 1;
                low += .17f * (noise - low);
                float signal;
                if (kind == SurfaceImpact.Kind.Metal)
                    signal = noise * Mathf.Exp(-t * 100) * .5f
                        + (Mathf.Sin(2 * Mathf.PI * 1730 * t) + .45f * Mathf.Sin(2 * Mathf.PI * 2941 * t)) * Mathf.Exp(-t * 9) * .3f;
                else if (kind == SurfaceImpact.Kind.Wood)
                    signal = noise * Mathf.Exp(-t * 85) * .5f + low * Mathf.Exp(-t * 22) * 1.4f
                        + Mathf.Sin(2 * Mathf.PI * 240 * t) * Mathf.Exp(-t * 35) * .3f;
                else signal = noise * Mathf.Exp(-t * 35) * .6f + low * Mathf.Exp(-t * 20);
                samples[i] = Mathf.Clamp(signal, -.95f, .95f) * Mathf.Clamp01(t * 2000) * Mathf.Clamp01((duration - t) * 100);
            }
            clip = AudioClip.Create("Impact " + kind, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); clips[kind] = clip;
        }
        int slot = voice++ % voices.Length;
        if (!voices[slot])
        {
            var go = new GameObject("Pooled surface impact audio");
            voices[slot] = go.AddComponent<AudioSource>();
            voices[slot].playOnAwake = false;
        }
        var source = voices[slot]; source.Stop(); source.transform.position = point;
        source.outputAudioMixerGroup = SkillSfx.MixerGroup;
        source.spatialBlend = 1; source.minDistance = 6; source.maxDistance = 55;
        source.dopplerLevel = 0; source.priority = 80;
        source.volume = (kind == SurfaceImpact.Kind.Wood ? .85f : .65f) * SkillSfx.MasterVolume; source.pitch = Random.Range(.92f, 1.08f);
        source.clip = clip; source.Play();
    }

    public static void Mark(SurfaceImpact.Kind kind, Transform target, Vector3 point, Vector3 normal)
    {
        float size = kind == SurfaceImpact.Kind.Concrete ? .17f : .12f;
        // Include the rotated square footprint in the support test.
        if (!CombatBloodMark.SupportsMark(target, point, normal, size * 1.42f)) return;
        if (!materials.TryGetValue(kind, out var material) || !material)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float px = (x - 31.5f) / 31.5f, py = (y - 31.5f) / 31.5f;
                float angle = Mathf.Atan2(py, px);
                float radius = Mathf.Sqrt(px * px + py * py);
                float edge = .6f + .08f * Mathf.Sin(angle * 9) + .04f * Mathf.Sin(angle * 17);
                Color color = kind == SurfaceImpact.Kind.Metal ? new Color(.55f, .57f, .6f)
                    : kind == SurfaceImpact.Kind.Wood ? new Color(.4f, .24f, .1f) : new Color(.58f, .56f, .51f);
                if (kind == SurfaceImpact.Kind.Wood) radius = Mathf.Sqrt(px * px * 3f + py * py * .8f);
                color = Color.Lerp(new Color(.035f, .025f, .02f), color, Mathf.SmoothStep(.22f, .55f, radius));
                color.a = Mathf.Clamp01((edge - radius) * 35);
                pixels[y * 64 + x] = color;
            }
            texture.SetPixels(pixels); texture.wrapMode = TextureWrapMode.Clamp; texture.Apply(false, true);
            material = SkillVfxFactory.GetAlphaMaterial(texture); materials[kind] = material;
        }
        while (marks.Count > 0 && !marks.Peek()) marks.Dequeue();
        if (marks.Count >= 64) { var oldest = marks.Dequeue(); if (oldest) Object.Destroy(oldest); }
        var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mark.name = "Surface scar " + kind;
        var collider = mark.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
        mark.transform.SetPositionAndRotation(point + normal * .003f,
            Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360)));
        mark.transform.localScale = Vector3.one * size; mark.transform.SetParent(target, true);
        var renderer = mark.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        marks.Enqueue(mark); Object.Destroy(mark, 18f);
    }
}
