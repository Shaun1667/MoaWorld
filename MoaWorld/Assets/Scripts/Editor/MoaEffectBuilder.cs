using UnityEditor;
using UnityEngine;

namespace MoaWorld.EditorTools
{
    // Assigns each species' skill visuals from the effect packs in Assets/EffectAsset: a cast effect at the
    // attacker (slash, swipe, dust) and a hit effect on the target, chosen to match the element and the animal.
    // Visual only; skill numbers are unchanged. Run again after editing the table: MoaWorld > Assign Moa Effects.
    // SimpleFX is not used (its materials do not render in URP); NamuFX's flames turn pink and Explosion_01 shows hard
    // square edges at this size, so they are left out too.
    public static class MoaEffectBuilder
    {
        private const string SpeciesRoot = "Assets/Data/MoaSpecies";
        private const string Hovl = "Assets/EffectAsset/Hovl Studio/Magic effects pack/Prefabs/";
        private const string NamuSlash = "Assets/EffectAsset/NamuFX/Simple Stylized Slash/Prefabs/";
        private const string Namu = "Assets/EffectAsset/NamuFX/Stylized VFX Doodles/Prefabs/";

        private readonly struct Fx
        {
            public readonly string path;
            public readonly float scale;   // fits a ~1 m moa
            public readonly Vector3 euler; // rotation relative to the attack direction
            public readonly float offset;  // cast only: meters in front of the attacker's body center

            public Fx(string path, float scale, Vector3 euler = default, float offset = 0.6f)
            {
                this.path = path;
                this.scale = scale;
                this.euler = euler;
                this.offset = offset;
            }
        }

        // NamuFX slashes are arcs around their pivot, authored standing up; laid flat (-90 on X, so the arc opens
        // forward) and centered on the attacker they sweep across its front. Hovl slashes already lie flat in front.
        private static readonly Vector3 LayFlat = new Vector3(-90f, 0f, 0f);
        private static readonly Fx BasicSlash = new Fx(NamuSlash + "basicslash", 0.25f, LayFlat, 0f);
        private static readonly Fx FireSlash = new Fx(NamuSlash + "fireslash", 0.25f, LayFlat, 0f);
        private static readonly Fx BigFireSlash = new Fx(NamuSlash + "fireslash", 0.35f, LayFlat, 0f);
        private static readonly Fx WaterSlash = new Fx(NamuSlash + "waterslash", 0.25f, LayFlat, 0f);
        private static readonly Fx StoneSlash = new Fx(Hovl + "Slash effects/Stone slash", 0.4f);
        private static readonly Fx SnowSlash = new Fx(Hovl + "Slash effects/Snow slash", 0.4f);
        private static readonly Fx ElectroSlash = new Fx(Hovl + "Slash effects/Electro slash", 0.4f);
        private static readonly Fx DustPuff = new Fx(Hovl + "Smoke effects/Dust puff", 0.12f, default, 0.5f);
        private static readonly Fx SparksRed = new Fx(Hovl + "Sparks/Sparks explode red", 1.2f);
        private static readonly Fx SparksGreen = new Fx(Hovl + "Sparks/Sparks explode green", 1.2f);
        private static readonly Fx SparksBlue = new Fx(Hovl + "Sparks/Sparks explode blue", 1.2f);
        private static readonly Fx SparksYellow = new Fx(Hovl + "Sparks/Sparks explode yellow", 1.2f);
        private static readonly Fx SparksWhite = new Fx(Hovl + "Sparks/Sparks explode white", 1.2f);
        private static readonly Fx SparksPink = new Fx(Hovl + "Sparks/Sparks explode pink", 1.2f);
        private static readonly Fx GreenHit = new Fx(Hovl + "Hits and explosions/Green hit", 0.8f);
        private static readonly Fx HolyHit = new Fx(Hovl + "Hits and explosions/Holy hit", 0.8f);
        private static readonly Fx SnowHit = new Fx(Hovl + "Hits and explosions/Snow hit", 0.8f);
        private static readonly Fx StarHit = new Fx(Hovl + "Hits and explosions/Star hit", 0.8f);
        private static readonly Fx StonesHit = new Fx(Hovl + "Hits and explosions/Stones hit", 0.8f);
        private static readonly Fx ElectroHit = new Fx(Hovl + "Hits and explosions/Electro hit", 0.8f);
        private static readonly Fx BigFireBurst = new Fx(Namu + "AnimeExplosion", 0.6f);
        private static readonly Fx WaterSplash = new Fx(Namu + "Water_Splash", 0.9f);
        private static readonly Fx Poison = new Fx(Namu + "Poison", 0.9f);
        private static readonly Fx None = new Fx(null, 1f);

        private static readonly (string speciesId, Fx cast, Fx hit)[] Table =
        {
            // Fire
            ("LeopardCat", FireSlash, SparksRed),
            ("Badger", StoneSlash, SparksRed),
            ("RedFox", FireSlash, SparksYellow),
            ("Tiger", BigFireSlash, BigFireBurst),
            // Water
            ("Otter", WaterSlash, WaterSplash),
            ("GoldenFrog", SparksBlue, WaterSplash),
            ("WildBoar", DustPuff, WaterSplash),
            // Grass
            ("Rabbit", None, GreenHit),
            ("RoeDeer", SparksGreen, GreenHit),
            ("AsiaticBlackBear", StoneSlash, GreenHit),
            ("Marten", BasicSlash, SparksGreen),
            // Normal
            ("WaterDeer", DustPuff, StonesHit),
            ("SwallowtailButterfly", SparksPink, StarHit),
            ("Chipmunk", None, StonesHit),
            ("Goshawk", BasicSlash, HolyHit),
            ("EagleOwl", ElectroSlash, HolyHit),
            ("RedCrownedCrane", SnowSlash, SnowHit),
            ("Magpie", BasicSlash, SparksWhite),
            ("Bumblebee", SparksYellow, ElectroHit),
            ("Hedgehog", SparksWhite, StonesHit),
            ("RatSnake", None, Poison),
        };

        [MenuItem("MoaWorld/Assign Moa Effects")]
        public static void Assign()
        {
            int assigned = 0;
            foreach ((string speciesId, Fx cast, Fx hit) in Table)
            {
                var species = AssetDatabase.LoadAssetAtPath<MoaSpecies>($"{SpeciesRoot}/{speciesId}.asset");
                if (species == null)
                {
                    Debug.LogWarning($"[MoaEffectBuilder] Missing species {speciesId}");
                    continue;
                }
                species.castEffect = Load(cast.path);
                species.castEffectScale = cast.scale;
                species.castEffectEuler = cast.euler;
                species.castEffectOffset = cast.offset;
                species.hitEffect = Load(hit.path);
                species.hitEffectScale = hit.scale;
                EditorUtility.SetDirty(species);
                assigned++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoaEffectBuilder] Assigned effects to {assigned}/{Table.Length} species.");
        }

        private static GameObject Load(string path)
        {
            if (path == null)
            {
                return null;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[MoaEffectBuilder] Missing effect {path}");
            }
            return prefab;
        }
    }
}
