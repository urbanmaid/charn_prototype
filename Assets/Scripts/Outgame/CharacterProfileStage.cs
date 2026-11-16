using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

// Presentation-only copy: gameplay scripts, physics and animation events never run.
public sealed class CharacterProfileStage : MonoBehaviour
{
    GameObject stage, model;
    Camera cameraView;
    RenderTexture target;
    PlayableGraph graph;
    AnimationClip idleCopy, entranceCopy;
    AnimationMixerPlayable mixer;
    float elapsed;
    string personaKey;
    Vector3 origin;
    Animator bodyAnimator;

    public bool Show(string key, RawImage display)
    {
        var data = Resources.Load<PersonaData>("Scriptable Object/Persona/" + key + "/" + key);
        if (!data || data.personaDataVisual == null || !data.personaDataVisual.avatarMesh) return false;
        personaKey = key;
        origin = new Vector3(0, -1000, 0);
        stage = new GameObject("Profile presentation stage");
        stage.SetActive(false);
        stage.transform.position = origin;
        model = Instantiate(data.personaDataVisual.avatarMesh, stage.transform);
        model.transform.localPosition = Vector3.zero;
        foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) { r.isKinematic = true; r.detectCollisions = false; }
        foreach (var a in model.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; }
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var animator = model.GetComponentInChildren<Animator>(true);
        if (!animator || !animator.avatar || !animator.avatar.isHuman) return false;
        bodyAnimator = animator;
        animator.runtimeAnimatorController = null;
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var idle = Resources.Load<AnimationClip>("CharacterProfiles/Motions/Idle");
        var entrance = Resources.Load<AnimationClip>("CharacterProfiles/Motions/Walk");
        if (!idle || !entrance) return false;
        idleCopy = Instantiate(idle); idleCopy.events = new AnimationEvent[0];
        entranceCopy = Instantiate(entrance); entranceCopy.events = new AnimationEvent[0];
        graph = PlayableGraph.Create("Profile presentation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(AnimationClipPlayable.Create(graph, entranceCopy), 0, mixer, 0);
        graph.Connect(AnimationClipPlayable.Create(graph, idleCopy), 0, mixer, 1);
        var output = AnimationPlayableOutput.Create(graph, "Character", animator); output.SetSourcePlayable(mixer);
        stage.SetActive(true);
        graph.Play(); graph.Evaluate(0);
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(origin + Vector3.up, Vector3.one);
        if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); }
        float height = Mathf.Max(.5f, bounds.size.y);
        var cameraObject = new GameObject("Profile camera"); cameraObject.transform.SetParent(stage.transform);
        cameraView = cameraObject.AddComponent<Camera>();
        cameraView.enabled = false; cameraView.cullingMask = 1 << 31;
        cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.94f, .96f, .99f);
        cameraView.orthographic = true;
        cameraView.orthographicSize = height * .6f;
        cameraView.fieldOfView = 30; cameraView.nearClipPlane = .05f; cameraView.farClipPlane = height * 12;
        cameraView.transform.position = bounds.center + new Vector3(0, height * .04f, height * 2.5f);
        cameraView.transform.LookAt(bounds.center);
        target = new RenderTexture(750, 760, 24); target.Create();
        cameraView.targetTexture = target; display.texture = target;
        var lamp = new GameObject("Portrait key light").AddComponent<Light>();
        lamp.transform.SetParent(stage.transform); lamp.transform.rotation = Quaternion.Euler(35, -35, 0);
        lamp.type = LightType.Directional; lamp.intensity = 1.3f; lamp.cullingMask = 1 << 31;
        return true;
    }
    void LateUpdate()
    {
        if (!cameraView || !model) return;
        elapsed += Time.unscaledDeltaTime;
        float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .65f) / .55f));
        mixer.SetInputWeight(0, 1 - blend); mixer.SetInputWeight(1, blend);
        // Distinct entrances: deliberate quarter-turn, brisk frontal approach, gentle turn.
        float angle = personaKey == "Amelia" ? -28 : personaKey == "Fiona" ? 22 : -12;
        float duration = personaKey == "Fiona" ? .85f : 1.3f;
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
        model.transform.localRotation = Quaternion.Euler(0, Mathf.Lerp(angle, 0, t), 0);
        model.transform.localPosition = new Vector3(0, 0, Mathf.Lerp(-.65f, 0, t));
        FitCameraToBody();
        cameraView.Render();
    }
    void FitCameraToBody()
    {
        if (!cameraView || !model || !bodyAnimator) return;
        // Skin bounds can be stale before the first rendered animation frame.
        // Fit to the current humanoid bones so retargeted entrances cannot crop the torso.
        var head = bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
        var leftFoot = bodyAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var rightFoot = bodyAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (head && leftFoot && rightFoot)
        {
            float bottom = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - .15f;
            float top = head.position.y + .3f;
            float height = Mathf.Max(.5f, top - bottom);
            Vector3 center = new Vector3(model.transform.position.x, (bottom + top) * .5f, model.transform.position.z);
            cameraView.orthographicSize = height * .62f;
            cameraView.transform.position = center + Vector3.forward * (height * 3f);
            cameraView.transform.LookAt(center);
            cameraView.farClipPlane = height * 12f;
        }
    }
    void OnDisable() { if (graph.IsValid()) graph.Stop(); if (stage) stage.SetActive(false); }
    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (stage) Destroy(stage);
        if (target) { target.Release(); Destroy(target); }
        if (idleCopy) Destroy(idleCopy);
        if (entranceCopy) Destroy(entranceCopy);
    }
}

