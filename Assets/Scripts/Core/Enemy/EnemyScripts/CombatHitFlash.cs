using System.Collections.Generic;
using UnityEngine;

// Brief, renderer-only feedback for confirmed hits. It does not alter enemy AI or damage.
public sealed class CombatHitFlash : MonoBehaviour
{
    private sealed class Slot
    {
        public Renderer renderer;
        public int materialIndex;
        public bool hasBaseColor;
        public bool hasColor;
        public Color baseColor;
        public MaterialPropertyBlock block;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private float remaining;
    private float duration;
    private float intensity;
    private Color tint;
    private Color baseTint;

    public static void Pulse(Animator animator, Color baseTint, bool headshot)
    {
        if (!animator) return;
        var flash = animator.GetComponent<CombatHitFlash>();
        if (!flash) flash = animator.gameObject.AddComponent<CombatHitFlash>();
        flash.SetPulse(baseTint, headshot);
    }

    private void Awake()
    {
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!material || !material.name.StartsWith("char_enemy", System.StringComparison.Ordinal)) continue;
                bool hasBase = material.HasProperty("_BaseColor");
                bool hasColor = material.HasProperty("_Color");
                if (!hasBase && !hasColor) continue;
                slots.Add(new Slot
                {
                    renderer = renderer,
                    materialIndex = i,
                    hasBaseColor = hasBase,
                    hasColor = hasColor,
                    block = new MaterialPropertyBlock()
                });
            }
        }
    }

    private void SetPulse(Color baseTint, bool headshot)
    {
        this.baseTint = baseTint;
        tint = headshot ? new Color(1f, 0.82f, 0.62f, 1f) : Color.white;
        duration = headshot ? 0.15f : 0.09f;
        intensity = headshot ? 0.95f : 0.68f;
        remaining = Mathf.Max(remaining, duration);
        Apply(baseTint, intensity);
    }

    private void Update()
    {
        if (remaining <= 0f) return;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        float envelope = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / duration) * intensity;
        Apply(baseTint, envelope);
    }

    private void Apply(Color baseTint, float amount)
    {
        Color result = Color.Lerp(baseTint, tint, amount);
        foreach (var slot in slots)
        {
            if (!slot.renderer) continue;
            slot.block.Clear();
            slot.renderer.GetPropertyBlock(slot.block, slot.materialIndex);
            if (slot.hasBaseColor) slot.block.SetColor("_BaseColor", result);
            if (slot.hasColor) slot.block.SetColor("_Color", result);
            slot.renderer.SetPropertyBlock(slot.block, slot.materialIndex);
        }
    }

    private void OnDisable() => Apply(baseTint, 0f);
}
