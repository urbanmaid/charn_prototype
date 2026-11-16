using System.Collections.Generic;
using UnityEngine;

/// <summary>One owned bridge from the selected pier entrance to actual terrain across water.</summary>
public static class VandalismConnectedBridge
{
    public static void RepairPierCollision(Transform stage)
    {
        int repaired = 0;
        foreach (var group in stage.GetComponentsInChildren<LODGroup>(true))
        {
            if (!group.name.StartsWith("EA03_Prop_Pier_")) continue;
            var levels = group.GetLODs();
            if (levels.Length == 0) continue;
            bool valid = true;
            foreach (var renderer in levels[0].renderers)
                if (!renderer || !renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) valid = false;
            if (!valid || levels[0].renderers.Length == 0) continue;
            // Includes collider overrides serialized in the map, not just the source prefab.
            foreach (var old in group.GetComponentsInChildren<Collider>(true))
                if (!old.isTrigger) { old.enabled = false; Object.Destroy(old); }
            foreach (var renderer in levels[0].renderers)
            {
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = false;
            }
            repaired++;
        }
        GameplayDiagnostics.Step("PIER COLLISION repaired instances=" + repaired);
    }

    public static bool Build(Transform parent, Vector3 entrance, Vector3 preferredDirection,
        out Transform root, out Vector3 landing, List<GameObject> spans)
    {
        root = null; landing = default;
        var prefab = Resources.Load<GameObject>("Vandalism/DestructiblePier");
        if (!prefab || !FindOppositeBank(parent, entrance, preferredDirection, out landing)) return false;
        var lod = prefab.GetComponent<LODGroup>();
        if (!lod || lod.GetLODs().Length == 0) return false;
        const float module = 6f;
        Vector3 tangent = Vector3.ProjectOnPlane(preferredDirection, Vector3.up).normalized;
        // First two spans continue the existing pier exactly. Only then bend towards the far bank.
        Vector3 join = entrance - tangent * .08f;
        Vector3 bendStart = entrance + tangent * 12f;
        Vector3 control = bendStart + tangent * 12f;
        Vector3 destination = landing;
        float distance = Vector3.Distance(bendStart, landing) + 12f;
        int curvedCount = Mathf.CeilToInt(distance / module);
        int count = curvedCount + 2;
        Vector3 Point(int index)
        {
            if (index <= 2) return Vector3.Lerp(join, bendStart, index / 2f);
            float t = (index - 2f) / curvedCount;
            return (1-t)*(1-t)*bendStart + 2*(1-t)*t*control + t*t*destination;
        }
        // Out parameters cannot be captured by local functions.
        root = new GameObject("Continuous riverside bridge - owned spans").transform;
        root.SetParent(parent);
        for (int i = 0; i < count; i++)
        {
            Vector3 from = Point(i), to = Point(i + 1);
            float step = Vector3.Distance(from, to);
            Vector3 deck = (from + to) * .5f;
            Quaternion rotation = Quaternion.LookRotation(to - from) * Quaternion.Euler(0, -90f, 0);
            var segment = Object.Instantiate(prefab, root);
            segment.name = $"Destructible bridge section {i + 1:D2}";
            segment.transform.localScale = new Vector3((step + .08f) / module, 1, 1);
            // Authored deck is 2.7m above this pier prefab's origin (posts continue beneath it).
            Vector3 offset = new Vector3(3f * segment.transform.localScale.x,
                2.7f, -2f);
            segment.transform.SetPositionAndRotation(deck - rotation * offset, rotation);
            foreach (Transform t in segment.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var group in segment.GetComponentsInChildren<LODGroup>())
            {
                var levels = group.GetLODs();
                group.enabled = false;
                for (int level = 0; level < levels.Length; level++)
                    foreach (var r in levels[level].renderers)
                        if (r) { r.gameObject.SetActive(level == 0); r.enabled = level == 0; }
            }
            // Match the map: mate at (6,0,-4), rotated 180 degrees; never mirror the mesh.
            var opposite = Object.Instantiate(prefab, segment.transform);
            opposite.name = "Opposite deck half";
            // Verified against the authored pier pair in vandalism_block_map.
            opposite.transform.localPosition = new Vector3(6f, 0, -4f);
            opposite.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            opposite.transform.localScale = Vector3.one;
            foreach (Transform child in opposite.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
            foreach (var group in opposite.GetComponentsInChildren<LODGroup>())
            {
                var levels = group.GetLODs(); group.enabled = false;
                for (int level = 0; level < levels.Length; level++)
                    foreach (var r in levels[level].renderers)
                        if (r) { r.gameObject.SetActive(level == 0); r.enabled = level == 0; }
            }
            var combinedLod = segment.GetComponent<LODGroup>();
            Vector3 reference = combinedLod.localReferencePoint; reference.x = 3f; reference.z = -2f;
            combinedLod.localReferencePoint = reference;
            segment.SetActive(true);
            spans.Add(segment);
        }
        Debug.Log($"Connected river bridge built: {entrance} -> {landing}; {distance:F1}m, {count} visible sections.", parent);
        return true;
    }

    private static bool FindOppositeBank(Transform stage, Vector3 start, Vector3 forward, out Vector3 bank)
    {
        bank = default;
        var terrains = stage.GetComponentsInChildren<Terrain>();
        if (terrains.Length == 0) return false;
        float water = -2f;
        foreach (Transform t in stage.GetComponentsInChildren<Transform>())
            if (t.name == "Water") { water = t.position.y; break; }
        float best = float.PositiveInfinity;
        // Require a real stretch of submerged terrain followed by supported land, not a longer dead-end pier.
        for (int angle = -25; angle <= 25; angle += 5)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
            float wetLength = 0;
            for (float d = 4f; d <= 300f; d += 2f)
            {
                Vector3 p = start + direction * d;
                if (!Height(terrains, p, out float h)) break;
                if (h < water + .15f) { wetLength += 2f; continue; }
                if (wetLength < 20f || d < 35f) { wetLength = 0; continue; }
                if (!Height(terrains, p + direction * 8f, out float inland) || inland < water + 1f) continue;
                float score = d + Mathf.Abs(angle) * 2f;
                if (score < best)
                {
                    best = score;
                    bank = p + direction * 8f;
                    Height(terrains, bank, out float endHeight);
                    bank.y = endHeight + .2f;
                }
                break;
            }
        }
        return best < float.PositiveInfinity;
    }

    private static bool Height(Terrain[] terrains, Vector3 point, out float height)
    {
        foreach (var terrain in terrains)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
            height = terrain.SampleHeight(point) + terrain.transform.position.y;
            return true;
        }
        height = 0; return false;
    }
}
