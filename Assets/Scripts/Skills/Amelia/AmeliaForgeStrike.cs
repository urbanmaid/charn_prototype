using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reinforced Materials visual (forge): a hammer-strike flash with a heat shockwave and a spark fountain at
/// Amelia's feet, then molten cracks split across the ground like lightning out to the skill radius. Cracks
/// run exactly to each ally in range (igniting them when they arrive); the rest run to the edge so the range
/// reads. Hot white-yellow at the growing tip, orange behind, cooling to dark red and fading.
/// Cracks are generated every cast, so they never look the same twice.
/// </summary>
public class AmeliaForgeStrike : MonoBehaviour
{
    private const float GrowTime = 0.85f, CoolStart = 1.25f, CoolEnd = 2.7f, ScorchEnd = 3.8f;
    private const float StepMin = 0.22f, StepMax = 0.4f;
    private const int MinCracks = 7;

    private class Crack
    {
        public List<Vector3> points = new();
        public float startTime, length, width;
        public LineRenderer scorch, core, glow;
        public HealthManager target;       // Ignited when the crack reaches the end
        public bool fired;
    }

    private readonly List<Crack> cracks = new();
    private float time, radius, speed;
    private System.Action<HealthManager> onReach;
    private Light heat;
    private int collapseBeat;
    private static readonly float[] CollapseTimes = { .28f, .62f, .98f };

    private static readonly Color Tip = new Color(1f, 0.92f, 0.65f, 1f);
    private static readonly Color Hot = new Color(1f, 0.45f, 0.1f, 1f);
    private static readonly Color Ember = new Color(0.55f, 0.08f, 0.03f, 1f);

    public static void Play(Transform caster, float radius, IList<HealthManager> receivers, System.Action<HealthManager> onReach)
    {
        Vector3 center = Ground(caster.position, caster.position.y);
        GameObject go = new GameObject("VFX_AmeliaForge");
        go.transform.position = center;
        AmeliaForgeStrike forge = go.AddComponent<AmeliaForgeStrike>();
        forge.radius = radius;
        forge.speed = radius / GrowTime;
        forge.onReach = onReach;
        forge.Build(caster, receivers);
    }

    private void Build(Transform caster, IList<HealthManager> receivers)
    {
        Vector3 center = transform.position;
        AmeliaVfx.SpawnForgeImpact(center);
        CombatCameraFeedback.GroundSlam(center, radius * 2.5f, 1.65f, .9f, Hot);
        SkillSfx.Play("amelia_forge_crack", center, 0.75f, .82f, 0f, 0.05f);

        // One crack straight to each ally (the caster's own ignites at the strike)
        List<float> used = new();
        foreach (HealthManager h in receivers)
        {
            if (h == null) continue;
            Vector3 to = h.transform.position - center;
            to.y = 0f;
            if (to.magnitude < 0.6f)
            {
                onReach?.Invoke(h);
                continue;
            }
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            used.Add(yaw);
            AddCrack(center, yaw, to.magnitude, 0f, h, 0.24f);
        }

        // Fill the rest to the edge, spread evenly between the ally cracks
        int extra = Mathf.Max(MinCracks - used.Count, 3);
        float offset = Random.Range(0f, 360f);
        for (int i = 0; i < extra; i++)
        {
            float yaw = offset + i * 360f / extra + Random.Range(-12f, 12f);
            bool tooClose = false;
            foreach (float u in used) if (Mathf.Abs(Mathf.DeltaAngle(u, yaw)) < 18f) tooClose = true;
            if (tooClose) yaw += 22f;
            AddCrack(center, yaw, radius * Random.Range(0.92f, 1f), Random.Range(0f, 0.05f), null, 0.2f);
        }

        heat = new GameObject("Heat").AddComponent<Light>();
        heat.transform.SetParent(transform, false);
        heat.transform.localPosition = Vector3.up * 0.4f;
        heat.type = LightType.Point;
        heat.color = Hot;
        heat.range = radius * 1.3f;
        heat.shadows = LightShadows.None;
        Update();
    }

