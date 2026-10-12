using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoaWorld.EditorTools
{
    // Builds model prefabs for the village stations (Moa Box, shop, healing machine) from their Varco3D OBJ files
    // and swaps them in for the visuals of the station objects in the open scene, fitting each box collider.
    // Run again after replacing a model: MoaWorld > Build Station Models.
    public static class StationModelBuilder
    {
        private const string ModelRoot = "Assets/Varco3D";
        private const string PrefabRoot = "Assets/Prefabs/Stations";
        private const string MaterialRoot = "Assets/Materials/Stations";
        private const string CharacterShader = "MoaWorld/Stylized Character";
        private const string ModelChildName = "Model";
        private const float DefaultSceneYaw = 90f; // fronts face +X (toward the spawn point) unless already turned

        private struct Entry
        {
            public string folder;
            public float height;    // meters
            public float frontYaw;  // turns the OBJ so its front faces the prefab's +Z
            public System.Type station;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { folder = "MoaBox", height = 1.6f, frontYaw = 0f, station = typeof(MoaBox) },
            new Entry { folder = "Shop", height = 3.0f, frontYaw = 90f, station = typeof(Shop) },
            new Entry { folder = "MoaHealer", height = 1.8f, frontYaw = 0f, station = typeof(MoaHealer) },
        };

        [MenuItem("MoaWorld/Build Station Models")]
        public static void Build()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);
            int applied = 0;
            foreach (Entry entry in Entries)
            {
                GameObject prefab = BuildPrefab(entry);
                if (prefab != null && ApplyToScene(entry, prefab))
                {
                    applied++;
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[StationModelBuilder] Applied {applied}/{Entries.Length} station models.");
        }

        // Root > Mesh: the model faces +Z, sits on y = 0 and is centered on the root.
        private static GameObject BuildPrefab(Entry entry)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/{entry.folder}/{entry.folder}.obj");
            if (source == null)
            {
                Debug.LogWarning($"[StationModelBuilder] Missing model for {entry.folder}");
                return null;
            }

            var root = new GameObject($"{entry.folder}_Model");
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(source);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);

            Bounds bounds = mesh.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            float scale = entry.height / bounds.size.y;
            Quaternion rotation = Quaternion.Euler(0f, entry.frontYaw, 0f);
            mesh.transform.localRotation = rotation;
            mesh.transform.localScale = Vector3.one * scale;
            mesh.transform.localPosition = -(rotation * (bounds.center * scale)) + Vector3.up * (bounds.size.y * scale / 2f);

            Material material = GetMaterial(entry.folder);
            foreach (Renderer renderer in mesh.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{entry.folder}_Model.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Replaces the placeholder visuals under the station object; other children (e.g. the spawn point) stay.
        private static bool ApplyToScene(Entry entry, GameObject prefab)
        {
            var station = Object.FindAnyObjectByType(entry.station) as Component;
            if (station == null)
            {
                Debug.LogWarning($"[StationModelBuilder] No {entry.station.Name} in the open scene");
                return false;
            }
            Transform root = station.transform;

            float yaw = DefaultSceneYaw;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == ModelChildName)
                {
                    yaw = child.localEulerAngles.y;
                }
                if (child.name == ModelChildName || child.GetComponentInChildren<Renderer>() != null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            model.name = ModelChildName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(model, "Station model");

            FitCollider(root);
            return true;
        }

        private static void FitCollider(Transform root)
        {
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(root.gameObject);
            }

            bool any = false;
            var local = new Bounds();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                    Vector3 p = root.InverseTransformPoint(point);
                    if (!any)
                    {
                        local = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        local.Encapsulate(p);
                    }
                }
            }
            Undo.RecordObject(collider, "Fit station collider");
            collider.center = local.center;
            collider.size = local.size;
        }

        private static Material GetMaterial(string name)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            Shader shader = Shader.Find(CharacterShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            string folder = $"{ModelRoot}/{name}";
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/model-material_0-base_color.png"));
            string normalPath = $"{folder}/model-material_0-normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            // Same look as the moa and trainers so the village props match the characters.
            material.SetFloat("_BumpScale", 0.6f);
            material.SetFloat("_Wrap", 0.45f);
            material.SetFloat("_Edge", 0.45f);
            material.SetFloat("_Softness", 0.18f);
            material.SetColor("_ShadeColor", new Color(1f, 0.7f, 0.58f));
            material.SetFloat("_ShadeStrength", 0.18f);
            material.SetFloat("_AmbientStrength", 0.85f);
            material.SetFloat("_RimStrength", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
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
