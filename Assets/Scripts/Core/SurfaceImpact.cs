using System;
using UnityEngine;

// Optional override for surfaces whose materials do not describe their physical surface.
public sealed class SurfaceImpact : MonoBehaviour
{
    public enum Kind { Default, Metal, Concrete, Wood }
    public Kind surface;
    private static readonly System.Collections.Generic.Dictionary<GameObject, bool> poolable = new();
    private static bool CanPool(GameObject template)
    {
        if (poolable.TryGetValue(template, out bool result)) return result;
        result = true;
        foreach (var script in template.GetComponentsInChildren<MonoBehaviour>(true))
            if (script && !(script is ParticleSystemAutoDestroy) && !(script is AutoDestroyAfterSeconds)) { result = false; break; }
        poolable[template] = result;
        return result;
    }

    public static void Spawn(GameObject template, Transform target, Vector3 point, Vector3 normal)
    {
        if (!target || target.GetComponentInParent<HealthManager>()) return;
        Kind kind = Resolve(target);
        if (kind != Kind.Default)
        {
            SurfaceImpactDetails.Play(kind, point);
            SurfaceImpactDetails.Mark(kind, target, point, normal);
        }
        if (kind == Kind.Wood) { SpawnGenerated(kind, point, normal); return; }
        if (!template)
        {
            if (kind != Kind.Default) SpawnGenerated(kind, point, normal);
            return;
        }
        int poolKey = unchecked(template.GetInstanceID() * 397 ^ (int)kind);
        Quaternion rotation = kind == Kind.Concrete ? Quaternion.FromToRotation(Vector3.forward, normal) : template.transform.rotation;
        // Behaviour-driven prefabs may destroy themselves: retain their original lifecycle.
        bool reusable = CanPool(template);
        if (reusable && CombatParticlePool.Replay(poolKey, point + normal * .01f, rotation, 2f)) return;
        GameObject effect = Instantiate(template, point + normal * 0.01f, template.transform.rotation);
        effect.SetActive(true);
        // A hard lifetime also covers templates without automatic particle cleanup.
        if (!reusable) Destroy(effect, 2f);
        if (kind != Kind.Concrete) {
            if (reusable) CombatParticlePool.Register(effect, poolKey, 2f);
            return;
        }

        effect.transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool dust = particles.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0;
            var main = particles.main;
            main.loop = false;
            main.duration = 0.15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, dust ? 0.38f : 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(dust ? 0.15f : 0.8f, dust ? 0.45f : 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(dust ? 0.06f : 0.012f, dust ? 0.13f : 0.025f);
            main.startColor = new Color(0.46f, 0.44f, 0.40f, dust ? 0.28f : 0.8f);
            main.gravityModifier = dust ? 0f : 0.8f;
            main.maxParticles = dust ? 6 : 10;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer) particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(dust ? 4 : 7)) });
            var trails = particles.trails;
            trails.enabled = false;
            particles.Play(false);
        }
        if (reusable) CombatParticlePool.Register(effect, poolKey, 2f);
    }

    private static void SpawnGenerated(Kind kind, Vector3 point, Vector3 normal)
    {
        if (CombatParticlePool.Replay(-100 - (int)kind, point + normal * .015f, Quaternion.LookRotation(normal), .6f)) return;
        var root = new GameObject("VFX_SurfaceImpact_" + kind);
        root.transform.SetPositionAndRotation(point + normal * .015f, Quaternion.LookRotation(normal));
        bool concrete = kind == Kind.Concrete;
        bool wood = kind == Kind.Wood;
        var texture = SkillVfxFactory.Kenney(wood ? "dirt_02" : concrete ? "smoke_07" : "spark_01");
        var particles = SkillVfxFactory.CreateParticles(root.transform, "Impact", Vector3.zero, texture);
        var main = particles.main;
        main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, concrete ? .38f : wood ? .45f : .25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(concrete ? .3f : 1.8f, concrete ? 1f : 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(concrete ? .07f : .02f, concrete ? .17f : wood ? .09f : .05f);
        main.startColor = wood ? new Color(.45f, .28f, .12f, .9f) : concrete ? new Color(.46f, .44f, .4f, .6f) : new Color(1f, .72f, .25f);
        main.gravityModifier = concrete ? .1f : .8f;
        if (concrete || wood) particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = SkillVfxFactory.GetAlphaMaterial(texture);
        var shape = particles.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 40f; shape.radius = .025f;
        SkillVfxFactory.SetBurst(particles, concrete ? 7 : wood ? 20 : 14);
        SkillVfxFactory.SetFadeOut(particles);
        if (concrete)
        {
            var debris = SkillVfxFactory.CreateParticles(root.transform, "Chips", Vector3.zero, SkillVfxFactory.Kenney("dirt_02"));
            var chip = debris.main;
            chip.startLifetime = .25f;
            chip.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2f);
            chip.startSize = new ParticleSystem.MinMaxCurve(.015f, .035f);
            chip.startColor = new Color(.4f, .38f, .35f);
            chip.gravityModifier = 1f;
            var cone = debris.shape;
            cone.enabled = true; cone.shapeType = ParticleSystemShapeType.Cone; cone.angle = 45f; cone.radius = .02f;
            SkillVfxFactory.SetBurst(debris, 7);
            SkillVfxFactory.SetFadeOut(debris);
            debris.Play();
        }
        particles.Play();
        CombatParticlePool.Register(root, -100 - (int)kind, .6f);
    }

    private static Kind NameKind(string name)
    {
        if (string.IsNullOrEmpty(name)) return Kind.Default;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("wood") || lower.Contains("plank") || lower.Contains("timber") || lower.Contains("prop_pier")) return Kind.Wood;
        if (lower.Contains("car") || lower.Contains("vehicle")) return Kind.Metal;
        if (lower.Contains("brick") || lower.Contains("stone")) return Kind.Concrete;
        if (name.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("cement", StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Concrete;
        if (name.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0) return Kind.Metal;
        return Kind.Default;
    }

    public static Kind Resolve(Transform target)
    {
        var marker = target.GetComponentInParent<SurfaceImpact>();
        if (marker) return marker.surface;
        // Verified map assets: their shared material names do not describe physical surfaces.
        for (Transform node = target; node; node = node.parent)
        {
            if (node.name.StartsWith("SM_Crate_", StringComparison.OrdinalIgnoreCase)) return Kind.Wood;
            if (node.name.Equals("SM_Gate_Building", StringComparison.OrdinalIgnoreCase) ||
                node.name.StartsWith("SM_Gate_Building (", StringComparison.OrdinalIgnoreCase)) return Kind.Metal;
        }
        var collider = target.GetComponent<Collider>();
        if (collider && collider.sharedMaterial)
        {
            Kind physical = NameKind(collider.sharedMaterial.name);
            if (physical != Kind.Default) return physical;
        }
        var renderer = target.GetComponent<Renderer>();
        if (!renderer) renderer = target.GetComponentInParent<Renderer>();
        if (!renderer) return HierarchyKind(target);
        // Mixed meshes stay on the original effect unless explicitly marked.
        Kind result = Kind.Default;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (!material) return HierarchyKind(target);
            Kind next = NameKind(material.name);
            if (next == Kind.Default || (result != Kind.Default && result != next)) return HierarchyKind(target);
            result = next;
        }
        return result;
    }
    private static Kind HierarchyKind(Transform target)
    {
        for (var node = target; node; node = node.parent)
        {
            var kind = NameKind(node.name);
            if (kind != Kind.Default) return kind;
        }
        return Kind.Default;
    }

}
