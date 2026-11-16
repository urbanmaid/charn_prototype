using UnityEngine;

/// <summary>
/// Reinforced rounds on a character: a flame emblem glows in the chest (drawn just in front of the body toward
/// the camera so it reads as burning inside), the body outline glows red-hot and breathes, embers rise.
/// Ignite() flares it up when the forge crack arrives; End() cools the emblem into embers and removes it.
/// </summary>
public class AmeliaHeatAura : MonoBehaviour
{
    private const float IgniteTime = 0.25f, CoolTime = 0.6f;

    private static Texture2D emblemTexture;
    private static Texture2D EmblemTexture => emblemTexture != null ? emblemTexture : emblemTexture = Resources.Load<Texture2D>("VFX/Amelia/amelia_flame_emblem");

    private Transform target;
    private MeshRenderer emblem;
    private MaterialPropertyBlock block;
    private SkillVfxRunner embers;
    private float time, endTime = -1f;

    /// <summary> Start (or re-ignite) the heat aura on a character. </summary>
    public static AmeliaHeatAura Ignite(Transform target)
    {
        AmeliaHeatAura aura = null;
        foreach (AmeliaHeatAura a in FindObjectsByType<AmeliaHeatAura>(FindObjectsInactive.Exclude))
            if (a.target == target && a.endTime < 0f) aura = a;

        if (aura == null)
        {
            GameObject go = new GameObject("VFX_AmeliaHeat");
            aura = go.AddComponent<AmeliaHeatAura>();
            aura.target = target;
            aura.block = new MaterialPropertyBlock();
            aura.emblem = HawkShape.CreateQuad("Emblem", go.transform, AmeliaVfx.Additive(EmblemTexture));
            aura.embers = AmeliaVfx.SpawnHeatEmbers(target);
            SkillOverlay.Apply(target.gameObject, "reinforced", AmeliaVfx.FlameOrange * 1.2f, -1f, 1.5f, 2.2f);
        }
        aura.time = 0f;                                              // Flare again
        SkillSfx.PlayAttached("amelia_forge_ignite", target, 0.5f);
        AmeliaVfx.SpawnReinforceReceive(target);
        return aura;
    }

    /// <summary> End the heat aura on this character, if any. </summary>
    public static void EndOn(Transform body)
    {
        foreach (AmeliaHeatAura aura in FindObjectsByType<AmeliaHeatAura>(FindObjectsInactive.Exclude))
            if (aura.target == body) aura.End();
    }

    public void End()
    {
        if (endTime >= 0f) return;
        endTime = time;
        if (embers != null) embers.StopSoon(0.6f);
        SkillOverlay.Remove(target != null ? target.gameObject : null, "reinforced");
        if (target != null)
        {
            AmeliaVfx.SpawnHeatCoolDown(target);
            SkillSfx.PlayAttached("amelia_forge_cool", target, 0.5f);
        }
    }

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        time += Time.deltaTime;

        Camera cam = Camera.main;
        Vector3 chest = target.position + Vector3.up * 1.2f;
        Vector3 toCam = cam != null ? (cam.transform.position - chest).normalized : -target.forward;
        Vector3 pos = chest + toCam * 0.28f;                        // Just in front of the chest, facing the camera
        emblem.transform.position = pos;
        emblem.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);

        // Ignite flare -> steady pulse -> cool down
        float alpha, scale;
        if (endTime >= 0f)
        {
            float u = Mathf.Clamp01((time - endTime) / CoolTime);
            alpha = 0.9f * (1f - u);
            scale = 0.45f * (1f + u * 0.3f);
            if (u >= 1f) { Destroy(gameObject); return; }
        }
        else if (time < IgniteTime)
        {
            float u = time / IgniteTime;
            alpha = Mathf.Lerp(1.6f, 0.9f, u);
            scale = Mathf.Lerp(0.8f, 0.45f, 1f - (1f - u) * (1f - u));
        }
        else
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5f);
            alpha = 0.75f + 0.25f * pulse;
            scale = 0.45f * (1f + 0.04f * pulse);
        }
        emblem.transform.localScale = Vector3.one * scale;
        Color c = Color.white * alpha;
        c.a = Mathf.Clamp01(alpha);
        block.SetColor("_BaseColor", c);
        emblem.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (endTime < 0f && target != null) SkillOverlay.Remove(target.gameObject, "reinforced");
        if (embers != null) embers.StopSoon(0.3f);
    }
}
