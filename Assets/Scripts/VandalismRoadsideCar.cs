using UnityEngine;
using UnityEngine.AI;

/// <summary>Park lengthwise ON the opening wooden bridge, never on its shore or terrain.</summary>
public static class VandalismRoadsideCar
{
    public static GameObject TryPlace(Transform parent, Vector3 routePoint, Vector3 forward, int side, out string failure, int variant = 0)
    {
        failure = "no supported parking footprint";
        string[] models = { "DownhillCar", "DownhillSuv", "DownhillCar2", "DownhillVan" };
        var prefab = Resources.Load<GameObject>("Vandalism/" + models[Mathf.Abs(variant) % models.Length]);
        int coverLayer = LayerMask.NameToLayer("Cover");
        if (!prefab || coverLayer < 0) { failure = "missing vehicle prefab or Cover layer"; return null; }
        Bounds footprint = VehicleBounds(prefab.transform);
        float halfWidth = footprint.extents.x, halfLength = footprint.extents.z;
        if (halfWidth < .3f || halfLength < .5f) { failure = "invalid model bounds"; return null; }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (!Ground(routePoint, out var middle)) { failure = "no parking surface near navigation height"; return null; }
        // NavMesh corners and prefab origins can lie at a rail, not at the deck centre.
        // Recover the supported deck width before applying the parking offset.
        float left = 0, rightEdge = 0;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            float edge = 0;
            for (float offset = .25f; offset <= 5.5f; offset += .25f)
            {
                if (!Ground(middle.point + right * (offset * sign), out var sample)
                    || Mathf.Abs(sample.point.y - middle.point.y) > .35f) break;
                edge = offset;
            }
            if (sign < 0) left = edge; else rightEdge = edge;
        }
        if (left + rightEdge < halfWidth * 2f + 1.2f) { failure = $"surface too narrow ({left + rightEdge:F2}m)"; return null; }
        routePoint = middle.point + right * ((rightEdge - left) * .5f);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int sign = side * (attempt == 0 ? 1 : -1);
            // The authored pier has rails roughly 5m apart. A 2.21m-wide car fits on one
            // side lengthwise, leaving the opposite lane open; roadside offsets do not.
            float parkingOffset = Mathf.Clamp((left + rightEdge) * .5f - halfWidth - .25f, .15f, 1.2f);
            Vector3 center = routePoint + right * (sign * parkingOffset);
            Quaternion yaw = Quaternion.LookRotation(forward) * Quaternion.Euler(0, variant % 4 == 3 ? 180 : 0, 0);
            if (!Ground(center, out var ground)) continue;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, ground.normal) * yaw;
            center = ground.point;
            // Check four wheel-area supports against the same inclined plane.
            bool supported = true;
            foreach (float x in new[] { -halfWidth * .75f, halfWidth * .75f })
            foreach (float z in new[] { -halfLength * .65f, halfLength * .65f })
            {
                Vector3 wheel = center + rotation * new Vector3(x, 0, z);
                if (!Ground(wheel, out var support) || Mathf.Abs(support.point.y - wheel.y) > .22f
                    || Vector3.Dot(support.normal, ground.normal) < .94f) supported = false;
            }
            if (!supported) { failure = "wheel supports do not match the deck plane"; continue; }
            var blockers = Physics.OverlapBox(center + rotation * Vector3.up * (footprint.size.y * .5f + .15f),
                new Vector3(halfWidth + .05f, Mathf.Max(.2f, footprint.size.y * .5f - .15f), halfLength + .05f), rotation, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (blockers.Length > 0)
            {
                failure = $"vehicle overlaps {blockers[0].name}";
                continue;
            }

            var car = Object.Instantiate(prefab, center, rotation, parent);
            car.name = $"Downhill vehicle cover - {models[Mathf.Abs(variant) % models.Length]}";
            car.transform.position -= rotation * new Vector3(footprint.center.x, footprint.min.y, footprint.center.z);
            // Navigation may use a box, but bullets must hit the visible body rather than empty air.
            foreach (var collider in car.GetComponentsInChildren<Collider>())
            {
                if (collider is BoxCollider box)
                {
                    var obstacle = box.gameObject.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.center = box.center; obstacle.size = box.size;
                    obstacle.carving = true; obstacle.carveOnlyStationary = false;
                }
                collider.enabled = false;
                Object.Destroy(collider);
            }
            var detailed = new System.Collections.Generic.HashSet<Renderer>();
            var lower = new System.Collections.Generic.HashSet<Renderer>();
            foreach (var group in car.GetComponentsInChildren<LODGroup>())
            {
                var levels = group.GetLODs();
                for (int level = 0; level < levels.Length; level++)
                    foreach (var renderer in levels[level].renderers)
                        if (renderer) { if (level == 0) detailed.Add(renderer); else lower.Add(renderer); }
            }
            foreach (var mesh in car.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mesh.GetComponent<Renderer>();
                if (!mesh.sharedMesh || !renderer || (lower.Contains(renderer) && !detailed.Contains(renderer))) continue;
                mesh.gameObject.layer = coverLayer;
                var surface = mesh.gameObject.AddComponent<MeshCollider>();
                surface.sharedMesh = mesh.sharedMesh; surface.convex = false;
            }
            failure = null;
            return car;
        }
        return null;
    }

    private static Bounds VehicleBounds(Transform root)
    {
        Bounds result = default;
        bool found = false;
        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mesh.sharedMesh) continue;
            Bounds bounds = mesh.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = Vector3.Scale(root.InverseTransformPoint(mesh.transform.TransformPoint(corner)), root.localScale);
                if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    // Authored bridge stations: entry (359,14,133), first pier at z=153,
    // then the -45-degree descent towards the pier group at (331.25,6.35,182.01).
    public static Vector3[] OpeningRoute(Vector3 entry) => new[] {
        entry, entry + new Vector3(0, -1, 16), entry + new Vector3(-12, -5, 32),
        entry + new Vector3(-24, -8, 44), entry + new Vector3(-36, -11, 56)
    };

    public static bool TryDeck(Vector3 point, out RaycastHit hit)
    {
        hit = default;
        float nearest = float.PositiveInfinity;
        foreach (var candidate in Physics.RaycastAll(point + Vector3.up * 16f, Vector3.down, 32f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (candidate.normal.y < .8f || !IsBridgeDeck(candidate.collider)) continue;
            float difference = Mathf.Abs(candidate.point.y - point.y);
            if (difference >= nearest) continue;
            hit = candidate; nearest = difference;
        }
        return nearest < 8f;
    }

    private static bool Ground(Vector3 point, out RaycastHit hit)
    {
        hit = default;
        float nearest = 1.2f;
        foreach (var candidate in Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 4f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (candidate.normal.y < .8f || (!IsBridgeDeck(candidate.collider)
                && !(candidate.collider is TerrainCollider))) continue;
            float difference = Mathf.Abs(candidate.point.y - point.y);
            if (difference >= nearest) continue;
            hit = candidate; nearest = difference;
        }
        return hit.collider != null;
    }

    private static bool IsBridgeDeck(Collider collider)
    {
        // Do not accept terrain underneath/alongside the pier just because it is walkable.
        if (collider is TerrainCollider) return false;
        for (Transform t = collider.transform; t; t = t.parent)
            if (t.name.StartsWith("Bridge (") || t.name.Contains("Prop_Pier_")) return true;
        return false;
    }
}
