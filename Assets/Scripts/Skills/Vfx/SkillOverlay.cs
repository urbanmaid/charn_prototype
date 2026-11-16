using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Glow overlay on a character or weapon (Resources/VFX/SkillOverlay shader): adds one extra material to every
/// mesh renderer under the target, fades in, holds, fades out and removes itself. Applying the same key again
/// refreshes the duration. Used for the venom outline on slowed enemies and the scale pattern on the venom gun.
/// </summary>
public class SkillOverlay : MonoBehaviour
{
    private const float FadeIn = 0.15f, FadeOut = 0.5f;

    private static Shader shader;

    private string key;
    private Material material;
    private readonly List<Renderer> renderers = new();
    private float strength, remaining, fade;
    private bool infinite, ending;

    /// <summary> Overlay for duration seconds (negative = until Remove). Returns the running overlay. </summary>
    public static SkillOverlay Apply(GameObject target, string key, Color color, float duration,
        float rimStrength = 1.5f, float rimPower = 2.5f, float scaleStrength = 0f, float scaleDensity = 40f)
    {
        if (target == null) return null;
        foreach (SkillOverlay existing in target.GetComponents<SkillOverlay>())
        {
            if (existing.key != key) continue;
            existing.Refresh(duration);
            return existing;
        }

        if (shader == null) shader = Resources.Load<Shader>("VFX/SkillOverlay");
        if (shader == null) return null;

        SkillOverlay overlay = target.AddComponent<SkillOverlay>();
        overlay.key = key;
        overlay.material = new Material(shader) { name = "VFX_Overlay_" + key };
        overlay.material.SetColor("_Color", color);
        overlay.material.SetFloat("_RimStrength", rimStrength);
        overlay.material.SetFloat("_RimPower", rimPower);
        overlay.material.SetFloat("_ScaleStrength", scaleStrength);
        overlay.material.SetFloat("_ScaleDensity", scaleDensity);
        overlay.material.SetFloat("_Fade", 0f);
        overlay.Refresh(duration);
        overlay.Attach();
        return overlay;
    }

    /// <summary> Fade out and remove the overlay with this key, if any. </summary>
    public static void Remove(GameObject target, string key)
    {
        if (target == null) return;
        foreach (SkillOverlay existing in target.GetComponents<SkillOverlay>())
            if (existing.key == key) existing.ending = true;
    }

    private void Refresh(float duration)
    {
        infinite = duration < 0f;
        remaining = duration;
        ending = false;
    }

    private void Attach()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // Only real meshes: skip particles, trails, lines and skill effects
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (r.GetComponentInParent<SkillVfxRunner>() != null || r.name.StartsWith("VFX_")) continue;

            Material[] mats = r.sharedMaterials;
            Material[] added = new Material[mats.Length + 1];
            mats.CopyTo(added, 0);
            added[mats.Length] = material;
            r.sharedMaterials = added;
            renderers.Add(r);
        }
    }

    private void Detach()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            // Remove only our material, other systems may have changed the rest
            List<Material> mats = new(r.sharedMaterials);
            mats.Remove(material);
            r.sharedMaterials = mats.ToArray();
        }
        renderers.Clear();
    }

    private void Update()
    {
        if (!infinite && !ending)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) ending = true;
        }

        fade = ending
            ? Mathf.MoveTowards(fade, 0f, Time.deltaTime / FadeOut)
            : Mathf.MoveTowards(fade, 1f, Time.deltaTime / FadeIn);
        material.SetFloat("_Fade", fade);

        if (ending && fade <= 0f) Destroy(this);
    }

    private void OnDestroy()
    {
        Detach();
        if (material != null) Destroy(material);
    }
}
