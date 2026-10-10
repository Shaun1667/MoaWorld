using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Player HP. The server applies damage, regen and potions; everyone sees the synced value
    // (other players need it for the PvP target bar).
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerHealth : Combatant
    {
        private const int NoArmor = -1;

        private static readonly List<PlayerHealth> active = new List<PlayerHealth>();
        public static IReadOnlyList<PlayerHealth> Active => active;

        private readonly NetworkVariable<float> hp = new NetworkVariable<float>();
        private readonly NetworkVariable<int> armorTier = new NetworkVariable<int>(NoArmor);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            active.Clear();
        }

        private PlayerMovement movement;
        private Vector3 initialPosition;
        private float lastDamageTime = float.NegativeInfinity;

        public float CurrentHp => hp.Value;
        public int MaxHp => CalculateMaxHp(armorTier.Value);
        public int HighestArmorTier => armorTier.Value;

        // Players respawn instantly with no penalty, so they never stay down.
        public override bool IsAlive => true;
        public override int Defense => 0;
        public override MoaElement? Element => null;
        protected override bool IsDepleted => CurrentHp <= 0f;

        private Vector3 SpawnPosition => MoaBox.Instance != null ? MoaBox.Instance.SpawnPosition : initialPosition;

        public event Action<float, int> HpChanged;

        protected override void Awake()
        {
            base.Awake();
            OwnerRoot = transform;
            movement = GetComponent<PlayerMovement>();
            initialPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            hp.OnValueChanged += OnHpValueChanged;
            armorTier.OnValueChanged += OnArmorChanged;
            if (IsServer)
            {
                hp.Value = MaxHp;
            }
            active.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            hp.OnValueChanged -= OnHpValueChanged;
            armorTier.OnValueChanged -= OnArmorChanged;
            active.Remove(this);
        }

        private void OnHpValueChanged(float previous, float current) => HpChanged?.Invoke(current, MaxHp);
        private void OnArmorChanged(int previous, int current) => HpChanged?.Invoke(CurrentHp, MaxHp);

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }
            GameConfig config = GameConfig.Instance;
            if (CurrentHp < MaxHp && Time.time - lastDamageTime >= config.hpRegenDelaySeconds)
            {
                hp.Value = Mathf.Min(MaxHp, CurrentHp + config.hpRegenPerSecond * Time.deltaTime);
            }
        }

        // Server only.
        public void Heal(float amount)
        {
            hp.Value = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        // Server only. Assumption (design TBD): only the highest owned armor tier applies, bonuses do not stack.
        public void SetHighestArmorTier(int tierIndex)
        {
            armorTier.Value = Mathf.Max(armorTier.Value, tierIndex);
            hp.Value = Mathf.Min(CurrentHp, MaxHp);
        }

        protected override void ApplyDamage(float amount)
        {
            lastDamageTime = Time.time;
            hp.Value = Mathf.Max(0f, CurrentHp - amount);
        }

        protected override void OnDepleted(Combatant attacker)
        {
            lastDamageTime = float.NegativeInfinity;
            hp.Value = MaxHp;
            RespawnRpc(SpawnPosition);
        }

        // Movement is owner-authoritative, so the owner performs the respawn teleport.
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void RespawnRpc(Vector3 position)
        {
            movement.Teleport(position);
        }

        private static int CalculateMaxHp(int tier)
        {
            GameConfig config = GameConfig.Instance;
            int armorBonus = tier == NoArmor ? 0 : config.armorTiers[tier].hpBonus;
            return config.playerBaseHp + armorBonus;
        }

        public void WriteSave(PlayerSaveData data)
        {
            data.armorTier = armorTier.Value;
        }

        public void ReadSave(PlayerSaveData data)
        {
            armorTier.Value = Mathf.Clamp(data.armorTier, NoArmor, GameConfig.Instance.armorTiers.Length - 1);
            hp.Value = MaxHp;
        }
    }
}
