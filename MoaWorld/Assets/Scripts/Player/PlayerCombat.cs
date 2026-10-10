using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Summons party moa with number keys 1-6 (same key again recalls, another key swaps; one moa out at a time)
    // and decides what the summoned moa fights: the selected target first, otherwise whoever recently attacked us.
    // Input is read on the owner; summoning and target choice are decided on the server.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerTargeting), typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerNotifications))]
    public class PlayerCombat : NetworkBehaviour
    {
        private const int NoSlot = -1;

        [SerializeField] private SummonedMoa summonedMoaPrefab;

        private readonly NetworkVariable<int> activeSlot = new NetworkVariable<int>(NoSlot, NetworkVariableReadPermission.Owner);

        private PlayerParty party;
        private PlayerTargeting targeting;
        private PlayerHealth health;
        private PlayerNotifications notifications;
        private Combatant recentAttacker;
        private float recentAttackTime = float.NegativeInfinity;

        // Server: the target the owner selected.
        private Targetable selectedTarget;

        // Server: tracked by instance, not slot, so party reordering never points at the wrong moa.
        private MoaInstance activeInstance;

        // Server only.
        public SummonedMoa ActiveMoa { get; private set; }

        public int ActiveSlot => IsServer ? (activeInstance == null ? NoSlot : party.IndexOf(activeInstance)) : activeSlot.Value;

        public Vector3 SummonPoint => transform.position + transform.right * 1.5f - transform.forward;

        private void Awake()
        {
            party = GetComponent<PlayerParty>();
            targeting = GetComponent<PlayerTargeting>();
            health = GetComponent<PlayerHealth>();
            notifications = GetComponent<PlayerNotifications>();
            health.Damaged += OnOwnSideDamaged;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                targeting.TargetChanged += OnLocalTargetChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            targeting.TargetChanged -= OnLocalTargetChanged;
            if (IsServer)
            {
                Recall();
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                activeSlot.Value = ActiveSlot;
            }

            if (!IsOwner || UiState.IsMenuOpen)
            {
                return;
            }

            int slotCount = GameConfig.Instance.maxPartySize;
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + slot))
                {
                    ToggleSummonRpc(slot);
                }
            }
        }

        private void OnLocalTargetChanged(Targetable target)
        {
            NetworkObject targetObject = target != null ? target.GetComponent<NetworkObject>() : null;
            SelectTargetRpc(targetObject != null ? new NetworkObjectReference(targetObject) : default);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SelectTargetRpc(NetworkObjectReference target)
        {
            selectedTarget = target.TryGet(out NetworkObject targetObject) ? targetObject.GetComponent<Targetable>() : null;
        }

        // Server only.
        public Combatant GetCombatTarget()
        {
            if (selectedTarget != null && Vector3.Distance(transform.position, selectedTarget.transform.position) <= GameConfig.Instance.targetMaxDistance)
            {
                Combatant selectedCombatant = selectedTarget.GetComponent<Combatant>();
                if (IsValidEnemy(selectedCombatant))
                {
                    return selectedCombatant;
                }
            }

            bool inCombat = Time.time - recentAttackTime <= GameConfig.Instance.combatStateSeconds;
            return inCombat && IsValidEnemy(recentAttacker) ? recentAttacker : null;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ToggleSummonRpc(int slot)
        {
            ToggleSummon(slot);
        }

        // Server only.
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
            ActiveMoa.Unit.NetworkObject.Spawn(true);
            ActiveMoa.Initialize(moa, this);
            ActiveMoa.Unit.Damaged += OnOwnSideDamaged;
            ActiveMoa.Unit.Fainted += OnActiveMoaFainted;
            activeInstance = moa;
        }

        // Server only.
        public void Recall()
        {
            if (ActiveMoa != null && ActiveMoa.Unit.IsSpawned)
            {
                ActiveMoa.Unit.NetworkObject.Despawn(true);
            }
            ActiveMoa = null;
            activeInstance = null;
        }

        // Server only.
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
