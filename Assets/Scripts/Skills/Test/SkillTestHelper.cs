using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Test-only helper for skill scenes. Hotkeys to fill game changer point, reset cooldowns, damage/kill allies
/// and hit the player (to test shields), with on-screen guide. Put in a test scene only.
/// </summary>
public class SkillTestHelper : MonoBehaviour
{
    [SerializeField] private bool infiniteGameChanger = true;
    [SerializeField] private bool showGuide = true;
    [SerializeField] private float testDamage = 40f;
    [SerializeField] private bool spawnTrainingDummy = true;
    private StationaryTrainingDummy trainingDummy;
    private bool attemptedDummySpawn;

    [Header("Player hit test (shields)")]
    [SerializeField] private float playerHitDamage = 30f;
    [SerializeField] private float playerHeavyHitDamage = 150f;
    [SerializeField] private float incomingFireDamage = 15f;
    [SerializeField] private float incomingFireInterval = 0.35f;
    [SerializeField] private bool keepPlayerAlive = true;

    private SkillBehavior playerSkill;
    private PlayerHealth playerHealth;
    private bool incomingFire;
    private float nextFireTime;
    private GUIStyle boxStyle;
    private PauseUIController pauseUI;

    private void Awake()
    {
        // Sanitize old test scenes too; do not depend on rebuilding their binary assets.
        var scene = gameObject.scene;
        var actors = new List<AllyStateController>();
        foreach (var ally in FindObjectsByType<AllyStateController>(FindObjectsInactive.Include))
            if (ally.gameObject.scene == scene && ally.GetComponent<BasicBehaviour>() == null) actors.Add(ally);
        actors.Sort((a, b) =>
        {
            int active = b.gameObject.activeInHierarchy.CompareTo(a.gameObject.activeInHierarchy);
            return active != 0 ? active : string.CompareOrdinal(a.name, b.name);
        });
        string selectedPersona = scene.name.Replace("SkillTest", "");
        var personaKeys = new List<string> { "Amelia", "Fiona", "Lucia" };
        if (!personaKeys.Remove(selectedPersona))
        {
            Debug.LogError("[SkillTest] Unknown training character: " + selectedPersona, this);
            return;
        }
        // Three characters total: the selected player and the other two companions.
        while (actors.Count < personaKeys.Count)
        {
            var prefab = Resources.Load<GameObject>("SkillTest/Ally");
            if (prefab == null) { Debug.LogError("[SkillTest] Missing ally prefab.", this); break; }
            var instance = Instantiate(prefab);
            instance.tag = "PA";
            var ally = instance.GetComponent<AllyStateController>();
            if (ally == null) { Destroy(instance); break; }
            var playerActor = FindAnyObjectByType<BasicBehaviour>();
            if (playerActor != null)
                instance.transform.position = playerActor.transform.position +
                    Quaternion.Euler(0, 150 + actors.Count * 30, 0) * playerActor.transform.forward * 2.5f;
            actors.Add(ally);
        }
        for (int i = 0; i < actors.Count; i++)
        {
            var ally = actors[i];
            if (i >= personaKeys.Count)
            {
                ally.gameObject.SetActive(false);
                Destroy(ally.gameObject);
                continue;
            }
            ally.tag = "PA";
            ally.EnablePersonaPoseCorrection();
            var persona = ally.GetComponent<PersonaBehavior>();
            if (persona != null)
            {
                persona.EnablePersonaVisuals();
                string key = personaKeys[i];
                var data = Resources.Load<PersonaData>("Scriptable Object/Persona/" + key + "/" + key);
                persona.InsertPersona(data);
                ally.name = "Ally_" + key;
            }
            ally.gameObject.SetActive(true);
        }
        // Legacy allies outside the FSM roster must not inflate the test squad.
        foreach (var health in FindObjectsByType<AllyHealthManager>(FindObjectsInactive.Include))
            if (health.gameObject.scene == scene && health.GetComponent<AllyStateController>() == null &&
                health.GetComponent<BasicBehaviour>() == null)
            {
                health.gameObject.SetActive(false);
                Destroy(health.gameObject);
            }
        var player = FindAnyObjectByType<BasicBehaviour>();
        if (player != null) player.GetComponent<PersonaBehavior>()?.EnablePersonaVisuals();
    }

