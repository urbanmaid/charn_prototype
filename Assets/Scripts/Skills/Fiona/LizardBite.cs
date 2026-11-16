using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// "Lizard's First Bite": a 3D swamp lizard (Blender model, Resources/VFX/lizard_mesh.bytes,
/// source Tools/Blender/lizard_mesh.py) slithers onto the gun, coils around it hugging the gun's real shape,
/// and bites the muzzle (onBite). It keeps biting while the venom rounds last; Release() melts it into the gun.
/// The model is straight along its spine; every frame its spine is bent along a path that spirals around
/// the gun (CPU curve deform), so it really wraps the weapon.
/// </summary>
public class LizardBite : MonoBehaviour
{
    private const float Length = 0.8f;               // Lizard length in meters
    private const float Slim = 0.8f;                 // Body thickness relative to the model
    private const float SlitherTime = 0.55f, BiteTime = 0.6f, DissolveTime = 0.45f, MaxHold = 60f;
    private const float Step = 0.004f;               // Path sampling step (m)
    private const int Slices = 48;                   // Gun cross-section profile along its length
    private const float Clearance = 0.006f;          // Gap between the lizard's belly and the gun

    // Rest mesh: spine along +y (0 tail tip .. 1 snout), back +z, right +x
    private static Vector3[] restPositions, restNormals;
    private static Vector2[] restUV;
    private static int[][] submeshes;
    private static float[] bellyDepth;               // Per spine bin: how far the belly/legs reach below the spine
    private static Texture2D albedo, normalMap;
    private static Material skinMaterial, eyeMaterial;

    private Transform weapon;
    private Action<Vector3> onBite;
    private Action<List<Vector3>> onDissolve;
    private float time, releaseTime = -1f;
    private bool bitten, dissolving;

    private Mesh mesh;
    private Vector3[] positions, normals;

    // Gun frame and profile in weapon space (weapon position/rotation, no scale)
    private Vector3 center, axis, up, side;
    private float zMin, zMax, zMuzzle, theta0;
    private readonly float[] upMin = new float[Slices], upMax = new float[Slices], sideMin = new float[Slices], sideMax = new float[Slices];

    // Path: uniform arc samples
    private readonly List<Vector3> pathPoints = new(), pathT = new(), pathN = new(), pathB = new();
    private float pathStart;
    private Vector3 biteLocal;
    private float coilStart, coilTurns;              // Solved once, reused every frame

    /// <summary>
    /// Lizard coils around the gun (weapon root, muzzle, mesh bounds) and bites the muzzle. onBite(world point)
    /// at the bite; onDissolve(world points along the coil) when released.
    /// </summary>
    public static LizardBite Spawn(Transform weapon, Transform muzzle, Bounds gunBounds, Action<Vector3> onBite, Action<List<Vector3>> onDissolve)
    {
        if (!Load()) return null;

        GameObject go = new GameObject("VFX_LizardBite");
        LizardBite lizard = go.AddComponent<LizardBite>();
        lizard.weapon = weapon;
        lizard.onBite = onBite;
        lizard.onDissolve = onDissolve;
        try
        {
            lizard.MeasureGun(muzzle, gunBounds);
        }
        catch (Exception e)
        {
            // Never break the skill over the visual: no lizard this time
            Debug.LogWarning("[LizardBite] Could not measure the gun, skipping the lizard: " + e.Message);
            Destroy(go);
            return null;
        }

        lizard.mesh = new Mesh { name = "VFX_LizardMesh" };
        lizard.mesh.MarkDynamic();
        lizard.positions = new Vector3[restPositions.Length];
        lizard.normals = new Vector3[restNormals.Length];
        lizard.mesh.vertices = restPositions;
        lizard.mesh.normals = restNormals;
        lizard.mesh.uv = restUV;
        lizard.mesh.subMeshCount = submeshes.Length;
        for (int i = 0; i < submeshes.Length; i++) lizard.mesh.SetTriangles(submeshes[i], i);
        lizard.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6f);   // Deformed every frame, never cull

