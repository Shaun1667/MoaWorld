using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Purchases, only while standing at the shop. The client asks; the server checks range and coins.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerHealth), typeof(PlayerNotifications))]
    public class PlayerShop : NetworkBehaviour
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

        public void BuyMoaBalls(int count)
        {
            BuyMoaBallsRpc(count);
        }

        public void BuyArmor(int tier)
        {
            BuyArmorRpc(tier);
        }

        public void BuyPotion(int tier)
        {
            BuyPotionRpc(tier);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BuyMoaBallsRpc(int count)
        {
            if (count <= 0 || !TryPay(GameConfig.Instance.moaBallPrice * count))
            {
                return;
            }
            inventory.AddMoaBalls(count);
            notifications.Notify($"모아볼 {count}개를 샀어요");
        }

        // Assumption (design TBD): only the best armor counts, so buying an equal or lower tier is refused.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BuyArmorRpc(int tier)
        {
            if (tier < 0 || tier >= GameConfig.Instance.armorTiers.Length)
            {
                return;
            }
            if (tier <= health.HighestArmorTier)
            {
                notifications.Notify("이미 더 좋은 방어구를 가지고 있어요");
                return;
            }
            ArmorTier armor = GameConfig.Instance.armorTiers[tier];
            if (!TryPay(armor.price))
            {
                return;
            }
            health.SetHighestArmorTier(tier);
            notifications.Notify($"방어구 {tier + 1}단계 착용! 최대 체력 {health.MaxHp}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BuyPotionRpc(int tier)
        {
            if (tier < 0 || tier >= GameConfig.Instance.potionTiers.Length || !TryPay(GameConfig.Instance.potionTiers[tier].price))
            {
                return;
            }
            inventory.AddPotion(tier);
            notifications.Notify($"포션({GameConfig.Instance.potionTiers[tier].healAmount})을 샀어요");
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
