using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoaWorld.EditorTools
{
    // Builds the world around the central village from the EnvironmentAsset packs: four biomes with ground patches,
    // trees, rocks and plants, colliders on the existing hills and mountains, an invisible world edge, and wild moa
    // spawners per biome. Everything generated lives under the "World" root and is rebuilt from scratch on each run
    // with a fixed seed, so the result is the same every time. Run: MoaWorld > Build World.
    public static class WorldBuilder
    {
        private const int Seed = 1667;
        private const string RootName = "World";
        private const string TerrainGroupName = "Group Terrain";
        private const string TestSpawnerName = "MoaSpawner_Test";
        private const string GeneratedRoot = "Assets/World";
        private const string TerrainMeshRoot = GeneratedRoot + "/Generated/Terrain";
        private const float TileBorderEase = 0.12f; // fraction of the tile width over which its border drops to the ground
        private const string WildMoaPrefabPath = "Assets/Prefabs/WildMoa.prefab";
        private const string SpeciesRoot = "Assets/Data/MoaSpecies";

        private const string Env = "Assets/EnvironmentAsset/";
        private const string Embers = Env + "EmbersStorm -  Free Nature Pack/Prefabs/";
        private const string Happy = Env + "Happy Little Trees - Free nature pack by Nebula/Prefabs/";
        private const string Moose = Env + "TooMooseGames/Nature Biomes Pack - Low Poly/Prefabs";
        private const string PolyRocks = Env + "PolyOne/Rocks Stylized/Prefabs";

        private static readonly Vector2 VillageCenter = new Vector2(-57.7f, -20.4f); // Moa Box and shop
        private const float VillageClearRadius = 30f;
        private const float WorldEdge = 480f;      // invisible walls; the ground plane ends at 500
        private const float RayHeight = 200f;
        private const float MinGroundNormalY = 0.75f;
        private const float TreeTrunkRadius = 0.45f; // meters, regardless of the tree's scale
        private const float TreeTrunkHeight = 5f;
        private const float SmallDecorHeight = 1.5f; // decor below this casts no shadow

        private enum Kind
        {
            Tree,  // collider only on the trunk, so players walk under the canopy
            Solid, // mesh collider (rocks, logs, stumps)
            Decor, // no collider (grass, flowers, bushes)
        }

        private class PropSet
        {
            public string folder;
            public string filter; // regex on the prefab name
            public int count;
            public float minScale;
            public float maxScale;
            public Kind kind;
            public float spacing;        // min distance to other trees/solids, 0 = no check
            public float minRadius = 0f; // fraction of the biome radius
            public float maxRadius = 1f;

            public PropSet(string folder, string filter, int count, float minScale, float maxScale, Kind kind, float spacing = 0f)
            {
                this.folder = folder;
                this.filter = filter;
                this.count = count;
                this.minScale = minScale;
                this.maxScale = maxScale;
                this.kind = kind;
                this.spacing = spacing;
            }

            public PropSet Ring(float min, float max)
            {
                minRadius = min;
                maxRadius = max;
                return this;
            }
        }

        private class Patch
        {
            public string name;
            public float radius;
            public float height;
            public Color color;
            public bool water;

            public Patch(string name, float radius, float height, Color color, bool water = false)
            {
                this.name = name;
                this.radius = radius;
                this.height = height;
                this.color = color;
                this.water = water;
            }
        }

        private class SpawnerSpec
        {
            public Vector2 position;
            public float radius;
            public int maxAlive;

            public SpawnerSpec(float x, float z, float radius, int maxAlive)
            {
                position = new Vector2(x, z);
                this.radius = radius;
                this.maxAlive = maxAlive;
            }
        }

        private class Biome
        {
            public string name;
            public Vector2 center;
            public float radius;
            public Patch[] patches = new Patch[0];
            public PropSet[] props;
            public SpawnerSpec[] spawners;
            public int minLevel; // TBD: level ranges per biome
            public int maxLevel;
            public (string speciesId, float weight)[] species;
        }

        // Biome centers are world XZ around the village.
        private static Biome[] CreateBiomes()
        {
            return new[]
            {
                new Biome
                {
                    name = "Meadow", // around the village: starter area
                    center = VillageCenter,
                    radius = 85f,
                    props = new[]
                    {
                        new PropSet(Happy + "Trees/Birch", "^Birch$", 10, 0.9f, 1.2f, Kind.Tree, 6f).Ring(0.45f, 1f),
                        new PropSet(Happy + "Trees/Poplar", "^Poplar$", 10, 0.8f, 1.1f, Kind.Tree, 6f).Ring(0.45f, 1f),
                        new PropSet(Happy + "Trees/Sapling", ".", 15, 1f, 1.3f, Kind.Decor),
                        new PropSet(Embers + "Trees/Bush", ".", 25, 0.3f, 0.45f, Kind.Decor),
                        new PropSet(Happy + "Rocks", ".", 12, 0.8f, 1.4f, Kind.Solid, 4f),
                        new PropSet(Embers + "Plants/Flowers", ".", 260, 0.5f, 0.8f, Kind.Decor),
                        new PropSet(Happy + "Foilage/Flowers", ".", 120, 1.5f, 2.5f, Kind.Decor),
                        new PropSet(Moose, "^Grass 1 G[12]$", 220, 1f, 1.6f, Kind.Decor),
                    },
                    spawners = new[]
                    {
                        new SpawnerSpec(-57.7f, 35f, 22f, 6),
                        new SpawnerSpec(-118f, -5f, 25f, 6),
                    },
                    minLevel = 1,
                    maxLevel = 5,
                    species = new[] { ("Rabbit", 3f), ("Chipmunk", 3f), ("Magpie", 2f), ("SwallowtailButterfly", 2f), ("Bumblebee", 2f), ("Hedgehog", 1f) },
                },
                new Biome
                {
                    name = "Forest", // north
                    center = new Vector2(-40f, 125f),
                    radius = 90f,
                    patches = new[] { new Patch("ForestFloor", 98f, 0.04f, new Color(0.36f, 0.56f, 0.27f)) },
                    props = new[]
                    {
                        new PropSet(Embers + "Trees/Oak", ".", 55, 0.3f, 0.42f, Kind.Tree, 7f),
                        new PropSet(Embers + "Trees/Pine tree", ".", 45, 0.32f, 0.45f, Kind.Tree, 6f),
                        new PropSet(Happy + "Trees/Oak", "^Oak$", 30, 0.8f, 1.1f, Kind.Tree, 7f),
                        new PropSet(Happy + "Trees/Birch", "^Birch$", 35, 0.9f, 1.2f, Kind.Tree, 4f),
                        new PropSet(Happy + "Trees/Spruce", "^Spruce_008$", 30, 0.8f, 1.1f, Kind.Tree, 5f),
                        new PropSet(Happy + "Trees/Ash", "^Ash$", 20, 0.8f, 1f, Kind.Tree, 6f),
                        new PropSet(Moose, "^Tree [2-6] G[12]$", 35, 1f, 1.4f, Kind.Tree, 5f),
                        new PropSet(Happy + "Trees", "Stump", 20, 1f, 1.2f, Kind.Solid, 2f),
                        new PropSet(Moose, "^Log [1-5] Moss$", 20, 1f, 1.2f, Kind.Solid, 3f),
                        new PropSet(Moose, "^Rock [1-6] Moss$", 20, 0.8f, 1.3f, Kind.Solid, 3f),
                        new PropSet(Embers + "Trees/Bush", ".", 50, 0.35f, 0.55f, Kind.Decor),
                        new PropSet(Happy + "Trees/Bush", "^Bush_001$", 20, 0.5f, 0.8f, Kind.Decor),
                        new PropSet(Happy + "Trees", "Clutter", 25, 1f, 1.2f, Kind.Decor),
                        new PropSet(Embers + "Plants/Mushrooms", ".", 40, 0.6f, 1f, Kind.Decor),
                        new PropSet(Moose, "^Mushroom 1 (Blue|Green)$", 15, 0.6f, 0.9f, Kind.Decor),
                        new PropSet(Moose, "^Mushroom 1 (Red|Brown|White)$", 25, 0.6f, 1f, Kind.Decor),
                        new PropSet(Embers + "Plants/Grass", ".", 140, 0.35f, 0.5f, Kind.Decor),
                        new PropSet(Embers + "Plants/Plant", ".", 70, 0.25f, 0.4f, Kind.Decor),
                        new PropSet(Embers + "Plants/Flowers", ".", 60, 0.5f, 0.8f, Kind.Decor),
                    },
                    spawners = new[]
                    {
                        new SpawnerSpec(-40f, 105f, 30f, 7),
                        new SpawnerSpec(-95f, 160f, 25f, 6),
                        new SpawnerSpec(15f, 165f, 25f, 6),
                    },
                    minLevel = 3,
                    maxLevel = 10,
                    species = new[] { ("RoeDeer", 3f), ("Marten", 2f), ("Hedgehog", 2f), ("Chipmunk", 1f), ("EagleOwl", 1f), ("AsiaticBlackBear", 1f), ("Rabbit", 1f) },
                },
                new Biome
                {
                    name = "Lakeside", // west: shallow lake (walkable, no swimming) with sandy shore
                    center = new Vector2(-215f, 90f),
                    radius = 85f,
                    patches = new[]
                    {
                        new Patch("LakeShore", 52f, 0.06f, new Color(0.88f, 0.82f, 0.6f)),
                        new Patch("LakeWater", 42f, 0.16f, new Color(0.25f, 0.55f, 0.82f, 0.72f), water: true),
                    },
                    props = new[]
                    {
                        new PropSet(Happy + "Trees/Poplar", "^Poplar$", 25, 0.8f, 1.1f, Kind.Tree, 4f).Ring(0.62f, 1f),
                        new PropSet(Happy + "Trees/Birch", "^Birch$", 30, 0.9f, 1.2f, Kind.Tree, 4f).Ring(0.62f, 1f),
                        new PropSet(Embers + "Trees/Oak", ".", 15, 0.26f, 0.34f, Kind.Tree, 7f).Ring(0.65f, 1f),
                        new PropSet(Moose, "^Tree [2-6] G[12]$", 25, 1f, 1.3f, Kind.Tree, 5f).Ring(0.62f, 1f),
                        new PropSet(Moose, "^Rock [1-6]$", 25, 0.8f, 1.5f, Kind.Solid, 3f).Ring(0.5f, 1f),
                        new PropSet(Moose, "^Log [1-5]$", 10, 1f, 1.2f, Kind.Solid, 3f).Ring(0.6f, 1f),
                        new PropSet(Embers + "Trees/Bush", ".", 35, 0.35f, 0.5f, Kind.Decor).Ring(0.62f, 1f),
                        // Reeds along the waterline.
                        new PropSet(Embers + "Plants/Grass", ".", 120, 0.35f, 0.55f, Kind.Decor).Ring(0.42f, 0.62f),
                        new PropSet(Embers + "Plants/Plant", ".", 40, 0.25f, 0.35f, Kind.Decor).Ring(0.45f, 0.65f),
                        new PropSet(Moose, "^Grass 1 G[12]$", 120, 1f, 1.5f, Kind.Decor).Ring(0.6f, 1f),
                        new PropSet(Embers + "Plants/Flowers", ".", 60, 0.5f, 0.8f, Kind.Decor).Ring(0.62f, 1f),
                    },
                    spawners = new[]
                    {
                        new SpawnerSpec(-215f, 90f, 32f, 8),
                        new SpawnerSpec(-265f, 135f, 22f, 5),
                        new SpawnerSpec(-165f, 55f, 22f, 5),
                    },
                    minLevel = 5,
                    maxLevel = 12,
                    species = new[] { ("Otter", 3f), ("GoldenFrog", 3f), ("RedCrownedCrane", 2f), ("WaterDeer", 2f), ("WildBoar", 2f) },
                },
                new Biome
                {
                    name = "Badlands", // east: dry sand, cacti and dead trees
                    center = new Vector2(135f, 100f),
                    radius = 90f,
                    patches = new[] { new Patch("BadlandsSand", 98f, 0.05f, new Color(0.86f, 0.74f, 0.5f)) },
                    props = new[]
                    {
                        new PropSet(Moose, "^Cactus [1-3] (G1|G2|O1)$", 90, 1f, 1.4f, Kind.Tree, 4f),
                        new PropSet(Embers + "Trees/Dead Trees", "^dead tree", 25, 0.22f, 0.35f, Kind.Tree, 8f),
                        new PropSet(Happy + "Trees", "Dead", 20, 0.8f, 1f, Kind.Tree, 5f),
                        new PropSet(Moose, "^Rock [1-6] Sand$", 50, 1f, 2f, Kind.Solid, 4f),
                        new PropSet(Embers + "Rocks", ".", 25, 0.3f, 0.55f, Kind.Solid, 6f),
                        new PropSet(Embers + "Trees/Dead Trees", "^Dead bush", 35, 0.8f, 1.2f, Kind.Decor),
                        new PropSet(Moose, "^Tumbleweed", 25, 1f, 1.4f, Kind.Decor),
                        new PropSet(Moose, "^Tree Branch [12] B", 20, 1f, 1.3f, Kind.Decor),
                        new PropSet(Moose, "^Grass 1 O[12]$", 140, 1f, 1.6f, Kind.Decor),
                    },
                    spawners = new[]
                    {
                        new SpawnerSpec(135f, 100f, 30f, 7),
                        new SpawnerSpec(180f, 150f, 22f, 6),
                        new SpawnerSpec(95f, 60f, 22f, 6),
                    },
                    minLevel = 8,
                    maxLevel = 16,
                    species = new[] { ("LeopardCat", 3f), ("RedFox", 3f), ("Badger", 2f), ("RatSnake", 2f), ("Bumblebee", 1f) },
                },
                new Biome
                {
                    name = "Mountains", // south: the existing hills, cliffs and mountains
                    center = new Vector2(-46f, -165f),
                    radius = 150f,
                    props = new[]
                    {
                        new PropSet(Embers + "Trees/Pine tree", ".", 70, 0.3f, 0.45f, Kind.Tree, 6f),
                        new PropSet(Happy + "Trees/Spruce", "^Spruce_008$", 40, 0.8f, 1.1f, Kind.Tree, 5f),
                        new PropSet(Embers + "Trees/Dead Trees", "^dead tree", 15, 0.22f, 0.32f, Kind.Tree, 8f),
                        new PropSet(Embers + "Rocks", ".", 55, 0.3f, 0.65f, Kind.Solid, 6f),
                        new PropSet(PolyRocks, "SM_Rocks_(03|04|05|10|11)", 55, 1.5f, 3f, Kind.Solid, 4f),
                        new PropSet(Happy + "Rocks", ".", 40, 1.5f, 3f, Kind.Solid, 4f),
                        new PropSet(Embers + "Plants/Grass", ".", 80, 0.35f, 0.5f, Kind.Decor),
                        new PropSet(Moose, "^Grass 1 G2$", 100, 1f, 1.5f, Kind.Decor),
                    },
                    spawners = new[]
                    {
                        new SpawnerSpec(-94f, -159f, 30f, 6),
                        new SpawnerSpec(-5f, -162f, 30f, 6),
                        new SpawnerSpec(23f, -113f, 25f, 5),
                        new SpawnerSpec(-88f, -222f, 25f, 5),
                    },
                    minLevel = 12,
                    maxLevel = 22,
                    species = new[] { ("Tiger", 2f), ("Goshawk", 2f), ("AsiaticBlackBear", 2f), ("EagleOwl", 1f), ("Badger", 1f), ("RatSnake", 1f), ("WildBoar", 1f) },
                },
            };
        }

        private static readonly Dictionary<string, List<GameObject>> prefabCache = new Dictionary<string, List<GameObject>>();
        private static System.Random random;
        private static readonly Dictionary<(Material, Kind), Material> foliageCache = new Dictionary<(Material, Kind), Material>();
        private const string FoliageShader = "MoaWorld/Stylized Foliage";
        private const float TreeWind = 0.5f;  // sway grows with height, so tall trees need less
        private const float DecorWind = 8f;   // grass and flowers are short and sway visibly

        // Swaps the asset pack material for a wind-swaying copy (one per source material and kind).
        private static void ApplyFoliage(GameObject instance, Kind kind)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        materials[i] = GetFoliageMaterial(materials[i], kind);
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material GetFoliageMaterial(Material source, Kind kind)
        {
            if (foliageCache.TryGetValue((source, kind), out Material cached))
            {
                return cached;
            }
            EnsureFolder($"{GeneratedRoot}/Materials/Foliage");
            string path = $"{GeneratedRoot}/Materials/Foliage/{source.name}_{kind}.mat";
            Shader shader = Shader.Find(FoliageShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (source.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            }
            if (source.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            }
            material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2f);
            material.SetFloat("_WindStrength", kind == Kind.Tree ? TreeWind : DecorWind);
            EditorUtility.SetDirty(material);
            foliageCache[(source, kind)] = material;
            return material;
        }

        [MenuItem("MoaWorld/Build World")]
        public static void Build()
        {
            random = new System.Random(Seed);
            prefabCache.Clear();
            foliageCache.Clear();
            EnsureFolder(GeneratedRoot + "/Generated");
            EnsureFolder(GeneratedRoot + "/Materials");

            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }
            GameObject testSpawner = GameObject.Find(TestSpawnerName);
            if (testSpawner != null)
            {
                Object.DestroyImmediate(testSpawner);
            }

            PrepareTerrain();

            Transform root = new GameObject(RootName).transform;
            Transform spawnerRoot = NewChild(root, "Spawners");
            BuildWorldEdge(NewChild(root, "WorldEdge"));

            int propCount = 0;
            foreach (Biome biome in CreateBiomes())
            {
                Transform biomeRoot = NewChild(root, biome.name);
                foreach (Patch patch in biome.patches)
                {
                    BuildPatch(biomeRoot, biome, patch);
                }
                // Colliders of earlier props must exist before later ones are placed.
                Physics.SyncTransforms();
                var occupied = new List<(Vector2 position, float spacing)>();
                foreach (PropSet set in biome.props)
                {
                    propCount += Scatter(biomeRoot, biome, set, occupied);
                }
                for (int i = 0; i < biome.spawners.Length; i++)
                {
                    BuildSpawner(spawnerRoot, biome, biome.spawners[i], i);
                }
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log($"[WorldBuilder] Built {propCount} props and {spawnerRoot.childCount} spawners.");
        }

        // ---------- terrain ----------

        // The hills, cliffs and mountains already in the scene have no colliders; players would walk through them.
        // The hills, cliffs and mountains already in the scene are square tiles whose borders stand above the ground
        // plane and end in a vertical wall, which looks like a floating slab. Each tile gets a copy of its mesh with
        // the border eased down to the ground, plus a collider (they had none, so players walked through them).
        private static void PrepareTerrain()
        {
            GameObject group = GameObject.Find(TerrainGroupName);
            if (group == null)
            {
                return;
            }
            EnsureFolder(TerrainMeshRoot);
            int index = 0;
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>())
            {
                // Always start from the asset pack's mesh so running again does not flatten the edges twice.
                var original = PrefabUtility.GetCorrespondingObjectFromSource(filter);
                Mesh source = original != null ? original.sharedMesh : filter.sharedMesh;
                Mesh eased = SaveAsset(EaseTileBorder(source), $"{TerrainMeshRoot}/{filter.name}_{index++}.asset");
                filter.sharedMesh = eased;

                var collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                }
                collider.sharedMesh = eased;
            }
            Physics.SyncTransforms();
        }

        private static Mesh EaseTileBorder(Mesh source)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name + "_Eased";
            Bounds bounds = source.bounds;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float toEdgeX = Mathf.Min(v.x - bounds.min.x, bounds.max.x - v.x) / bounds.size.x;
                float toEdgeZ = Mathf.Min(v.z - bounds.min.z, bounds.max.z - v.z) / bounds.size.z;
                float t = Mathf.Clamp01(Mathf.Min(toEdgeX, toEdgeZ) / TileBorderEase);
                v.y *= t * t * (3f - 2f * t); // smoothstep: ground level at the border, untouched inside
                vertices[i] = v;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildWorldEdge(Transform parent)
        {
            const float height = 60f;
            const float thickness = 2f;
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                var wall = new GameObject($"Edge{i}");
                wall.transform.SetParent(parent, false);
                wall.transform.position = alongX ? new Vector3(0f, height / 2f, sign * WorldEdge) : new Vector3(sign * WorldEdge, height / 2f, 0f);
                var box = wall.AddComponent<BoxCollider>();
                box.size = alongX ? new Vector3(WorldEdge * 2f, height, thickness) : new Vector3(thickness, height, WorldEdge * 2f);
            }
        }

        // ---------- ground patches ----------

        private static void BuildPatch(Transform parent, Biome biome, Patch patch)
        {
            var go = new GameObject(patch.name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(biome.center.x, patch.height, biome.center.y);

            Mesh mesh = SaveAsset(CreateDisc(patch.radius), $"{GeneratedRoot}/Generated/{patch.name}.asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(patch.name, patch.color, patch.water);
            if (patch.water)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                // Walkable, slightly above the base plane so it does not z-fight with it.
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // Flat disc with a wavy edge so patches do not look like perfect circles.
        private static Mesh CreateDisc(float radius)
        {
            const int segments = 128;
            float phaseA = Range(0f, Mathf.PI * 2f);
            float phaseB = Range(0f, Mathf.PI * 2f);
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = Vector2.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float r = radius * (1f + 0.08f * Mathf.Sin(3f * angle + phaseA) + 0.04f * Mathf.Sin(7f * angle + phaseB));
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                uvs[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].z) * 0.1f;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i == segments - 1 ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Ground patches use the stylized ground shader (noise-varied shades of the patch color); water stays URP Lit.
        private static Material GetMaterial(string name, Color color, bool transparent)
        {
            string path = $"{GeneratedRoot}/Materials/{name}.mat";
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Lit" : "MoaWorld/Stylized Ground");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (!transparent)
            {
                material.SetColor("_ColorA", color * 0.9f);
                material.SetColor("_ColorB", color * 1.08f);
                material.SetColor("_PatchColor", Color.Lerp(color, new Color(0.45f, 0.36f, 0.24f), 0.35f));
                material.SetColor("_RockColor", Color.Lerp(color, new Color(0.55f, 0.52f, 0.48f), 0.6f));
                material.SetFloat("_PatchAmount", 0.3f);
                EditorUtility.SetDirty(material);
                return material;
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", transparent ? 0.85f : 0.1f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- props ----------

        private static int Scatter(Transform parent, Biome biome, PropSet set, List<(Vector2 position, float spacing)> occupied)
        {
            List<GameObject> prefabs = FindPrefabs(set.folder, set.filter);
            if (prefabs.Count == 0)
            {
                Debug.LogWarning($"[WorldBuilder] No prefabs for {set.folder} / {set.filter}");
                return 0;
            }

            int placed = 0;
            for (int attempt = 0; attempt < set.count * 8 && placed < set.count; attempt++)
            {
                if (!TryPickPoint(biome, set, out Vector2 point) || !IsFree(point, set.spacing, occupied)
                    || !TryFindGround(point, out Vector3 ground))
                {
                    continue;
                }

                GameObject prefab = prefabs[random.Next(prefabs.Count)];
                float scale = Range(set.minScale, set.maxScale);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                // Unpacked so the colliders can be replaced freely.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.transform.SetPositionAndRotation(ground + Vector3.down * 0.05f, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                instance.transform.localScale = Vector3.one * scale;
                SetUpColliders(instance, set.kind, scale);
                if (set.kind == Kind.Solid)
                {
                    GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic);
                }
                else
                {
                    // Not batched: the wind shader needs each plant's own pivot (static batching merges them).
                    ApplyFoliage(instance, set.kind);
                }

                if (set.spacing > 0f)
                {
                    occupied.Add((point, set.spacing));
                }
                placed++;
            }
            Physics.SyncTransforms();
            return placed;
        }

        private static bool TryPickPoint(Biome biome, PropSet set, out Vector2 point)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float min = set.minRadius * set.minRadius;
            float max = set.maxRadius * set.maxRadius;
            float distance = Mathf.Sqrt(Range(min, max)) * biome.radius; // uniform over the ring
            point = biome.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            // Thin out the outer edge so biomes fade into the plain instead of ending in a hard circle.
            float edge = Mathf.InverseLerp(0.8f, 1f, distance / biome.radius);
            if (Range(0f, 1f) < edge)
            {
                return false;
            }
            return Vector2.Distance(point, VillageCenter) > VillageClearRadius;
        }

        private static bool IsFree(Vector2 point, float spacing, List<(Vector2 position, float spacing)> occupied)
        {
            if (spacing <= 0f)
            {
                return true;
            }
            foreach ((Vector2 position, float otherSpacing) in occupied)
            {
                if (Vector2.Distance(point, position) < (spacing + otherSpacing) * 0.5f)
                {
                    return false;
                }
            }
            return true;
        }

        // Ground under the point: the plane, a ground patch or a hill. Not on other props or steep cliff faces.
        private static bool TryFindGround(Vector2 point, out Vector3 ground)
        {
            ground = default;
            var origin = new Vector3(point.x, RayHeight, point.y);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (hit.normal.y < MinGroundNormalY || hit.collider.GetComponentInParent<WorldProp>() != null
                || hit.collider.GetComponentInParent<MoaBox>() != null || hit.collider.GetComponentInParent<Shop>() != null
                || hit.collider.GetComponentInParent<MoaHealer>() != null)
            {
                return false;
            }
            ground = hit.point;
            return true;
        }

        private static void SetUpColliders(GameObject instance, Kind kind, float scale)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(collider);
            }

            switch (kind)
            {
                case Kind.Tree:
                {
                    var trunk = instance.AddComponent<CapsuleCollider>();
                    trunk.radius = TreeTrunkRadius / scale;
                    trunk.height = TreeTrunkHeight / scale;
                    trunk.center = Vector3.up * (TreeTrunkHeight / 2f / scale);
                    instance.AddComponent<WorldProp>();
                    break;
                }
                case Kind.Solid:
                    foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
                    {
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                    instance.AddComponent<WorldProp>();
                    break;
                default:
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer.bounds.size.y < SmallDecorHeight)
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                        }
                    }
                    break;
            }
        }

        // ---------- spawners ----------

        private static void BuildSpawner(Transform parent, Biome biome, SpawnerSpec spec, int index)
        {
            var go = new GameObject($"Spawner_{biome.name}_{index + 1}");
            go.transform.SetParent(parent, false);
            go.transform.position = TryFindGround(spec.position, out Vector3 ground) ? ground : new Vector3(spec.position.x, 0f, spec.position.y);
            var spawner = go.AddComponent<MoaSpawner>();

            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("wildMoaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WildMoa>(WildMoaPrefabPath);
            serialized.FindProperty("radius").floatValue = spec.radius;
            serialized.FindProperty("maxAlive").intValue = spec.maxAlive;
            serialized.FindProperty("minLevel").intValue = biome.minLevel;
            serialized.FindProperty("maxLevel").intValue = biome.maxLevel;
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = biome.species.Length;
            for (int i = 0; i < biome.species.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                var species = AssetDatabase.LoadAssetAtPath<MoaSpecies>($"{SpeciesRoot}/{biome.species[i].speciesId}.asset");
                if (species == null)
                {
                    Debug.LogWarning($"[WorldBuilder] Missing species {biome.species[i].speciesId}");
                }
                entry.FindPropertyRelative("species").objectReferenceValue = species;
                entry.FindPropertyRelative("weight").floatValue = biome.species[i].weight;
                entry.FindPropertyRelative("time").enumValueIndex = (int)MoaSpawner.SpawnTime.Any;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        private static List<GameObject> FindPrefabs(string folder, string filter)
        {
            string key = folder + "|" + filter;
            if (prefabCache.TryGetValue(key, out List<GameObject> cached))
            {
                return cached;
            }
            var regex = new Regex(filter);
            var result = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Colliders/") || path.Contains("/Particles/"))
                {
                    continue;
                }
                if (regex.IsMatch(Path.GetFileNameWithoutExtension(path)))
                {
                    result.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
            }
            prefabCache[key] = result;
            return result;
        }

        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
