using UnityEngine;

namespace MoaWorld
{
    // Summons party moa with number keys 1-6 (same key again recalls, another key swaps; one moa out at a time)
    // and decides what the summoned moa fights: the selected target first, otherwise whoever recently attacked us.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerTargeting), typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerNotifications))]
    public class PlayerCombat : MonoBehaviour
    {
        private const int NoSlot = -1;

        [SerializeField] private SummonedMoa summonedMoaPrefab;

        private PlayerParty party;
        private PlayerTargeting targeting;
        private PlayerHealth health;
        private PlayerNotifications notifications;
        private Combatant recentAttacker;
        private float recentAttackTime = float.NegativeInfinity;

        // Tracked by instance, not slot, so party reordering never points at the wrong moa.
        private MoaInstance activeInstance;

        public SummonedMoa ActiveMoa { get; private set; }
        public int ActiveSlot => activeInstance == null ? NoSlot : party.IndexOf(activeInstance);

        public Vector3 SummonPoint => transform.position + transform.right * 1.5f - transform.forward;

        private void Awake()
        {
            party = GetComponent<PlayerParty>();
            targeting = GetComponent<PlayerTargeting>();
            health = GetComponent<PlayerHealth>();
            notifications = GetComponent<PlayerNotifications>();
            health.Damaged += OnOwnSideDamaged;
        }

        private void Update()
        {
            if (UiState.IsMenuOpen)
            {
                return;
            }

            int slotCount = GameConfig.Instance.maxPartySize;
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + slot))
                {
                    ToggleSummon(slot);
                }
            }
        }

        public Combatant GetCombatTarget()
        {
            Targetable selected = targeting.CurrentTarget;
            if (selected != null)
            {
                Combatant selectedCombatant = selected.GetComponent<Combatant>();
                if (IsValidEnemy(selectedCombatant))
                {
                    return selectedCombatant;
                }
            }

            bool inCombat = Time.time - recentAttackTime <= GameConfig.Instance.combatStateSeconds;
            return inCombat && IsValidEnemy(recentAttacker) ? recentAttacker : null;
        }

        public void ToggleSummon(int slot)
        {
            MoaInstance moa = party.Get(slot);
            if (moa == null)
            {
                return;
            }
            if (moa == activeInstance)
            {
                Recall();
                return;
            }
            if (moa.IsFainted)
            {
                notifications.Notify("기절한 모아는 꺼낼 수 없어요. 모아 박스에서 회복시켜 주세요");
                return;
            }

            Recall();
            ActiveMoa = Instantiate(summonedMoaPrefab, SummonPoint, transform.rotation);
            ActiveMoa.Initialize(moa, this);
            ActiveMoa.Unit.Damaged += OnOwnSideDamaged;
            ActiveMoa.Unit.Fainted += OnActiveMoaFainted;
            activeInstance = moa;
        }

        public void Recall()
        {
            if (ActiveMoa != null)
            {
                Destroy(ActiveMoa.gameObject);
            }
            ActiveMoa = null;
            activeInstance = null;
        }

        public void RecallIfActive(MoaInstance moa)
        {
            if (moa == activeInstance)
            {
                Recall();
            }
        }

        private bool IsValidEnemy(Combatant combatant)
        {
            return combatant != null && combatant.IsAttackable && combatant.OwnerRoot != transform;
        }

        private void OnOwnSideDamaged(Combatant attacker, float amount)
        {
            if (attacker != null && attacker.OwnerRoot != transform)
            {
                recentAttacker = attacker;
                recentAttackTime = Time.time;
            }
        }

        private void OnActiveMoaFainted(Combatant attacker)
        {
            Recall();
        }
    }
}
