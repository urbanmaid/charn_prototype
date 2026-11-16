using UnityEngine;

/// <summary>
/// Shared generated resources for bird visuals: quad mesh, hawk and crow textures, gold (additive), dark and crow (alpha) materials.
/// </summary>
public static class HawkShape
{
    private static Mesh quad;
    private static Texture2D texture, crowTexture;
    private static Material gold, dark, crow;

    public static Mesh Quad
    {
        get
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "HawkQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white }; // Particle shader multiplies vertex color
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }

    /// <summary> Glowing gold (additive). Tint with _BaseColor through a MaterialPropertyBlock. </summary>
    public static Material Gold
    {
        get
        {
            if (gold != null) return gold;
            gold = new Material(SkillVfxFactory.GetAdditiveMaterial(Texture));
            if (gold.HasProperty("_Cull")) gold.SetFloat("_Cull", 0f);
            gold.name = "VFX_HawkGold";
            return gold;
        }
    }

    /// <summary> Dark silhouette / ground shadow (alpha blended). </summary>
    public static Material Dark
    {
        get
        {
            if (dark != null) return dark;
            dark = new Material(SkillVfxFactory.GetAlphaMaterial(Texture));
            if (dark.HasProperty("_Cull")) dark.SetFloat("_Cull", 0f);
            dark.name = "VFX_HawkDark";
            return dark;
        }
    }

    /// <summary> Crow (alpha blended, cool black with feather sheen). Tint with _BaseColor through a MaterialPropertyBlock. </summary>
    public static Material Crow
    {
        get
        {
            if (crow != null) return crow;
            crow = new Material(SkillVfxFactory.GetAlphaMaterial(CrowTexture));
            if (crow.HasProperty("_Cull")) crow.SetFloat("_Cull", 0f);
            crow.name = "VFX_Crow";
            return crow;
        }
    }

    /// <summary> Renderer with the bird quad as a child of parent. </summary>
    public static MeshRenderer CreateQuad(string name, Transform parent, Material material)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = Quad;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    #region Texture

    // Detailed birds seen from below (head up), built from shaded tapered feathers
    // (darker edges, bright shaft, optional cross bars, dark tip). RGB is a tinted luminance, A is the coverage.
    private const int TexSize = 512;
    private const float AA = 2f / TexSize * 1.2f;
    private static float[] texAlpha, texLum;

    /// <summary> Hawk: barred fan tail, fingered primaries, streaked chest, hooked beak. Warm tint. </summary>
    public static Texture2D Texture => texture != null ? texture : texture = Generate(false);

    /// <summary> Crow: rounded plain tail, seven wide-spread fingers, stocky body, heavy beak. Cool tint. </summary>
    public static Texture2D CrowTexture => crowTexture != null ? crowTexture : crowTexture = Generate(true);

