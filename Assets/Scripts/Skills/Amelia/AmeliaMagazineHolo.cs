using UnityEngine;

/// <summary>
/// Supply received: a hologram magazine flickers in beside the ally's chest, hovers, slides into the gun
/// with a snap flash, while a scan ring sweeps up the body.
/// </summary>
public class AmeliaMagazineHolo : MonoBehaviour
{
    private const float Appear = 0.15f, Hover = 0.25f, Slide = 0.15f, ScanTime = 0.35f;

    private Transform target;
    private float time;
    private bool snapped;
    private MeshRenderer mag, scanRing;
    private MaterialPropertyBlock block;
    private Vector3 side;

    public static void Play(Transform target)
    {
        GameObject go = new GameObject("VFX_AmeliaMagazine");
        AmeliaMagazineHolo holo = go.AddComponent<AmeliaMagazineHolo>();
        holo.target = target;
        holo.block = new MaterialPropertyBlock();
        holo.mag = HawkShape.CreateQuad("Magazine", go.transform, AmeliaVfx.Additive(AmeliaSupplyScan.MagazineTexture));
        holo.scanRing = HawkShape.CreateQuad("Scan", null, AmeliaVfx.Additive(AmeliaSupplyScan.RingTexture));
        holo.scanRing.transform.SetParent(go.transform, true);
        // Magazine appears on the camera-facing side of the chest
        Camera cam = Camera.main;
        holo.side = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : target.right;
        SkillSfx.PlayAttached("amelia_supply_mag", target, 0.6f);   // The clunk lands as the magazine snaps in
        holo.Update();
    }

    private void Update()
    {
        if (target == null) { Destroy(gameObject); return; }
        time += Time.deltaTime;

        Vector3 chest = target.position + Vector3.up * 1.15f;
        Vector3 start = chest + side * 0.45f + Vector3.up * 0.1f;
        Vector3 gun = chest + target.forward * 0.25f - Vector3.up * 0.15f;

        // Magazine: flicker in, hover, slide into the gun, flash
        float alpha, scale;
        Vector3 pos;
        if (time < Appear)
        {
            float u = time / Appear;
            alpha = u * (Random.value < 0.3f ? 0.4f : 1f);        // Hologram flicker
            scale = Mathf.Lerp(0.6f, 1f, u);
            pos = start;
        }
        else if (time < Appear + Hover)
        {
            alpha = 1f;
            scale = 1f;
            pos = start + Vector3.up * Mathf.Sin((time - Appear) * 10f) * 0.01f;
        }
        else
        {
            float u = Mathf.Clamp01((time - Appear - Hover) / Slide);
            alpha = 1f - u * 0.6f;
            scale = Mathf.Lerp(1f, 0.55f, u * u);
            pos = Vector3.Lerp(start, gun, u * u);
            if (u >= 1f && !snapped)
            {
                snapped = true;
                AmeliaVfx.SpawnSupplyReceive(target);          // Snap flash at the chest
            }
        }
        Camera cam = Camera.main;
        mag.transform.position = pos;
        if (cam != null) mag.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
        mag.transform.localScale = new Vector3(0.22f, 0.44f, 1f) * scale;
        Tint(mag, snapped ? 0f : alpha);

        // Scan ring rising up the body
        float s = Mathf.Clamp01(time / ScanTime);
        scanRing.transform.position = target.position + Vector3.up * Mathf.Lerp(0.05f, 1.9f, s);
        scanRing.transform.rotation = Quaternion.Euler(90f, time * 120f, 0f);
        scanRing.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        Tint(scanRing, Mathf.Sin(s * Mathf.PI) * 0.9f);

        if (snapped && time > ScanTime) Destroy(gameObject);
    }

    private void Tint(MeshRenderer r, float alpha)
    {
        Color c = AmeliaVfx.SupplyWhite * alpha;
        c.a = alpha;
        block.SetColor("_BaseColor", c);
        r.SetPropertyBlock(block);
    }
}
