using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Self-contained presentation scene; never loads the mission or its enemies.</summary>
public sealed class BridgeAirstrikePreview : MonoBehaviour
{
    private readonly List<GameObject> spans = new();
    private Transform stage, view;
    private Vector3 viewPosition;
    private Quaternion viewRotation;
    private Material ground, water;
    private float started = -100f;
    private bool playing;
    private float collapseShake;

    private void Start()
    {
        var cameraObject = new GameObject("Preview Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        view = cameraObject.transform;
        view.position = new Vector3(-23, 16, -20);
        view.LookAt(new Vector3(2, 3, 30));
        viewPosition = view.position; viewRotation = view.rotation;
        var camera = cameraObject.GetComponent<Camera>();
        camera.farClipPlane = 500; camera.fieldOfView = 65;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.28f, .34f, .4f);
        var sun = new GameObject("Preview Sun", typeof(Light));
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        sun.GetComponent<Light>().type = LightType.Directional;
        sun.GetComponent<Light>().intensity = 1.6f;
        RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        ground = new Material(shader) { color = new Color(.32f, .3f, .22f) };
        water = new Material(shader) { color = new Color(.06f, .22f, .28f) };
        Build();
    }

    private void Build()
    {
        if (stage) Destroy(stage.gameObject);
        spans.Clear(); collapseShake = 0; playing = false; started = -100f;
        stage = new GameObject("Isolated bridge and river").transform;
        Surface("River", new Vector3(0, -2.5f, 35), new Vector3(150, 1, 75), water);
        Surface("Near bank", new Vector3(0, -1, -12), new Vector3(150, 3, 30), ground);
        Surface("Opposite bank", new Vector3(0, -1, 82), new Vector3(150, 3, 30), ground);
        var prefab = Resources.Load<GameObject>("Vandalism/DestructiblePier");
        if (!prefab) { Debug.LogError("Preview bridge resource missing."); return; }
        var root = new GameObject("Connected bridge").transform;
        root.SetParent(stage);
        for (int i = 0; i < 14; i++)
        {
            var span = Instantiate(prefab, root);
            var lod = span.GetComponent<LODGroup>();
            var rotation = Quaternion.Euler(0, -90, 0);
            Vector3 center = lod.localReferencePoint; center.y = 2.7f;
            span.transform.SetPositionAndRotation(new Vector3(0, 1, i * 5.5f) - rotation * center, rotation);
            foreach (Transform child in span.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
            var levels = lod.GetLODs(); lod.enabled = false;
            for (int level = 0; level < levels.Length; level++)
                foreach (var renderer in levels[level].renderers)
                    if (renderer) { renderer.gameObject.SetActive(level == 0); renderer.enabled = level == 0; }
            spans.Add(span);
        }
    }

    private void Surface(string label, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = label; go.transform.SetParent(stage);
        go.transform.position = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) Build();
        if (kb != null && kb.spaceKey.wasPressedThisFrame) Play();
    }

    private void Play()
    {
        if (playing || spans.Count < 3) return;
        playing = true; started = Time.time;
        var airstrike = stage.gameObject.AddComponent<VandalismBridgeAirstrike>();
        airstrike.CollapseImpulse += strength => collapseShake = Mathf.Min(.9f, collapseShake + strength * .4f);
        airstrike.PlayPreview(spans[0].transform.parent, spans);
    }

    private void LateUpdate()
    {
        if (!view) return;
        float age = Time.time - started - (.65f + Mathf.Sqrt(48f / 9.81f));
        float shake = 0;
        if (playing)
            for (int i = 0; i < 3; i++)
            {
                float impactAge = age - i * .28f;
                if (impactAge >= 0 && impactAge < 5) shake += .9f * Mathf.Exp(-impactAge * 1.3f);
            }
        collapseShake *= Mathf.Exp(-Time.deltaTime * 4f);
        shake = Mathf.Min(shake + collapseShake, 1.85f);
        view.position = viewPosition + new Vector3(Mathf.Sin(age * 51), Mathf.Sin(age * 67), 0) * (.22f * shake);
        view.rotation = viewRotation * Quaternion.Euler(Mathf.Sin(age * 61) * shake, 0, Mathf.Sin(age * 43) * shake);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(20, 20, 360, 100), "Bridge Airstrike Preview — Space: strike / R: reset");
        if (GUI.Button(new Rect(35, 60, 155, 40), "Start airstrike")) Play();
        if (GUI.Button(new Rect(205, 60, 155, 40), "Reset bridge")) Build();
    }

    private void OnDestroy() { if (ground) Destroy(ground); if (water) Destroy(water); }
}
