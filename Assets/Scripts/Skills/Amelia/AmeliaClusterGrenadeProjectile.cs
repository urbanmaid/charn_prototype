using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime projectile and original procedural audio/VFX for Amelia's cluster grenade.</summary>
public sealed class AmeliaClusterGrenadeProjectile : MonoBehaviour
{
    private static PhysicsMaterial bounceMaterial;
    private GameObject owner;
    private Charm.Mechanism.Missions.CombatSource combatSource;
    private float primaryRadius, submunitionRadius, fuse;
    private bool detonated;
    private Rigidbody body;
    private float nextBounceSound;

    public static void Throw(GameObject caster, Vector3 origin, Vector3 target, float speed,
        float primaryRadius, float submunitionRadius)
    {
        // Keep collision at unit scale; the painted canister is a separate visual hierarchy.
        var grenade = new GameObject("Amelia Cluster Grenade");
        grenade.name = "Amelia Cluster Grenade";
        grenade.transform.position = origin;
        var collider = grenade.AddComponent<SphereCollider>();
        collider.radius = .06f;
        AmeliaGrenadeVfx.BuildShell(grenade.transform, false);
        var projectile = grenade.AddComponent<AmeliaClusterGrenadeProjectile>();
        projectile.owner = caster;
        projectile.combatSource = Charm.Mechanism.Missions.CombatTelemetry.Capture(caster);
        projectile.primaryRadius = primaryRadius;
        projectile.submunitionRadius = submunitionRadius;
        projectile.fuse = 2.4f;
        projectile.body = grenade.AddComponent<Rigidbody>();
        projectile.body.mass = .8f;
        projectile.body.useGravity = false;
        projectile.body.linearDamping = 0f;
        projectile.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectile.body.linearVelocity = LaunchVelocity(origin, target, speed);
        projectile.body.angularVelocity = new Vector3(3.5f, 1.2f, 2f);
        collider.sharedMaterial = BounceMaterial;
        foreach (Collider own in caster.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(collider, own, true);
        AmeliaGrenadeAudio.Play(AmeliaGrenadeAudio.Kind.Throw, origin, .55f);
        AmeliaGrenadeVfx.AttachFuse(grenade.transform);
        projectile.StartCoroutine(projectile.FuseWarning(grenade, projectile.fuse, true));
        Object.Destroy(grenade, 5f);
    }

    public const float MaxThrowRange = 18f;
    public static float Gravity => Mathf.Max(.1f, -Physics.gravity.y * 1.6f);

    public static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, float speed)
    {
        Vector3 offset = Vector3.ClampMagnitude(target - origin, MaxThrowRange);
        float horizontal = new Vector2(offset.x, offset.z).magnitude;
        float apex = Mathf.Max(0f, offset.y) + Mathf.Clamp(horizontal * .12f, 1.2f, 3.5f);
        float upward = Mathf.Sqrt(2f * Gravity * apex);
        float flight = upward / Gravity + Mathf.Sqrt(2f * (apex - offset.y) / Gravity);
        return Vector3.ClampMagnitude(new Vector3(offset.x, 0f, offset.z) / flight, speed) + Vector3.up * upward;
    }

