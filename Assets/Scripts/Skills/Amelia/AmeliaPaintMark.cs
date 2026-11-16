using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bounded, surface-checked scorch marks left by the cluster grenade.</summary>
public sealed class AmeliaPaintMark : MonoBehaviour
{
    private const int Limit = 72;
    private const float Lifetime = 25f;
    private static readonly Queue<AmeliaPaintMark> marks = new();
    private static readonly Material[] materials = new Material[3];
    private Renderer surface;
    private MaterialPropertyBlock block;
    private Color color;
    private float age;

    // Build the small shared texture set on equip rather than during the first explosion.
    public static void Prepare()
    {
        for (int i = 0; i < materials.Length; i++) GetMaterial(i);
    }

    public static void Splash(Vector3 center, float radius, bool primary)
    {
        Vector3 origin = center + Vector3.up * .15f;
        Place(origin, Vector3.down, radius, primary ? 2.6f : 1.5f, true);
        int count = primary ? 4 : 2;
        float offset = Random.Range(0f, 360f);
        bool groundNearby = Physics.Raycast(origin, Vector3.down, out RaycastHit floor,
            radius, ~0, QueryTriggerInteraction.Ignore) && floor.normal.y > .5f;
        for (int i = 0; i < count; i++)
        {
            float angle = (offset + i * 360f / count) * Mathf.Deg2Rad;
            Vector3 flat = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 direction = (flat + Vector3.down * .5f).normalized;
            if (groundNearby && i % 3 != 0)
            {
                // Aim at spread-out floor positions: a fixed down angle piles every mark beneath the blast.
                Vector3 destination = floor.point + flat * radius * Random.Range(.15f, .45f);
                direction = (destination - origin).normalized;
            }
            else if (i % 3 == 0) direction = (flat + Vector3.up * .12f).normalized;
            Place(origin, direction, radius, Random.Range(.65f, primary ? 1.9f : 1.2f), i % 3 != 0);
        }
    }

    private static void Place(Vector3 from, Vector3 direction, float range, float size, bool orange)
    {
        // The first solid surface blocks paint, including characters in front of a wall.
        if (!Physics.Raycast(from, direction, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore) ||
            hit.collider.GetComponentInParent<HealthManager>() ||
            (hit.rigidbody != null && !hit.rigidbody.isKinematic)) return;
        Quaternion rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        bool fits = false;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            fits = true;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector3 edge = hit.point + rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size * .5f;
                if (!Physics.Raycast(edge + hit.normal * .06f, -hit.normal, out RaycastHit support,
                    .12f, ~0, QueryTriggerInteraction.Ignore) || support.collider != hit.collider ||
                    Vector3.Dot(support.normal, hit.normal) < .98f)
                { fits = false; break; }
            }
            if (fits) break;
            size *= .6f;
        }
        if (!fits) return;
        Material material = GetMaterial(Random.Range(0, materials.Length));
        if (material == null) return;
        while (marks.Count > 0 && !marks.Peek()) marks.Dequeue();
        while (marks.Count >= Limit)
        {
            var oldest = marks.Dequeue();
            if (oldest) Destroy(oldest.gameObject);
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Amelia blast scorch";
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        go.transform.SetPositionAndRotation(hit.point + hit.normal * Random.Range(.006f, .009f), rotation);
        go.transform.localScale = Vector3.one * size;
        go.transform.SetParent(hit.collider.transform, true);
        var mark = go.AddComponent<AmeliaPaintMark>();
        mark.surface = go.GetComponent<Renderer>();
        mark.surface.sharedMaterial = material;
        mark.surface.shadowCastingMode = ShadowCastingMode.Off;
        mark.surface.receiveShadows = false;
        mark.block = new MaterialPropertyBlock();
        mark.color = orange ? new Color(.045f, .035f, .025f, .82f) : new Color(.07f, .06f, .045f, .65f);
        mark.Apply();
        marks.Enqueue(mark);
    }

    private static Material GetMaterial(int variant)
    {
        if (materials[variant] != null) return materials[variant];
        var shader = Resources.Load<Shader>("VFX/CombatBloodMark");
        if (shader == null) return null;
        const int size = 192;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "AmeliaScorchMask_" + variant, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float px = x / (size - 1f) * 2f - 1f, py = y / (size - 1f) * 2f - 1f;
            float a = Mathf.Atan2(py, px), r = Mathf.Sqrt(px * px + py * py);
            float noise = Mathf.PerlinNoise(px * 5f + 19f + variant * 7f, py * 5f + 13f);
            float edge = .72f + .08f * Mathf.Sin(a * 9f + variant) + (noise - .5f) * .15f;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - r) / .38f));
            alpha *= Mathf.Lerp(.45f, 1f, noise);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        materials[variant] = new Material(shader) { name = "Amelia scorch " + variant, mainTexture = texture };
        return materials[variant];
    }

    private void Update()
    {
        age += Time.deltaTime;
        Apply();
        if (age >= Lifetime) Destroy(gameObject);
    }

    private void Apply()
    {
        Color tint = color;
        tint.a *= Mathf.Clamp01((Lifetime - age) / 3f);
        block.SetColor("_Color", tint);
        surface.SetPropertyBlock(block);
    }
}
