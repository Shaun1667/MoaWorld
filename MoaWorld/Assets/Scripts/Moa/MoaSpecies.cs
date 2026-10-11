using UnityEngine;

namespace MoaWorld
{
    [CreateAssetMenu(fileName = "MoaSpecies", menuName = "MoaWorld/Moa Species")]
    public class MoaSpecies : ScriptableObject
    {
        public string speciesId;
        public string displayName;
        public MoaElement element;

        [Tooltip("3D model with an Animator (built by MoaWorld > Build Moa Models). Empty: element-colored capsule.")]
        public GameObject modelPrefab;

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

        [Header("Skill visuals (assigned by MoaWorld > Assign Moa Effects). Empty: element-colored orb")]
        [Tooltip("Played in front of the attacker, facing the target (slash, swipe).")]
        public GameObject castEffect;
        public float castEffectScale = 1f;
        [Tooltip("Extra rotation relative to the attack direction (some effects are authored lying in a different plane).")]
        public Vector3 castEffectEuler;
        [Tooltip("Meters in front of the attacker's body center.")]
        public float castEffectOffset = 0.6f;
        [Tooltip("Played where the skill hits the target.")]
        public GameObject hitEffect;
        public float hitEffectScale = 1f;

        [Header("Wild behaviour (TBD)")]
        public bool aggressive;
    }
}