    public static Vector3 AimPoint(GameObject caster)
    {
        Vector3 origin = caster.transform.position + Vector3.up * 1.4f + caster.transform.forward * .65f;
        Vector3 target = origin + caster.transform.forward * 12f;
        Camera camera = Camera.main;
        if (camera != null && caster.CompareTag("Player"))
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            target = ray.GetPoint(50f);
            float closest = 80f;
            foreach (var hit in Physics.RaycastAll(ray, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(caster.transform) || hit.distance >= closest) continue;
                closest = hit.distance; target = hit.point;
            }
        }
        return origin + Vector3.ClampMagnitude(target - origin, MaxThrowRange);
    }

    private void Update()
    {
        if (detonated) return;
        fuse -= Time.deltaTime;
        if (fuse <= 0f) Detonate();
    }

    private void FixedUpdate()
    {
        if (!detonated && body && !body.isKinematic)
            body.AddForce(Vector3.down * Gravity, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (detonated || Time.time < nextBounceSound || collision.relativeVelocity.sqrMagnitude < 1f) return;
        nextBounceSound = Time.time + .14f;
        AmeliaGrenadeAudio.Play(AmeliaGrenadeAudio.Kind.Bounce, transform.position, .4f);
    }

    private void Detonate()
    {
        if (detonated) return;
        detonated = true;
        Vector3 center = transform.position;
        if (body != null) body.isKinematic = true;
        var collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        foreach (Light light in GetComponentsInChildren<Light>()) light.enabled = false;
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>()) trail.emitting = false;
        Blast(center, primaryRadius, 48f, 8f);
        AmeliaGrenadeVfx.Burst(center, primaryRadius, true);
        CombatCameraFeedback.Explosion(center, primaryRadius * 1.5f);
        AmeliaPaintMark.Splash(center, primaryRadius, true);
        AmeliaGrenadeAudio.Play(AmeliaGrenadeAudio.Kind.MainBlast, center, 1f);

        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + i * 90f + Random.Range(-12f, 12f);
            Vector3 flat = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            StartCoroutine(SpawnSubmunition(center + Vector3.up * .2f + flat * .18f, flat, i));
        }
        Destroy(gameObject, 3f);
    }

    private IEnumerator SpawnSubmunition(Vector3 point, Vector3 direction, int index)
    {
        var child = new GameObject("Cluster Submunition");
        child.name = "Cluster Submunition";
        child.transform.position = point;
        AmeliaGrenadeVfx.BuildShell(child.transform, true);
        var collider = child.AddComponent<SphereCollider>();
        collider.radius = .04f;
        collider.sharedMaterial = BounceMaterial;
        var rb = child.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = .12f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = .15f;
        rb.angularVelocity = Random.onUnitSphere * 12f;
        rb.linearVelocity = direction * Random.Range(2.7f, 3.4f) + Vector3.up * 3.5f;
        if (owner != null)
            foreach (Collider own in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, own, true);
        AmeliaGrenadeVfx.AttachFuse(child.transform);
        StartCoroutine(FuseWarning(child, .92f + index * .045f, index == 0));
        Destroy(child, 3f);
        yield return new WaitForSeconds(.92f + index * .045f);
        if (child == null) yield break;
        Vector3 center = child.transform.position;
        collider.enabled = false;
        Blast(center, submunitionRadius, 22f, 5f);
        AmeliaGrenadeVfx.Burst(center, submunitionRadius, false);
        CombatCameraFeedback.Explosion(center, submunitionRadius * 1.2f);
        AmeliaPaintMark.Splash(center, submunitionRadius, false);
        AmeliaGrenadeAudio.Play(AmeliaGrenadeAudio.Kind.SubBlast, center, .72f);
        Destroy(child);
    }

    private static PhysicsMaterial BounceMaterial
    {
        get
        {
            if (bounceMaterial == null)
                bounceMaterial = new PhysicsMaterial("Amelia grenade bounce")
                {
                    bounciness = .52f, dynamicFriction = .55f, staticFriction = .65f,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
            return bounceMaterial;
        }
    }

    private IEnumerator FuseWarning(GameObject target, float remaining, bool audible)
    {
        // Both stages use the same accelerating countdown; one voice represents the clustered bomblets.
        float nextTick = 0f;
        Light glow = target.GetComponentInChildren<Light>();
        while (target && remaining > 0f && (target != gameObject || !detonated))
        {
            if (remaining <= .85f && nextTick <= 0f)
            {
                if (audible) AmeliaGrenadeAudio.Play(AmeliaGrenadeAudio.Kind.Warning, target.transform.position, .65f);
                nextTick = Mathf.Lerp(.075f, .23f, Mathf.Clamp01(remaining / .85f));
                if (glow) { glow.color = new Color(1f, .18f, .035f); glow.intensity = 3.5f; glow.range = 1.8f; }
            }
            if (glow && remaining <= .85f)
                glow.intensity = Mathf.MoveTowards(glow.intensity, .25f, Time.deltaTime * 32f);
            yield return null;
            remaining -= Time.deltaTime;
            nextTick -= Time.deltaTime;
        }
    }

    private void Blast(Vector3 center, float radius, float peakDamage, float edgeDamage)
    {
        var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<HealthManager>();
        foreach (Collider hit in hits)
        {
            if (hit == null || (owner != null && hit.transform.root == owner.transform.root)) continue;
            HealthManager health = hit.GetComponentInParent<HealthManager>();
            if (health == null || health.dead || !health.isActiveAndEnabled || AllyTargeting.IsAlly(health) || damaged.Contains(health)) continue;
            Vector3 point = hit.ClosestPoint(center);
            Vector3 delta = point - center;
            float distance = delta.magnitude;
            if (distance > radius || !AllyTargeting.HasLineOfSight(center + Vector3.up * .12f,
                point, transform, health.transform.root)) continue;
            damaged.Add(health);
            float t = Mathf.Clamp01(distance / radius);
            float damage = Mathf.Lerp(peakDamage, edgeDamage, t * t);
            using (Charm.Mechanism.Missions.CombatTelemetry.UseSource(combatSource))
            health.TakeDamage(point, distance > .01f ? delta.normalized : Vector3.up,
                damage, hit, owner, HealthManager.DamageType.Skill);
        }
    }

}

