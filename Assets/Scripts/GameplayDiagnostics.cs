using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Observation only: never changes input, pause state or scene loading.
public sealed class GameplayDiagnostics : MonoBehaviour
{
    private static StreamWriter writer;
    private static readonly object gate = new object();
    private float nextSample, nextHeartbeat;
    private string lastState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        lock (gate)
        {
            writer?.Dispose(); writer = null;
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "gameplay-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
                Debug.Log("[GameDiag] Diagnostic log: " + path);
            }
            catch (Exception e) { Debug.LogWarning("[GameDiag] File logging unavailable: " + e.Message); }
        }
        var go = new GameObject("Gameplay diagnostics");
        DontDestroyOnLoad(go); go.AddComponent<GameplayDiagnostics>();
        Step("SESSION BEGIN");
    }

    public static void Step(string message)
    {
        string line = "[GameDiag] " + DateTime.UtcNow.ToString("O") + " " + message;
        lock (gate)
        {
            try { writer?.WriteLine(line); } catch (IOException) { }
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += Loaded;
        Application.logMessageReceived += Error;
    }
    private void Loaded(Scene scene, LoadSceneMode mode) => Step("SCENE LOADED " + scene.path + " mode=" + mode);
    private void Error(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert || type == LogType.Warning)
            Step(type + " " + message + "\n" + stack);
    }
    private void OnApplicationFocus(bool focused) => Step("APPLICATION FOCUS " + focused);
    private void OnApplicationPause(bool paused) => Step("APPLICATION PAUSE " + paused);

    private void Update()
    {
        bool manual = Keyboard.current?.f12Key.wasPressedThisFrame ?? false;
        if (!manual && Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + .5f;
        var player = PlayerControlResolver.Resolve(null);
        var state = new StringBuilder();
        state.Append("scene=").Append(SceneManager.GetActiveScene().name)
            .Append(" timeScale=").Append(Time.timeScale).Append(" focused=").Append(Application.isFocused)
            .Append(" cursor=").Append(Cursor.lockState).Append(" ").Append(GameplayCursor.DiagnosticState());
        if (player)
        {
            state.Append(" player=").Append(player.name).Append("#").Append(player.GetInstanceID())
                .Append(" active=").Append(player.gameObject.activeInHierarchy);
            foreach (var component in player.GetComponents<Behaviour>())
                state.Append(" ").Append(component.GetType().Name).Append("=").Append(component.enabled);
            var input = player.GetComponent<PlayerInput>();
            if (input)
            {
                state.Append(" inputActive=").Append(input.inputIsActive).Append(" map=").Append(input.currentActionMap?.name)
                    .Append(" scheme=").Append(input.currentControlScheme);
                foreach (var device in input.devices) state.Append(" device=").Append(device.displayName).Append("#").Append(device.deviceId);
                if (input.actions != null)
                    foreach (var action in input.actions) state.Append(" action:").Append(action.name).Append("=").Append(action.enabled);
            }
        }
        else state.Append(" player=NONE");
        string current = state.ToString();
        bool changed = current != lastState;
        if (manual || changed || Time.unscaledTime >= nextHeartbeat)
        {
            Step((manual ? "F12 SNAPSHOT " : changed ? "STATE CHANGE " : "HEARTBEAT ") + current);
            var kb = Keyboard.current;
            Step("INPUT frame=" + Time.frameCount + " WASD=" + (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed))
                + " LMB=" + (Mouse.current?.leftButton.isPressed ?? false)
                + " position=" + (player ? player.transform.position.ToString("F3") : "none"));
            var input = player ? player.GetComponent<PlayerInput>() : null;
            if (input && input.actions != null)
                foreach (var action in input.actions)
                    if (action.name == "Move" || action.name == "Shoot" || action.name == "Fire" || action.name == "Attack" || action.name == "Look" || action.name == "Aim")
                        Step("ACTION " + action.name + " phase=" + action.phase + " value=" + action.ReadValueAsObject());
            if (player)
            {
                var shooting = player.GetComponent<ShootBehaviour>();
                if (shooting) Step("SHOT " + shooting.DiagnosticState());
                Step("MOVEMENT tempLock=" + player.GetTempLockStatus());
                if (player.GetCamScript) Step("CAMERA " + player.GetCamScript.DiagnosticState());
            }
            if (manual)
            {
                var system = UnityEngine.EventSystems.EventSystem.current;
                Step("UI EventSystem=" + (system ? system.name + " active=" + system.isActiveAndEnabled : "none")
                    + " module=" + (system && system.currentInputModule ? system.currentInputModule.GetType().Name : "none")
                    + " selected=" + (system && system.currentSelectedGameObject ? system.currentSelectedGameObject.name : "none"));
                foreach (var group in FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None))
                    Step("UI GROUP " + group.name + " alpha=" + group.alpha + " interactable=" + group.interactable + " blocks=" + group.blocksRaycasts);
            }
            lastState = current; nextHeartbeat = Time.unscaledTime + 5f;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= Loaded;
        Application.logMessageReceived -= Error;
    }
    private void OnApplicationQuit()
    {
        Step("SESSION END");
        lock (gate) { writer?.Dispose(); writer = null; }
    }
}
