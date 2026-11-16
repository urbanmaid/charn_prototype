using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers Amelia's merged Supply Overdrive, cluster grenade, and Quartermastering on her skill asset.
/// Runs from menu, or automatically when Temp/agent_amelia_setup.request exists.
/// </summary>
[InitializeOnLoad]
public static class AmeliaSkillSetup
{
    private const string AssetPath = "Assets/Resources/Scriptable Object/Persona/Amelia/AmeliaSkill.asset";
    private const string RequestPath = "Temp/agent_amelia_setup.request";

    static AmeliaSkillSetup()
    {
        EditorApplication.delayCall += RunIfRequested;
    }

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunIfRequested;
            return;
        }
        File.Delete(RequestPath);
        Register();
    }

    [MenuItem("Tools/Skills/Amelia - Merge Q and Add Cluster Grenade")]
    public static void Register()
    {
        PersonaDataSkill skillAsset = AssetDatabase.LoadAssetAtPath<PersonaDataSkill>(AssetPath);
        if (skillAsset == null)
        {
            Debug.LogError("[AmeliaSkillSetup] Skill asset not found: " + AssetPath);
            return;
        }

        Undo.RecordObject(skillAsset, "Register Amelia Skills");

        skillAsset.skill1 ??= new NormalSkillData();
        skillAsset.skill1.logic = new AmeliaSkillSupplyOverdrive();
        skillAsset.skill1.skillName = "Supply Overdrive";
        skillAsset.skill1.desc = "You and allies within 5m get a fresh 250% magazine. Its rounds deal 160% damage until empty or reloaded.";
        skillAsset.skill1.cooldown = 16f;
        skillAsset.skill2 ??= new NormalSkillData();
        skillAsset.skill2.skillName = "Cluster Grenade";
        skillAsset.skill2.desc = "Throw a cluster grenade. The main blast releases four damaging submunitions.";
        skillAsset.skill2.cooldown = 18f;
        skillAsset.skill2.logic = new AmeliaSkillClusterGrenade();

        EditorUtility.SetDirty(skillAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[AmeliaSkillSetup] Amelia skills registered on " + AssetPath);
    }
}
