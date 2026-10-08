using UnityEngine;

namespace MoaWorld
{
    // Owned moa out in the world: follows its owner and auto-attacks whatever the owner is fighting.
    [RequireComponent(typeof(MoaUnit))]
    public class SummonedMoa : MonoBehaviour
    {
        private MoaUnit unit;
        private PlayerCombat owner;

        public MoaUnit Unit => unit;

        private void Awake()
        {
            unit = GetComponent<MoaUnit>();
        }

        public void Initialize(MoaInstance moa, PlayerCombat ownerCombat)
        {
            owner = ownerCombat;
            unit.Initialize(moa, ownerCombat.transform);
            name = $"MyMoa_{moa.speciesId}_Lv{moa.level}";
        }

        private void Update()
        {
            if (owner == null || !unit.IsAlive)
            {
                return;
            }

            GameConfig config = GameConfig.Instance;
            Vector3 ownerPosition = owner.transform.position;

            if (Vector3.Distance(transform.position, ownerPosition) > config.moaLeashDistance)
            {
                unit.Teleport(owner.SummonPoint);
                return;
            }

            Combatant target = owner.GetCombatTarget();
            if (target != null)
            {
                unit.SetDestination(target.transform.position, unit.SkillReach(target) * 0.9f);
                unit.TryUseSkill(target);
            }
            else
            {
                unit.SetDestination(ownerPosition, config.moaFollowDistance);
            }
        }
    }
}