    private static Texture2D Generate(bool isCrow)
    {
        const int n = TexSize;
        texAlpha = new float[n * n];
        texLum = new float[n * n];
        for (int i = 0; i < texLum.Length; i++) texLum[i] = 1f;

        // Tail: hawk has a broad barred fan, crow a rounded plain one
        int tailCount = isCrow ? 10 : 12;
        for (int i = 0; i < tailCount; i++)
        {
            float k = i - (tailCount - 1) * 0.5f;
            float ang = (-90f + k * (isCrow ? 3.4f : 4.2f)) * Mathf.Deg2Rad;
            Vector2 b = new Vector2(k * (isCrow ? 0.013f : 0.012f), isCrow ? -0.18f : -0.2f);
            float length = isCrow ? 0.3f - Mathf.Abs(k) * 0.006f : 0.4f;
            Feather(false, b, b + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * length,
                isCrow ? 0.036f : 0.032f, 0.042f, isCrow ? 0.15f : 0.35f, isCrow ? 0 : 4, 0.5f);
        }
        // Secondaries: trailing edge of the arm
        for (int i = 0; i < 13; i++)
        {
            float bx = 0.08f + i * 0.043f;
            Vector2 b = new Vector2(bx, WingEdge(bx) - 0.12f);
            float ang = (-92f + i * (isCrow ? 2.2f : 2.4f)) * Mathf.Deg2Rad;
            float length = isCrow ? 0.23f - i * 0.003f : 0.24f - i * 0.004f;
            Feather(true, b, b + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * length,
                isCrow ? 0.037f : 0.036f, isCrow ? 0.034f : 0.032f, isCrow ? 0.15f : 0.4f, isCrow ? 0 : 3, 0.5f);
        }
        // Primaries: hawk tips split into fingers, crow has seven clearly separated fingers
        float[] angles = isCrow
            ? new[] { 22f, 9f, -4f, -17f, -30f, -43f, -56f }
            : new[] { 18f, 10f, 3f, -4f, -11f, -18f, -26f, -34f, -42f, -50f };
        float[] lengths = isCrow
            ? new[] { 0.30f, 0.36f, 0.38f, 0.37f, 0.34f, 0.30f, 0.26f }
            : new[] { 0.30f, 0.36f, 0.39f, 0.40f, 0.39f, 0.36f, 0.33f, 0.30f, 0.27f, 0.25f };
        for (int i = angles.Length - 1; i >= 0; i--)
        {
            float ang = angles[i] * Mathf.Deg2Rad;
            Vector2 b = isCrow ? new Vector2(0.55f - i * 0.016f, 0.15f - i * 0.026f) : new Vector2(0.56f - i * 0.012f, 0.15f - i * 0.018f);
            Feather(true, b, b + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * lengths[i],
                0.05f, isCrow ? 0.018f : 0.022f, isCrow ? 0.2f : 0.55f, isCrow ? 0 : 3, isCrow ? 0.5f : 0.45f);
        }

        float bodyRx = isCrow ? 0.14f : 0.12f, bodyY = isCrow ? 0f : -0.02f;
        float headR = isCrow ? 0.085f : 0.07f, headRy = isCrow ? 0.09f : 0.08f, headY = isCrow ? 0.27f : 0.26f;
        float beakStart = isCrow ? 0.33f : 0.32f, beakLength = isCrow ? 0.11f : 0.07f, beakWidth = isCrow ? 0.04f : 0.028f;

        for (int py = 0; py < n; py++)
        {
            float y = py / (n - 1f) * 2f - 1f;
            for (int px = 0; px < n; px++)
            {
                float x = px / (n - 1f) * 2f - 1f;
                float ax = Mathf.Abs(x);
                int idx = py * n + px;

                // Arm with layered covert scallops
                float le = WingEdge(ax);
                float depth = 0.28f - 0.08f * Mathf.Clamp01(ax / 0.66f);
                float wing = ax < 0.64f && y < le && y > le - depth ? Mathf.Clamp01(Mathf.Min(le - y, y - (le - depth)) / AA) : 0f;
                wing = Mathf.Max(wing, Cover(Mathf.Sqrt((ax - 0.6f) * (ax - 0.6f) + (y - 0.1f) * (y - 0.1f)) - 0.1f));
                if (wing > 0f)
                {
                    float v = Mathf.Clamp01((le - y) / depth);
                    float row = Mathf.Floor(v * 4f);
                    float fx = Frac(ax * 26f + row * 0.5f) - 0.5f;
                    float fy = Frac(v * 4f);
                    float scallop = Mathf.Clamp01(1f - (fx * fx * 3.2f + (1f - fy) * (1f - fy)));
                    float shade = (0.6f + 0.4f * Mathf.Sqrt(scallop)) * (v < 0.25f ? 1f : 0.92f);
                    Paint(idx, wing, shade);
                }

                // Body: hawk chest is streaked, crow is plain
                float bx = x / bodyRx, by = (y - bodyY) / 0.24f;
                float body = Cover((Mathf.Sqrt(bx * bx + by * by) - 1f) * (isCrow ? 0.13f : 0.12f));
                if (body > 0f)
                {
                    float lighter = Mathf.Clamp01((y + 0.2f) / 0.4f);
                    float shade = isCrow
                        ? 0.85f + 0.1f * lighter
                        : (0.8f + 0.2f * Mathf.Cos(x * 55f + Mathf.Sin(y * 20f) * 1.2f)) * (0.9f + 0.1f * lighter);
                    Paint(idx, body, shade);
                }

                // Head and beak
                float hx = x / headR, hy = (y - headY) / headRy;
                float head = Cover(Mathf.Sqrt(hx * hx + hy * hy) * headR - headR);
                if (head > 0f) Paint(idx, head, 0.95f);
                if (y > beakStart && y < beakStart + beakLength)
                {
                    float bt = Mathf.Clamp01((y - beakStart) / beakLength);
                    float taper = isCrow ? 1f - bt * Mathf.Sqrt(bt) : 1f - bt;
                    float beak = Cover(ax - (beakWidth * taper + (isCrow ? 0.004f : 0.006f)));
                    if (beak > 0f) Paint(idx, beak, isCrow ? 0.75f : 0.6f);
                }
            }
        }

        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            name = isCrow ? "VFX_CrowShape" : "VFX_HawkShape"
        };
        Color32[] pixels = new Color32[n * n];
        for (int i = 0; i < pixels.Length; i++)
        {
            float l = Mathf.Clamp01(texLum[i]);
            pixels[i] = isCrow
                ? new Color(l * 0.86f, l * 0.92f, l, texAlpha[i])
                : new Color(l, l * 0.88f, l * 0.68f, texAlpha[i]);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        texAlpha = texLum = null;
        return tex;
    }

