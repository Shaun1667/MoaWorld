using System.Text;
using TMPro;
using UnityEngine;

namespace MoaWorld
{
    // Inventory counts (bottom right) and short notification messages (center) for the local player.
    public class PlayerHud : LocalPlayerView
    {
        private const float MessageSeconds = 2.5f;

        private readonly StringBuilder builder = new StringBuilder();
        private PlayerInventory inventory;
        private PlayerNotifications notifications;
        private TextMeshProUGUI inventoryText;
        private TextMeshProUGUI messageText;
        private float messageHideTime;

        private void Awake()
        {
            inventoryText = UiFactory.CreateText("InventoryText", transform, 26, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(-390f, 100f), new Vector2(700f, 120f));
            UiFactory.AddOutline(inventoryText);
            messageText = UiFactory.CreateText("Message", transform, 30, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(1200f, 60f));
            UiFactory.AddOutline(messageText);
            inventoryText.text = string.Empty;
            messageText.text = string.Empty;
        }

        protected override void Bind(GameObject player)
        {
            inventory = player.GetComponent<PlayerInventory>();
            notifications = player.GetComponent<PlayerNotifications>();
            inventory.Changed += RefreshInventory;
            notifications.Notified += ShowMessage;
            RefreshInventory();
        }

        protected override void Unbind()
        {
            inventory.Changed -= RefreshInventory;
            notifications.Notified -= ShowMessage;
            inventory = null;
            notifications = null;
            inventoryText.text = string.Empty;
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(messageText.text) && Time.time >= messageHideTime)
            {
                messageText.text = string.Empty;
            }
        }

        private void RefreshInventory()
        {
            builder.Clear();
            builder.Append("모아 코인 ").Append(inventory.Coins).Append('\n');
            builder.Append("모아볼 ").Append(inventory.MoaBalls).Append('\n');
            builder.Append("포션");
            int tierCount = GameConfig.Instance.potionTiers.Length;
            for (int tier = 0; tier < tierCount; tier++)
            {
                builder.Append("  F").Append(tier + 1).Append(" ×").Append(inventory.GetPotionCount(tier));
            }
            inventoryText.text = builder.ToString();
        }

        private void ShowMessage(string message)
        {
            messageText.text = message;
            messageHideTime = Time.time + MessageSeconds;
        }
    }
}
