using System;
using UnityEngine;

namespace MoaWorld
{
    // Short Korean on-screen messages for the local player (capture results, box errors, ...).
    public class PlayerNotifications : MonoBehaviour
    {
        public event Action<string> Notified;

        public void Notify(string message)
        {
            Notified?.Invoke(message);
        }
    }
}
