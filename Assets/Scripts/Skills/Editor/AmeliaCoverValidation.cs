using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EnemyAI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable play-mode regression check; never saves the open scene.</summary>
[InitializeOnLoad]
public static class AmeliaCoverValidation
{
    private const string Request = "Temp/amelia_cover_validate.request";
    private const string Running = "AmeliaCoverValidation.Running";
    private static IEnumerator run;
    private static GameObject fixture;
    private static GameObject threat;
    private static GameObject probe;
    private static GeneralStats stats;
    private static NavMeshData data;
    private static NavMeshDataInstance instance;
    private static readonly List<string> results = new();
    static AmeliaCoverValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
                run = Validate();
        };
    }

    [MenuItem("Tools/Skills/Validate Amelia Field Cover (Play Mode)")]
    public static void RequestValidation() => File.WriteAllText(Request, "validate");

    private static void Tick()
    {
        if (run != null)
        {
            try { if (run.MoveNext()) return; Finish(null); }
            catch (Exception e) { Finish(e.ToString()); }
            return;
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool passed, string label)
    {
        if (!passed) throw new Exception(label);
        results.Add("PASS " + label);
    }

    private static IEnumerator Validate()
    {
        results.Clear();
        var asset = AssetDatabase.LoadAssetAtPath<PersonaDataSkill>(
            "Assets/Resources/Scriptable Object/Persona/Amelia/AmeliaSkill.asset");
        Check((asset.skill1.logic is AmeliaSkillSupplyOverdrive || asset.skill1.logic is AmeliaSkillExtendMagazine) &&
            (asset.skill2 == null || asset.skill2.logic is AmeliaSkillPowerBuff || asset.skill2.logic is AmeliaSkillClusterGrenade),
            "Asset slots: combined supply Q and cluster grenade E are available at runtime");
        Vector3 center = new Vector3(1000, 0, 1000);
        fixture = new GameObject("Field cover validation (temporary)");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(fixture.transform);
        floor.transform.position = center + Vector3.down * .1f;
        floor.transform.localScale = new Vector3(30, .2f, 30);
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
            size = floor.transform.localScale, area = 0 };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
            new List<NavMeshBuildSource> { source }, new Bounds(center, new Vector3(40, 5, 40)),
            Vector3.zero, Quaternion.identity);
        Check(data != null, "Test navigation mesh built");
        instance = NavMesh.AddNavMeshData(data);
        var owner = new GameObject("Supply caster");
        owner.transform.SetParent(fixture.transform);
        owner.transform.position = center + Vector3.back * 4;
        var placement = owner.AddComponent<AmeliaCoverPlacement>();
        placement.Configure(null, SkillSlot.Skill2, 6);
        Physics.SyncTransforms();
        yield return null;
        Check(placement.TryPlacement(out Vector3 position, out Quaternion rotation), "Open floor placement accepted");
        Check(!placement.IsPreviewing, "Preview initially hidden");
        Check(!placement.BeginOrConfirm(out _, out _) && placement.IsPreviewing,
            "First press only enters preview");
        placement.CancelPreview();
        Check(!placement.IsPreviewing, "Preview can be cancelled");
        Check(!placement.BeginOrConfirm(out _, out _) && placement.BeginOrConfirm(out _, out _) && !placement.IsPreviewing,
            "Second press confirms valid landing point");
        var movementBody = owner.AddComponent<Rigidbody>();
        movementBody.useGravity = false;
        movementBody.linearVelocity = Vector3.right * 2;
        Check(placement.TryPlacement(out var movingPosition, out var movingRotation) &&
            movingPosition.x > owner.transform.position.x + 3.5f &&
            Vector3.Dot(movingRotation * Vector3.forward, Vector3.right) > .99f,
            "Moving direction overrides facing direction");
        movementBody.linearVelocity = Vector3.zero;
        Check(Vector3.Dot(placement.PlacementDirection(), owner.transform.forward) > .99f,
            "Stationary placement uses character facing");
        var lookup = fixture.AddComponent<CoverLookup>();
        lookup.Setup(LayerMask.GetMask("Cover"));
        var cover = DestructibleCover.Create(owner, position, rotation, 600, 12, true);
        cover.transform.SetParent(fixture.transform);
        Check(!cover.IsLanded && !cover.GetComponent<Collider>().enabled &&
            cover.transform.position.y > position.y + 10, "Barricade starts in sky without solid collision");
        float until = Time.time + .3f;
        while (Time.time < until) yield return null;
        Check(cover != null && !cover.IsLanded && cover.transform.position.y > position.y &&
            cover.transform.position.y < position.y + DestructibleCover.DropHeight,
            "Barricade visibly descends to locked destination");
        until = Time.time + 1f;
        while (Time.time < until) yield return null;
        Check(cover != null && cover.IsLanded && cover.GetComponent<Collider>().enabled &&
            Vector3.Distance(cover.transform.position, position) < .01f,
            "Landing enables solid cover at confirmed point");
        Check(!placement.TryPlacement(out _, out _), "Overlapping placement rejected");
        placement.BeginOrConfirm(out _, out _);
        Check(!placement.BeginOrConfirm(out _, out _) && placement.IsPreviewing,
            "Invalid second press keeps preview active");
        placement.CancelPreview();
        Check(cover.GetCoverSpots().Length == 4, "Four reachable cover spots outside carved obstacle");
        var coversField = typeof(CoverLookup).GetField("covers", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(Array.IndexOf((GameObject[])coversField.GetValue(lookup), cover.gameObject) >= 0,
            "Runtime cover registered in lookup");

        var agentObject = new GameObject("Navigation probe");
        agentObject.transform.SetParent(fixture.transform);
        agentObject.transform.position = center + new Vector3(0, 0, -4);
        var agent = agentObject.AddComponent<NavMeshAgent>();
        agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        agent.speed = 6;
        agent.acceleration = 30;
        yield return null;
        yield return null;
        var path = new NavMeshPath();
        bool sampledGoal = NavMesh.SamplePosition(center + Vector3.forward * 4, out var goal, 1f, NavMesh.AllAreas);
        bool foundPath = agent.CalculatePath(goal.position, path);
        results.Add($"INFO goal={sampledGoal}/{goal.position} agentType={agent.agentTypeID} mask={agent.areaMask}");
        results.Add($"INFO cover={position} agent={agent.transform.position} onMesh={agent.isOnNavMesh} path={foundPath}/{path.status} corners={path.corners.Length}");
        Check(foundPath &&
            path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 2,
            "Navigation routes around barricade");

        // Keep the controller inactive to test its real cover methods without unrelated sensing/animation setup.
        probe = new GameObject("FSM cover probe");
        probe.SetActive(false);
        probe.transform.position = agentObject.transform.position;
        var controller = probe.AddComponent<StateController>();
        var reservedField = typeof(StateController).GetField("coverSpot", BindingFlags.Static | BindingFlags.NonPublic);
        if (reservedField.GetValue(null) == null) reservedField.SetValue(null, new Dictionary<int, Vector3>());
        controller.CoverSpot = Vector3.positiveInfinity;
        stats = ScriptableObject.CreateInstance<GeneralStats>();
        stats.coverMask = stats.obstacleMask = LayerMask.GetMask("Cover");
        controller.generalStats = stats;
        controller.nav = agent;
        controller.coverLookup = lookup;
        controller.viewRadius = 40;
        controller.nearRadius = .5f;
        threat = new GameObject("Known threat"); // Separate root: siblings of the caster are not valid targets.
        threat.transform.position = center + Vector3.forward * 8;
        controller.aimTarget = threat.transform;
        controller.personalTarget = threat.transform.position + Vector3.up;
        // Start exposed to one side, then use the same selection and movement as the FSM.
        probe.transform.position = center + new Vector3(4, 0, -4);
        agent.Warp(probe.transform.position);
        controller.BeginCoverMovement(false, 6);
        results.Add($"INFO FSM known={controller.HasKnownTarget} hash={controller.coverHash} ready={controller.CoverReady} reason={controller.CoverSelectionReason}");
        Check(controller.coverHash == cover.gameObject.GetHashCode() && controller.CoverReady,
            "FSM selects damageable barricade as protective cover");
        until = Time.time + 4;
        while (Time.time < until && Vector3.Distance(agent.transform.position, controller.CoverSpot) > .3f)
            yield return null;
        Check(Vector3.Distance(agent.transform.position, controller.CoverSpot) <= .3f,
            "Agent arrives at selected cover spot");
        CaptureCover(cover.transform.position);
        cover.HitCallback(new HealthManager.DamageInfo(center, Vector3.forward, 125));
        Check(cover.Health == 475 && !cover.dead, "Bullet callback reduces durability once");
        cover.TakeDamage(center, Vector3.forward, -50);
        Check(cover.Health == 475, "Negative damage ignored");
        cover.TakeDamage(center, Vector3.forward, 500, damageType: HealthManager.DamageType.Skill);
        Check(cover.dead && !cover.gameObject.activeSelf && !controller.CoverReady,
            "Destruction immediately removes protection and collision");
        Check(Array.IndexOf((GameObject[])coversField.GetValue(lookup), cover.gameObject) < 0,
            "Destroyed cover removed from lookup");
        controller.NotifyCoverRemoved(cover.gameObject.GetHashCode());
        Check(!CoverLookup.IsFinite(controller.CoverSpot), "FSM abandons destroyed reservation");
        yield return null;
        until = Time.time + .4f;
        while (Time.time < until) yield return null;
        Check(agent.CalculatePath(center + Vector3.forward * 4, path) && path.status == NavMeshPathStatus.PathComplete,
            "Navigation remains reachable after destruction");
        var expiring = DestructibleCover.Create(owner, position, rotation, 600, .2f);
        expiring.transform.SetParent(fixture.transform);
        until = Time.time + .5f;
        while (Time.time < until) yield return null;
        Check(expiring == null, "Lifetime expiry removes barricade");
        bool refunded = false;
        var blocked = DestructibleCover.Create(owner, position, rotation, 600, 12, true, () => refunded = true);
        blocked.transform.SetParent(fixture.transform);
        var intruder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        intruder.transform.SetParent(fixture.transform);
        intruder.transform.position = position + Vector3.up * .7f;
        Physics.SyncTransforms();
        until = Time.time + 1.2f;
        while (Time.time < until) yield return null;
        Check(blocked == null && refunded, "Occupied landing cancels drop and invokes cooldown refund");
        Object.Destroy(intruder);
        yield return null;
        var shortLived = DestructibleCover.Create(owner, position, rotation, 600, .2f, true);
        shortLived.transform.SetParent(fixture.transform);
        until = Time.time + .4f;
        while (Time.time < until) yield return null;
        Check(shortLived != null && !shortLived.IsLanded, "Lifetime does not expire during descent");
        until = Time.time + 1;
        while (Time.time < until) yield return null;
        Check(shortLived == null, "Lifetime starts at landing");
        placement.Deploy(position, rotation, 600, 12);
        placement.Deploy(position + Vector3.right * 4, rotation, 600, 12);
        int owned = 0;
        foreach (var item in Object.FindObjectsByType<DestructibleCover>(FindObjectsSortMode.None))
            if (Vector3.Distance(item.transform.position, center) < 20) owned++;
        Check(owned == 1, "Recasting leaves exactly one active barricade");
    }

    private static void CaptureCover(Vector3 position)
    {
        var cameraObject = new GameObject("Validation render camera");
        cameraObject.transform.SetParent(fixture.transform);
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = position + new Vector3(4, 3, -5);
        camera.transform.LookAt(position + Vector3.up * .7f);
        camera.fieldOfView = 38;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .18f);
        var texture = new RenderTexture(960, 640, 24);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
        pixels.Apply();
        File.WriteAllBytes("Temp/amelia_cover_preview.png", pixels.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        Object.Destroy(texture);
        Object.Destroy(pixels);
        Object.Destroy(cameraObject);
    }

    private static void Finish(string error)
    {
        run = null;
        if (error != null) results.Add("FAIL " + error);
        File.WriteAllLines("Temp/amelia_cover_validation.txt", results);
        if (fixture != null) Object.Destroy(fixture);
        if (threat != null) Object.Destroy(threat);
        if (probe != null) Object.Destroy(probe);
        if (instance.valid) instance.Remove();
        if (data != null) Object.Destroy(data);
        if (stats != null) Object.Destroy(stats);
        SessionState.SetBool(Running, false);
        Debug.Log("[AmeliaCoverValidation] " + (error == null ? "PASS" : error));
        EditorApplication.isPlaying = false;
    }
}
