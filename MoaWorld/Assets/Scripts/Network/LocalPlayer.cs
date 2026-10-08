using System;
using UnityEngine;

namespace MoaWorld
{
    // The player object controlled on this machine. Camera and HUD bind to it when it spawns.
    public static class LocalPlayer
    {
        public static GameObject Current { get; private set; }

        public static event Action<GameObject> Changed;

        public static void Set(GameObject player)
        {
            Current = player;
            Changed?.Invoke(player);
        }

        public static void Clear(GameObject player)
        {
            if (Current == player)
            {
                Set(null);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
            Changed = null;
        }
    }
}
