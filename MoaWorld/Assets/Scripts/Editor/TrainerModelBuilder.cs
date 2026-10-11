using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MoaWorld.EditorTools
{
    // Builds the player trainers from the rigged FBX files made by Tools/Blender/rig_trainer.py: clip import
    // settings, one shared Animator controller (locomotion + jump on the base layer, throw/summon/hit on an
    // upper-body layer so they play while running), stylized materials and a model prefab per trainer, and puts the
    // prefabs on the Player prefab's PlayerModel. Run again after re-rigging: MoaWorld > Build Trainer Models.
    public static class TrainerModelBuilder
    {
        private const string ModelRoot = "Assets/Varco3D";
        private const string AnimationRoot = "Assets/Animations/Trainer";
        private const string PrefabRoot = "Assets/Prefabs/Trainers";
        private const string MaterialRoot = "Assets/Materials/Trainer";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string CharacterShader = "MoaWorld/Stylized Character";
        private const float Height = 1.7f; // meters; the player's capsule is 2 m tall

        // Index matches PlayerNetwork.Trainer (0 = male, 1 = female).
        private static readonly string[] Trainers = { "TrainerMale", "TrainerFemale" };
        private static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "Fall" };

        [MenuItem("MoaWorld/Build Trainer Models")]
        public static void Build()
        {
            EnsureFolder(AnimationRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);

            // All imports first: reimporting an FBX after a prefab was saved can drop that prefab's controller link.
            var fbxPaths = new List<string>();
            foreach (string name in Trainers)
            {
                string fbx = $"{ModelRoot}/{name}/{name}_Rigged.fbx";
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) != null;
                if (!exists)
                {
                    Debug.LogWarning($"[TrainerModelBuilder] Missing {fbx}");
                }
                else
                {
                    ConfigureClips(fbx);
                }
                fbxPaths.Add(exists ? fbx : null);
            }

            string controllerSource = fbxPaths.Find(p => p != null);
            AnimatorController controller = controllerSource != null ? BuildController(LoadClips(controllerSource), controllerSource) : null;
            AssetDatabase.SaveAssets();

            var prefabs = new List<GameObject>();
            for (int i = 0; i < Trainers.Length; i++)
            {
                prefabs.Add(fbxPaths[i] != null ? BuildPrefab(Trainers[i], fbxPaths[i], controller) : null);
            }
            AssignToPlayer(prefabs.ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log($"[TrainerModelBuilder] Built {prefabs.FindAll(p => p != null).Count}/{Trainers.Length} trainers.");
        }

        // Blender exports takes as "Armature|Idle"; renamed to the action names, looping where it makes sense.
        private static void ConfigureClips(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation take in takes)
            {
                int bar = take.takeName.LastIndexOf('|');
                take.name = bar >= 0 ? take.takeName.Substring(bar + 1) : take.takeName;
                take.loopTime = System.Array.IndexOf(LoopingClips, take.name) >= 0;
            }
            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, AnimationClip> LoadClips(string path)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    clips[clip.name] = clip;
                }
            }
            return clips;
        }

        // Both trainers share bone names, so one controller with the male clips drives either model.
        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, string fbx)
        {
            string path = $"{AnimationRoot}/Trainer.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Summon", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            AnimatorControllerParameter[] parameters = controller.parameters; // a copy; written back below
            parameters[1].defaultBool = true; // Grounded
            controller.parameters = parameters;

            // Base layer: Idle/Walk/Run by speed, and the jump sequence.
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 0.35f);
            tree.AddChild(clips["Run"], 1f);
            machine.defaultState = locomotion;

            AnimatorState jumpUp = machine.AddState("JumpUp");
            jumpUp.motion = clips["JumpUp"];
            AnimatorState fall = machine.AddState("Fall");
            fall.motion = clips["Fall"];
            AnimatorState land = machine.AddState("Land");
            land.motion = clips["Land"];

            Transition(locomotion, jumpUp, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            Transition(locomotion, fall, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            AnimatorStateTransition upToFall = Transition(jumpUp, fall, 0.1f);
            upToFall.hasExitTime = true;
            upToFall.exitTime = 0.95f;
            Transition(jumpUp, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            Transition(fall, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            AnimatorStateTransition landDone = Transition(land, locomotion, 0.15f);
            landDone.hasExitTime = true;
            landDone.exitTime = 0.7f;

            // Upper body: actions that play on top of whatever the legs are doing.
            var layer = new AnimatorControllerLayer
            {
                name = "Actions",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = BuildUpperBodyMask(fbx),
                stateMachine = new AnimatorStateMachine { name = "Actions" },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);
            AnimatorStateMachine actions = controller.layers[1].stateMachine;
            AnimatorState empty = actions.AddState("Empty");
            actions.defaultState = empty;
            foreach (string action in new[] { "Throw", "Summon", "Hit" })
            {
                AnimatorState state = actions.AddState(action);
                state.motion = clips[action];
                AnimatorStateTransition enter = actions.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, action);
                enter.duration = 0.05f;
                enter.canTransitionToSelf = action == "Hit";
                AnimatorStateTransition back = state.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.15f;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        // Spine and everything above it (arms, head, hair); hips and legs stay with the base layer.
        private static AvatarMask BuildUpperBodyMask(string fbx)
        {
            string path = $"{AnimationRoot}/UpperBody.mask";
            AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var paths = new List<string>();
            CollectPaths(model.transform, "", paths);
            mask.transformCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
            {
                mask.SetTransformPath(i, paths[i]);
                mask.SetTransformActive(i, paths[i].Contains("/Spine"));
            }
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        private static void CollectPaths(Transform t, string prefix, List<string> paths)
        {
            foreach (Transform child in t)
            {
                string path = prefix.Length == 0 ? child.name : $"{prefix}/{child.name}";
                paths.Add(path);
                CollectPaths(child, path, paths);
            }
        }

        // Root > Mesh (rigged model with the Animator), scaled to Height with the feet at the root.
        private static GameObject BuildPrefab(string name, string fbx, AnimatorController controller)
        {
            var root = new GameObject($"{name}_Model");
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            var skin = mesh.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.sharedMaterial = GetMaterial(name);
            skin.updateWhenOffscreen = false;

            Bounds bounds = skin.sharedMesh.bounds;
            float scale = Height / bounds.size.y;
            mesh.transform.localScale = Vector3.one * scale;
            // Mesh space is the Blender model space: feet sit at bounds.min.y.
            mesh.transform.localPosition = Vector3.up * (-bounds.min.y * scale);

            var animator = mesh.GetComponent<Animator>();
            if (animator == null)
            {
                animator = mesh.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}_Model.prefab");
            Object.DestroyImmediate(root);
            return prefab;
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
            material.SetFloat("_BumpScale", 0.8f);
            material.SetFloat("_Wrap", 0.45f);
            material.SetFloat("_Edge", 0.45f);
            material.SetFloat("_Softness", 0.15f);
            material.SetColor("_ShadeColor", new Color(1f, 0.72f, 0.62f));
            material.SetFloat("_ShadeStrength", 0.2f);
            material.SetFloat("_AmbientStrength", 0.85f);
            material.SetFloat("_RimStrength", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignToPlayer(GameObject[] prefabs)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var model = contents.GetComponent<PlayerModel>();
                if (model == null)
                {
                    model = contents.AddComponent<PlayerModel>();
                }
                var serialized = new SerializedObject(model);
                SerializedProperty list = serialized.FindProperty("trainerPrefabs");
                list.arraySize = prefabs.Length;
                for (int i = 0; i < prefabs.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                }
                Transform body = contents.transform.Find("Body");
                serialized.FindProperty("placeholder").objectReferenceValue = body != null ? body.gameObject : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
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
