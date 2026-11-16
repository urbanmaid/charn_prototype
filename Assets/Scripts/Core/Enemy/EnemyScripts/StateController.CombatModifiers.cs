using System.Collections.Generic;
using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// Per-character runtime combat modifiers (ClassStats is a shared asset, so buffs must not touch it).
    /// Applied by ingame effects such as Lucia's boost skill.
    /// </summary>
    public partial class StateController
    {
        private readonly List<float> shotRateModifiers = new();
        private readonly List<float> magazineModifiers = new();
        private readonly List<float> damageModifiers = new();
        private int infiniteAmmoCount;

        /// <summary> Multiplied with ClassStats.bulletDamage (1 + sum of modifiers). </summary>
        public float DamageMultiplier { get; private set; } = 1f;

        /// <summary> Shots don't use bullets while true. </summary>
        public bool HasInfiniteAmmo => infiniteAmmoCount > 0;

        public void AddDamageModifier(float multiplier)
        {
            damageModifiers.Add(multiplier);
            DamageMultiplier = SumModifiers(damageModifiers, 0.01f, 5f);
        }

        public void RemoveDamageModifier(float multiplier)
        {
            if (damageModifiers.Remove(multiplier))
                DamageMultiplier = SumModifiers(damageModifiers, 0.01f, 5f);
        }

        public void AddInfiniteAmmo() { infiniteAmmoCount++; }
        public void RemoveInfiniteAmmo() { infiniteAmmoCount = Mathf.Max(0, infiniteAmmoCount - 1); }

        /// <summary> Supply: a full magazine at once (no reload). </summary>
        public void RefillMagazine()
        {
            bullets = EffectiveMagBullets;
        }

        /// <summary> Multiplied with ClassStats.shotRateFactor. </summary>
        public float ShotRateMultiplier { get; private set; } = 1f;

        /// <summary> Multiplied with mag capacity and burst length. </summary>
        public float MagazineMultiplier { get; private set; } = 1f;

        public int EffectiveMagBullets => Mathf.Max(1, Mathf.CeilToInt(magBullets * MagazineMultiplier));

        public void AddShotRateModifier(float multiplier)
        {
            shotRateModifiers.Add(multiplier);
            ShotRateMultiplier = SumModifiers(shotRateModifiers, 0.1f, 10f);
        }

        public void RemoveShotRateModifier(float multiplier)
        {
            if (shotRateModifiers.Remove(multiplier))
                ShotRateMultiplier = SumModifiers(shotRateModifiers, 0.1f, 10f);
        }

        public void AddMagazineModifier(float multiplier)
        {
            magazineModifiers.Add(multiplier);
            MagazineMultiplier = SumModifiers(magazineModifiers, 0.1f, 20f);

            // Bonus bullets are given immediately
            if (!reloading)
                bullets = EffectiveMagBullets;
        }

        public void RemoveMagazineModifier(float multiplier)
        {
            if (!magazineModifiers.Remove(multiplier))
                return;

            MagazineMultiplier = SumModifiers(magazineModifiers, 0.1f, 20f);
            bullets = Mathf.Min(bullets, EffectiveMagBullets);
        }

        private static float SumModifiers(List<float> modifiers, float min, float max)
        {
            float total = 1f;
            foreach (float mod in modifiers)
                total += mod;
            return Mathf.Clamp(total, min, max);
        }
    }
}
