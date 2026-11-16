using System.Collections.Generic;
using UnityEngine;

/// <summary>Individual boards, rails and posts replace an intact span at the instant of fracture.</summary>
public sealed class VandalismTimberFracture : MonoBehaviour
{
    private struct Fragment
    {
        public Transform transform;
        public Vector3 position, velocity, spin;
        public Quaternion rotation;
        public float drag;
    }
    private readonly List<Fragment> fragments = new();
    private Mesh timber;
    private float age;

    public static void Break(GameObject span, Transform owner, Vector3 blast, int budget, int seed, bool explosive = true, Vector3 inheritedVelocity = default)
    {
        var go = new GameObject("Separated deck boards, rails and posts");
        go.transform.SetParent(owner, false);
        var fracture = go.AddComponent<VandalismTimberFracture>();
        fracture.Build(span, blast, budget, seed, explosive, inheritedVelocity);
        // The complete bridge section must disappear, not continue tumbling underneath debris.
        span.SetActive(false);
    }

    private void Build(GameObject span, Vector3 blast, int budget, int seed, bool explosive, Vector3 inheritedVelocity)
    {
        timber = BrokenTimberMesh();
        Material wood = null;
        foreach (var r in span.GetComponentsInChildren<MeshRenderer>())
            if (r.enabled && r.sharedMaterial) { wood = r.sharedMaterial; break; }
        var random = new System.Random(seed);
        var lod = span.GetComponent<LODGroup>();
        Vector3 center = lod ? lod.localReferencePoint : new Vector3(2.9f, 1.9f, -.78f);
        // Coordinates follow the authored pier: X runs along its length, Y is height.
        float length = 5.8f, width = span.transform.Find("Opposite deck half") ? 4f : 3.2f;
        int deckPieces = Mathf.Max(6, budget - 10);
        for (int i = 0; i < budget; i++)
        {
            Vector3 local, size;
            float heaviness;
            if (i < deckPieces)
            {
                // Split each crosswise board into independently ejected left/right halves.
                int row = i / 2;
                float x = center.x - length * .5f + (row + .5f) * length / Mathf.Ceil(deckPieces / 2f);
                float z = center.z + (i % 2 == 0 ? -.25f : .25f) * width;
                local = new Vector3(x, 2.7f, z);
                size = new Vector3(length / Mathf.Ceil(deckPieces / 2f) * .9f, .13f, width * .48f);
                heaviness = .75f;
            }
            else
            {
                int j = i - deckPieces;
                float side = j % 2 == 0 ? -1 : 1;
                float x = center.x + (j / 2 % 3 - 1) * length * .32f;
                if (j < 4)
                {
                    local = new Vector3(x, 3.55f, center.z + side * width * .5f);
                    size = new Vector3(2f, .16f, .15f); heaviness = .85f;
                }
                else if (j < 8)
                {
                    local = new Vector3(x, 2.9f, center.z + side * width * .5f);
                    size = new Vector3(.18f, 1.5f, .18f); heaviness = 1.2f;
                }
                else
                {
                    local = new Vector3(center.x, 2.35f, center.z + side * width * .37f);
                    size = new Vector3(2.6f, .27f, .26f); heaviness = 1.6f;
                }
            }
            var piece = new GameObject(i < deckPieces ? "Split deck plank" : "Broken rail or support");
            piece.transform.SetParent(transform, false);
            piece.transform.position = span.transform.TransformPoint(local);
            piece.transform.rotation = span.transform.rotation * Quaternion.Euler(0, (float)random.NextDouble() * 10f - 5f, 0);
            piece.transform.localScale = Vector3.Scale(size, span.transform.lossyScale);
            piece.AddComponent<MeshFilter>().sharedMesh = timber;
            piece.AddComponent<MeshRenderer>().sharedMaterial = wood;
            Vector3 radial = Vector3.ProjectOnPlane(piece.transform.position - blast, Vector3.up).normalized;
            Vector3 scatter = span.transform.forward * ((float)random.NextDouble() * 2f - 1f)
                + span.transform.right * ((float)random.NextDouble() * 2f - 1f);
            float strength = 1.8f + (float)random.NextDouble() * 2.2f;
            fragments.Add(new Fragment {
                transform = piece.transform, position = piece.transform.position, rotation = piece.transform.rotation,
                drag = .65f / heaviness,
                velocity = explosive
                    ? (radial * strength + scatter * 1.1f + Vector3.up * (1.5f + (float)random.NextDouble() * 3f)) / heaviness
                    : inheritedVelocity + scatter * .4f + Vector3.down * (1f + (float)random.NextDouble() * 1.2f),
                spin = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f,
                    (float)random.NextDouble() - .5f) * ((explosive ? 240f : 55f) / heaviness)
            });
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        // Air drag slows light chips; gravity gives the heavy beams a fast, weighty descent.
        
        foreach (var f in fragments)
        {
            float travel = (1f - Mathf.Exp(-f.drag * age)) / f.drag;
            f.transform.position = f.position + f.velocity * travel + Vector3.down * (4.9f * age * age);
            f.transform.rotation = f.rotation * Quaternion.Euler(f.spin * travel);
        }
        if (age > 6f) Destroy(gameObject);
    }

    private static Mesh BrokenTimberMesh()
    {
        // Uneven torn ends, rather than pristine cube ends, remain visible on larger timbers.
        Vector3[] corners = {
            new(-.5f,-.5f,-.5f), new(.43f,-.5f,-.5f), new(.5f,.5f,-.5f), new(-.39f,.5f,-.5f),
            new(-.44f,-.5f,.5f), new(.5f,-.5f,.5f), new(.38f,.5f,.5f), new(-.5f,.5f,.5f)
        };
        int[] faces = { 0,3,2,1, 4,5,6,7, 0,1,5,4, 3,7,6,2, 0,4,7,3, 1,2,6,5 };
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int f = 0; f < 6; f++)
        {
            int first = vertices.Count;
            for (int v = 0; v < 4; v++) vertices.Add(corners[faces[f * 4 + v]]);
            uv.Add(new Vector2(0,0)); uv.Add(new Vector2(0,1)); uv.Add(new Vector2(1,1)); uv.Add(new Vector2(1,0));
            triangles.AddRange(new[] { first, first+1, first+2, first, first+2, first+3 });
        }
        var mesh = new Mesh { name = "Splintered timber" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy() { if (timber) Destroy(timber); }
}
