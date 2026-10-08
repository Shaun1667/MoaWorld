using UnityEngine;

namespace MoaWorld
{
    // F1-F5 drink the potion of that tier. A potion is not used up when HP is already full.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerHealth), typeof(PlayerNotifications))]
    public class PlayerPotionUse : MonoBehaviour
    {
        private PlayerInventory inventory;
        private PlayerHealth health;
        private PlayerNotifications notifications;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            health = GetComponent<PlayerHealth>();
            notifications = GetComponent<PlayerNotifications>();
        }

        private void Update()
        {
            if (UiState.IsMenuOpen)
            {
                return;
            }

            int tierCount = GameConfig.Instance.potionTiers.Length;
            for (int tier = 0; tier < tierCount; tier++)
            {
                if (Input.GetKeyDown(KeyCode.F1 + tier))
                {
                    TryUse(tier);
                }
            }
        }

        public bool TryUse(int tier)
        {
            if (health.CurrentHp >= health.MaxHp)
            {
                notifications.Notify("체력이 이미 가득 차 있어요");
                return false;
            }
            if (!inventory.TryUsePotion(tier))
            {
                notifications.Notify($"포션({GameConfig.Instance.potionTiers[tier].healAmount})이 없어요");
                return false;
            }
            health.Heal(GameConfig.Instance.potionTiers[tier].healAmount);
            return true;
        }
    }
}