    // Jagged path toward yaw, with side branches
    private void AddCrack(Vector3 center, float yaw, float length, float delay, HealthManager target, float width)
    {
        Crack c = NewCrack(delay, width, target);
        Vector3 end = center + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * length;
        Vector3 p = center;
        c.points.Add(Lift(p));
        float heading = yaw;
        while (Vector3.Distance(Flat(p), Flat(end)) > StepMax)
        {
            Vector3 toEnd = Flat(end) - Flat(p);
            float want = Mathf.Atan2(toEnd.x, toEnd.z) * Mathf.Rad2Deg;
            heading = Mathf.LerpAngle(heading, want, 0.55f) + Random.Range(-28f, 28f);
            p += Quaternion.Euler(0f, heading, 0f) * Vector3.forward * Random.Range(StepMin, StepMax);
            c.points.Add(Lift(p));

            // Branch off now and then
            if (Random.value < 0.22f && c.points.Count > 2)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float along = Vector3.Distance(Flat(center), Flat(p));
                AddBranch(p, heading + side * Random.Range(30f, 60f), Random.Range(0.5f, 1.4f), delay + along / speed, width * 0.55f);
            }
        }
        c.points.Add(Lift(end));
        c.length = PathLength(c.points);
        cracks.Add(c);
    }

    private void AddBranch(Vector3 from, float heading, float length, float delay, float width)
    {
        Crack c = NewCrack(delay, width, null);
        Vector3 p = from;
        c.points.Add(Lift(p));
        float travelled = 0f;
        while (travelled < length)
        {
            heading += Random.Range(-30f, 30f);
            float step = Random.Range(StepMin * 0.7f, StepMax * 0.7f);
            p += Quaternion.Euler(0f, heading, 0f) * Vector3.forward * step;
            travelled += step;
            c.points.Add(Lift(p));
        }
        c.length = PathLength(c.points);
        cracks.Add(c);
    }

    private Crack NewCrack(float delay, float width, HealthManager target)
    {
        Crack c = new Crack { startTime = delay, width = width, target = target };
        // Charred band under the molten line: gives contrast on any ground
        c.scorch = Line("Scorch", width * 3.2f, AmeliaVfx.AlphaTwoSided(AmeliaVfx.LineTexture));
        c.glow = Line("Glow", width * 3.5f, AmeliaVfx.AdditiveBright(AmeliaVfx.LineTexture, 1.5f));
        c.core = Line("Core", width, AmeliaVfx.AdditiveBright(AmeliaVfx.LineTexture, 3f));
        return c;
    }

    private LineRenderer Line(string name, float width, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);       // Lie flat on the ground
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.sharedMaterial = material;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
        lr.widthMultiplier = width;
        lr.positionCount = 0;
        return lr;
    }

    private void Update()
    {
        time += Time.deltaTime;
        // Three outward collapse beats, shared by sound, ground eruptions and camera response.
        while (collapseBeat < CollapseTimes.Length && time >= CollapseTimes[collapseBeat])
        {
            float reach = radius * (.3f + collapseBeat * .3f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 120f + collapseBeat * 35f, 0f) * Vector3.forward * reach;
                AmeliaVfx.SpawnForgeCrackEnd(Ground(transform.position + offset, transform.position.y));
            }
            CombatCameraFeedback.GroundSlam(transform.position, radius * 2.5f,
                1.35f - collapseBeat * .2f, 1f, Hot);
            SkillSfx.Play("amelia_forge_crack", transform.position, .48f - collapseBeat * .08f,
                .76f + collapseBeat * .04f);
            collapseBeat++;
        }
        bool alive = time < ScorchEnd;

        foreach (Crack c in cracks)
        {
            float grown = Mathf.Max(0f, (time - c.startTime) * speed);
            int count = ShownPoints(c, grown, out Vector3 tipPoint);
            float cool = Mathf.Clamp01((time - c.startTime - CoolStart) / (CoolEnd - CoolStart));
            float fade = 1f - Mathf.SmoothStep(0.6f, 1f, cool);
            Color baseCol = Color.Lerp(Hot, Ember, cool);
            Color tipCol = grown < c.length ? Tip : Color.Lerp(Hot, Ember, cool);
            Apply(c.core, c, count, tipPoint, baseCol * fade, tipCol * fade);
            Apply(c.glow, c, count, tipPoint, baseCol * 0.5f * fade, tipCol * 0.5f * fade);
            float char01 = 0.8f * (1f - Mathf.Clamp01((time - c.startTime - CoolEnd + 0.4f) / (ScorchEnd - CoolEnd)));
            Color soot = new Color(0.07f, 0.035f, 0.02f, char01 * 0.85f);
            Apply(c.scorch, c, count, tipPoint, soot, soot);

            if (!c.fired && c.target != null && grown >= c.length)
            {
                c.fired = true;
                onReach?.Invoke(c.target);
                AmeliaVfx.SpawnForgeCrackEnd(c.points[c.points.Count - 1]);
            }
        }

        heat.intensity = 1.4f * Mathf.Clamp01(1f - time / 1.2f) + 1f * Mathf.Clamp01(1f - time / 0.15f);
        if (!alive) Destroy(gameObject);
    }

    private static int ShownPoints(Crack c, float grown, out Vector3 tip)
    {
        tip = c.points[0];
        if (grown <= 0f) return 0;
        float acc = 0f;
        for (int i = 1; i < c.points.Count; i++)
        {
            float seg = Vector3.Distance(c.points[i - 1], c.points[i]);
            if (acc + seg >= grown)
            {
                tip = Vector3.Lerp(c.points[i - 1], c.points[i], (grown - acc) / Mathf.Max(seg, 1e-4f));
                return i + 1;
            }
            acc += seg;
        }
        tip = c.points[c.points.Count - 1];
        return c.points.Count;
    }

    private static void Apply(LineRenderer lr, Crack c, int count, Vector3 tip, Color start, Color end)
    {
        lr.positionCount = count;
        for (int i = 0; i < count - 1; i++) lr.SetPosition(i, c.points[i]);
        if (count > 0) lr.SetPosition(count - 1, tip);
        lr.startColor = start;
        lr.endColor = end;
    }

    private static float PathLength(List<Vector3> pts)
    {
        float l = 0f;
        for (int i = 1; i < pts.Count; i++) l += Vector3.Distance(pts[i - 1], pts[i]);
        return l;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private Vector3 Lift(Vector3 p) => Ground(p, transform.position.y) + Vector3.up * 0.04f;

    private static Vector3 Ground(Vector3 p, float fallbackY)
    {
        if (Physics.Raycast(new Vector3(p.x, fallbackY + 1.5f, p.z), Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<HealthManager>() == null)
            return hit.point;
        return new Vector3(p.x, fallbackY, p.z);
    }
}
