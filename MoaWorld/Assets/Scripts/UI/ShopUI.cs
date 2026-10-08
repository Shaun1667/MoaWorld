using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoaWorld
{
    // Shop screen: moa balls, armor tiers and potions, bought with moa coins.
    public class ShopUI : ProximityMenu
    {
        private const int BulkBallCount = 10;
        private const float ButtonWidth = 300f;
        private const float ButtonHeight = 70f;
        private const float RowSpacing = 10f;
        private const float FirstRowY = -195f;

        private static readonly float[] ColumnX = { 180f, 500f, 820f };

        private PlayerInventory inventory;
        private PlayerHealth health;
        private PlayerShop shop;

        private TextMeshProUGUI coinsHeader;
        private TextMeshProUGUI ballsInfo;
        private Button[] ballButtons;
        private TextMeshProUGUI[] ballLabels;
        private Button[] armorButtons;
        private TextMeshProUGUI[] armorLabels;
        private Button[] potionButtons;
        private TextMeshProUGUI[] potionLabels;

        protected override bool IsPlayerNear => shop.IsNearShop;
        protected override string PromptText => "[E] 상점 열기";

        protected override void Bind(GameObject player)
        {
            inventory = player.GetComponent<PlayerInventory>();
            health = player.GetComponent<PlayerHealth>();
            shop = player.GetComponent<PlayerShop>();
            inventory.Changed += RefreshNow;
        }

        protected override void Unbind()
        {
            base.Unbind();
            inventory.Changed -= RefreshNow;
            inventory = null;
            health = null;
            shop = null;
        }

        protected override void Refresh()
        {
            GameConfig config = GameConfig.Instance;
            coinsHeader.text = $"보유 모아 코인  {inventory.Coins}";

            ballsInfo.text = $"보유 {inventory.MoaBalls}개";
            int[] ballCounts = { 1, BulkBallCount };
            for (int i = 0; i < ballButtons.Length; i++)
            {
                int price = config.moaBallPrice * ballCounts[i];
                ballLabels[i].text = $"모아볼 {ballCounts[i]}개\n{price} 코인";
                SetEnabledLook(ballButtons[i], inventory.Coins >= price);
            }

            for (int tier = 0; tier < armorButtons.Length; tier++)
            {
                ArmorTier armor = config.armorTiers[tier];
                string state;
                bool available = false;
                if (tier == health.HighestArmorTier)
                {
                    state = "착용 중";
                }
                else if (tier < health.HighestArmorTier)
                {
                    state = "더 좋은 방어구 보유";
                }
                else
                {
                    state = $"{armor.price} 코인";
                    available = inventory.Coins >= armor.price;
                }
                armorLabels[tier].text = $"방어구 {tier + 1}단계 · 체력 +{armor.hpBonus}\n{state}";
                SetEnabledLook(armorButtons[tier], available);
            }

            for (int tier = 0; tier < potionButtons.Length; tier++)
            {
                PotionTier potion = config.potionTiers[tier];
                potionLabels[tier].text = $"[F{tier + 1}] 포션 (회복 {potion.healAmount}) · 보유 {inventory.GetPotionCount(tier)}\n{potion.price} 코인";
                SetEnabledLook(potionButtons[tier], inventory.Coins >= potion.price);
            }
        }

        private static void SetEnabledLook(Button button, bool enabledLook)
        {
            ((Image)button.targetGraphic).color = enabledLook ? UiFactory.ButtonColor : UiFactory.DisabledButtonColor;
        }

        protected override GameObject BuildPanel()
        {
            GameConfig config = GameConfig.Instance;
            GameObject panel = UiFactory.CreatePanel("ShopPanel", transform, new Vector2(1000f, 660f));
            Transform root = panel.transform;

            UiFactory.CreateText("Title", root, 36, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 50f))
                .text = "상점";
            coinsHeader = UiFactory.CreateText("Coins", root, 24, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(600f, 36f));

            string[] headers = { "모아볼", "방어구", "포션" };
            for (int column = 0; column < headers.Length; column++)
            {
                UiFactory.CreateText($"Header{column}", root, 26, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(ColumnX[column], -130f), new Vector2(ButtonWidth, 40f))
                    .text = headers[column];
            }

            int[] ballCounts = { 1, BulkBallCount };
            ballButtons = new Button[ballCounts.Length];
            ballLabels = new TextMeshProUGUI[ballCounts.Length];
            for (int i = 0; i < ballCounts.Length; i++)
            {
                int count = ballCounts[i];
                ballButtons[i] = UiFactory.CreateButton($"Ball{i}", root, RowPosition(0, i), new Vector2(ButtonWidth, ButtonHeight), 20,
                    () => shop.TryBuyMoaBalls(count), out ballLabels[i]);
            }
            ballsInfo = UiFactory.CreateText("BallsInfo", root, 20, TextAlignmentOptions.Center, new Vector2(0f, 1f), RowPosition(0, ballCounts.Length), new Vector2(ButtonWidth, 40f));

            armorButtons = new Button[config.armorTiers.Length];
            armorLabels = new TextMeshProUGUI[config.armorTiers.Length];
            for (int tier = 0; tier < armorButtons.Length; tier++)
            {
                int t = tier;
                armorButtons[tier] = UiFactory.CreateButton($"Armor{tier}", root, RowPosition(1, tier), new Vector2(ButtonWidth, ButtonHeight), 18,
                    () => shop.TryBuyArmor(t), out armorLabels[tier]);
            }

            potionButtons = new Button[config.potionTiers.Length];
            potionLabels = new TextMeshProUGUI[config.potionTiers.Length];
            for (int tier = 0; tier < potionButtons.Length; tier++)
            {
                int t = tier;
                potionButtons[tier] = UiFactory.CreateButton($"Potion{tier}", root, RowPosition(2, tier), new Vector2(ButtonWidth, ButtonHeight), 18,
                    () => shop.TryBuyPotion(t), out potionLabels[tier]);
            }

            UiFactory.CreateText("Hint", root, 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(900f, 40f))
                .text = "포션은 F1~F5 키로 사용해요   ·   E / Esc 닫기";
            return panel;
        }

        private static Vector2 RowPosition(int column, int row)
        {
            return new Vector2(ColumnX[column], FirstRowY - row * (ButtonHeight + RowSpacing));
        }
    }
}