    private void Start()
    {
        // Reuse the stage pause screen, including its Escape frame guard and cursor restoration.
        pauseUI = FindAnyObjectByType<PauseUIController>();
        GameObject spawnedPauseScreen = null;
        if (pauseUI == null)
        {
            var prefab = Resources.Load<GameObject>("SkillTest/PauseUI");
            if (prefab == null)
            {
                Debug.LogError("[SkillTest] Missing Resources/SkillTest/PauseUI prefab.", this);
                return;
            }
            spawnedPauseScreen = Instantiate(prefab);
            spawnedPauseScreen.name = "Skill Test Pause UI";
            spawnedPauseScreen.SetActive(true);
            pauseUI = spawnedPauseScreen.GetComponentInChildren<PauseUIController>(true);
            if (pauseUI != null) pauseUI.gameObject.SetActive(true);
        }

        // Older test scenes may already contain the controller. Configure that instance too:
        // otherwise it keeps the stage CanvasGroup and the pause call can hide gameplay with no
        // visible overlay in the standalone training scene.
        if (pauseUI != null)
        {
            Canvas pauseCanvas = spawnedPauseScreen != null
                ? spawnedPauseScreen.GetComponentInChildren<Canvas>(true)
                : pauseUI.GetComponentInChildren<Canvas>(true);
            if (pauseCanvas == null)
                pauseCanvas = pauseUI.transform.root.GetComponentInChildren<Canvas>(true);
            if (pauseCanvas != null)
            {
                pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                pauseCanvas.sortingOrder = 200;
                pauseUI.ConfigureForSkillTest(pauseCanvas);
            }
            else
                Debug.LogError("[SkillTest] Pause UI has no Canvas to display.", pauseUI);
        }

        // Some standalone test scenes have no UI input system yet.
        var events = EventSystem.current;
        if (events == null)
            events = new GameObject("Skill Test EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        if (events.GetComponent<BaseInputModule>() == null)
            events.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void Update()
    {
        if (pauseUI != null && pauseUI.IsPaused) return;
        if (playerSkill == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerSkill = player.GetComponent<SkillBehavior>();
        }
        if (playerHealth == null && playerSkill != null) playerHealth = playerSkill.GetComponent<PlayerHealth>();
        if (spawnTrainingDummy && !attemptedDummySpawn && playerSkill != null)
        {
            attemptedDummySpawn = true;
            PlaceTrainingDummy();
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) FillGameChanger();
        if (kb.f2Key.wasPressedThisFrame) ResetCooldowns();
        if (kb.f3Key.wasPressedThisFrame) KillNearestAlly();
        if (kb.f4Key.wasPressedThisFrame) DamageAllAllies();
        if (kb.f5Key.wasPressedThisFrame) infiniteGameChanger = !infiniteGameChanger;
        if (kb.f6Key.wasPressedThisFrame) showGuide = !showGuide;
        if (kb.f7Key.wasPressedThisFrame) HitPlayer(playerHitDamage);
        if (kb.f9Key.wasPressedThisFrame) HitPlayer(playerHeavyHitDamage);
        if (kb.f10Key.wasPressedThisFrame) incomingFire = !incomingFire;
        if (kb.f8Key.wasPressedThisFrame) ReloadScene();
        if (kb.f11Key.wasPressedThisFrame) PlaceTrainingDummy();

        if (infiniteGameChanger) FillGameChanger();

        // Simulated enemy fire: steady small hits on the player
        if (incomingFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + incomingFireInterval;
            HitPlayer(incomingFireDamage);
        }
    }

    private void PlaceTrainingDummy()
    {
        if (playerSkill == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(playerSkill.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        Vector3 origin = playerSkill.transform.position + Vector3.up;
        float distance = 8f;
        foreach (RaycastHit obstacle in Physics.RaycastAll(origin, forward, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (obstacle.transform.IsChildOf(playerSkill.transform) ||
                (trainingDummy != null && obstacle.transform.IsChildOf(trainingDummy.transform))) continue;
            distance = Mathf.Min(distance, Mathf.Max(1.5f, obstacle.distance - 1f));
        }
        Vector3 point = playerSkill.transform.position + forward * distance;
        float nearestGround = float.PositiveInfinity;
        foreach (RaycastHit ground in Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ground.collider.GetComponentInParent<HealthManager>() != null ||
                ground.normal.y < .6f || ground.distance >= nearestGround) continue;
            nearestGround = ground.distance;
            point = ground.point;
        }
        if (float.IsPositiveInfinity(nearestGround)) return;
        Quaternion rotation = Quaternion.LookRotation(-forward);
        if (trainingDummy == null) trainingDummy = StationaryTrainingDummy.Create(point, rotation);
        else trainingDummy.transform.SetPositionAndRotation(point, rotation);
    }

    // Hit the player from the front (goes through shields first, like a real shot)
    private void HitPlayer(float damage)
    {
        if (playerHealth == null || playerHealth.dead) return;
        Transform t = playerHealth.transform;
        Vector3 from = t.position + t.forward * 8f + Vector3.up * 1.4f;
        Vector3 hitPoint = t.position + Vector3.up * 1.2f;
        playerHealth.TakeDamage(hitPoint, (hitPoint - from).normalized, damage, null, null, HealthManager.DamageType.Normal);

        // Testing shields shouldn't end in death: refill health, keep the shield as it is
        if (keepPlayerAlive && !playerHealth.dead && playerHealth.health < playerHealth.TotalHealth * 0.35f)
            playerHealth.RestoreRuntimeHealth(playerHealth.TotalHealth, playerHealth.shieldHealth, playerHealth.TotalShieldHealth);
    }

    private void FillGameChanger()
    {
        if (playerSkill != null) playerSkill.gameChangerPoint = 999999;
    }

    private void ResetCooldowns()
    {
        Dictionary<SkillSlot, float> cooldowns = playerSkill != null ? playerSkill.GetCooldowns() : null;
        if (cooldowns == null) return;

        foreach (SkillSlot slot in new List<SkillSlot>(cooldowns.Keys))
            cooldowns[slot] = 0f;
        Debug.Log("[SkillTest] Cooldowns reset");
    }

    private void KillNearestAlly()
    {
        HealthManager nearest = null;
        float best = float.MaxValue;
        Vector3 origin = playerSkill != null ? playerSkill.transform.position : Vector3.zero;

        foreach (HealthManager health in FindAllies())
        {
            if (health.dead || health is PlayerHealth) continue;
            float d = (health.transform.position - origin).sqrMagnitude;
            if (d < best) { best = d; nearest = health; }
        }

        if (nearest == null)
        {
            Debug.Log("[SkillTest] No living ally to kill");
            return;
        }

        nearest.TakeDamage(nearest.transform.position + Vector3.up, Vector3.forward, 999999f, null, null, HealthManager.DamageType.Environment);
        Debug.Log("[SkillTest] Killed " + nearest.name);
    }

    private void DamageAllAllies()
    {
        foreach (HealthManager health in FindAllies())
        {
            if (health.dead) continue;
            health.TakeDamage(health.transform.position + Vector3.up, Vector3.forward, testDamage, null, null, HealthManager.DamageType.Normal);
        }
        Debug.Log($"[SkillTest] Damaged all allies by {testDamage}");
    }

    // Test scenes are not in build settings, so use editor loading in play mode
    private static void ReloadScene()
    {
        string path = SceneManager.GetActiveScene().path;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(path);
#endif
    }

    private string SkillName(SkillSlot slot)
    {
        var skills = playerSkill != null ? playerSkill.GetSkills() : null;
        return skills != null && skills.TryGetValue(slot, out SkillData data) && data != null ? data.skillName : "-";
    }

    private static List<HealthManager> FindAllies()
    {
        List<HealthManager> result = new();
        AllyTargeting.CollectAllies(result);
        return result;
    }

    private void OnGUI()
    {
        if (!showGuide || (pauseUI != null && pauseUI.IsPaused)) return;

        boxStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };

        string gc = playerSkill != null ? playerSkill.gameChangerPoint.ToString() : "-";
        string hp = playerHealth != null ? $"HP {playerHealth.health:0} / Shield {playerHealth.shieldHealth:0}" : "-";
        string text =
            "<b>Skill Test</b>\n" +
            $"Q: {SkillName(SkillSlot.Skill1)}   E: {SkillName(SkillSlot.Skill2)}   X: {SkillName(SkillSlot.GameChanger)}\n" +
            $"F1: Fill game changer   F5: Infinite game changer [{(infiniteGameChanger ? "ON" : "OFF")}] (point {gc})\n" +
            "F2: Reset cooldowns   F3: Kill nearest ally   F4: Damage all allies\n" +
            $"F7: Hit me ({playerHitDamage:0})   F9: Heavy hit ({playerHeavyHitDamage:0})   F10: Enemy fire on me [{(incomingFire ? "ON" : "OFF")}]   {hp}\n" +
            "ESC: Pause / Return to menu   F6: Hide guide   F8: Reload scene\n" +
            "F11: Place infinite-HP dummy in front of player" +
            (trainingDummy != null ? $"   Hits {trainingDummy.Hits} / Headshots {trainingDummy.Headshots}" : "");
        GUI.Box(new Rect(10, 10, 720, 140), text, boxStyle);
    }
}
