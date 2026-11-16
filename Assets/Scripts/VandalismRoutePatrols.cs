using System.Collections;
using System.Collections.Generic;
using EnemyAI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Cover-backed encounters on the ordered mission-objective routes only.</summary>
public sealed class VandalismRoutePatrols : MonoBehaviour
{
    private readonly List<StateController> templates = new();
    private readonly List<StateController> openingEnemies = new();
    private readonly List<Vector3> occupied = new();
    private readonly List<Transform> objectives = new();
    private readonly List<Vector3> posts = new();
    private Vector3 entry;
    private int spawned;
    private int downhillCars;
    private int relocatedOpeningEnemies;
    private NavMeshQueryFilter filter;
    private const int MaxExtraEnemies = 30;
    private const float RouteWidth = 9f;

    public void Configure(StageMetaData stage, Vector3 start, IReadOnlyList<GameObject> missions)
    {
        entry = start;
        foreach (var spawn in stage.MissionObjectSpawns)
        {
            if (!spawn.Prefab) continue;
            foreach (var enemy in spawn.Prefab.GetComponentsInChildren<StateController>(true))
            {
                if (enemy.TeamCode == 0 || !enemy.GetComponent<NavMeshAgent>()) continue;
                if (templates.Exists(t => t.name.Split(' ')[0] == enemy.name.Split(' ')[0])) continue;
                templates.Add(enemy);
            }
        }
        foreach (var mission in missions)
        {
            foreach (var enemy in mission.GetComponentsInChildren<StateController>(true))
                {
                occupied.Add(enemy.transform.position);
                if (enemy.TeamCode != 0 && Vector3.Distance(entry, enemy.transform.position) < 65f)
                    openingEnemies.Add(enemy);
            }
            foreach (var progress in mission.GetComponentsInChildren<MissionProgressData>(true))
            {
                foreach (var unit in progress.MissionProgressUnits)
                {
                    AddObjective(unit.triggerObject);
                    AddObjective(unit.triggerTargetObject);
                }
                AddObjective(progress.GoalObject);
            }
        }
    }

    private void AddObjective(GameObject marker)
    {
        if (!marker) return;
        if (objectives.Count > 0 && Vector3.Distance(objectives[objectives.Count - 1].position,
            marker.transform.position) < 2f) return;
        objectives.Add(marker.transform);
    }

