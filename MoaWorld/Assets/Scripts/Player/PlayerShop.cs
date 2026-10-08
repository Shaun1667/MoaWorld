using UnityEngine;

namespace MoaWorld
{
    // Purchases, only while standing at the shop. Validated by the server (host) once networking is added.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerHealth), typeof(PlayerNotifications))]
    public class PlayerShop : MonoBehaviour
    {
        private PlayerInventory inventory;
        private PlayerHealth health;
        private PlayerNotifications notifications;

        public bool IsNearShop => Shop.Instance != null && Shop.Instance.IsInRange(transform.position);

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            health = GetComponent<PlayerHealth>();
            notifications = GetComponent<PlayerNotifications>();
        }

        public bool TryBuyMoaBalls(int count)
        {
            if (!TryPay(GameConfig.Instance.moaBallPrice * count))
            {
                return false;
            }
            inventory.AddMoaBalls(count);
            notifications.Notify($"모아볼 {count}개를 샀어요");
            return true;
        }

        // Assumption (design TBD): only the best armor counts, so buying an equal or lower tier is refused.
        public bool TryBuyArmor(int tier)
        {
            if (tier <= health.HighestArmorTier)
            {
                notifications.Notify("이미 더 좋은 방어구를 가지고 있어요");
                return false;
            }
            ArmorTier armor = GameConfig.Instance.armorTiers[tier];
            if (!TryPay(armor.price))
            {
                return false;
            }
            health.SetHighestArmorTier(tier);
            notifications.Notify($"방어구 {tier + 1}단계 착용! 최대 체력 {health.MaxHp}");
            return true;
        }

        public bool TryBuyPotion(int tier)
        {
            if (!TryPay(GameConfig.Instance.potionTiers[tier].price))
            {
                return false;
            }
            inventory.AddPotion(tier);
            notifications.Notify($"포션({GameConfig.Instance.potionTiers[tier].healAmount})을 샀어요");
            return true;
        }

        private bool TryPay(int price)
        {
            if (!IsNearShop)
            {
                return false;
            }
            if (!inventory.TrySpendCoins(price))
            {
                notifications.Notify("모아 코인이 부족해요");
                return false;
            }
            return true;
        }
    }
}
