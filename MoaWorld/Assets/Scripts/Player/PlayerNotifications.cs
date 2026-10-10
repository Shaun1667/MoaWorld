using System;
using Unity.Netcode;

namespace MoaWorld
{
    // Short Korean on-screen messages for the local player (capture results, box errors, ...).
    // Server-side game logic calls Notify and the message is shown on the owning player's screen.
    public class PlayerNotifications : NetworkBehaviour
    {
        public event Action<string> Notified;

        public void Notify(string message)
        {
            if (IsServer)
            {
                ShowRpc(message);
            }
            else
            {
                Notified?.Invoke(message);
            }
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ShowRpc(string message)
        {
            Notified?.Invoke(message);
        }
    }
}
