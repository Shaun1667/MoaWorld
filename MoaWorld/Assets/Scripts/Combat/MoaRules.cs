using UnityEngine;

namespace MoaWorld
{
    // Damage, affinity and exp formulas. All numbers come from GameConfig (formulas are TBD).
    public static class MoaRules
    {
        public static float GetAffinityMultiplier(MoaElement attack, MoaElement? defense)
        {
            if (defense == null)
            {
                return 1f;
            }

            GameConfig config = GameConfig.Instance;
            if (Beats(attack, defense.Value))
            {
                return config.advantageDamageMultiplier;
            }
            if (Beats(defense.Value, attack))
            {
                return config.disadvantageDamageMultiplier;
            }
            return 1f;
        }

        // Water > Fire > Grass > Water. Normal beats nothing and loses to nothing.
        private static bool Beats(MoaElement attack, MoaElement defense)
        {
            return (attack == MoaElement.Water && defense == MoaElement.Fire)
                || (attack == MoaElement.Fire && defense == MoaElement.Grass)
                || (attack == MoaElement.Grass && defense == MoaElement.Water);
        }

        public static float CalculateDamage(MoaInstance attacker, Combatant defender)
        {
            GameConfig config = GameConfig.Instance;
            int attack = attacker.Attack;
            float raw = attacker.Species.skillPower * config.damageScale * attack / Mathf.Max(1f, attack + defender.Defense);
            return Mathf.Max(1f, raw * GetAffinityMultiplier(attacker.Species.element, defender.Element));
        }

        public static int ExpToNextLevel(int level)
        {
            GameConfig config = GameConfig.Instance;
            return Mathf.RoundToInt(config.expBaseRequirement * Mathf.Pow(config.expCurveMultiplier, level - 1));
        }

        public static int ExpReward(int defeatedLevel)
        {
            return GameConfig.Instance.expRewardPerLevel * defeatedLevel;
        }

        // TBD formula: linear in remaining HP, from the full-HP rate up to the near-zero-HP rate.
        public static float CaptureChance(MoaInstance moa)
        {
            GameConfig config = GameConfig.Instance;
            float hpRatio = Mathf.Clamp01(moa.currentHp / moa.MaxHp);
            return Mathf.Lerp(config.captureRateAtZeroHp, config.captureRateAtFullHp, hpRatio);
        }
    }
}
