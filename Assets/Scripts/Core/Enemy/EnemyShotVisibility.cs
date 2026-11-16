using UnityEngine;

/// <summary>Bounded, depth-tested moving tracers for hostile fire; never extends past the actual hit.</summary>
public sealed class EnemyShotVisibility : MonoBehaviour
{
    private static EnemyShotVisibility instance;
    private const int Capacity = 32;
    private readonly LineRenderer[] lines = new LineRenderer[Capacity];
    private readonly Vector3[] starts = new Vector3[Capacity], ends = new Vector3[Capacity];
    private readonly float[] ages = new float[Capacity];
    private int next;
    private Material material;

    public static void Show(Vector3 start, Vector3 end)
    {
        var camera = Camera.main;
        if (!camera || Vector3.Distance(camera.transform.position, start) > 150f) return;
        if (!instance) instance = new GameObject("Hostile shot visibility pool").AddComponent<EnemyShotVisibility>();
        instance.Emit(start, end);
    }

    private void Emit(Vector3 start, Vector3 end)
    {
        int slot = next++ % Capacity;
        if (!material)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader) return;
            material = new Material(shader);
        }
        if (!lines[slot])
        {
            var go = new GameObject("Incoming tracer");
            go.transform.SetParent(transform);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = .055f; line.endWidth = .025f;
            line.startColor = new Color(1f, .5f, .12f, .85f);
            line.endColor = new Color(1f, .92f, .65f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lines[slot] = line;
        }
        starts[slot] = start; ends[slot] = start + Vector3.ClampMagnitude(end - start, 150f);
        ages[slot] = 0; lines[slot].enabled = true;
        lines[slot].SetPosition(0, start); lines[slot].SetPosition(1, start);
    }

    private void Update()
    {
        for (int i = 0; i < Capacity; i++)
        {
            var line = lines[i];
            if (!line || !line.enabled) continue;
            ages[i] += Time.deltaTime;
            Vector3 delta = ends[i] - starts[i];
            float distance = delta.magnitude;
            float head = ages[i] * 180f;
            if (head > distance + 5f) { line.enabled = false; continue; }
            Vector3 direction = delta.normalized;
            line.SetPosition(0, starts[i] + direction * Mathf.Clamp(head - 5f, 0, distance));
            line.SetPosition(1, starts[i] + direction * Mathf.Min(head, distance));
        }
    }

    private void OnDestroy() { if (material) Destroy(material); if (instance == this) instance = null; }
}