internal static class AmeliaGrenadeVfx
{
    private static Material orangeShell, tealShell, metalShell, paleShell;
    private static Texture2D shardTexture;

    public static void BuildShell(Transform root, bool small, bool held = false)
    {
        if (orangeShell == null)
        {
            orangeShell = ShellMaterial(new Color(.95f, .27f, .035f));
            tealShell = ShellMaterial(new Color(.035f, .65f, .46f));
            metalShell = ShellMaterial(new Color(.12f, .14f, .13f));
            paleShell = ShellMaterial(new Color(.91f, .87f, .67f));
        }
        float s = held ? .22f : small ? .25f : .38f;
        Part(root, PrimitiveType.Cylinder, "Paint canister", Vector3.zero,
            new Vector3(.24f, .13f, .24f) * s, small ? tealShell : orangeShell);
        Part(root, PrimitiveType.Cylinder, "Bottom rim", Vector3.down * .12f * s,
            new Vector3(.25f, .018f, .25f) * s, tealShell);
        Part(root, PrimitiveType.Cylinder, "Label band", Vector3.up * .035f * s,
            new Vector3(.245f, .024f, .245f) * s, paleShell);
        Part(root, PrimitiveType.Cylinder, "Fuse cap", Vector3.up * .145f * s,
            new Vector3(.12f, .03f, .12f) * s, metalShell);
        Part(root, PrimitiveType.Cube, "Safety lever", new Vector3(.07f, .16f, 0f) * s,
            new Vector3(.21f, .025f, .055f) * s, metalShell);
    }

