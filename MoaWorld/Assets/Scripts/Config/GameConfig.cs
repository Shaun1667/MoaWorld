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
        public float cameraShoulderOffset = 0.8f; // keeps the crosshair off the player's own body

        [Header("Targeting")]
        public float targetMaxDistance = 30f;    // TBD: max distance from player to a selectable target
        public float targetAimRadius = 0.3f;     // aim assist thickness of the crosshair ray

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
        public float captureShakeSeconds = 2f;    // TBD: time inside the ball before the result
        public float ballThrowSpeed = 20f;
        public float ballMaxFlightSeconds = 3f;
        public float ballRadius = 0.15f;

        [Header("Moa Level & Stats")]
        public int moaLevelCap = 100;            // TBD
        public int ivMin = 0;                    // TBD: individual value range, placeholder
        public int ivMax = 31;                   // TBD
        public float expCurveMultiplier = 1.2f;  // TBD: exp required scales per level by this factor
        public int expBaseRequirement = 20;      // TBD: exp needed from level 1 to 2
        public int expRewardPerLevel = 10;       // TBD: exp for a final blow = this * defeated moa level

        [Header("Combat")]
        public float damageScale = 2f;           // TBD: damage = power * scale * ATK / (ATK + DEF) * affinity
        public float combatStateSeconds = 10f;   // TBD: how long an attacker keeps the player "in combat"

        [Header("Element Affinity (Water > Fire > Grass > Water, Normal has no affinity)")]
        public float advantageDamageMultiplier = 1.5f;    // TBD
        public float disadvantageDamageMultiplier = 0.67f; // TBD

        [Header("Moa Movement")]
        public float moaMoveSpeed = 7f;
        public float moaTurnSpeed = 720f;
        public float moaFollowDistance = 2.5f;   // summoned moa trails the owner at this distance
        public float moaLeashDistance = 25f;     // summoned moa teleports back beyond this distance

        [Header("Element Colors (placeholder capsule visuals)")]
        public Color normalColor = new Color(0.85f, 0.85f, 0.8f);
        public Color waterColor = new Color(0.25f, 0.5f, 1f);
        public Color fireColor = new Color(1f, 0.35f, 0.2f);
        public Color grassColor = new Color(0.3f, 0.8f, 0.3f);

        [Header("Wild Moa (global defaults, per-species data overrides later)")]
        public float wildAggroRange = 8f;        // TBD: detection range for aggressive moa
        public float wildChaseRange = 20f;       // TBD: wild moa gives up beyond this distance from its spawn point
        public float wildRespawnSeconds = 60f;    // TBD: time before a defeated wild moa reappears
        public int wildCoinDropMin = 5;           // TBD
        public int wildCoinDropMax = 15;          // TBD

        [Header("Economy (confirmed values)")]
        public int coinDespawnMinutes = 15;
        public float moaBoxHealMinutes = 10f;
        public float moaBoxInteractRange = 3f;
        public float shopInteractRange = 3f;
        public float coinPickupRadius = 1.5f;
        public int maxPartySize = 6;
        public int storagePageSize = 30;
        public int storagePageCount = 30;

        [Header("World (confirmed values)")]
        [UnityEngine.Serialization.FormerlySerializedAs("dayNightCycleMinutes")]
        public float dayNightPhaseMinutes = 15f; // day lasts this long, then night lasts this long
        public int maxPlayers = 20;

        [Header("Network")]
        public ushort gamePort = 7777;
        public ushort discoveryPort = 47777;
        public float discoveryBroadcastSeconds = 1f;
        public float discoveryRoomTimeoutSeconds = 3f;

        [Header("Save (host PC)")]
        public string saveFileName = "world.json";   // stored under Application.persistentDataPath
        public float autosaveSeconds = 60f;          // TBD: also saved when a player leaves and when the room closes

        public Color GetElementColor(MoaElement element)
        {
            switch (element)
            {
                case MoaElement.Water: return waterColor;
                case MoaElement.Fire: return fireColor;
                case MoaElement.Grass: return grassColor;
                default: return normalColor;
            }
        }
    }
}
