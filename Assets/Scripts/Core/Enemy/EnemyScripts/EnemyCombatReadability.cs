using UnityEngine;

namespace EnemyAI
{
    public static class EnemyCombatReadability
    {
        public static void Apply(EnemyHealth enemy, StateController controller)
        {
            if (!controller || controller.TeamCode == 0) return;
            Color color = GetTint(controller);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (!material || !material.name.StartsWith("char_enemy", System.StringComparison.Ordinal)) continue;
                    block.Clear();
                    renderer.GetPropertyBlock(block, i);
                    if (material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color);
                    if (material.HasProperty("_Color")) block.SetColor("_Color", color);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        public static Color GetTint(StateController controller)
        {
            if (!controller || controller.TeamCode == 0) return Color.black;
            bool shortWeapon = controller && controller.classStats && controller.classStats.weaponType == ClassStats.WeaponType.SHORT;
            return shortWeapon ? new Color(0.62f, 0.38f, 0.24f) : new Color(0.48f, 0.53f, 0.57f);
        }
    }
}