    private static Material ShellMaterial(Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .32f);
        return m;
    }

    private static void Part(Transform root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Texture2D ShardTexture
    {
        get
        {
            if (shardTexture != null) return shardTexture;
            const int n = 64;
            shardTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { name = "Amelia angular paint shard", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f), v = y / (n - 1f);
                float center = .42f + .13f * v;
                float halfWidth = v < .35f ? Mathf.Lerp(.02f, .32f, v / .35f) : Mathf.Lerp(.32f, .01f, (v - .35f) / .65f);
                float alpha = Mathf.Clamp01((halfWidth - Mathf.Abs(u - center)) * n);
                pixels[y * n + x] = new Color(1f, 1f, 1f, alpha);
            }
            shardTexture.SetPixels(pixels);
            shardTexture.Apply(false, true);
            return shardTexture;
        }
    }
    public static void AttachFuse(Transform target)
    {
        var lightObject = new GameObject("Grenade fuse glow");
        lightObject.transform.SetParent(target, false);
        var light = lightObject.AddComponent<Light>();
        light.color = new Color(1f, .5f, .08f);
        light.range = .9f;
        light.intensity = .6f;
        var trail = target.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = SkillVfxFactory.GetAdditiveMaterial(SkillVfxFactory.Kenney("spark_01"));
        trail.time = .12f;
        trail.startWidth = .045f;
        trail.endWidth = 0f;
        trail.startColor = new Color(.2f, 1f, .55f, .7f);
        trail.endColor = new Color(.04f, .65f, .35f, 0f);
        trail.minVertexDistance = .04f;
    }

    public static void Burst(Vector3 center, float radius, bool primary)
    {
        var root = new GameObject(primary ? "VFX_AmeliaClusterBlast" : "VFX_AmeliaSubBlast");
        root.transform.position = center;
        var runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 1.2f;
        float scale = primary ? 1.9f : 1.1f;
        // Transparent textured particles disappear quickly; there is no solid blast sphere.
        var flash = Particles(root.transform, "Ignition", "star_08", 1,
            .09f, 0f, 2.5f * scale, new Color(1f, .92f, .55f));
        var flashShape = flash.shape;
        flashShape.enabled = false;
        var flame = Particles(root.transform, "Orange blast wedges", "flame_05", primary ? 20 : 12,
            .32f, 8f * scale, 1.25f * scale, new Color(1f, .38f, .045f));
        flame.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAdditiveMaterial(ShardTexture);
        var flameSize = flame.sizeOverLifetime;
        flameSize.enabled = true;
        flameSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .35f), new Keyframe(.35f, 1f), new Keyframe(1f, 0f)));
        var sparks = Particles(root.transform, "Hot fragments", "spark_01", primary ? 42 : 20,
            .48f, 9f * scale, .09f * scale, new Color(1f, .8f, .28f));
        var sparkMain = sparks.main;
        sparkMain.gravityModifier = 1.1f;
        var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.velocityScale = .045f;
        sparkRenderer.lengthScale = 1.4f;
        var smoke = Particles(root.transform, "Smoke puffs", "smoke_07", primary ? 10 : 5,
            .85f, 1.7f * scale, 1.1f * scale, new Color(.24f, .22f, .19f, .3f));
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney("smoke_07"));
        var smokeSize = smoke.sizeOverLifetime;
        smokeSize.enabled = true;
        smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .4f, 1f, 1.8f));
        var paint = Particles(root.transform, "Ash spray", "dirt_02", primary ? 28 : 14,
            .55f, 7f * scale, .22f * scale, Color.white);
        paint.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            SkillVfxFactory.GetAlphaMaterial(SkillVfxFactory.Kenney("dirt_02"));
        var paintMain = paint.main;
        paintMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.22f, .16f, .09f), new Color(.08f, .065f, .05f));
        paintMain.gravityModifier = 1.8f;
        var shards = Particles(root.transform, "Teal shell shards", "spark_01", primary ? 16 : 8,
            .38f, 7.5f * scale, .5f * scale, new Color(.06f, .93f, .53f));
        shards.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(ShardTexture);
        var shardMain = shards.main;
        shardMain.gravityModifier = 1.8f;
        var rotation = shards.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
        // Keep the radius cue on a nearby surface and match the actual damage radius.
        if (Physics.Raycast(center + Vector3.up * .1f, Vector3.down, out RaycastHit floor,
            1.2f, ~0, QueryTriggerInteraction.Ignore) && floor.normal.y > .7f &&
            floor.collider.GetComponentInParent<HealthManager>() == null)
        {
            var ringRoot = new GameObject("Surface shockwave");
            ringRoot.transform.SetParent(root.transform, false);
            ringRoot.transform.position = floor.point + Vector3.up * .025f;
            SkillVfxFactory.SpawnGroundRing(ringRoot.transform, new Color(1f, .6f, .14f, .28f),
                .2f, radius * 2f / .85f, .2f, 0f);
        }
        var light = SkillVfxFactory.CreateLight(root.transform, Vector3.up * .2f,
            new Color(1f, .55f, .15f), radius);
        runner.AddLight(light, primary ? 3f : 1.5f, .01f, .18f);
        runner.Play();
    }

    private static ParticleSystem Particles(Transform root, string name, string texture,
        int count, float life, float speed, float size, Color color)
    {
        // Factory stops and clears newly created systems before any module is configured.
        var ps = SkillVfxFactory.CreateParticles(root, name, Vector3.zero, SkillVfxFactory.Kenney(texture));
        var main = ps.main;
        main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * .75f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
        main.startColor = color;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .12f;
        SkillVfxFactory.SetBurst(ps, count);
        SkillVfxFactory.SetFadeOut(ps);
        return ps;
    }
}

