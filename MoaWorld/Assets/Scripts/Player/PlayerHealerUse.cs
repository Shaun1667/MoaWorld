using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // E at the healing machine heals every party moa (fainted ones too). The server checks the range.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerCombat), typeof(PlayerNotifications))]
    public class PlayerHealerUse : NetworkBehaviour
    {
        private PlayerParty party;
        private PlayerCombat combat;
        private PlayerNotifications notifications;

        public bool IsNearHealer => MoaHealer.Instance != null && MoaHealer.Instance.IsInRange(transform.position);

        private void Awake()
        {
            party = GetComponent<PlayerParty>();
            combat = GetComponent<PlayerCombat>();
            notifications = GetComponent<PlayerNotifications>();
        }

        private void Update()
        {
            if (IsOwner && !UiState.IsMenuOpen && IsNearHealer && Input.GetKeyDown(KeyCode.E))
            {
                UseHealer();
            }
        }

        public void UseHealer()
        {
            HealRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void HealRpc()
        {
            if (!IsNearHealer)
            {
                return;
            }
            if (party.Members.Count == 0)
            {
                notifications.Notify("회복할 모아가 없어요");
                return;
            }

            party.HealAll();
            if (combat.ActiveMoa != null)
            {
                combat.ActiveMoa.Unit.SyncMoa();
            }
            notifications.Notify("모아들이 모두 회복됐어요!");
        }
    }
}
