using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers LuciaSkillNanoBoost in place of LuciaSkillReviveOneAlly on Lucia's skill asset.
/// The ReviveOneAlly class itself is kept in code.
/// </summary>
public static class LuciaSkillSetup
{
    private const string AssetPath = "Assets/Resources/Scriptable Object/Persona/Lucia/LuciaSkill.asset";

    [MenuItem("Tools/Skills/Lucia - Register Nano Boost Skill")]
    public static void RegisterNanoBoost()
    {
        PersonaDataSkill skillAsset = AssetDatabase.LoadAssetAtPath<PersonaDataSkill>(AssetPath);
        if (skillAsset == null)
        {
            Debug.LogError("[LuciaSkillSetup] Skill asset not found: " + AssetPath);
            return;
        }

        SkillData slotData = FindSlot(skillAsset, out string slotName);
        if (slotData == null)
        {
            Debug.LogWarning("[LuciaSkillSetup] No slot uses LuciaSkillReviveOneAlly or LuciaSkillNanoBoost. Nothing changed.");
            return;
        }

        Undo.RecordObject(skillAsset, "Register Lucia Nano Boost");
        if (!(slotData.logic is LuciaSkillNanoBoost))
            slotData.logic = new LuciaSkillNanoBoost();
        slotData.skillName = "Nano Boost";
        slotData.desc = "Boost the aimed ally: damage reduction, faster fire rate and bigger magazine for a while.";

        EditorUtility.SetDirty(skillAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LuciaSkillSetup] Nano Boost registered on {slotName} of {AssetPath}");
    }

    private static SkillData FindSlot(PersonaDataSkill skillAsset, out string slotName)
    {
        slotName = null;
        SkillData[] slots = { skillAsset.skill1, skillAsset.skill2, skillAsset.gameChanger };
        string[] names = { "skill1", "skill2", "gameChanger" };

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i]?.logic is LuciaSkillReviveOneAlly || slots[i]?.logic is LuciaSkillNanoBoost)
            {
                slotName = names[i];
                return slots[i];
            }
        }
        return null;
    }
}