        go.AddComponent<MeshFilter>().sharedMesh = lizard.mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { skinMaterial, eyeMaterial };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        lizard.SolveCoil();
        lizard.UpdatePose();
        return lizard;
    }

    /// <summary> Venom rounds are over: let go and melt into the gun. </summary>
    public void Release()
    {
        if (releaseTime < 0f) releaseTime = Mathf.Max(time, BiteTime + 0.1f);
    }

    /// <summary> Build mesh, textures and materials ahead of the first cast. </summary>
    public static void Prewarm() => Load();

    private static Matrix4x4 WeaponSpace(Transform weapon) => Matrix4x4.TRS(weapon.position, weapon.rotation, Vector3.one);

    private void Update()
    {
        time += Time.deltaTime;
        if (weapon == null || !weapon.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        if (releaseTime < 0f && time > MaxHold) Release();
        UpdatePose();

        Matrix4x4 toWorld = WeaponSpace(weapon);
        if (!bitten && time >= BiteTime)
        {
            bitten = true;
            onBite?.Invoke(toWorld.MultiplyPoint3x4(biteLocal));
        }
        if (releaseTime >= 0f && !dissolving && time >= releaseTime)
        {
            dissolving = true;
            List<Vector3> points = new();
            for (int i = 0; i <= 8; i++) points.Add(toWorld.MultiplyPoint3x4(PathAt(Length * i / 8f).p));
            onDissolve?.Invoke(points);
        }
        if (dissolving && time >= releaseTime + DissolveTime) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    #region Gun shape
    // Axis from the gun's middle to the muzzle; profile of the gun's extent (up/side) along that axis,
    // from the mesh vertices when readable, otherwise from each part's bounds.
    private void MeasureGun(Transform muzzle, Bounds gunBounds)
    {
        Matrix4x4 toLocal = WeaponSpace(weapon).inverse;
        Vector3 muzzleLocal = toLocal.MultiplyPoint3x4(muzzle.position);
        center = toLocal.MultiplyPoint3x4(gunBounds.center);
        axis = (muzzleLocal - center).normalized;
        up = Vector3.ProjectOnPlane(toLocal.MultiplyVector(Vector3.up), axis).normalized;
        if (up.sqrMagnitude < 0.5f) up = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
        side = Vector3.Cross(axis, up);
        zMuzzle = Vector3.Dot(muzzleLocal - center, axis);

        List<Vector3> points = new();
        int readable = 0, boxed = 0;
        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>())
        {
            // Only the gun's own meshes (skip particles/trails, e.g. the venom drip on the muzzle)
            if (!r.enabled) continue;
            Mesh m = null;
            if (r is SkinnedMeshRenderer skinned) m = skinned.sharedMesh;
            else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter)) m = filter.sharedMesh;
            if (m == null) continue;
            Matrix4x4 m2l = toLocal * r.localToWorldMatrix;
            if (m.isReadable)
            {
                readable++;
                Vector3[] verts = m.vertices;
                int stride = Mathf.Max(1, verts.Length / 6000);
                for (int i = 0; i < verts.Length; i += stride) points.Add(m2l.MultiplyPoint3x4(verts[i]));
            }
            else
            {
                // Fill the part's box so every slice it covers gets its extent
                boxed++;
                Bounds b = m.bounds;
                for (int x = 0; x <= 4; x++)
                    for (int y = 0; y <= 4; y++)
                        for (int z = 0; z <= 4; z++)
                            points.Add(m2l.MultiplyPoint3x4(b.min + Vector3.Scale(b.size, new Vector3(x, y, z) / 4f)));
            }
        }
        if (points.Count == 0) points.Add(center);

        zMin = float.MaxValue; zMax = float.MinValue;
        foreach (Vector3 p in points)
        {
            float z = Vector3.Dot(p - center, axis);
            zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z);
        }
        zMax = Mathf.Max(zMax, zMuzzle);
        if (zMax - zMin < 0.1f) { zMin -= 0.2f; zMax += 0.05f; }

        for (int i = 0; i < Slices; i++) { upMin[i] = sideMin[i] = float.MaxValue; upMax[i] = sideMax[i] = float.MinValue; }
        foreach (Vector3 p in points)
        {
            Vector3 d = p - center;
            int i = SliceOf(Vector3.Dot(d, axis));
            float u = Vector3.Dot(d, up), s = Vector3.Dot(d, side);
            upMin[i] = Mathf.Min(upMin[i], u); upMax[i] = Mathf.Max(upMax[i], u);
            sideMin[i] = Mathf.Min(sideMin[i], s); sideMax[i] = Mathf.Max(sideMax[i], s);
        }
        // Empty slices borrow from the nearest filled one; then widen by neighbours so the coil never cuts corners
        for (int i = 0; i < Slices; i++)
        {
            if (upMin[i] <= upMax[i]) continue;
            for (int k = 1; k < Slices; k++)
            {
                int j = i - k >= 0 && upMin[i - k] <= upMax[i - k] ? i - k : (i + k < Slices && upMin[i + k] <= upMax[i + k] ? i + k : -1);
                if (j < 0) continue;
                upMin[i] = upMin[j]; upMax[i] = upMax[j]; sideMin[i] = sideMin[j]; sideMax[i] = sideMax[j];
                break;
            }
            if (upMin[i] > upMax[i]) { upMin[i] = sideMin[i] = -0.02f; upMax[i] = sideMax[i] = 0.02f; }
        }
        float[] a = (float[])upMin.Clone(), b2 = (float[])upMax.Clone(), c = (float[])sideMin.Clone(), e = (float[])sideMax.Clone();
        for (int i = 0; i < Slices; i++)
        {
            for (int k = -2; k <= 2; k++)
            {
                int j = Mathf.Clamp(i + k, 0, Slices - 1);
                upMin[i] = Mathf.Min(upMin[i], a[j]); upMax[i] = Mathf.Max(upMax[i], b2[j]);
                sideMin[i] = Mathf.Min(sideMin[i], c[j]); sideMax[i] = Mathf.Max(sideMax[i], e[j]);
            }
        }

        float widest = 0f;
        for (int i = 0; i < Slices; i++) widest = Mathf.Max(widest, upMax[i] - upMin[i], sideMax[i] - sideMin[i]);
        Debug.Log($"[LizardBite] gun profile: meshes readable={readable} boxed={boxed} points={points.Count} length={zMax - zMin:0.00}m muzzleAt={zMuzzle:0.00} widest={widest:0.000}m");

        // Start the coil on the side facing the camera so the approach is visible
        Camera cam = Camera.main;
        Vector3 toCamera = cam != null ? toLocal.MultiplyVector(cam.transform.position - gunBounds.center) : -up;
        Vector3 camRadial = Vector3.ProjectOnPlane(toCamera, axis);
        theta0 = camRadial.sqrMagnitude > 1e-4f ? Mathf.Atan2(Vector3.Dot(camRadial, side), Vector3.Dot(camRadial, up)) : 0f;
    }

    private int SliceOf(float z) => Mathf.Clamp(Mathf.FloorToInt((z - zMin) / (zMax - zMin) * Slices), 0, Slices - 1);

    private void Profile(float z, out Vector2 mid, out Vector2 half)
    {
        float f = Mathf.Clamp((z - zMin) / (zMax - zMin) * Slices - 0.5f, 0f, Slices - 1.001f);
        if (float.IsNaN(f)) f = 0f;
        int i = Mathf.FloorToInt(f);
        float t = f - i;
        int j = Mathf.Min(i + 1, Slices - 1);
        float u0 = Mathf.Lerp(upMin[i], upMin[j], t), u1 = Mathf.Lerp(upMax[i], upMax[j], t);
        float s0 = Mathf.Lerp(sideMin[i], sideMin[j], t), s1 = Mathf.Lerp(sideMax[i], sideMax[j], t);
        mid = new Vector2((u0 + u1) * 0.5f, (s0 + s1) * 0.5f);
        half = new Vector2((u1 - u0) * 0.5f, (s1 - s0) * 0.5f);
    }

    // How far below the spine the body reaches at this arc (so the belly rests on the gun instead of inside it)
    private static float Depth(float arc)
    {
        int i = Mathf.Clamp(Mathf.FloorToInt(arc / Length * bellyDepth.Length), 0, bellyDepth.Length - 1);
        return bellyDepth[i] * Length * Slim;
    }

    // Point on the coil: around the gun's cross-section (ellipse fitted to the slice) at angle theta
    private Vector3 CoilPoint(float z, float theta, float arc, float squeeze)
    {
        Profile(z, out Vector2 mid, out Vector2 half);
        float lift = (Depth(arc) + Clearance) * squeeze;
        float cu = Mathf.Cos(theta), sn = Mathf.Sin(theta);
        return center + axis * z + up * (mid.x + cu * (half.x + lift)) + side * (mid.y + sn * (half.y + lift));
    }

    // Along the top of the gun, from the coil's end to the muzzle
    private Vector3 TopPoint(float z, float arc)
    {
        Profile(z, out Vector2 mid, out Vector2 half);
        return center + axis * z + up * (mid.x + half.x + Depth(arc) + Clearance) + side * mid.y;
    }
    #endregion

    #region Coil path
    private const float HeadRun = 0.14f;             // The head runs along the top of the gun to the muzzle

    // Turns end on top of the gun; the coil's start is moved so the whole path is exactly one lizard long
    private void SolveCoil()
    {
        float zEnd = zMuzzle - HeadRun;
        float frac = Mathf.Repeat(-theta0 / (2f * Mathf.PI), 1f);
        float lowest = Mathf.Max(zMin + 0.03f, zMuzzle - 0.45f);
        coilTurns = frac + 1f;
        coilStart = lowest;
        for (int n = 1; n <= 4; n++)
        {
            float turns = frac + n;
            float atLowest = CoilLength(lowest, zEnd, turns);
            float atHighest = CoilLength(zEnd - 0.06f, zEnd, turns);
            coilTurns = turns;
            if (atLowest < Length - HeadRun) { coilStart = lowest; continue; }   // Too short even at full span: add a turn
            if (atHighest > Length - HeadRun) { coilStart = zEnd - 0.06f; break; }
            float lo = lowest, hi = zEnd - 0.06f;
            for (int k = 0; k < 20; k++)
            {
                float midZ = (lo + hi) * 0.5f;
                if (CoilLength(midZ, zEnd, turns) > Length - HeadRun) lo = midZ; else hi = midZ;
            }
            coilStart = (lo + hi) * 0.5f;
            break;
        }
    }

    private float CoilLength(float zStart, float zEnd, float turns)
    {
        float length = 0f;
        Vector3 prev = CoilPoint(zStart, theta0, 0f, 1f);
        const int samples = 160;
        for (int i = 1; i <= samples; i++)
        {
            float f = i / (float)samples;
            Vector3 p = CoilPoint(Mathf.Lerp(zStart, zEnd, f), theta0 + 2f * Mathf.PI * turns * f, f * (Length - HeadRun), 1f);
            length += Vector3.Distance(prev, p);
            prev = p;
        }
        return length;
    }

    // Straight approach -> coil (belly on the gun, back outward) -> along the top to the muzzle.
    // Sampled at uniform arc length so each spine position maps to a point.
    private void BuildPath(float squeeze)
    {
        float zEnd = zMuzzle - HeadRun;
        float coilArc = Length - HeadRun;

        // Dense coil polyline, then resampled by arc
        List<Vector3> coil = new();
        List<float> coilLen = new();
        const int samples = 400;
        float acc = 0f;
        for (int i = 0; i <= samples; i++)
        {
            float f = i / (float)samples;
            Vector3 p = CoilPoint(Mathf.Lerp(coilStart, zEnd, f), theta0 + 2f * Mathf.PI * coilTurns * f, f * coilArc, squeeze);
            if (i > 0) acc += Vector3.Distance(coil[i - 1], p);
            coil.Add(p);
            coilLen.Add(acc);
        }
        float scale = acc > 1e-4f ? coilArc / acc : 1f;   // Tiny mismatch from squeeze: stretch to fit
        Vector3 startTangent = (coil[1] - coil[0]).normalized;
        biteLocal = TopPoint(zMuzzle + 0.03f, Length);

        pathPoints.Clear(); pathT.Clear(); pathN.Clear(); pathB.Clear();
        pathStart = -Length - 0.05f;
        float pathEnd = Length + 0.08f;
        int k = 0;
        for (float a = pathStart; a <= pathEnd; a += Step)
        {
            Vector3 p;
            if (a < 0f) p = coil[0] + startTangent * a;
            else if (a < coilArc)
            {
                float target = a / scale;
                while (k < samples - 1 && coilLen[k + 1] < target) k++;
                float seg = coilLen[k + 1] - coilLen[k];
                p = Vector3.Lerp(coil[k], coil[k + 1], seg > 1e-6f ? (target - coilLen[k]) / seg : 0f);
            }
            else
            {
                // Head: from the coil's end (on top of the gun) along the top of the barrel, snout just past the muzzle
                float t = (a - coilArc) / HeadRun;
                float z = Mathf.Lerp(zEnd, zMuzzle + 0.03f, t);
                Vector3 top = TopPoint(z, a);
                p = Vector3.Lerp(coil[samples] + axis * (z - zEnd), top, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.3f)));
            }
            pathPoints.Add(p);
        }
        for (int i = 0; i < pathPoints.Count; i++)
        {
            Vector3 t = (pathPoints[Mathf.Min(i + 1, pathPoints.Count - 1)] - pathPoints[Mathf.Max(i - 1, 0)]).normalized;
            // Back faces away from the gun
            float z = Vector3.Dot(pathPoints[i] - center, axis);
            Profile(z, out Vector2 mid, out _);
            Vector3 core = center + axis * z + up * mid.x + side * mid.y;
            Vector3 radial = pathPoints[i] - core;
            Vector3 b = Vector3.ProjectOnPlane(radial.sqrMagnitude > 1e-6f ? radial : up, t).normalized;
            if (b.sqrMagnitude < 0.5f) b = Vector3.ProjectOnPlane(up, t).normalized;
            pathT.Add(t);
            pathB.Add(b);
            pathN.Add(Vector3.Cross(t, b));
        }
    }

    private struct Frame { public Vector3 p, t, n, b; }

    private Frame PathAt(float arc)
    {
        float f = (arc - pathStart) / Step;
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, pathPoints.Count - 2);
        float u = Mathf.Clamp01(f - i);
        return new Frame
        {
            p = Vector3.Lerp(pathPoints[i], pathPoints[i + 1], u),
            t = Vector3.Lerp(pathT[i], pathT[i + 1], u).normalized,
            b = Vector3.Lerp(pathB[i], pathB[i + 1], u).normalized,
            n = Vector3.Lerp(pathN[i], pathN[i + 1], u).normalized,
        };
    }
    #endregion

    #region Pose
    private void UpdatePose()
    {
        transform.SetPositionAndRotation(weapon.position, weapon.rotation);

        // Slither along the path, lunge at the bite, then keep biting (small chomps, slow breathing)
        float progress;
        if (time < SlitherTime)
        {
            float u = time / SlitherTime;
            progress = 1f - (1f - u) * (1f - u);
        }
        else
        {
            float lunge = Mathf.Clamp01(1f - Mathf.Abs(time - BiteTime) / 0.08f);
            float chomp = time > BiteTime + 0.3f ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin((time - BiteTime) * 2.6f)), 12f) : 0f;
            progress = 1f + 0.03f * lunge + 0.008f * chomp;
        }
        float bite = Mathf.Clamp01(1f - Mathf.Abs(time - BiteTime - 0.05f) / 0.25f);
        float breathe = time > BiteTime ? 0.5f + 0.5f * Mathf.Sin((time - BiteTime) * 3.2f) : 0f;
        float squeeze = 1f - 0.12f * bite - 0.04f * breathe;
        BuildPath(squeeze);

        // Grows in at the start, melts into the gun when released
        float thickness = Slim * Mathf.SmoothStep(0.2f, 1f, time / 0.12f);
        if (dissolving) thickness *= 1f - Mathf.SmoothStep(0f, 1f, (time - releaseTime) / DissolveTime);
        float wiggle = 0.02f * (1f - Mathf.Clamp01(time / SlitherTime));

        float shift = Length * (progress - 1f);
        for (int i = 0; i < restPositions.Length; i++)
        {
            Vector3 r = restPositions[i];
            float arc = Mathf.Clamp(r.y, -0.01f, 1.01f) * Length + shift;
            Frame f = PathAt(arc);
            float x = r.x * Length * thickness + wiggle * Mathf.Sin(r.y * 18f - time * 30f);
            float z = r.z * Length * thickness;
            positions[i] = f.p + f.n * x + f.b * z;
            Vector3 n = restNormals[i];
            normals[i] = f.n * n.x + f.t * n.y + f.b * n.z;
        }
        mesh.vertices = positions;
        mesh.normals = normals;
    }
    #endregion

    #region Assets
    private static bool Load()
    {
        if (restPositions != null) return true;
        TextAsset data = Resources.Load<TextAsset>("VFX/lizard_mesh");
        if (data == null) return false;

        using (BinaryReader reader = new BinaryReader(new MemoryStream(data.bytes)))
        {
            int count = reader.ReadInt32();
            int subCount = reader.ReadInt32();
            restPositions = new Vector3[count];
            restNormals = new Vector3[count];
            restUV = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                restPositions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                restNormals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                restUV[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }
            submeshes = new int[subCount][];
            for (int s = 0; s < subCount; s++)
            {
                int n = reader.ReadInt32();
                submeshes[s] = new int[n];
                for (int i = 0; i < n; i++) submeshes[s][i] = reader.ReadInt32();
            }
        }

        // Belly depth per spine bin (includes legs), for resting the belly on the gun
        bellyDepth = new float[40];
        foreach (Vector3 v in restPositions)
        {
            int bin = Mathf.Clamp(Mathf.FloorToInt(v.y * bellyDepth.Length), 0, bellyDepth.Length - 1);
            bellyDepth[bin] = Mathf.Max(bellyDepth[bin], -v.z);
        }

        BuildSkinTextures();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        skinMaterial = new Material(lit) { name = "VFX_LizardSkin" };
        skinMaterial.SetTexture("_BaseMap", albedo);
        skinMaterial.SetColor("_BaseColor", Color.white);
        skinMaterial.SetTexture("_BumpMap", normalMap);
        skinMaterial.SetFloat("_BumpScale", 1f);
        skinMaterial.EnableKeyword("_NORMALMAP");
        skinMaterial.SetFloat("_Smoothness", 0.6f);          // Wet, glossy skin
        skinMaterial.SetFloat("_Metallic", 0f);
        skinMaterial.SetFloat("_Cull", 0f);

        eyeMaterial = new Material(lit) { name = "VFX_LizardEye" };
        Color amber = new Color(1f, 0.62f, 0.08f);
        eyeMaterial.SetColor("_BaseColor", amber);
        eyeMaterial.SetFloat("_Smoothness", 0.95f);
        eyeMaterial.SetColor("_EmissionColor", amber * 2.5f);
        eyeMaterial.EnableKeyword("_EMISSION");
        return true;
    }

    // Scales laid out in rows around (u: belly 0/1, back 0.5) and along (v) the body.
    // Dark swamp back with cross bands, speckled olive flanks, pale belly. Normal map from the scale domes.
    private static void BuildSkinTextures()
    {
        const int w = 128, h = 1024;
        const float around = 22f, along = 190f;
        Color back = new Color(0.1f, 0.16f, 0.045f), flank = new Color(0.24f, 0.34f, 0.09f), belly = new Color(0.55f, 0.56f, 0.3f);
        Color speckle = new Color(0.5f, 0.58f, 0.18f);

        float[] height = new float[w * h];
        Color32[] color = new Color32[w * h];
        for (int py = 0; py < h; py++)
        {
            float v = py / (float)h;
            for (int px = 0; px < w; px++)
            {
                float u = px / (float)w;
                float gu = u * around, gv = v * along;
                float row = Mathf.Floor(gv);
                float cellU = Mathf.Floor(gu + row * 0.5f);
                float cx = Frac(gu + row * 0.5f) - 0.5f, cy = Frac(gv) - 0.5f;
                float dome = Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx * 1.3f + cy * cy * 1.8f) * 1.9f);
                height[py * w + px] = Mathf.Sqrt(dome);

                float dorsal = Mathf.Cos((u - 0.5f) * Mathf.PI * 2f);          // 1 back, -1 belly
                Color c = Color.Lerp(belly, flank, Mathf.SmoothStep(-0.75f, -0.2f, dorsal));
                c = Color.Lerp(c, back, Mathf.SmoothStep(0.15f, 0.8f, dorsal));
                if (dorsal > 0f && Mathf.Sin(v * Mathf.PI * 2f * 16f) > 0.55f) c *= 0.62f;   // Cross bands
                float hash = Frac(Mathf.Sin(cellU * 12.9898f + row * 78.233f) * 43758.5453f);
                if (hash > 0.86f && dorsal > -0.5f && dorsal < 0.7f) c = Color.Lerp(c, speckle, 0.7f);
                c *= 0.72f + 0.38f * dome;                                        // Scale shading
                color[py * w + px] = c;
            }
        }

        albedo = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "VFX_LizardAlbedo", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        albedo.SetPixels32(color);
        albedo.Apply(true);

        Color32[] packed = new Color32[w * h];
        const float strength = 2.2f;
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float dx = height[py * w + (px + 1) % w] - height[py * w + (px - 1 + w) % w];
                float dy = height[Mathf.Min(py + 1, h - 1) * w + px] - height[Mathf.Max(py - 1, 0) * w + px];
                Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                packed[py * w + px] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        normalMap = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "VFX_LizardNormal", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        normalMap.SetPixels32(packed);
        normalMap.Apply(true);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);
    #endregion
}
