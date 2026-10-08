using System;
using UnityEngine;

namespace MoaWorld
{
    // Server-owned once networking is added: only the host changes these values.
    public class PlayerInventory : MonoBehaviour
    {
        public int MoaBalls { get; private set; }
        public bool HasEverCaptured { get; private set; }

        public event Action Changed;

        public void AddMoaBalls(int amount)
        {
            MoaBalls += amount;
            Changed?.Invoke();
        }

        public bool TryUseMoaBall()
        {
            if (MoaBalls <= 0)
            {
                return false;
            }
            MoaBalls--;
            Changed?.Invoke();
            return true;
        }

        public void MarkCaptured()
        {
            HasEverCaptured = true;
            Changed?.Invoke();
        }
    }
}
