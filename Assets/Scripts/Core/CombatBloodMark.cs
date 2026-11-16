using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CombatBloodMark : MonoBehaviour
{
    private const int MaxMarks = 24;
    private const float Lifetime = 12f;
    private static readonly Queue<CombatBloodMark> marks = new Queue<CombatBloodMark>();
    private static readonly RaycastHit[] hits = new RaycastHit[32];
    private static readonly Material[] bloodMaterials = new Material[3];
    private Renderer meshRenderer;
    private MaterialPropertyBlock block;
    private float age;
    private Collider support;
    private static readonly AudioClip[] impactClips = new AudioClip[3];

    public static void PlayImpactSound(Vector3 point, GameObject origin = null, bool headshot = false)
    {
        int index = Random.Range(0, impactClips.Length);
        if (impactClips[index] == null)
            impactClips[index] = Resources.Load<AudioClip>("SFX/enemy_flesh_" + (index + 1));
        var clip = impactClips[index];
        if (clip == null) return;
        var go = new GameObject("Enemy flesh impact audio");
        go.transform.position = point;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        bool localShot = origin != null && origin.GetComponentInParent<PlayerHealth>() != null;
        source.spatialBlend = localShot ? 0f : 1f;
        source.minDistance = 5f;
        source.maxDistance = 45f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        source.pitch = Random.Range(.94f, 1.06f);
        // Local hit confirmation must remain audible at long range. Leave room for the headshot accent.
        source.volume = localShot ? (headshot ? .18f : .32f) : .55f * SkillSfx.MasterVolume;
        source.outputAudioMixerGroup = SkillSfx.MixerGroup;
        source.priority = localShot ? 32 : 90;
        source.Play();
        Destroy(go, clip.length / .94f + .1f);
    }

    private void Awake()
    {
        // Unity 객체는 필드 초기화가 아닌 Awake에서 생성한다.
        block = new MaterialPropertyBlock();
    }

    public static void SpawnImpact(Vector3 point, Vector3 direction)
    {
        Vector3 position = point + (direction.sqrMagnitude > .001f ? -direction.normalized * .04f : Vector3.zero);
        Quaternion rotation = Quaternion.LookRotation(direction.sqrMagnitude > .001f ? -direction : Vector3.up);
        if (CombatParticlePool.Replay(-200, position, rotation, .75f)) return;
        var root = new GameObject("VFX_BloodImpact");
        root.transform.position = point + (direction.sqrMagnitude > .001f ? -direction.normalized * .04f : Vector3.zero);
        var texture = SkillVfxFactory.Kenney("dirt_02");
        var particles = SkillVfxFactory.CreateParticles(root.transform, "Blood spray", Vector3.zero, texture);
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(texture);
        var main = particles.main;
        main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(.14f, .34f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.38f, .005f, .005f, 1f), new Color(.85f, .035f, .02f, 1f));
        main.gravityModifier = 1.3f;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = .025f;
        root.transform.rotation = rotation;
        SkillVfxFactory.SetBurst(particles, 28);
        SkillVfxFactory.SetFadeOut(particles);
        // Full opacity from the first frame; the generic skill fade-in hid the initial blood impact.
        var colorOverLife = particles.colorOverLifetime;
        var opacity = new Gradient();
        opacity.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.9f, .5f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = opacity;
        particles.Play();
        CombatParticlePool.Register(root, -200, .75f);
    }

    public static void Spawn(Vector3 point, Vector3 direction, bool lethal)
    {
        Vector3 forward = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
        Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, flat);
        Vector3 origin = point + Vector3.up * .08f;
        Vector3 last = new Vector3(float.PositiveInfinity, 0f, 0f);
        // First stain follows the bullet into a nearby wall. Subsequent droplets fan out
        // from the contact point; every path is ray-tested so they cannot jump through walls.
        if (FindSurface(origin, forward, 3.5f, out RaycastHit direct)) {
            PlaceMark(direct, lethal, 1f);
            last = direct.point;
        }
        int count = lethal ? 3 : 2;
        for (int i = 0; i < count; i++) {
            Vector3 spray = flat * Random.Range(.7f, 1.7f) +
                side * Random.Range(-.85f, .85f) + Vector3.down * Random.Range(.55f, 1.3f);
            if (!FindSurface(origin, spray.normalized, 4.5f, out RaycastHit hit)) continue;
            if ((hit.point - last).sqrMagnitude < .12f) continue;
            PlaceMark(hit, lethal, i == 0 ? 1f : .6f);
            last = hit.point;
        }
    }

    public static void SpawnDrip(Vector3 origin)
    {
        if (FindSurface(origin, Vector3.down, 2f, out var hit) && hit.normal.y > .65f)
            PlaceMark(hit, false, .22f);
    }

    private static void PlaceMark(RaycastHit hit, bool lethal, float scale)
    {
        Material material = BloodMaterial(Random.Range(0, bloodMaterials.Length));
        if (!material) return;
        bool floor = hit.normal.y > .65f;
        float size = (floor ? (lethal ? 1.7f : 1.05f) : (lethal ? 1.1f : .7f)) * Random.Range(.85f, 1.12f) * scale;
        Quaternion rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        // Shrink near edges, using the same character-filtered query as placement.
        bool supported = false;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            supported = SupportsMark(hit.collider, hit.point, hit.normal, rotation, size);
            if (supported) break;
            size *= .65f;
        }
        if (!supported) return;
        while (marks.Count > 0 && !marks.Peek()) marks.Dequeue();
        if (marks.Count >= MaxMarks) { var oldest = marks.Dequeue(); if (oldest) Destroy(oldest.gameObject); }
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "VFX_BloodMark";
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        go.transform.SetPositionAndRotation(hit.point + hit.normal * 0.008f, rotation);
        go.transform.localScale = Vector3.one * size;
        go.transform.SetParent(hit.collider.transform, true);
        var mark = go.AddComponent<CombatBloodMark>();
        mark.support = hit.collider;
        mark.meshRenderer = go.GetComponent<Renderer>();
        mark.meshRenderer.sharedMaterial = material;
        mark.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        mark.meshRenderer.receiveShadows = false;
        mark.ApplyColor();
        marks.Enqueue(mark);
    }

    public static bool SupportsMark(Transform target, Vector3 point, Vector3 normal, float size)
    {
        return target && SupportsMark(target.GetComponent<Collider>(), point, normal,
            Quaternion.LookRotation(normal), size);
    }

    private static bool SupportsMark(Collider surface, Vector3 point, Vector3 normal, Quaternion rotation, float size)
    {
        // Coarse boxes/convex hulls can span empty space around cars and railings.
        // Persistent marks require a surface collider that follows the actual geometry.
        if (!surface || !surface.enabled || surface.GetComponentInParent<HealthManager>()) return false;
        if (!(surface is TerrainCollider) && !(surface is MeshCollider mesh && !mesh.convex)) return false;
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 5; x++)
        {
            Vector3 sample = point + rotation * new Vector3((x / 4f - .5f) * size, (y / 4f - .5f) * size, 0);
            if (!surface.Raycast(new Ray(sample + normal * .025f, -normal), out var contact, .05f)
                || Vector3.Dot(contact.normal, normal) < .98f
                || Mathf.Abs(Vector3.Dot(contact.point - sample, normal)) > .006f) return false;
        }
        return true;
    }

    private static Material BloodMaterial(int variant)
    {
        if (bloodMaterials[variant]) return bloodMaterials[variant];
        var shader = Resources.Load<Shader>("VFX/CombatBloodMark");
        if (!shader) return null;
        const int resolution = 128;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        { name = "BloodPool_" + variant, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var random = new System.Random(831 + variant * 71);
        var drops = new Vector3[14];
        for (int i = 0; i < drops.Length; i++) {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = .55f + (float)random.NextDouble() * .3f;
            drops[i] = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance,
                .015f + (float)random.NextDouble() * .045f);
        }
        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++) {
            float px = x / (resolution - 1f) * 2f - 1f, py = y / (resolution - 1f) * 2f - 1f;
            float angle = Mathf.Atan2(py, px);
            float radius = Mathf.Sqrt(px * px * 1.2f + py * py * .85f);
            float noise = Mathf.PerlinNoise(px * 7f + 21f + variant * 8f, py * 7f + 9f);
            float edge = .5f + .09f * Mathf.Sin(angle * 3f + variant) +
                .055f * Mathf.Sin(angle * 7f + variant * 2f) + (noise - .5f) * .08f;
            float alpha = Mathf.SmoothStep(0f, 1f, (edge - radius) / .045f);
            foreach (var drop in drops) {
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(drop.x, drop.y));
                alpha = Mathf.Max(alpha, Mathf.Clamp01((drop.z - distance) / .012f));
            }
            // Dense dark center, thinner reddish edges and uneven absorption.
            float density = Mathf.Clamp01(radius / .65f);
            float red = Mathf.Lerp(.65f, 1f, density) * Mathf.Lerp(.85f, 1f, noise);
            pixels[y * resolution + x] = new Color(red, red * .8f, red * .7f,
                alpha * Mathf.Lerp(.95f, .65f, density));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        bloodMaterials[variant] = new Material(shader) { name = "Blood pool " + variant, mainTexture = texture };
        return bloodMaterials[variant];
    }

    private static bool FindSurface(Vector3 from, Vector3 direction, float distance, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(from, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit[] candidates = count == hits.Length
            ? Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore) : hits;
        if (candidates != hits) count = candidates.Length;
        result = default;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (candidates[i].collider.GetComponentInParent<HealthManager>() || candidates[i].distance >= nearest) continue;
            nearest = candidates[i].distance;
            result = candidates[i];
        }
        return nearest < float.MaxValue;
    }

    private void Update()
    {
        if (!support || !support.enabled || !support.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (age >= Lifetime - 2f) ApplyColor();
        if (age >= Lifetime) Destroy(gameObject);
    }

    private void ApplyColor()
    {
        block.SetColor("_Color", new Color(.72f, .012f, .018f, .92f * Mathf.Clamp01((Lifetime - age) / 2f)));
        meshRenderer.SetPropertyBlock(block);
    }
}
