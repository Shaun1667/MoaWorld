using UnityEngine;
using UnityEngine.UI;

namespace MoaWorld
{
    public class PlayerHud : MonoBehaviour
    {
        private const float MessageSeconds = 2.5f;

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerNotifications notifications;
        [SerializeField] private Text moaBallText;
        [SerializeField] private Text messageText;

        private float messageHideTime;

        private void OnEnable()
        {
            inventory.Changed += RefreshInventory;
            notifications.Notified += ShowMessage;
            RefreshInventory();
            messageText.text = string.Empty;
        }

        private void OnDisable()
        {
            inventory.Changed -= RefreshInventory;
            notifications.Notified -= ShowMessage;
        }

        private void Update()
        {
            if (messageText.text.Length > 0 && Time.time >= messageHideTime)
            {
                messageText.text = string.Empty;
            }
        }

        private void RefreshInventory()
        {
            moaBallText.text = $"모아볼 {inventory.MoaBalls}";
        }

        private void ShowMessage(string message)
        {
            messageText.text = message;
            messageHideTime = Time.time + MessageSeconds;
        }
    }
}
