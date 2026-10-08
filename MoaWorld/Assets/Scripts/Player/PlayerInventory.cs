using System;
using UnityEngine;

namespace MoaWorld
{
    // Server-owned once networking is added: only the host changes these values.
    public class PlayerInventory : MonoBehaviour
    {
        [NonSerialized] private int[] potions; // NonSerialized: Unity would otherwise hand us an empty array

        public int Coins { get; private set; }
        public int MoaBalls { get; private set; }
        public bool HasEverCaptured { get; private set; }

        // Lazy so UI that subscribes before this component's Awake can still read counts.
        private int[] Potions => potions ?? (potions = new int[GameConfig.Instance.potionTiers.Length]);

        public event Action Changed;

        public int GetPotionCount(int tier)
        {
            return Potions[tier];
        }

        public void AddCoins(int amount)
        {
            Coins += amount;
            Changed?.Invoke();
        }

        public bool TrySpendCoins(int amount)
        {
            if (Coins < amount)
            {
                return false;
            }
            Coins -= amount;
            Changed?.Invoke();
            return true;
        }

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

        public void AddPotion(int tier)
        {
            Potions[tier]++;
            Changed?.Invoke();
        }

        public bool TryUsePotion(int tier)
        {
            if (Potions[tier] <= 0)
            {
                return false;
            }
            Potions[tier]--;
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
