using System;
using Unity.Netcode;

namespace MoaWorld
{
    // Coins, moa balls and potions. Only the server changes these; the owning client gets a synced copy.
    public class PlayerInventory : NetworkBehaviour
    {
        private readonly NetworkVariable<int> coins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<int> moaBalls = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<bool> hasEverCaptured = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Owner);
        private NetworkList<int> potions;

        public int Coins => coins.Value;
        public int MoaBalls => moaBalls.Value;
        public bool HasEverCaptured => hasEverCaptured.Value;

        public event Action Changed;

        private void Awake()
        {
            // NetworkList must be created before the object spawns.
            potions = new NetworkList<int>(null, NetworkVariableReadPermission.Owner);
        }

        public override void OnNetworkSpawn()
        {
            coins.OnValueChanged += OnIntChanged;
            moaBalls.OnValueChanged += OnIntChanged;
            hasEverCaptured.OnValueChanged += OnBoolChanged;
            potions.OnListChanged += OnPotionsChanged;
            if (IsServer)
            {
                EnsurePotionSlots();
            }
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            coins.OnValueChanged -= OnIntChanged;
            moaBalls.OnValueChanged -= OnIntChanged;
            hasEverCaptured.OnValueChanged -= OnBoolChanged;
            potions.OnListChanged -= OnPotionsChanged;
        }

        private void OnIntChanged(int previous, int current) => Changed?.Invoke();
        private void OnBoolChanged(bool previous, bool current) => Changed?.Invoke();
        private void OnPotionsChanged(NetworkListEvent<int> change) => Changed?.Invoke();

        private void EnsurePotionSlots()
        {
            int tierCount = GameConfig.Instance.potionTiers.Length;
            while (potions.Count < tierCount)
            {
                potions.Add(0);
            }
        }

        public int GetPotionCount(int tier)
        {
            return potions != null && tier >= 0 && tier < potions.Count ? potions[tier] : 0;
        }

        // Everything below is server only.

        public void AddCoins(int amount)
        {
            coins.Value += amount;
        }

        public bool TrySpendCoins(int amount)
        {
            if (coins.Value < amount)
            {
                return false;
            }
            coins.Value -= amount;
            return true;
        }

        public void AddMoaBalls(int amount)
        {
            moaBalls.Value += amount;
        }

        public bool TryUseMoaBall()
        {
            if (moaBalls.Value <= 0)
            {
                return false;
            }
            moaBalls.Value--;
            return true;
        }

        public void AddPotion(int tier)
        {
            EnsurePotionSlots();
            potions[tier]++;
        }

        public bool TryUsePotion(int tier)
        {
            if (GetPotionCount(tier) <= 0)
            {
                return false;
            }
            potions[tier]--;
            return true;
        }

        public void MarkCaptured()
        {
            hasEverCaptured.Value = true;
        }

        public void WriteSave(PlayerSaveData data)
        {
            data.coins = coins.Value;
            data.moaBalls = moaBalls.Value;
            data.hasEverCaptured = hasEverCaptured.Value;
            data.potions = new int[potions.Count];
            for (int i = 0; i < potions.Count; i++)
            {
                data.potions[i] = potions[i];
            }
        }

        public void ReadSave(PlayerSaveData data)
        {
            coins.Value = data.coins;
            moaBalls.Value = data.moaBalls;
            hasEverCaptured.Value = data.hasEverCaptured;
            EnsurePotionSlots();
            for (int i = 0; i < potions.Count; i++)
            {
                potions[i] = data.potions != null && i < data.potions.Length ? data.potions[i] : 0;
            }
        }
    }
}
