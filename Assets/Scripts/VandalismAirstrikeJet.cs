using System.Collections.Generic;
using UnityEngine;

/// <summary>Original fighter silhouette for the local airstrike prototype.</summary>
public sealed class VandalismAirstrikeJet : MonoBehaviour
{
    private Mesh mesh;
    private Material skin;

    public static Transform Create(Transform parent)
    {
        var go = new GameObject("Airstrike fighter");
        go.transform.SetParent(parent);
        go.AddComponent<VandalismAirstrikeJet>().Build();
        return go.transform;
    }

    private void Build()
    {
        skin = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        skin.color = new Color(.12f, .15f, .18f);
        skin.SetColor("_BaseColor", skin.color);
        skin.SetFloat("_Metallic", .55f);
        skin.SetFloat("_Smoothness", .35f);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            // Thin aerodynamic surfaces remain legible from above and below.
            vertices.Add(c); vertices.Add(b); vertices.Add(a);
            triangles.Add(i + 3); triangles.Add(i + 4); triangles.Add(i + 5);
        }
        Vector3 nose = new(0, 0, 8), tail = new(0, 0, -6);
        Vector3 left = new(-.8f, 0, 0), right = new(.8f, 0, 0);
        Vector3 top = new(0, .8f, 0), bottom = new(0, -.55f, 0);
        Face(nose, left, top); Face(nose, top, right);
        Face(nose, right, bottom); Face(nose, bottom, left);
        Face(tail, top, left); Face(tail, right, top);
        Face(tail, bottom, right); Face(tail, left, bottom);
        foreach (float side in new[] { -1f, 1f })
        {
            Face(new Vector3(side * .5f, 0, 3), new Vector3(side * 7f, -.1f, -2.6f), new Vector3(side * .6f, 0, -3.5f));
            Face(new Vector3(side * .4f, .15f, -3.8f), new Vector3(side * 3f, .2f, -6f), new Vector3(side * .4f, .15f, -6f));
            Face(new Vector3(side * .45f, .2f, -3), new Vector3(side * 1.4f, 2.6f, -5.5f), new Vector3(side * .55f, .2f, -6f));
        }
        mesh = new Mesh { name = "Airstrike swept-wing fighter" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = skin;
        foreach (float x in new[] { -.4f, .4f })
        {
            var trailObject = new GameObject("Engine exhaust");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = new Vector3(x, 0, -5.8f);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney("smoke_04"));
            trail.time = .55f; trail.startWidth = .28f; trail.endWidth = 1.2f;
            trail.startColor = new Color(.7f, .75f, .8f, .3f); trail.endColor = new Color(.5f, .55f, .6f, 0);
            trail.minVertexDistance = .5f;
        }
    }

    private void OnDestroy()
    {
        if (mesh) Destroy(mesh);
        if (skin) Destroy(skin);
    }
}
