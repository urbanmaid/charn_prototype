using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opening airstrike prototype. A scenic side-span collapses, preserving the playable bridge.</summary>
public sealed class VandalismBridgeAirstrike : MonoBehaviour
{
    private Vector3 entry, objective, approach, strike;
    private bool configured, fired;
    private bool restoreTriggered, restoredGeometry;
    public bool WasTriggered => fired;

    public void RestoreCheckpointState(bool wasTriggered)
    {
        if(!wasTriggered)return;
        restoreTriggered=true;fired=true;sequenceFinished=true;
        if(effects!=null){StopAllCoroutines();ApplyRestoredGeometry();}
    }

    private void ApplyRestoredGeometry()
    {
        if(restoredGeometry)return;
        restoredGeometry=true;
        if(replacedPier){foreach(var r in replacedPier.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in replacedPier.GetComponentsInChildren<Collider>())c.enabled=false;}
        foreach(var span in originalSpans)
        {
            var lod=span.GetComponent<LODGroup>();Vector3 center=lod?lod.localReferencePoint:new Vector3(2.9f,1.9f,-.78f);center.y=2.7f;
            var cut=new GameObject("Destroyed pier navigation cut");cut.transform.SetParent(transform);cut.transform.SetPositionAndRotation(span.transform.TransformPoint(center),span.transform.rotation);
            var obstacle=cut.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(6.5f,4f,4f);obstacle.carving=true;obstacle.carveOnlyStationary=false;
            span.SetActive(false);
        }
        FindAnyObjectByType<StageSceneInitializer>()?.NotifyCoverGeometryChanged();
    }
    private Charm.Mechanism.Missions.StageMissionController missionController;
    private string openingMissionId;

    public void ConfigureMission(Vector3 start, Vector3 destination,
        Charm.Mechanism.Missions.StageMissionController controller, string missionId)
    {
        entry = start; objective = destination; missionController = controller;
        openingMissionId = missionId; configured = true;
    }

    private bool OpeningMissionActive()
    {
        if (missionController == null) return true;
        var run = missionController.Runtime;
        return run != null && run.HasStarted && !run.IsCleared
            && run.Definitions[run.CurrentMissionIndex].Id == openingMissionId;
    }
    private readonly List<Transform> pieces = new();
    private Transform scenicSpan;
    private GameObject replacedPier;
    private readonly List<GameObject> originalSpans = new();
    private readonly Transform[] aircraft = new Transform[3];
    private Vector3 bridgeDirection;
    private AudioClip incomingClip, blastClip, collapseClip, splashClip;
    private float flightStarted;
    private int completedBombs;
    private const float JetSpeed = 70f;
    private Vector3 FlightVelocity => Vector3.Cross(Vector3.up, approach).normalized * JetSpeed;
    private static float ReleaseTime(int slot) => .65f + slot * .28f;
    private GameObject effects;
    private Vector3 nearImpact, entrance;
    private bool sequenceFinished;
    private readonly List<Vector3> bombImpacts = new();

    private void OnDisable()
    {
        if (fired && !sequenceFinished)
            Debug.LogWarning("Opening airstrike interrupted before completion (stage disabled or play stopped).", this);
    }

    public void Configure(Vector3 start, IReadOnlyList<GameObject> missions)
    {
        entry = start;
        foreach (var root in missions)
        foreach (var mission in root.GetComponentsInChildren<MissionProgressData>(true))
        {
            if (mission.MissionProgressUnits.Count == 0 || !mission.MissionProgressUnits[0].triggerObject) continue;
            objective = mission.MissionProgressUnits[0].triggerObject.transform.position;
            configured = true;
            return;
        }
    }