    private IEnumerator Start()
    {
        if (templates.Count == 0 || objectives.Count == 0) yield break;
        filter = new NavMeshQueryFilter {
            agentTypeID = templates[0].GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas
        };
        NavMeshHit origin = default;
        bool ready = false;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return null;
            if (NavMesh.SamplePosition(entry, out origin, 12f, filter)) { ready = true; break; }
        }
        if (!ready) { Debug.LogWarning("Vandalism encounters: entry NavMesh unavailable.", this); yield break; }
        // Mission enemies may be instantiated after Configure; collect live actors now.
        foreach (var enemy in FindObjectsByType<StateController>(FindObjectsSortMode.None))
        {
            if (enemy.TeamCode == 0 || !enemy.GetComponent<NavMeshAgent>()
                || Vector3.Distance(entry, enemy.transform.position) > 65f) continue;
            if (!openingEnemies.Contains(enemy)) openingEnemies.Add(enemy);
            if (!occupied.Contains(enemy.transform.position)) occupied.Add(enemy.transform.position);
        }
        openingEnemies.Sort((a, b) => (a.transform.position - entry).sqrMagnitude
            .CompareTo((b.transform.position - entry).sqrMagnitude));
        Physics.SyncTransforms();
        yield return PlaceOpeningCars();
        Vector3 previous = origin.position;
        for (int leg = 0; leg < objectives.Count && spawned < MaxExtraEnemies; leg++)
        {
            // A broken leg must not be bypassed by inventing a route to a later objective.
            if (!NavMesh.SamplePosition(objectives[leg].position, out var goal, 3f, filter)) break;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(previous, goal.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) break;
            Vector3[] route = path.corners;
            int budget = Mathf.Min(MaxExtraEnemies, 14 + leg * 8);
            float distance = 12f;
            int beforeLeg = spawned;
            for (int i = 1; i < route.Length && spawned < budget; i++)
            {
                Vector3 direction = route[i] - route[i - 1];
                float length = direction.magnitude;
                if (length < .01f) continue;
                direction /= length;
                while (distance < length && spawned < budget)
                {
                    Vector3 point = route[i - 1] + direction * distance;
                    distance += leg == 0 ? 4f : 6f;
                    if (!CandidateAllowed(point)) continue;
                    Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                    if (forward.sqrMagnitude < .5f) continue;
                    // Prefer real cover already in the street: cars, crates, low walls, corners.
                    if (TryExistingCover(point, forward, route, out Vector3 guard))
                    {
                        SpawnGuard(guard, forward);
                        posts.Add(point);
                        continue;
                    }
                }
                distance -= length;
            }
            Debug.Log($"Vandalism objective leg {leg + 1}: {spawned - beforeLeg} guards using existing scenery.", this);
            previous = goal.position;
        }
        string report = $"Vandalism objective encounters: {spawned} cover-backed guards; {downhillCars} downhill car positions; {objectives.Count} objective markers.";
        if (spawned == 0) Debug.LogWarning(report + " No safe covered posts passed placement checks.", this);
        else Debug.Log(report, this);
    }

    private IEnumerator PlaceOpeningCars()
    {
        var stations = new List<Vector3>(VandalismRoadsideCar.OpeningRoute(entry));
        // Continue down the visible long ramp to its lower landing, beyond the old final station.
        stations.Add(entry + new Vector3(-48, -13, 68));
        stations.Add(entry + new Vector3(-60, -14, 80));
        stations.Add(entry + new Vector3(-66, -14, 87));
        // Reserve two uphill vehicles as player fallback cover before the first enemy post.
        stations.Insert(1, Vector3.Lerp(stations[0], stations[1], .5f));
        // Intermediate parked vehicles form a chain of cover through the long descent.
        for (int segment = stations.Count - 2; segment >= 2; segment--)
            stations.Insert(segment + 1, Vector3.Lerp(stations[segment], stations[segment + 1], .5f));
        Vector3[] route = stations.ToArray();
        var parkedPositions = new List<Vector3>();
        for (int i = 1; i < route.Length - 1; i++)
        {
            Vector3 forward = i <= 2 ? Vector3.forward
                : Vector3.ProjectOnPlane(route[i + 1] - route[i], Vector3.up).normalized;
            Vector3 point = route[i];
            GameObject car = null;
            string failure = "no nearby navigation surface";
            // Search along the descent, using NavMesh height rather than approximate authored Y.
            // This prevents the elevated railing top being mistaken for the parking surface.
            for (int attempt = 0; attempt < 11 && !car; attempt++)
            {
                float shift = attempt == 0 ? 0 : ((attempt + 1) / 2) * 1.25f * (attempt % 2 == 1 ? 1 : -1);
                Vector3 candidate = route[i] + forward * shift;
                if (!NavMesh.SamplePosition(candidate, out var parking, 4f, filter)
                    || Vector3.ProjectOnPlane(parking.position - candidate, Vector3.up).magnitude > 2f) continue;
                point = parking.position;
                if (parkedPositions.Exists(p => Vector3.Distance(p, point) < 10.5f))
                { failure = "too close to the preceding vehicle"; continue; }
                car = VandalismRoadsideCar.TryPlace(transform, point, forward, (i / 2) % 2 == 0 ? 1 : -1, out failure, i - 1);
                if (!car) continue;
                Physics.SyncTransforms();
                yield return null; yield return null;
                var bypass = new NavMeshPath();
                bool open = NavMesh.SamplePosition(point - forward * 6f, out var from, 3f, filter)
                    && NavMesh.SamplePosition(point + forward * 6f, out var to, 3f, filter)
                    && NavMesh.CalculatePath(from.position, to.position, filter, bypass)
                    && bypass.status == NavMeshPathStatus.PathComplete && PathLength(bypass.corners) < 24f;
                if (!open)
                {
                    car.SetActive(false); Destroy(car); car = null;
                    failure = "no navigable bypass";
                    yield return null; yield return null;
                }
            }
            if (!car)
            {
                Debug.LogWarning($"Opening bridge station {i}: all nearby parking positions failed: {failure}.", this);
                continue;
            }
            downhillCars++;
            parkedPositions.Add(car.transform.position);
            int before = spawned;
            if (i > 2)
            {
                int guardsAtCar = 0;
                for (int pass = 0; pass < 3 && guardsAtCar < 3; pass++)
                foreach (var body in car.GetComponentsInChildren<Collider>())
                {
                    if (guardsAtCar >= 3 || spawned >= MaxExtraEnemies) break;
                    if (!TryBehindCover(body, point, forward, route, out var guard)) continue;
                    // Move exposed authored enemies first; do not merely add more shooters.
                    var exposed = openingEnemies.Find(IsExposedAtEntry);
                    var agent = exposed ? exposed.GetComponent<NavMeshAgent>() : null;
                    Vector3 oldPosition = exposed ? exposed.transform.position : Vector3.zero;
                    if (agent && agent.isOnNavMesh && agent.Warp(guard))
                    {
                        occupied.Remove(oldPosition);
                        exposed.transform.position = guard;
                        exposed.transform.rotation = Quaternion.LookRotation(-forward);
                        var post = new GameObject("Relocated downhill covered post").transform;
                        post.SetParent(transform); post.position = guard;
                        exposed.SetWaypoints(new[] { post });
                        occupied.Add(guard); openingEnemies.Remove(exposed); relocatedOpeningEnemies++;
                        Debug.Log($"Opening guard relocated behind vehicle at {guard}.", this);
                    }
                    else if (!exposed) SpawnGuard(guard, forward);
                    else
                    {
                        Debug.LogWarning($"Opening guard {exposed.name}: relocation failed; no extra enemy added.", exposed);
                        continue;
                    }
                    guardsAtCar++;
                }
            }
            posts.Add(point);
            Debug.Log($"Opening bridge station {i}: {car.name} placed at {car.transform.position}; guards {spawned - before}.", this);
        }
        // Authored guards that could not use a vehicle get another search along the descent.
        foreach (var enemy in openingEnemies.ToArray())
        {
            if (!IsExposedAtEntry(enemy)) continue;
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (!agent || !agent.isOnNavMesh) continue;
            for (int station = 3; station < route.Length - 1; station++)
            {
                Vector3 forward = Vector3.ProjectOnPlane(route[station + 1] - route[station], Vector3.up).normalized;
                if (!TryExistingCover(route[station], forward, route, out var destination)) continue;
                Vector3 old = enemy.transform.position;
                if (!agent.Warp(destination)) continue;
                var post = new GameObject("Protected downhill approach post").transform;
                post.SetParent(transform); post.position = destination;
                enemy.SetWaypoints(new[] { post });
                enemy.transform.rotation = Quaternion.LookRotation(-forward);
                occupied.Remove(old); occupied.Add(destination);
                openingEnemies.Remove(enemy); relocatedOpeningEnemies++;
                break;
            }
        }
        int exposedRemaining = openingEnemies.FindAll(IsExposedAtEntry).Count;
        Debug.Log($"Opening approach: {downhillCars} cover vehicles, {relocatedOpeningEnemies} existing enemies relocated, {exposedRemaining} exposed enemies remaining.", this);
        if (exposedRemaining > 0)
            Debug.LogWarning("Opening approach still has exposed enemies: insufficient validated covered positions.", this);
        FindAnyObjectByType<StageSceneInitializer>()?.NotifyCoverGeometryChanged();
    }

    private bool IsExposedAtEntry(StateController enemy)
    {
        if (!enemy || !enemy.gameObject.activeInHierarchy) return false;
        if (Vector3.Distance(entry, enemy.transform.position) < 25f) return true;
        Vector3 origin = entry + Vector3.up * 1.4f;
        Vector3 delta = enemy.transform.position + Vector3.up * 1.4f - origin;
        // Character colliders cannot count as scenery concealing their own position.
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<HealthManager>()
                || hit.collider.transform.IsChildOf(enemy.transform)) continue;
            return false;
        }
        return true;
    }

    private bool HiddenFromStart(Vector3 position)
    {
        foreach (float height in new[] { .9f, 1.6f })
        {
            Vector3 from = entry + Vector3.up * height;
            Vector3 delta = position + Vector3.up * 1.4f - from;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<HealthManager>()) continue;
                blocked = true; break;
            }
            if (!blocked) return false;
        }
        return true;
    }

    private bool CandidateAllowed(Vector3 point)
    {
        if (Vector3.Distance(entry, point) < 25f) return false;
        foreach (var marker in objectives) if (Vector3.Distance(marker.position, point) < 6f) return false;
        foreach (var post in posts) if (Vector3.Distance(post, point) < 5f) return false;
        return true;
    }

    private bool TryExistingCover(Vector3 point, Vector3 forward, Vector3[] route, out Vector3 guard)
    {
        guard = default;
        // Existing scenery may be on Default rather than the AI Cover layer.
        var colliders = Physics.OverlapSphere(point + Vector3.up, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(colliders, (a, b) => a.bounds.SqrDistance(point).CompareTo(b.bounds.SqrDistance(point)));
        foreach (var cover in colliders)
        {
            if (cover is TerrainCollider || cover.GetComponentInParent<HealthManager>()
                || cover.attachedRigidbody != null || cover.bounds.size.y < .8f
                || cover.bounds.min.y > point.y + .6f || cover.bounds.max.y < point.y + .8f) continue;
            if (TryBehindCover(cover, point, forward, route, out guard)) return true;
        }
        return false;
    }

    private bool TryBehindCover(Collider cover, Vector3 point, Vector3 forward, Vector3[] route, out Vector3 guard)
    {
        guard = default;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        // Sample local surfaces, not the far end of an entire building's bounding box.
        // This admits road-facing corners and parked cars on long/sloping approach streets.
        for (int row = 0; row < 3; row++)
        for (int column = -3; column <= 3; column++)
        {
            Vector3 probe = point + forward * (row * 3f) + side * (column * 2f) + Vector3.up * .9f;
            Vector3 surface;
            if (cover is MeshCollider mesh && !mesh.convex)
            {
                Vector3 toward = cover.bounds.center - probe;
                toward.y = 0;
                if (toward.sqrMagnitude < .01f || !cover.Raycast(new Ray(probe, toward.normalized), out var face, 16f)) continue;
                surface = face.point;
            }
            else surface = cover.ClosestPoint(probe);
            Vector3 outward = Vector3.ProjectOnPlane(probe - surface, Vector3.up);
            if (outward.sqrMagnitude < .01f) continue;
            Vector3 desired = surface + outward.normalized * .75f;
            desired.y = point.y;
            if (TryGuard(desired, point, forward, route, cover, out guard)) return true;
        }
        return false;
    }

    private bool TryGuard(Vector3 desired, Vector3 point, Vector3 forward, Vector3[] route,
        Collider cover, out Vector3 guard)
    {
        guard = default;
        if (!NavMesh.SamplePosition(desired, out var hit, 1.5f, filter)
            || Mathf.Abs(hit.position.y - point.y) > 1.8f || !CandidateAllowed(hit.position)
            || DistanceToRoute(hit.position, route) > RouteWidth) return false;
        foreach (var other in occupied) if (Vector3.Distance(other, hit.position) < 1.6f) return false;
        if (Physics.CheckCapsule(hit.position + Vector3.up * .45f, hit.position + Vector3.up * 1.5f,
            .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(point, hit.position, filter, path)
            || path.status != NavMeshPathStatus.PathComplete || PathLength(path.corners) > 19f) return false;
        // Opening enemies must be hidden from both standing and low spawn viewpoints.
        if (Vector3.Distance(entry, hit.position) < 100f && !HiddenFromStart(hit.position)) return false;
        // A nearby object is not enough: it must actually occlude the incoming direction.
        Vector3 probe = hit.position + Vector3.up * .9f;
        Vector3 approach = point - forward * 7f + Vector3.up * .9f;
        Vector3 incoming = (approach - probe).normalized;
        if (!cover.Raycast(new Ray(probe, incoming), out _, 8f)) return false;
        // A firing edge must be reachable; never put a guard in a sealed pocket behind scenery.
        Vector3 side = Vector3.Cross(Vector3.up, incoming).normalized;
        bool canPeek = false;
        for (int i = 0; i < 6 && !canPeek; i++)
        {
            float offset = (i / 2 + 1) * 1.2f * (i % 2 == 0 ? 1 : -1);
            if (!NavMesh.SamplePosition(hit.position + side * offset, out var peek, .4f, filter)
                || DistanceToRoute(peek.position, route) > RouteWidth
                || NavMesh.Raycast(hit.position, peek.position, out _, filter)) continue;
            canPeek = !Physics.Linecast(peek.position + Vector3.up * 1.4f,
                point - forward * 6f + Vector3.up * 1.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        if (!canPeek) return false;
        guard = hit.position;
        return true;
    }

    private void SpawnGuard(Vector3 position, Vector3 forward)
    {
        var template = templates.Find(t => t.name.Contains("rifle")) ?? templates[0];
        var post = new GameObject("Covered guard post").transform;
        post.SetParent(transform); post.position = position;
        var enemy = Instantiate(template, position, Quaternion.LookRotation(-forward), transform);
        enemy.name = $"Objective cover guard {++spawned}";
        enemy.aimTarget = null;
        enemy.viewRadius = Mathf.Min(50f, enemy.viewRadius * 1.15f);
        enemy.viewAngle = Mathf.Min(180f, enemy.viewAngle + 15f);
        enemy.perceptionRadius = Mathf.Min(25f, enemy.perceptionRadius * 1.15f);
        enemy.nearRadius = enemy.perceptionRadius * .5f;
        // Hold a covered post until the ordinary combat FSM engages; do not patrol into the road.
        enemy.SetWaypoints(new[] { post });
        enemy.gameObject.SetActive(true);
        occupied.Add(position);
    }

    private static float PathLength(Vector3[] corners)
    {
        float length = 0;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    private static float DistanceToRoute(Vector3 point, Vector3[] corners)
    {
        float distance = float.PositiveInfinity;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 delta = corners[i] - corners[i - 1];
            float t = Mathf.Clamp01(Vector3.Dot(point - corners[i - 1], delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
            distance = Mathf.Min(distance, Vector3.Distance(point, corners[i - 1] + delta * t));
        }
        return distance;
    }
}
