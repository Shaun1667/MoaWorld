using UnityEngine;

namespace MoaWorld
{
    [System.Serializable]
    public class ArmorTier
    {
        public int hpBonus;
        public int price; // TBD: price not decided in design doc, placeholder
    }

    [System.Serializable]
    public class PotionTier
    {
        public int healAmount;
        public int price; // TBD: price not decided in design doc, placeholder
    }

    // Single source of truth for every tunable number in the game.
    // Fields marked TBD are placeholders for values not yet decided in the design doc.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "MoaWorld/Game Config")]
    public class GameConfig : ScriptableObject
    {
        private static GameConfig instance;
        public static GameConfig Instance => instance != null ? instance : instance = Resources.Load<GameConfig>("GameConfig");

        [Header("Player")]
        public int playerBaseHp = 100;
        public int startingMoaBalls = 5;
        public float hpRegenDelaySeconds = 5f;   // TBD: seconds of no damage before regen starts
        public float hpRegenPerSecond = 2f;      // TBD: regen speed

        [Header("Player Movement")]
        public float playerMoveSpeed = 6f;
        public float playerTurnSpeed = 720f;     // degrees per second

        [Header("Camera")]
        public float cameraDistance = 8f;
        public float cameraMinDistance = 3f;
        public float cameraMaxDistance = 15f;
        public float cameraZoomSpeed = 4f;
        public float cameraOrbitSensitivity = 3f;
        public float cameraMinPitch = -10f;
        public float cameraMaxPitch = 70f;
        public float cameraTargetHeight = 1.5f;

        [Header("Armor (5 tiers, highest owned applies, not stacked)")]
        public ArmorTier[] armorTiers = new ArmorTier[]
        {
            new ArmorTier { hpBonus = 100, price = 100 },
            new ArmorTier { hpBonus = 250, price = 250 },
            new ArmorTier { hpBonus = 400, price = 500 },
            new ArmorTier { hpBonus = 600, price = 900 },
            new ArmorTier { hpBonus = 1000, price = 1500 },
        };

        [Header("Potion (5 tiers)")]
        public PotionTier[] potionTiers = new PotionTier[]
        {
            new PotionTier { healAmount = 100, price = 20 },
            new PotionTier { healAmount = 200, price = 35 },
            new PotionTier { healAmount = 300, price = 50 },
            new PotionTier { healAmount = 400, price = 65 },
            new PotionTier { healAmount = 500, price = 80 },
        };

        [Header("Capture")]
        public int moaBallPrice = 10; // TBD
        [Range(0f, 1f)]
        public float captureRateAtFullHp = 0.05f; // TBD: chance to catch a weak wild moa at full HP
        [Range(0f, 1f)]
        public float captureRateAtZeroHp = 0.9f;  // TBD: chance to catch at near-zero HP

        [Header("Moa Level & Stats")]
        public int moaLevelCap = 100;            // TBD
        public int ivMin = 0;                    // TBD: individual value range, placeholder
        public int ivMax = 31;                   // TBD
        public float expCurveMultiplier = 1.2f;  // TBD: exp required scales per level by this factor

        [Header("Element Affinity (Water > Fire > Grass > Water, Normal has no affinity)")]
        public float advantageDamageMultiplier = 1.5f;    // TBD
        public float disadvantageDamageMultiplier = 0.67f; // TBD

        [Header("Wild Moa (global defaults, per-species data overrides later)")]
        public float wildAggroRange = 8f;        // TBD: detection range for aggressive moa
        public float wildRespawnSeconds = 60f;    // TBD: time before a defeated wild moa reappears
        public int wildCoinDropMin = 5;           // TBD
        public int wildCoinDropMax = 15;          // TBD

        [Header("Economy (confirmed values)")]
        public int coinDespawnMinutes = 15;
        public int moaBoxHealMinutes = 10;
        public int maxPartySize = 6;
        public int storagePageSize = 30;
        public int storagePageCount = 30;

        [Header("World (confirmed values)")]
        public int dayNightCycleMinutes = 15;
        public int maxPlayers = 20;
    }
}
