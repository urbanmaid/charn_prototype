using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Lucia skill test scene from PA_Swap:
/// - Player: char_lucia (same components/controls as char_shadow_professor, Lucia persona gives Lucia_test.vrm design at runtime)
/// - Exactly 3 allies (Ally_rifle_fsm) with Fiona / Amelia / Prof personas, tagged PA
/// - SkillTestHelper (game changer point, cooldown reset, damage/kill allies)
/// Rebuild from menu, or automatically when Temp/agent_build_scene.request exists.
/// Fiona version (FionaSkillTest) uses Fiona as player and Lucia / Amelia / Prof as allies.
/// Amelia version (AmeliaSkillTest) uses Amelia as player and Lucia / Fiona / Prof as allies.
/// </summary>
[InitializeOnLoad]
public static class LuciaSkillTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Test/LuciaSkillTest.unity";
    private const string BaseScenePath = "Assets/Scenes/Test/PA_Swap.unity";
    private const string PlayerName = "char_lucia";
    private const string LuciaPersonaPath = "Assets/Resources/Scriptable Object/Persona/Lucia/Lucia.asset";
    private const string AllyPrefabPath = "Assets/Prefabs/Ally/Ally_rifle_fsm.prefab";
    private const string RequestPath = "Temp/agent_build_scene.request";
    private const string FionaRequestPath = "Temp/agent_build_fiona_scene.request";
    private const string FionaScenePath = "Assets/Scenes/Test/FionaSkillTest.unity";
    private const string FionaPersonaPath = "Assets/Resources/Scriptable Object/Persona/Fiona/Fiona.asset";
    private const string AmeliaRequestPath = "Temp/agent_build_amelia_scene.request";
    private const string AmeliaScenePath = "Assets/Scenes/Test/AmeliaSkillTest.unity";
    private const string AmeliaPersonaPath = "Assets/Resources/Scriptable Object/Persona/Amelia/Amelia.asset";

    private static readonly string[] AmeliaAllyPersonaPaths =
    {
        "Assets/Resources/Scriptable Object/Persona/Lucia/Lucia.asset",
        "Assets/Resources/Scriptable Object/Persona/Fiona/Fiona.asset",
        "Assets/Resources/Scriptable Object/Persona/_Prof/Prof.asset",
    };

    private static readonly string[] FionaAllyPersonaPaths =
    {
        "Assets/Resources/Scriptable Object/Persona/Lucia/Lucia.asset",
        "Assets/Resources/Scriptable Object/Persona/Amelia/Amelia.asset",
        "Assets/Resources/Scriptable Object/Persona/_Prof/Prof.asset",
    };

    private static readonly string[] AllyPersonaPaths =
    {
        "Assets/Resources/Scriptable Object/Persona/Fiona/Fiona.asset",
        "Assets/Resources/Scriptable Object/Persona/Amelia/Amelia.asset",
        "Assets/Resources/Scriptable Object/Persona/_Prof/Prof.asset",
    };

    static LuciaSkillTestSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfRequested;
    }

    private static void BuildIfRequested()
    {
        if (!File.Exists(RequestPath) && !File.Exists(FionaRequestPath) && !File.Exists(AmeliaRequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfRequested;
            return;
        }
        if (File.Exists(RequestPath)) { File.Delete(RequestPath); Build(); }
        if (File.Exists(FionaRequestPath)) { File.Delete(FionaRequestPath); BuildFiona(); }
        if (File.Exists(AmeliaRequestPath)) { File.Delete(AmeliaRequestPath); BuildAmelia(); }
    }

    [MenuItem("Tools/Skills/Build Lucia Skill Test Scene")]
    public static void Build()
    {
        BuildScene(ScenePath, LuciaPersonaPath, AllyPersonaPaths);
    }

    [MenuItem("Tools/Skills/Build Fiona Skill Test Scene")]
    public static void BuildFiona()
    {
        BuildScene(FionaScenePath, FionaPersonaPath, FionaAllyPersonaPaths);
    }

    [MenuItem("Tools/Skills/Build Amelia Skill Test Scene")]
    public static void BuildAmelia()
    {
        BuildScene(AmeliaScenePath, AmeliaPersonaPath, AmeliaAllyPersonaPaths);
    }

    private static void BuildScene(string scenePath, string playerPersonaPath, string[] allyPersonaPaths)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        PersonaData lucia = AssetDatabase.LoadAssetAtPath<PersonaData>(playerPersonaPath);
        GameObject allyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AllyPrefabPath);
        if (lucia == null || allyPrefab == null)
        {
            Debug.LogError("[SkillTestScene] Player persona or ally prefab not found.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            AssetDatabase.DeleteAsset(scenePath);
        if (!AssetDatabase.CopyAsset(BaseScenePath, scenePath))
        {
            Debug.LogError("[SkillTestScene] Failed to copy " + BaseScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();

        // Player: char_lucia with Lucia persona
        GameObject player = roots.FirstOrDefault(g => g.name == PlayerName && g.GetComponent<PlayerHealth>() != null);
        if (player == null)
        {
            Debug.LogError("[SkillTestScene] Player 'char_lucia' not found in " + BaseScenePath);
            return;
        }
        player.SetActive(true);
        player.tag = "Player";
        SetPersona(player, lucia);
        player.name = "char_" + lucia.name.ToLower();

        // Remove every existing ally (active or deprecated) so the scene has exactly 3
        int removed = 0;
        foreach (GameObject root in roots)
        {
            if (root == player) continue;
            if (root.GetComponent<AllyStateController>() != null || root.GetComponent<AllyHealthManager>() != null || root.CompareTag("PA"))
            {
                Object.DestroyImmediate(root);
                removed++;
            }
        }

        // 3 allies around player, on NavMesh
        for (int i = 0; i < allyPersonaPaths.Length; i++)
        {
            GameObject ally = (GameObject)PrefabUtility.InstantiatePrefab(allyPrefab, scene);
            PersonaData persona = AssetDatabase.LoadAssetAtPath<PersonaData>(allyPersonaPaths[i]);
            ally.name = "Ally_" + (persona != null ? persona.name : (i + 1).ToString());
            // Prefab is tagged Player by mistake; PA keeps FindGameObjectWithTag("Player") pointing to the real player
            ally.tag = "PA";

            Vector3 offset = Quaternion.Euler(0f, 150f + 30f * i, 0f) * player.transform.forward * 2.5f;
            Vector3 position = player.transform.position + offset;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                position = hit.position;
            ally.transform.SetPositionAndRotation(position, player.transform.rotation);

            if (persona != null) SetPersona(ally, persona);
        }

        // Test helper
        GameObject helper = new GameObject("_SkillTestHelper");
        SceneManager.MoveGameObjectToScene(helper, scene);
        helper.AddComponent<SkillTestHelper>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player;

        Debug.Log($"[SkillTestScene] Built {scenePath} from {BaseScenePath}. Player={player.name}({lucia.name}), removed old allies={removed}, allies=3. Press Play.");
    }

    private static void SetPersona(GameObject target, PersonaData persona)
    {
        PersonaBehavior pb = target.GetComponent<PersonaBehavior>();
        if (pb == null) return;

        SerializedObject so = new SerializedObject(pb);
        so.FindProperty("personaData").objectReferenceValue = persona;
        so.FindProperty("applyPersonaMesh").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        var ally = target.GetComponent<AllyStateController>();
        if (ally != null) ally.EnablePersonaPoseCorrection();
    }
}
