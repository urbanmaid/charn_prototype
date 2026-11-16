using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers Fiona's skills (Lizard's First Bite / Crow's Wingbeat / Hawk's Ambush) on her skill asset.
/// Cooldown and required point already set on the asset are kept.
/// Runs from menu, or automatically when Temp/agent_fiona_setup.request exists.
/// </summary>
[InitializeOnLoad]
public static class FionaSkillSetup
{
    private const string AssetPath = "Assets/Resources/Scriptable Object/Persona/Fiona/FionaSkill.asset";
    private const string RequestPath = "Temp/agent_fiona_setup.request";

    static FionaSkillSetup()
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

    [MenuItem("Tools/Skills/Fiona - Register Skills (Lizard / Crow / Hawk)")]
    public static void Register()
    {
        PersonaDataSkill skillAsset = AssetDatabase.LoadAssetAtPath<PersonaDataSkill>(AssetPath);
        if (skillAsset == null)
        {
            Debug.LogError("[FionaSkillSetup] Skill asset not found: " + AssetPath);
            return;
        }

        Undo.RecordObject(skillAsset, "Register Fiona Skills");

        skillAsset.skill1 ??= new NormalSkillData();
        skillAsset.skill1.logic = new FionaSkillLizardBite();
        skillAsset.skill1.skillName = "Lizard's First Bite";
        skillAsset.skill1.desc = "Instant reload. Venom rounds poison enemies for 8 damage per second and slow them by 25% for 4s.";

        skillAsset.skill2 ??= new NormalSkillData();
        skillAsset.skill2.logic = new FionaSkillCrowWingbeat();
        skillAsset.skill2.skillName = "Crow's Wingbeat";
        skillAsset.skill2.desc = "200 shield for 8s. 25% damage reduction for 10s.";

        skillAsset.gameChanger ??= new GameChangerData();
        skillAsset.gameChanger.logic = new FionaSkillHawkAmbush();
        skillAsset.gameChanger.skillName = "Hawk's Ambush";
        skillAsset.gameChanger.desc = "Wait and fire to hit every enemy in sight. 180 -> 300 damage over 2s. 65% refund if not fired in 4s.";

        EditorUtility.SetDirty(skillAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[FionaSkillSetup] Fiona skills registered on " + AssetPath);
    }
}
