using System;
using UnityEngine;

namespace MoaWorld
{
    // One individual moa. Plain serializable data so it can live in a party, the Moa Box, or a save file.
    [Serializable]
    public class MoaInstance
    {
        public string speciesId;
        public int level;
        public int ivHp;
        public int ivAttack;
        public int ivDefense;
        public float currentHp;
        public int exp;

        [NonSerialized] private MoaSpecies species;

        public MoaSpecies Species
        {
            get
            {
                if (species == null)
                {
                    species = MoaDatabase.Instance.Get(speciesId);
                }
                return species;
            }
        }

        // Stat formula is TBD: base + growth per level + individual value.
        public int MaxHp => Species.baseHp + Mathf.RoundToInt(Species.hpGrowth * (level - 1)) + ivHp;
        public int Attack => Species.baseAttack + Mathf.RoundToInt(Species.attackGrowth * (level - 1)) + ivAttack;
        public int Defense => Species.baseDefense + Mathf.RoundToInt(Species.defenseGrowth * (level - 1)) + ivDefense;

        public bool IsFainted => currentHp <= 0f;

        // Returns the number of levels gained. Max HP growth is added to current HP.
        public int AddExp(int amount)
        {
            int levelCap = GameConfig.Instance.moaLevelCap;
            int gained = 0;
            exp += amount;
            while (level < levelCap && exp >= MoaRules.ExpToNextLevel(level))
            {
                exp -= MoaRules.ExpToNextLevel(level);
                int oldMaxHp = MaxHp;
                level++;
                gained++;
                currentHp = Mathf.Min(MaxHp, currentHp + (MaxHp - oldMaxHp));
            }
            if (level >= levelCap)
            {
                exp = 0;
            }
            return gained;
        }

        public static MoaInstance Create(MoaSpecies species, int level)
        {
            GameConfig config = GameConfig.Instance;
            var moa = new MoaInstance
            {
                speciesId = species.speciesId,
                species = species,
                level = Mathf.Clamp(level, 1, config.moaLevelCap),
                ivHp = UnityEngine.Random.Range(config.ivMin, config.ivMax + 1),
                ivAttack = UnityEngine.Random.Range(config.ivMin, config.ivMax + 1),
                ivDefense = UnityEngine.Random.Range(config.ivMin, config.ivMax + 1),
            };
            moa.currentHp = moa.MaxHp;
            return moa;
        }
    }
}
