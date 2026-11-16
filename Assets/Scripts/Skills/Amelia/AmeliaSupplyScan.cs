using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Supply Boost visual (tactical hologram): a scan ring with ticks and orange brackets sweeps out to the skill
/// radius over a hex-grid wave, ">>" chevrons race out along the ground, then the ring holds at the radius,
/// turning slowly, so the range reads. Each ally the wave reaches gets a hologram magazine that slides in,
/// and a scan ring sweeps up their body. Textures: Resources/VFX/Amelia (source Tools/Vfx/amelia_textures.py).
/// </summary>
public class AmeliaSupplyScan : MonoBehaviour
{
    private const float SweepTime = 0.4f, HoldUntil = 1.1f, FadeUntil = 1.5f;
    private const int Chevrons = 6;

    private static Texture2D ringTex, hexTex, chevronTex, magazineTex;
    public static Texture2D RingTexture => ringTex != null ? ringTex : ringTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_scan_ring");
    public static Texture2D HexTexture => hexTex != null ? hexTex : hexTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_hex_disc");
    public static Texture2D ChevronTexture => chevronTex != null ? chevronTex : chevronTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_chevron");
    public static Texture2D MagazineTexture => magazineTex != null ? magazineTex : magazineTex = Resources.Load<Texture2D>("VFX/Amelia/amelia_magazine");

    private float radius, time;
    private MeshRenderer ring, hex;
    private readonly List<MeshRenderer> chevrons = new();
    private readonly List<(Transform target, float at)> pending = new();
    private MaterialPropertyBlock block;
    private Light glow;
    private float spin;

    /// <summary> Wave from the caster's feet to radius; receivers light up as the wave reaches them. </summary>
    public static void Play(Transform caster, float radius, IEnumerable<HealthManager> receivers)
    {
        Vector3 ground = caster.position;
        if (Physics.Raycast(caster.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            ground = hit.point;

        GameObject go = new GameObject("VFX_AmeliaSupplyScan");
        go.transform.position = ground + Vector3.up * 0.03f;
        AmeliaSupplyScan scan = go.AddComponent<AmeliaSupplyScan>();
        scan.radius = radius;
        scan.block = new MaterialPropertyBlock();
        scan.spin = Random.Range(0f, 360f);
        scan.Build();

        foreach (HealthManager h in receivers)
        {
            if (h == null) continue;
            Vector3 d = h.transform.position - ground;
            float reach = Mathf.Clamp01(new Vector2(d.x, d.z).magnitude / radius);
            scan.pending.Add((h.transform, SweepTime * (1f - (1f - reach) * (1f - reach))));
        }
    }

    private void Build()
    {
        hex = Flat("Hex", HexTexture);
        ring = Flat("Ring", RingTexture);
        for (int i = 0; i < Chevrons; i++)
            chevrons.Add(Flat("Chevron", ChevronTexture));

        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.up * 0.6f;
        glow.type = LightType.Point;
        glow.color = AmeliaVfx.SupplyWhite;
        glow.range = radius * 1.2f;
        glow.shadows = LightShadows.None;
        Update();
    }

    private MeshRenderer Flat(string name, Texture2D texture)
    {
        MeshRenderer r = HawkShape.CreateQuad(name, transform, AmeliaVfx.Additive(texture));
        r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return r;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float sweep = Mathf.Clamp01(time / SweepTime);
        float eased = 1f - (1f - sweep) * (1f - sweep);           // Fast out, settles at the edge
        float d = radius * 2f * eased;

        // Hex wave follows the ring and dies right after the sweep
        hex.transform.localScale = new Vector3(d, d, 1f);
        Tint(hex, AmeliaVfx.SupplyWhite, (1f - Mathf.Clamp01((time - SweepTime * 0.6f) / 0.45f)) * 0.9f);

        // Ring: sweeps out, holds at the radius turning slowly, then fades
        spin += Time.deltaTime * (sweep < 1f ? 60f : 18f);
        ring.transform.localScale = new Vector3(d / 0.975f, d / 0.975f, 1f);   // Texture edge sits at 97.5%
        ring.transform.localRotation = Quaternion.Euler(90f, spin, 0f);
        float ringAlpha = time < HoldUntil ? 1f : 1f - Mathf.Clamp01((time - HoldUntil) / (FadeUntil - HoldUntil));
        float flash = 1f + 0.6f * Mathf.Clamp01(1f - Mathf.Abs(time - SweepTime) / 0.12f);   // Brighter as it lands
        Tint(ring, AmeliaVfx.SupplyWhite, ringAlpha * flash);

        // Chevrons race out along the ground and fade at the edge
        for (int i = 0; i < chevrons.Count; i++)
        {
            float t = Mathf.Clamp01((time - 0.05f - (i % 2) * 0.06f) / 0.45f);
            float yaw = i * 360f / Chevrons + 30f;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float dist = Mathf.Lerp(0.8f, radius * 0.9f, 1f - (1f - t) * (1f - t));
            Transform c = chevrons[i].transform;
            c.localPosition = dir * dist + Vector3.up * 0.01f;
            c.localRotation = Quaternion.LookRotation(Vector3.down, dir) * Quaternion.Euler(0f, 0f, 90f);   // ">>" points outward
            c.localScale = new Vector3(0.9f, 0.45f, 1f);
            float a = t <= 0f ? 0f : Mathf.Sin(t * Mathf.PI) * 1.2f;
            Tint(chevrons[i], AmeliaVfx.SupplyWhite, Mathf.Clamp01(a));
        }

        glow.intensity = 1.1f * ringAlpha * Mathf.Lerp(1.2f, 0.5f, sweep);

        // Allies light up when the wave reaches them
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (time < pending[i].at) continue;
            if (pending[i].target != null) AmeliaMagazineHolo.Play(pending[i].target);
            pending.RemoveAt(i);
        }

        if (time >= FadeUntil && pending.Count == 0) Destroy(gameObject);
    }

    private void Tint(MeshRenderer r, Color c, float alpha)
    {
        alpha = Mathf.Max(0f, alpha);
        block.SetColor("_BaseColor", new Color(c.r * alpha, c.g * alpha, c.b * alpha, alpha));   // Additive: scale color too
        r.SetPropertyBlock(block);
    }
}