    // Leading edge of the wing: rises from the shoulder and arches over the wrist
    private static float WingEdge(float ax) => 0.13f + 0.09f * Mathf.Sin(Mathf.PI * Mathf.Clamp(ax, 0f, 0.7f) / 0.7f * 0.9f);

    private static float Cover(float distance) => Mathf.Clamp01(0.5f - distance / AA);

    private static float Frac(float v) => v - Mathf.Floor(v);

    private static void Paint(int idx, float coverage, float shade)
    {
        texLum[idx] = texLum[idx] * (1f - coverage) + shade * coverage;
        texAlpha[idx] = Mathf.Max(texAlpha[idx], coverage);
    }

    // Tapered feather from base a to tip b (right wing coordinates); mirrored also draws it on the left wing
    private static void Feather(bool mirrored, Vector2 a, Vector2 b, float ra, float rb, float darkTip, int bars, float edge)
    {
        const int n = TexSize;
        float pad = Mathf.Max(ra, rb) + AA;
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float minX = Mathf.Min(a.x, b.x) - pad, maxX = Mathf.Max(a.x, b.x) + pad;
        int y0 = ToPixel(Mathf.Min(a.y, b.y) - pad), y1 = ToPixel(Mathf.Max(a.y, b.y) + pad);

        for (int side = 0; side < (mirrored ? 2 : 1); side++)
        {
            float sign = side == 0 ? 1f : -1f;
            int x0 = ToPixel(side == 0 ? minX : -maxX), x1 = ToPixel(side == 0 ? maxX : -minX);
            for (int py = y0; py <= y1; py++)
            {
                float y = py / (n - 1f) * 2f - 1f;
                for (int px = x0; px <= x1; px++)
                {
                    float x = (px / (n - 1f) * 2f - 1f) * sign;
                    float t = Mathf.Clamp01(((x - a.x) * ab.x + (y - a.y) * ab.y) / len2);
                    float cx = a.x + ab.x * t, cy = a.y + ab.y * t;
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float r = ra + (rb - ra) * t;
                    float d = dist - r;
                    float c = Cover(d);
                    if (c <= 0f) continue;

                    float inner = Mathf.Clamp01(-d / Mathf.Max(r, 1e-3f));
                    float shade = (1f - edge) + edge * Mathf.Sqrt(inner);
                    if (t < 0.93f) shade += 0.22f * Mathf.Clamp01(1f - dist / 0.005f);              // Shaft
                    if (bars > 0 && Mathf.Cos(t * Mathf.PI * 2f * bars) <= -0.2f) shade *= 0.78f;   // Cross bars
                    shade *= 1f - darkTip * Mathf.Clamp01((t - 0.75f) / 0.25f);                      // Dark tip
                    Paint(py * n + px, c, shade);
                }
            }
        }
    }