/// <summary>Short synthesized throw, deep primary boom, and bright submunition pop; no external audio assets required.</summary>
internal static class AmeliaGrenadeAudio
{
    internal enum Kind { Throw, MainBlast, SubBlast, Bounce, Warning }
    private static readonly AudioClip[] clips = new AudioClip[5];
    public static void Play(Kind kind, Vector3 position, float volume, float audibleRange = 65f)
    {
        int index = (int)kind;
        if (clips[index] == null) clips[index] = Build(kind);
        var go = new GameObject("SFX_AmeliaGrenade_" + kind);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clips[index]; source.volume = volume * SkillSfx.MasterVolume; source.spatialBlend = .85f;
        source.outputAudioMixerGroup = SkillSfx.MixerGroup;
        source.pitch = kind == Kind.SubBlast ? Random.Range(.93f, 1.08f) : 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 3f; source.maxDistance = audibleRange; source.rolloffMode = AudioRolloffMode.Linear;
        source.Play(); Object.Destroy(go, clips[index].length + .1f);
    }
    private static AudioClip Build(Kind kind)
    {
        int rate = 22050;
        float duration = kind == Kind.Warning ? .065f : kind == Kind.Throw || kind == Kind.Bounce ? .16f : kind == Kind.MainBlast ? .72f : .32f;
        int count = Mathf.CeilToInt(rate * duration);
        var samples = new float[count];
        var random = new System.Random(319 + (int)kind);
        float lowNoise = 0f, phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate, n = i / (float)count;
            float noise = (float)random.NextDouble() * 2f - 1f;
            lowNoise += .12f * (noise - lowNoise);
            float sample;
            if (kind == Kind.Warning)
            {
                // Original short electronic timer chirp, not a sampled game sound.
                phase += 2f * Mathf.PI * Mathf.Lerp(1400f, 2100f, n) / rate;
                sample = (Mathf.Sin(phase) * .7f + Mathf.Sin(phase * 2f) * .15f) * Mathf.Sin(Mathf.PI * n);
            }
            else if (kind == Kind.Bounce)
            {
                sample = noise * Mathf.Exp(-t * 120f) * .5f
                    + Mathf.Sin(2f * Mathf.PI * 720f * t) * Mathf.Exp(-t * 65f) * .2f;
            }
            else if (kind == Kind.Throw)
            {
                // Mechanical release click followed by a soft throw swish.
                sample = noise * Mathf.Exp(-t * 150f) * .6f
                    + lowNoise * Mathf.Sin(Mathf.PI * n) * .35f;
            }
            else
            {
                bool main = kind == Kind.MainBlast;
                float frequency = main ? Mathf.Lerp(100f, 38f, n) : Mathf.Lerp(150f, 60f, n);
                phase += 2f * Mathf.PI * frequency / rate;
                float crack = noise * Mathf.Exp(-t * (main ? 75f : 100f));
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * (main ? 9f : 17f));
                float debris = lowNoise * Mathf.Exp(-t * (main ? 6f : 12f));
                sample = crack * .7f + body * .6f + debris * .8f;
            }
            // Short attack and final fade avoid discontinuities and harsh clipping.
            sample *= Mathf.Clamp01(t / .001f) * Mathf.Clamp01((duration - t) / .025f);
            samples[i] = sample / (1f + Mathf.Abs(sample));
        }
        var clip = AudioClip.Create("AmeliaCluster_" + kind, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
