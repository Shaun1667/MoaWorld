using System;
using UnityEngine;

namespace MoaWorld
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerHealth : MonoBehaviour
    {
        private const int NoArmor = -1;

        // Moa Box spawn point later; falls back to the starting position.
        [SerializeField] private Transform respawnPoint;

        private PlayerMovement movement;
        private Vector3 initialPosition;
        private float lastDamageTime = float.NegativeInfinity;

        public float CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public int HighestArmorTier { get; private set; } = NoArmor;

        public event Action<float, int> HpChanged;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            initialPosition = transform.position;
            RecalculateMaxHp();
            SetHp(MaxHp);
        }

        private void Update()
        {
            GameConfig config = GameConfig.Instance;
            if (CurrentHp < MaxHp && Time.time - lastDamageTime >= config.hpRegenDelaySeconds)
            {
                SetHp(Mathf.Min(MaxHp, CurrentHp + config.hpRegenPerSecond * Time.deltaTime));
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            lastDamageTime = Time.time;
            SetHp(Mathf.Max(0f, CurrentHp - amount));
            if (CurrentHp <= 0f)
            {
                Respawn();
            }
        }

        public void Heal(float amount)
        {
            SetHp(Mathf.Min(MaxHp, CurrentHp + amount));
        }

        // Assumption (design TBD): only the highest owned armor tier applies, bonuses do not stack.
        public void SetHighestArmorTier(int tierIndex)
        {
            HighestArmorTier = Mathf.Max(HighestArmorTier, tierIndex);
            RecalculateMaxHp();
            SetHp(Mathf.Min(CurrentHp, MaxHp));
        }

        private void RecalculateMaxHp()
        {
            GameConfig config = GameConfig.Instance;
            int armorBonus = HighestArmorTier == NoArmor ? 0 : config.armorTiers[HighestArmorTier].hpBonus;
            MaxHp = config.playerBaseHp + armorBonus;
        }

        private void Respawn()
        {
            movement.Teleport(respawnPoint != null ? respawnPoint.position : initialPosition);
            lastDamageTime = float.NegativeInfinity;
            SetHp(MaxHp);
        }

        private void SetHp(float value)
        {
            CurrentHp = value;
            HpChanged?.Invoke(CurrentHp, MaxHp);
        }
    }
}
