using UnityEngine;

namespace MoaWorld
{
    [CreateAssetMenu(fileName = "MoaSpecies", menuName = "MoaWorld/Moa Species")]
    public class MoaSpecies : ScriptableObject
    {
        public string speciesId;
        public string displayName;
        public MoaElement element;

        [Header("Base stats at level 1 (TBD)")]
        public int baseHp = 50;
        public int baseAttack = 10;
        public int baseDefense = 10;

        [Header("Growth per level (TBD, differs per species)")]
        public float hpGrowth = 5f;
        public float attackGrowth = 1f;
        public float defenseGrowth = 1f;

        [Header("Skill, one per moa (TBD). Skill element = species element")]
        public float skillPower = 10f;
        public float skillCooldown = 2f;
        public float skillRange = 2f;

        [Header("Wild behaviour (TBD)")]
        public bool aggressive;
    }
}
