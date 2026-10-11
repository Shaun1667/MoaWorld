using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoaWorld.EditorTools
{
    // Applies the game's look (bright, warm, saturated, soft shadows with ambient occlusion and blue distance
    // haze): stylized ground material on the plane and hills, one post-processing profile, SSAO, shadow range,
    // anti-aliasing, fog, sky and the WorldClock sun colors. Safe to run again. Run: MoaWorld > Apply Visual Style.
    // Moa materials (stylized character shader) are made by MoaWorld > Build Moa Models.
    public static class VisualStyleBuilder
    {
        private const string ProfilePath = "Assets/Settings/MoaWorldLook.asset";
        private const string GroundMaterialPath = "Assets/Materials/Ground/GroundGrass.mat";
        private const string SkyMaterialPath = "Assets/Materials/Sky/MoaSky.mat";
        private const string GroundShader = "MoaWorld/Stylized Ground";
        private const string PlaneName = "Plane";
        private const string TerrainGroupName = "Group Terrain";
        private const string MainVolumeName = "Global Volume";
        private const string OldVolumeName = "Post-processing";

        [MenuItem("MoaWorld/Apply Visual Style")]
        public static void Apply()
        {
            ApplyGround();
            ApplyPostProcessing();
            ApplyPipeline();
            ApplyCamera();
            ApplyAtmosphere();
            ApplySun();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[VisualStyleBuilder] Visual style applied.");
        }

        // ---------- ground ----------

        private static void ApplyGround()
        {
            Material ground = LoadOrCreate(GroundMaterialPath, Shader.Find(GroundShader));
            ground.SetColor("_ColorA", new Color(0.27f, 0.50f, 0.18f));
            ground.SetColor("_ColorB", new Color(0.42f, 0.63f, 0.24f));
            ground.SetColor("_PatchColor", new Color(0.58f, 0.55f, 0.32f));
            ground.SetColor("_RockColor", new Color(0.58f, 0.57f, 0.53f));
            ground.SetFloat("_PatchAmount", 0.22f);
            ground.SetFloat("_NoiseScale", 0.06f);
            ground.SetFloat("_PatchScale", 0.02f);
            EditorUtility.SetDirty(ground);

            GameObject plane = GameObject.Find(PlaneName);
            if (plane != null)
            {
                plane.GetComponent<Renderer>().sharedMaterial = ground;
            }
            GameObject terrain = GameObject.Find(TerrainGroupName);
            if (terrain != null)
            {
                foreach (Renderer renderer in terrain.GetComponentsInChildren<Renderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = ground;
                    }
                    renderer.sharedMaterials = materials;
                }
            }
        }

        // ---------- post-processing ----------

        private static void ApplyPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tonemapping = Get<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0.05f);
            color.contrast.Override(12f);
            color.saturation.Override(14f);

            var whiteBalance = Get<WhiteBalance>(profile);
            whiteBalance.temperature.Override(2f);

            var split = Get<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.55f, 0.78f));
            split.highlights.Override(new Color(1f, 0.93f, 0.85f));
            split.balance.Override(15f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);

            // One global volume with this profile; the asset pack's old volume is turned off so they do not stack.
            GameObject main = GameObject.Find(MainVolumeName);
            if (main != null)
            {
                Volume volume = main.GetComponent<Volume>();
                volume.sharedProfile = profile;
                volume.isGlobal = true;
                volume.weight = 1f;
            }
            GameObject old = GameObject.Find(OldVolumeName);
            if (old != null && old.GetComponent<Volume>() != null)
            {
                old.SetActive(false);
            }
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        // ---------- pipeline (shadows, SSAO) ----------

        private static void ApplyPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                return;
            }
            pipeline.shadowDistance = 120f; // the world is larger than the template's 50 m
            pipeline.shadowCascadeCount = 4;
            EditorUtility.SetDirty(pipeline);

            var serialized = new SerializedObject(pipeline);
            SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data)
                {
                    foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                    {
                        if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                        {
                            feature.SetActive(true);
                            var ssao = new SerializedObject(feature);
                            SetFloat(ssao, "m_Settings.Intensity", 1.6f);
                            SetFloat(ssao, "m_Settings.Radius", 0.35f);
                            SetFloat(ssao, "m_Settings.DirectLightingStrength", 0.3f);
                            ssao.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(feature);
                        }
                    }
                }
            }
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        // ---------- camera ----------

        private static void ApplyCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
        }

        // ---------- sky and fog ----------

        private static void ApplyAtmosphere()
        {
            Material sky = LoadOrCreate(SkyMaterialPath, Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.9f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.72f, 0.82f)); // matches the fog, so no grey band at the horizon
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 450f;
            RenderSettings.fogColor = new Color(0.72f, 0.83f, 0.95f);
            RenderSettings.ambientMode = AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
        }

        // ---------- sun ----------

        private static void ApplySun()
        {
            var sunColor = new Color(1f, 0.93f, 0.8f);
            WorldClock clock = Object.FindAnyObjectByType<WorldClock>();
            if (clock != null)
            {
                var serialized = new SerializedObject(clock);
                serialized.FindProperty("dayLightColor").colorValue = sunColor;
                serialized.FindProperty("dayLightMax").floatValue = 2.0f;
                serialized.FindProperty("dayAmbient").floatValue = 1.1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Light light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.color = sunColor;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.85f;
                    EditorUtility.SetDirty(light);
                }
            }
        }

        private static Material LoadOrCreate(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