    private IEnumerator Start()
    {
        if (!configured) yield break;
        // The opening follows authored pier geometry, independently of the route NavMesh below it.
        yield return null;
        Physics.SyncTransforms();
        Vector3[] route = VandalismRoadsideCar.OpeningRoute(entry);
        approach = Vector3.ProjectOnPlane(route[3] - route[2], Vector3.up).normalized;
        Vector3 anchor = route[2];
        if (!BuildSideSpan(anchor)) yield break;
        if(restoreTriggered){ApplyRestoredGeometry();yield break;}
        // Trigger on the descent with lead time for the ~3-second bombing approach.
        // Keep a 20m start clearance so the opening cannot fire at initial spawn.
        // Character switching uses the current controlled slot, not an arbitrary Player-tag ally.
        while (!fired)
        {
            var player = PlayerControlResolver.Resolve(null);
            if (player && OpeningMissionActive() && GameplayCursor.AllowsGameplayInput && Time.timeScale > 0f
                && Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(entrance.x, entrance.z)) <= 32f
                && Mathf.Abs(player.transform.position.y - entrance.y) <= 10f
                && Vector3.Distance(player.transform.position, entry) >= 20f)
            {
                Debug.Log($"Opening airstrike triggered on descent: player {player.transform.position}, bridge entrance {entrance}.", this);
                fired = true;
                yield return StartCoroutine(Sequence());
                yield break;
            }
            yield return new WaitForSeconds(.15f);
        }
    }

    private static bool IsBridge(Transform t)
    {
        for (; t; t = t.parent)
            if (t.name.StartsWith("Bridge (") || t.name.Contains("Prop_Pier_")) return true;
        return false;
    }

    private bool BuildSideSpan(Vector3 anchor)
    {
        Transform branch = null;
        float best = float.PositiveInfinity;
        foreach (Transform candidate in GetComponentsInChildren<Transform>())
        {
            if (candidate.name != "EA03_Prop_Pier_02a_PRE (5)") continue;
            float distance = Vector3.Distance(candidate.position, anchor);
            if (distance < best) { best = distance; branch = candidate; }
        }
        if (!branch || best > 65f)
        { Debug.LogWarning("Airstrike: existing riverside pier not found.", this); return false; }
        // Advance past the previously selected opening spur to the next pier group
        // along the approach to the mission. Ignore its adjoining 6m companion module.
        Transform previousBranch = branch;
        Transform nextBranch = null;
        Vector3 onward = Vector3.ProjectOnPlane(objective - previousBranch.position, Vector3.up).normalized;
        best = float.PositiveInfinity;
        foreach (Transform candidate in GetComponentsInChildren<Transform>())
        {
            if (!candidate.name.StartsWith("EA03_Prop_Pier_02a_PRE") || candidate == previousBranch) continue;
            Vector3 delta = candidate.position - previousBranch.position;
            float distance = delta.magnitude;
            if (distance < 15f || distance > 90f || Vector3.Dot(delta.normalized, onward) < .3f) continue;
            if (distance < best) { best = distance; nextBranch = candidate; }
        }
        if (!nextBranch)
        { Debug.LogWarning("Airstrike: next riverside pier not found; previous pier will not be destroyed.", this); return false; }
        branch = nextBranch;
        var lod = branch.GetComponent<LODGroup>();
        Vector3 localCenter = lod ? lod.localReferencePoint : new Vector3(2.9f, 1.9f, -.78f);
        localCenter.y = 2.7f;
        localCenter.x = 3f; localCenter.z = -2f; // Authored pair: (6,0,-4), yaw 180.
        Vector3 center = branch.TransformPoint(localCenter);
        // Join the physical deck endpoint, not the LOD sphere or an arbitrary near-bank point.
        Vector3 a = branch.TransformPoint(localCenter - Vector3.right * 3f);
        Vector3 b = branch.TransformPoint(localCenter + Vector3.right * 3f);
        entrance = Vector3.Distance(entry, a) < Vector3.Distance(entry, b) ? a : b;
        Vector3 join = entrance == a ? b : a;
        bridgeDirection = Vector3.ProjectOnPlane(join - entrance, Vector3.up).normalized;
        // Continue from the last authored module, not halfway along the existing pier.
        float furthest = 0f;
        foreach (Transform candidate in branch.parent.GetComponentsInChildren<Transform>())
        {
            if (candidate == branch || !candidate.name.StartsWith("EA03_Prop_Pier_02a_PRE")) continue;
            if (Vector3.Dot(candidate.right, branch.right) < .99f) continue;
            Vector3 delta = candidate.position - branch.position;
            float along = Vector3.Dot(delta, bridgeDirection);
            Vector3 lateral = delta - bridgeDirection * along;
            if (along > furthest && along <= 15f && lateral.magnitude < .15f)
                furthest = along;
        }
        join += bridgeDirection * furthest;
        var connectedSpans = new List<GameObject>();
        if (!VandalismConnectedBridge.Build(transform, join, bridgeDirection,
            out scenicSpan, out var farBank, connectedSpans))
        {
            Debug.LogError("Airstrike setup failed: no supported opposite bank/bridge asset. No disconnected substitute was created.", this);
            return false;
        }
        // Preserve the player's existing pier as the approach; extend its outer end.
        replacedPier = null;
        bridgeDirection = (farBank - entrance).normalized;
        approach = Vector3.ProjectOnPlane(bridgeDirection, Vector3.up).normalized;
        scenicSpan.rotation = Quaternion.identity;
        // Keep each bank's landing and destroy the entire connected middle, not just particles.
        for (int i = 1; i < connectedSpans.Count - 1; i++)
        {
            originalSpans.Add(connectedSpans[i]);
            pieces.Add(connectedSpans[i].transform);
        }
        if (pieces.Count == 0) { originalSpans.Add(connectedSpans[0]); pieces.Add(connectedSpans[0].transform); }
        strike = Vector3.Lerp(entrance, farBank, .5f);
        var impactLod = pieces[0].GetComponent<LODGroup>();
        Vector3 impactLocal = impactLod.localReferencePoint; impactLocal.y = 2.7f;
        nearImpact = pieces[0].TransformPoint(impactLocal);
        effects = new GameObject("Opening airstrike effects");
        effects.transform.SetParent(transform); effects.transform.position = strike;
        Debug.Log($"Riverside airstrike armed: existing pier {branch.position}, entrance {entrance}, approach radius 32m, height tolerance 10m. No kill requirement.", this);
        return true;
    }

    public void PlayPreview(Transform bridgeRoot, List<GameObject> sections)
    {
        if (fired || sections.Count < 3) return;
        scenicSpan = bridgeRoot;
        for (int i = 1; i < sections.Count - 1; i++) originalSpans.Add(sections[i]);
        Vector3 Deck(GameObject section) {
            var center = section.GetComponent<LODGroup>().localReferencePoint;
            center.y = 2.7f;
            return section.transform.TransformPoint(center);
        }
        nearImpact = Deck(originalSpans[0]);
        strike = Deck(originalSpans[originalSpans.Count / 2]);
        bridgeDirection = (strike - nearImpact).normalized;
        approach = bridgeDirection;
        effects = new GameObject("Preview airstrike effects");
        effects.transform.SetParent(transform);
        fired = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        Debug.Log($"Airstrike sequence began: near impact {nearImpact}, centre {strike}, timeScale {Time.timeScale}.", this);
        incomingClip = BuildIncoming();
        blastClip = BuildBlast();
        collapseClip = BuildCollapseSound(false); splashClip = BuildCollapseSound(true);
        flightStarted = Time.time;
        // A presentation-only aircraft failure must never cancel the actual impact/destruction.
        try
        {
            for (int i = 0; i < aircraft.Length; i++)
            {
                aircraft[i] = VandalismAirstrikeJet.Create(effects.transform);
                aircraft[i].name = $"Airstrike fighter {i + 1}";
                var sound = aircraft[i].gameObject.AddComponent<AudioSource>();
                sound.outputAudioMixerGroup = SkillSfx.MixerGroup;
                sound.spatialBlend = 1f; sound.minDistance = 45f; sound.maxDistance = 340f;
                sound.dopplerLevel = .75f; sound.spread = 70f;
                sound.volume = .48f * SkillSfx.MasterVolume;
                sound.clip = incomingClip;
                StartCoroutine(Flyover(aircraft[i], sound, i));
            }
        }
        catch (System.Exception error) { Debug.LogException(error, this); }
        // Independent releases keep the formation moving while all bombs fall ballistically.
        for (int i = 0; i < 3; i++) StartCoroutine(BombRun(i));
        while (completedBombs < 3) yield return null;
        yield return StartCoroutine(Collapse());
        yield return new WaitForSeconds(.45f);
        CombatCameraFeedback.GroundSlam(nearImpact, 120f, .8f, .8f, new Color(.35f, .3f, .23f));
        yield return new WaitForSeconds(14f);
        Destroy(effects);
        sequenceFinished = true;
        Debug.Log("Opening airstrike sequence completed.", this);
        enabled = false;
    }

    private GameObject BombTarget(int slot)
    {
        int middle = originalSpans.Count / 2;
        int spread = Mathf.Max(1, originalSpans.Count / 4);
        int index = slot == 0 ? middle : slot == 1 ? Mathf.Max(0, middle - spread)
            : Mathf.Min(originalSpans.Count - 1, middle + spread);
        return originalSpans[index];
    }

    private static Vector3 DeckCenter(GameObject span)
    {
        Vector3 local = span.GetComponent<LODGroup>().localReferencePoint; local.y = 2.7f;
        return span.transform.TransformPoint(local);
    }

    private IEnumerator BombRun(int slot)
    {
        var span = BombTarget(slot);
        Vector3 hit = DeckCenter(span);
        while (Time.time - flightStarted < ReleaseTime(slot)) yield return null;
        yield return StartCoroutine(Incoming(hit));
        bombImpacts.Add(hit);
        Blast(hit, slot == 0);
        if (span.activeSelf) yield return StartCoroutine(CollapseHit(span, hit, slot));
        completedBombs++;
    }

    private IEnumerator Incoming(Vector3 end)
    {
        // Same horizontal velocity as the aircraft; gravity bends the trajectory downwards.
        const float height = 24f;
        float duration = Mathf.Sqrt(2f * height / 9.81f);
        Vector3 velocity = FlightVelocity;
        Vector3 start = end - velocity * duration + Vector3.up * height;
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bomb.name = "Free-falling bomb";
        bomb.transform.SetParent(effects.transform);
        bomb.transform.localScale = new Vector3(.3f, .7f, .3f);
        var body = bomb.GetComponent<Collider>(); body.enabled = false; Destroy(body);
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", new Color(.12f, .14f, .13f));
        tint.SetColor("_Color", new Color(.12f, .14f, .13f));
        bomb.GetComponent<Renderer>().SetPropertyBlock(tint);
        float launched = Time.time;
        while (Time.time - launched < duration)
        {
            float t = Time.time - launched;
            bomb.transform.position = start + velocity * t + Vector3.down * (4.905f * t * t);
            bomb.transform.up = (velocity + Vector3.down * (9.81f * t)).normalized;
            yield return null;
        }
        Destroy(bomb);
    }

    private IEnumerator Flyover(Transform jet, AudioSource sound, int slot)
    {
        Vector3 velocity = FlightVelocity;
        float fallTime = Mathf.Sqrt(48f / 9.81f);
        Vector3 start = DeckCenter(BombTarget(slot)) - velocity * (ReleaseTime(slot) + fallTime)
            + Vector3.up * 25f;
        jet.position = start;
        jet.rotation = Quaternion.LookRotation(velocity);
        sound.Play();
        while (Time.time - flightStarted < 5.5f && jet)
        {
            float age = Time.time - flightStarted;
            jet.position = start + velocity * age;
            jet.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0, 0, -4f * Mathf.Sin(age));
            yield return null;
        }
        if (jet) Destroy(jet.gameObject);
    }

    private void Blast(Vector3 position, bool main)
    {
        Debug.Log($"Airstrike impact: {position}, primary={main}.", this);
        var audioObject = new GameObject("Airstrike blast crack, body and rumble");
        audioObject.transform.SetParent(effects.transform); audioObject.transform.position = position;
        var audio = audioObject.AddComponent<AudioSource>();
        audio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        audio.spatialBlend = .85f; audio.minDistance = 35f; audio.maxDistance = 250f;
        audio.rolloffMode = AudioRolloffMode.Linear; audio.dopplerLevel = 0;
        audio.volume = (main ? .85f : .7f) * SkillSfx.MasterVolume;
        audio.pitch = main ? 1f : .96f; audio.clip = blastClip; audio.Play();
        Destroy(audioObject, 6f);
        CombatCameraFeedback.Explosion(position, 120f);
        CombatCameraFeedback.GroundSlam(position, 120f, main ? 2.4f : 2.3f, 1.55f, new Color(.6f, .36f, .15f), 2.8f);
        var camera = Camera.main;
        if (camera && camera.TryGetComponent<ThirdPersonOrbitCam>(out var orbit))
            CombatCameraFeedback.Get(orbit).SkillPulse(main ? 2.4f : 2.2f, 1.5f, 24f, 1.1f);
        if (main) SkillScreenFx.Flash(new Color(1f, .78f, .48f, .15f), .16f);
        var root = new GameObject("Airstrike burst");
        root.transform.SetParent(effects.transform); root.transform.position = position;
        Fire(root.transform, 2.5f);
        var flash = SkillVfxFactory.CreateParticles(root.transform, "Hot impact core", Vector3.up * .4f, SkillVfxFactory.SoftDot);
        var flashMain = flash.main; flashMain.duration = .1f; flashMain.loop = false;
        flashMain.startLifetime = .18f; flashMain.startSpeed = 0f; flashMain.startSize = 13f;
        flashMain.startColor = new Color(1f, .82f, .48f, .8f);
        SkillVfxFactory.SetBurst(flash, 1); SkillVfxFactory.SetFadeOut(flash); flash.Play();
        Burst(root.transform, "smoke_04", 28, .55f, 7f, 5f, new Color(1f, .6f, .12f, .7f), false);
        // Smoke is delayed inside Fire so it cannot hide the initial orange core.
        Burst(root.transform, "spark_01", 75, 1.3f, 9f, .12f, new Color(1, .65f, .2f), false);
        var light = root.AddComponent<Light>();
        light.color = new Color(1, .45f, .15f); light.range = 40f; light.intensity = 9f;
        StartCoroutine(FadeBlastLight(light));
        Destroy(root, 13f);
    }

    private IEnumerator FadeBlastLight(Light light)
    {
        float age = 0;
        while (light && age < .65f)
        {
            age += Time.deltaTime;
            light.intensity = 12f * Mathf.Exp(-age * 7f);
            yield return null;
        }
        if (light) Destroy(light);
    }

    private void Fire(Transform root, float scale)
    {
        // A short volumetric-looking cloud burst expands and cools; no repeated flame silhouettes.
        var fire = SkillVfxFactory.CreateParticles(root, "Expanding combustion cloud", Vector3.zero, SkillVfxFactory.Kenney("smoke_07"));
        fire.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.ProjectOnPlane(bridgeDirection, Vector3.up).normalized);
        var main = fire.main;
        main.duration = .15f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f * scale, 3.4f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white; main.maxParticles = 260;
        main.gravityModifier = -.12f;
        var shape = fire.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = .9f * scale;
        shape.scale = new Vector3(1.8f, 1f, 1f); // Stretch across the bridge, not along it.
        var emission = fire.emission; emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(16 * scale)) });
        var colors = fire.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(1f, .92f, .65f), 0),
            new GradientColorKey(new Color(1f, .38f, .035f), .18f),
            new GradientColorKey(new Color(.32f, .12f, .045f), .45f),
            new GradientColorKey(new Color(.075f, .065f, .055f), .7f),
            new GradientColorKey(new Color(.14f, .13f, .12f), 1f)
        }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(.9f, .025f),
            new GradientAlphaKey(.8f, .55f), new GradientAlphaKey(0f, 1) });
        colors.color = gradient;
        var size = fire.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, .3f), new Keyframe(.18f, 1f), new Keyframe(.6f, 1.4f), new Keyframe(1, 1.7f)));
        fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney("smoke_07"));
        var noise = fire.noise; noise.enabled = true; noise.strength = .65f; noise.frequency = .8f;
        var drag = fire.limitVelocityOverLifetime; drag.enabled = true;
        drag.limit = 2f; drag.dampen = .45f;
        var spin = fire.rotationOverLifetime; spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-.45f, .45f);
        fire.Play();
        StartCoroutine(FireSmoke(root, scale));
    }

    private IEnumerator FireSmoke(Transform root, float scale)
    {
        yield return new WaitForSeconds(.45f);
        if (!root) yield break;
        var smokeRoot = new GameObject("Rising soot");
        smokeRoot.transform.SetParent(root, false);
        smokeRoot.transform.localPosition = Vector3.up * 2f;
        smokeRoot.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.ProjectOnPlane(bridgeDirection, Vector3.up).normalized);
        Burst(smokeRoot.transform, "smoke_04", Mathf.RoundToInt(32 * scale), 8f, 2.8f,
            4.2f * scale, new Color(.09f, .08f, .07f, .65f), true, 5f * scale);
    }

    private void Burst(Transform root, string texture, int count, float life, float speed, float size, Color color, bool smoke, float lateralRadius = 1.2f)
    {
        var ps = SkillVfxFactory.CreateParticles(root, texture, Vector3.zero, SkillVfxFactory.Kenney(texture));
        var main = ps.main;
        main.duration = .1f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(life * .7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .7f, size);
        main.startColor = color; main.maxParticles = count;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        if (smoke)
        {
            main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(color.r * 1.8f, color.g * 1.8f, color.b * 1.8f, color.a * .7f));
            var turbulence = ps.noise; turbulence.enabled = true;
            turbulence.strength = .7f; turbulence.frequency = .35f; turbulence.scrollSpeed = .4f;
        }
        main.gravityModifier = smoke ? -.08f : .3f;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 1.2f;
        shape.scale = new Vector3(lateralRadius / 1.2f, 1f, 1f);
        if (smoke) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney(texture));
        var growth = ps.sizeOverLifetime; growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .5f, 1, smoke ? 2f : .2f));
        SkillVfxFactory.SetBurst(ps, count); SkillVfxFactory.SetFadeOut(ps); ps.Play();
    }

    private IEnumerator CollapseHit(GameObject span, Vector3 hit, int seed)
    {
        yield return new WaitForSeconds(.18f);
        var cut = new GameObject("Bomb impact navigation cut");
        cut.transform.SetParent(transform);
        cut.transform.SetPositionAndRotation(hit, span.transform.rotation);
        var obstacle = cut.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(6.5f, 4f, 4f);
        obstacle.carving = true; obstacle.carveOnlyStationary = false;
        VandalismTimberFracture.Break(span, scenicSpan, hit - Vector3.up,
            Mathf.Clamp(650 / originalSpans.Count, 24, 64), 913 + seed);
    }

    private IEnumerator Collapse()
    {
        if (originalSpans.Count == 0) yield break;
        Debug.Log($"Airstrike timber fracture started: {originalSpans.Count} bridge sections.", this);
        if (replacedPier)
        {
            foreach (var renderer in replacedPier.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            foreach (var collider in replacedPier.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
        // Give the fireball time to establish before the supporting structure gives way.
        yield return new WaitForSeconds(.75f);
        originalSpans.Sort((a, b) => (a.transform.position - strike).sqrMagnitude.CompareTo((b.transform.position - strike).sqrMagnitude));
        int budget = Mathf.Clamp(650 / originalSpans.Count, 24, 64);
        for (int i = 0; i < originalSpans.Count; i++)
        {
            var original = originalSpans[i];
            if (!original.activeSelf) continue;
            var lod = original.GetComponent<LODGroup>();
            Vector3 center = lod ? lod.localReferencePoint : new Vector3(2.9f, 1.9f, -.78f);
            center.y = 2.7f;
            Vector3 impact = original.transform.TransformPoint(center);
            var cut = new GameObject("Destroyed pier navigation cut");
            cut.transform.SetParent(transform);
            cut.transform.SetPositionAndRotation(impact, original.transform.rotation);
            var obstacle = cut.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(6.5f, 4f, 4f);
            obstacle.carving = true; obstacle.carveOnlyStationary = false;

            StartCoroutine(FallUnsupported(original, impact, budget, 713 + i));
            // Propagation remains ordered, but adjacent supports do not fail on a metronome.
            var timing = new System.Random(1713 + i);
            yield return new WaitForSeconds(.16f + (float)timing.NextDouble() * .3f);
        }
        FindAnyObjectByType<StageSceneInitializer>()?.NotifyCoverGeometryChanged();
        Debug.Log($"Airstrike fracture completed: {originalSpans.Count} sections replaced with {budget * originalSpans.Count} independent timbers.", this);
    }

    public event System.Action<float> CollapseImpulse;

    private void CollapseFeedback(Vector3 point, float strength)
    {
        CombatCameraFeedback.GroundSlam(point, 90f, strength, .65f,
            new Color(.35f, .3f, .23f), 1.5f);
        CollapseImpulse?.Invoke(strength);
    }

    private IEnumerator FallUnsupported(GameObject span, Vector3 center, int budget, int seed)
    {
        PlayCollapseSound(collapseClip, center, .45f, .86f + (seed % 7) * .035f);
        foreach (var collider in span.GetComponentsInChildren<Collider>()) collider.enabled = false;
        Vector3 failedSupport = strike;
        float nearest = float.PositiveInfinity;
        foreach (Vector3 hit in bombImpacts)
        {
            float distance = (hit - center).sqrMagnitude;
            if (distance < nearest) { nearest = distance; failedSupport = hit; }
        }
        Vector3 inward = Vector3.ProjectOnPlane(failedSupport - center, Vector3.up).normalized;
        Vector3 pivot = center - inward * 2.7f;
        Vector3 axis = Vector3.Cross(Vector3.up, inward).normalized;
        Vector3 initialPosition = span.transform.position;
        Quaternion initialRotation = span.transform.rotation;
        var random = new System.Random(seed);
        float duration = .65f + (float)random.NextDouble() * .45f;
        float tilt = 35f + (float)random.NextDouble() * 25f;
        float roll = ((float)random.NextDouble() * 2f - 1f) * 13f;
        float sag = 1.2f + (float)random.NextDouble() * .7f;
        float age = 0;
        Vector3 localCenter = span.transform.InverseTransformPoint(center);
        Vector3 previousCenter = center;
        Vector3 fallVelocity = Vector3.down * 1.8f;
        CollapseDust(center, span.transform.rotation, false);
        bool snapped = false;
        // A short stressed sag gives way to an accelerating drop and uneven torsion.
        while (age < duration)
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            float fall = Mathf.Pow(Mathf.Clamp01((t - .2f) / .8f), 2f);
            float strain = Mathf.Sin(t * 35f) * .6f * (1f - t);
            Quaternion bend = Quaternion.AngleAxis(tilt * fall + strain, axis)
                * Quaternion.AngleAxis(roll * fall, inward);
            span.transform.SetPositionAndRotation(pivot + bend * (initialPosition - pivot)
                + Vector3.down * (.12f * t + fall * sag), bend * initialRotation);
            Vector3 currentCenter = span.transform.TransformPoint(localCenter);
            if (Time.deltaTime > .0001f) fallVelocity = (currentCenter - previousCenter) / Time.deltaTime;
            previousCenter = currentCenter;
            if (!snapped && t >= .48f)
            {
                snapped = true;
                CollapseDust(currentCenter, span.transform.rotation, true);
                PlayCollapseSound(collapseClip, currentCenter, .65f, .78f + (seed % 5) * .045f);
                CollapseFeedback(currentCenter, .65f);
            }
            yield return null;
        }
        VandalismTimberFracture.Break(span, scenicSpan, previousCenter, budget, seed, false,
            Vector3.ClampMagnitude(fallVelocity, 10f));
        float waterHeight = -2f;
        foreach (Transform child in GetComponentsInChildren<Transform>())
            if (child.name == "Water") { waterHeight = child.position.y; break; }
        float drop = Mathf.Max(0f, previousCenter.y - waterHeight);
        float downwardSpeed = Mathf.Max(1.8f, -Mathf.Clamp(fallVelocity.y, -10f, 0f));
        yield return new WaitForSeconds((Mathf.Sqrt(downwardSpeed * downwardSpeed + 19.62f * drop) - downwardSpeed) / 9.81f);
        Vector3 waterPoint = new Vector3(previousCenter.x, waterHeight, previousCenter.z);
        PlayCollapseSound(splashClip, waterPoint, .8f, .85f + (seed % 6) * .03f);
        CollapseFeedback(waterPoint, .9f);
    }

    private void CollapseDust(Vector3 point, Quaternion rotation, bool snap)
    {
        var dust = new GameObject(snap ? "Support snap dust" : "Stressed deck dust");
        dust.transform.SetParent(effects.transform);
        // Particle hemispheres emit along local Z; turn it upward, retaining the span's length axis.
        dust.transform.SetPositionAndRotation(point, rotation * Quaternion.Euler(-90f, 0f, 0f));
        Burst(dust.transform, "smoke_07", snap ? 22 : 9, snap ? 3.2f : 1.8f,
            snap ? 2.4f : .7f, snap ? 1.8f : .9f, new Color(.42f, .35f, .26f, .38f), true, 2.6f);
        if (snap)
            Burst(dust.transform, "dirt_02", 20, 1.3f, 2f, .13f, new Color(.34f, .25f, .15f), false, 2.6f);
        Destroy(dust, 4f);
    }

    private void PlayCollapseSound(AudioClip clip, Vector3 point, float volume, float pitch = 1f)
    {
        var go = new GameObject("Timber collapse / water impact audio");
        go.transform.SetParent(effects.transform); go.transform.position = point;
        var source = go.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = SkillSfx.MixerGroup;
        source.spatialBlend = .85f; source.minDistance = 25; source.maxDistance = 180;
        source.dopplerLevel = 0; source.volume = volume * SkillSfx.MasterVolume;
        source.pitch = pitch; source.clip = clip; source.Play(); Destroy(go, clip.length / pitch + .2f);
    }

    private static AudioClip BuildCollapseSound(bool water)
    {
        const int rate = 22050;
        var samples = new float[rate * 3];
        var rng = new System.Random(water ? 772 : 382);
        float low = 0, mid = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float n = (float)rng.NextDouble() * 2 - 1;
            low += .025f * (n - low); mid += .19f * (n - mid);
            float value;
            if (water)
                value = (mid * 1.2f + low * 3f) * Mathf.Exp(-t * 1.7f)
                    + n * .18f * Mathf.Exp(-t * .9f) * Mathf.Clamp01(t * 6);
            else
            {
                float cracks = 0;
                for (int k = 0; k < 7; k++)
                {
                    float elapsed = t - k * .14f;
                    if (elapsed >= 0) cracks += Mathf.Exp(-elapsed * (28 + k * 5));
                }
                value = n * cracks * .4f + low * 2.5f * Mathf.Exp(-t * 1.4f)
                    + mid * .65f * Mathf.Exp(-t * 2);
            }
            samples[i] = (float)System.Math.Tanh(value) * .8f * Mathf.Clamp01(t * 500)
                * Mathf.Clamp01((3 - t) * 4);
        }
        var clip = AudioClip.Create(water ? "Heavy timber splash" : "Progressive timber cracking and collapse", samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }

    private static AudioClip BuildIncoming()
    {
        const int rate = 44100;
        var samples = new float[rate * 6];
        var random = new System.Random(713);
        float low = 0, mid = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float noise = (float)random.NextDouble() * 2 - 1;
            low += .012f * (noise - low);
            mid += .13f * (noise - mid);
            float flutter = .85f + .15f * Mathf.Sin(2 * Mathf.PI * 17f * t);
            float engine = (low * 4f + mid * 1.1f + noise * .12f) * flutter;
            engine += Mathf.Sin(2 * Mathf.PI * 125f * t) * .045f;
            float envelope = Mathf.Clamp01(t / .25f) * Mathf.Clamp01((6f - t) / .7f);
            samples[i] = (float)System.Math.Tanh(engine * 1.25f) * .8f * envelope;
        }
        var clip = AudioClip.Create("Jet turbulent exhaust roar", samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }

    private static AudioClip BuildBlast()
    {
        const int rate = 44100;
        var samples = new float[rate * 5];
        var random = new System.Random(194);
        float low = 0, body = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float noise = (float)random.NextDouble() * 2 - 1;
            low += .009f * (noise - low); body += .09f * (noise - body);
            float crack = noise * Mathf.Exp(-t * 40f) * .65f;
            float thump = Mathf.Sin(2f * Mathf.PI * (42f * t + 12f * (1f - Mathf.Exp(-t * 8f))))
                * Mathf.Exp(-t * 3.5f) * .5f;
            float rumble = low * 4f * Mathf.Exp(-t * 1.1f) + body * 1.4f * Mathf.Exp(-t * 3f);
            float echo = t > .19f ? body * .65f * Mathf.Exp(-(t - .19f) * 2f) : 0;
            samples[i] = (float)System.Math.Tanh((crack + thump + rumble + echo) * 1.3f)
                * .88f * Mathf.Clamp01(t / .001f) * Mathf.Clamp01((5f - t) / .3f);
        }
        var clip = AudioClip.Create("Bridge airstrike layered blast", samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }

    private void OnDestroy()
    {
        if (incomingClip) Destroy(incomingClip);
        if (blastClip) Destroy(blastClip);
        if (collapseClip) Destroy(collapseClip);
        if (splashClip) Destroy(splashClip);

    }
}