    private static int ToPixel(float v) => Mathf.Clamp(Mathf.RoundToInt((v + 1f) * 0.5f * (TexSize - 1)), 0, TexSize - 1);

    #endregion
}

/// <summary>
/// Hawk-shaped shadow draped over whatever is below: every vertex of a grid is raycast down on its own,
/// so the shadow bends over slopes, steps and characters instead of the whole quad jumping to one hit point.
/// </summary>
public class HawkShadow
{
    private const float SurfaceOffset = 0.04f;

    private readonly int resolution;
    private readonly GameObject gameObject;
    private readonly Mesh mesh;
    private readonly MeshRenderer renderer;
    private readonly MaterialPropertyBlock block = new();
    private readonly Vector3[] vertices;
    private readonly Color[] colors;
    private float groundY = float.NaN;

    public HawkShadow(string name, int resolution)
    {
        this.resolution = resolution;
        int count = resolution * resolution;
        vertices = new Vector3[count];
        colors = new Color[count];
        Vector2[] uv = new Vector2[count];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        for (int j = 0, t = 0; j < resolution; j++)
        {
            for (int i = 0; i < resolution; i++)
            {
                uv[j * resolution + i] = new Vector2(i / (resolution - 1f), j / (resolution - 1f));
                if (i == resolution - 1 || j == resolution - 1) continue;
                int a = j * resolution + i;
                triangles[t++] = a; triangles[t++] = a + resolution; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = a + resolution; triangles[t++] = a + resolution + 1;
            }
        }

        mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.triangles = triangles;

        // World-space mesh: the object itself stays at the origin
        gameObject = new GameObject(name);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = HawkShape.Dark;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public void SetActive(bool active)
    {
        if (gameObject != null) gameObject.SetActive(active);
    }

    public void Destroy()
    {
        if (gameObject != null) Object.Destroy(gameObject);
        if (mesh != null) Object.Destroy(mesh);
    }

    /// <summary> Drape the shadow under position (source height), head pointing along heading. </summary>
    public void Place(Vector3 position, Vector3 heading, float width, float length, float alpha)
    {
        heading.y = 0f;
        if (heading.sqrMagnitude < 1e-6f) heading = Vector3.forward;
        Vector3 forward = heading.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        bool anyHit = false;
        float sumY = 0f;
        int hits = 0;
        for (int j = 0; j < resolution; j++)
        {
            float v = j / (resolution - 1f) - 0.5f;
            for (int i = 0; i < resolution; i++)
            {
                float u = i / (resolution - 1f) - 0.5f;
                int k = j * resolution + i;
                Vector3 origin = position + right * (u * width) + forward * (v * length);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    vertices[k] = hit.point + Vector3.up * SurfaceOffset + hit.normal * SurfaceOffset;
                    colors[k] = Color.white;
                    sumY += hit.point.y;
                    hits++;
                    anyHit = true;
                }
                else
                {
                    // Nothing below (off the edge of the map): keep the vertex, hide it
                    vertices[k] = new Vector3(origin.x, float.IsNaN(groundY) ? position.y - 20f : groundY, origin.z);
                    colors[k] = Color.clear;
                }
            }
        }
        if (hits > 0) groundY = sumY / hits;

        gameObject.SetActive(anyHit);
        if (!anyHit) return;
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
        block.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
        renderer.SetPropertyBlock(block);
    }
}
