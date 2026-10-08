using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoaWorld
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerHealth : Combatant
    {
        private const int NoArmor = -1;

        private static readonly List<PlayerHealth> active = new List<PlayerHealth>();
        public static IReadOnlyList<PlayerHealth> Active => active;

        private PlayerMovement movement;
        private Vector3 initialPosition;
        private float lastDamageTime = float.NegativeInfinity;

        public float CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public int HighestArmorTier { get; private set; } = NoArmor;

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
            RecalculateMaxHp();
            SetHp(MaxHp);
        }

        // The Moa Box doubles as the start point.
        private void Start()
        {
            movement.Teleport(SpawnPosition);
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        private void Update()
        {
            GameConfig config = GameConfig.Instance;
            if (CurrentHp < MaxHp && Time.time - lastDamageTime >= config.hpRegenDelaySeconds)
            {
                SetHp(Mathf.Min(MaxHp, CurrentHp + config.hpRegenPerSecond * Time.deltaTime));
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

        protected override void ApplyDamage(float amount)
        {
            lastDamageTime = Time.time;
            SetHp(Mathf.Max(0f, CurrentHp - amount));
        }

        protected override void OnDepleted(Combatant attacker)
        {
            movement.Teleport(SpawnPosition);
            lastDamageTime = float.NegativeInfinity;
            SetHp(MaxHp);
        }

        private void RecalculateMaxHp()
        {
            GameConfig config = GameConfig.Instance;
            int armorBonus = HighestArmorTier == NoArmor ? 0 : config.armorTiers[HighestArmorTier].hpBonus;
            MaxHp = config.playerBaseHp + armorBonus;
        }

        private void SetHp(float value)
        {
            CurrentHp = value;
            HpChanged?.Invoke(CurrentHp, MaxHp);
        }
    }
}
