using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MoaWorld.EditorTools
{
    // Builds a model prefab, animation clips (Idle / Move / Attack / Hit) and an override controller for each
    // moa that has a Varco3D model, then assigns the prefab to the species. The models are single static meshes
    // without a skeleton, so every clip moves the whole body (bob, hop, sway, lunge, squash).
    // Run again after adding or replacing a model: MoaWorld > Build Moa Models.
    public static class MoaModelBuilder
    {
        private const string ModelRoot = "Assets/Varco3D";
        private const string AnimationRoot = "Assets/Animations/Moa";
        private const string PrefabRoot = "Assets/Prefabs/MoaModels";
        private const string SpeciesRoot = "Assets/Data/MoaSpecies";
        private const string BodyPath = "Hover/Body";

        private enum Style
        {
            Walker,  // four-legged trot
            Hopper,  // hops along
            Waddler, // birds walking: side-to-side rock and pecking attack
            Flyer,   // hovers above the ground
            Slither, // snake: side sway and strike
        }

        private class Entry
        {
            public string speciesId;
            public string folder;
            public Style style;
            public float size;   // meters for the model's longest side (models are imported at 1)
            public float tempo;  // > 1 faster cycles
            public float amount; // > 1 bigger motions
            public float hover;  // height above the ground for flyers

            public Entry(string speciesId, string folder, Style style, float size, float tempo, float amount, float hover = 0f)
            {
                this.speciesId = speciesId;
                this.folder = folder;
                this.style = style;
                this.size = size;
                this.tempo = tempo;
                this.amount = amount;
                this.hover = hover;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("Bumblebee", "Bumblebee", Style.Flyer, 0.9f, 1.4f, 0.9f, 0.6f),
            new Entry("RedCrownedCrane", "Durumi", Style.Waddler, 1.7f, 0.75f, 0.8f),
            new Entry("GoldenFrog", "Frog", Style.Hopper, 1.0f, 0.9f, 1.1f),
            new Entry("Magpie", "Ggachi", Style.Hopper, 1.0f, 1.4f, 0.6f),
            new Entry("WaterDeer", "Gorani", Style.Walker, 1.4f, 1.15f, 1.0f),
            new Entry("RatSnake", "Gurungi", Style.Slither, 1.4f, 1.0f, 1.0f),
            new Entry("SwallowtailButterfly", "Horangnabi", Style.Flyer, 1.2f, 0.7f, 1.3f, 0.7f),
            new Entry("Goshawk", "Mae", Style.Waddler, 1.1f, 1.15f, 0.7f),
            new Entry("RoeDeer", "Noru", Style.Walker, 1.6f, 1.0f, 0.9f),
            new Entry("EagleOwl", "Owl", Style.Waddler, 1.2f, 0.85f, 1.1f),
            new Entry("Tiger", "Tiger", Style.Walker, 1.5f, 0.9f, 1.2f),
            new Entry("WildBoar", "Wildboar", Style.Walker, 1.4f, 1.25f, 1.1f),
        };

        [MenuItem("MoaWorld/Build Moa Models")]
        public static void BuildAll()
        {
            EnsureFolder(AnimationRoot);
            EnsureFolder(PrefabRoot);

            BaseController baseController = GetOrCreateBaseController();
            int built = 0;
            foreach (Entry entry in Entries)
            {
                if (Build(entry, baseController))
                {
                    built++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoaModelBuilder] Built {built}/{Entries.Length} moa models.");
        }

        private static bool Build(Entry entry, BaseController baseController)
        {
            string modelPath = $"{ModelRoot}/{entry.folder}/{entry.folder}.obj";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            MoaSpecies species = FindSpecies(entry.speciesId);
            if (source == null || species == null)
            {
                Debug.LogWarning($"[MoaModelBuilder] Skipped {entry.speciesId}: model={(source != null)} species={(species != null)}");
                return false;
            }

            string clipFolder = $"{AnimationRoot}/{entry.speciesId}";
            EnsureFolder(clipFolder);
            AnimationClip idle = SaveClip($"{clipFolder}/{entry.speciesId}_Idle.anim", true, clip => BuildIdle(clip, entry));
            AnimationClip move = SaveClip($"{clipFolder}/{entry.speciesId}_Move.anim", true, clip => BuildMove(clip, entry));
            AnimationClip attack = SaveClip($"{clipFolder}/{entry.speciesId}_Attack.anim", false, clip => BuildAttack(clip, entry));
            AnimationClip hit = SaveClip($"{clipFolder}/{entry.speciesId}_Hit.anim", false, clip => BuildHit(clip, entry));

            string overridePath = $"{clipFolder}/{entry.speciesId}.overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null)
            {
                overrides = new AnimatorOverrideController(baseController.controller);
                AssetDatabase.CreateAsset(overrides, overridePath);
            }
            overrides.runtimeAnimatorController = baseController.controller;
            overrides[baseController.idle] = idle;
            overrides[baseController.move] = move;
            overrides[baseController.attack] = attack;
            overrides[baseController.hit] = hit;
            EditorUtility.SetDirty(overrides);

            // Root (Animator) > Hover (fixed height) > Body (animated) > Mesh (scaled model).
            var root = new GameObject($"{entry.speciesId}_Model");
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = overrides;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var hover = new GameObject("Hover").transform;
            hover.SetParent(root.transform, false);
            hover.localPosition = Vector3.up * entry.hover;
            var body = new GameObject("Body").transform;
            body.SetParent(hover, false);
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(source);
            mesh.name = "Mesh";
            mesh.transform.SetParent(body, false);
            mesh.transform.localScale = Vector3.one * entry.size;

            string prefabPath = $"{PrefabRoot}/{entry.speciesId}_Model.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            species.modelPrefab = prefab;
            EditorUtility.SetDirty(species);
            return true;
        }

        // ---------- clips ----------

        private static void BuildIdle(AnimationClip clip, Entry e)
        {
            float a = e.amount;
            if (e.style == Style.Flyer)
            {
                float p = 1.0f / e.tempo;
                Key(clip, "m_LocalPosition.y", 0, 0f, p / 2, 0.08f * a, p, 0f);
                Key(clip, "localEulerAnglesRaw.z", 0, 0f, p / 4, 4f * a, p * 3 / 4, -4f * a, p, 0f);
                return;
            }

            float period = 2.0f / e.tempo;
            Key(clip, "m_LocalScale.y", 0, 1f, period / 2, 1f + 0.035f * a, period, 1f);
            Key(clip, "m_LocalScale.x", 0, 1f, period / 2, 1f - 0.015f * a, period, 1f);
            Key(clip, "m_LocalScale.z", 0, 1f, period / 2, 1f - 0.015f * a, period, 1f);
            if (e.style == Style.Slither)
            {
                Key(clip, "localEulerAnglesRaw.y", 0, 0f, period / 4, 5f * a, period * 3 / 4, -5f * a, period, 0f);
            }
        }

        private static void BuildMove(AnimationClip clip, Entry e)
        {
            float a = e.amount;
            switch (e.style)
            {
                case Style.Walker:
                {
                    float p = 0.45f / e.tempo;
                    Key(clip, "m_LocalPosition.y", 0, 0f, p / 4, 0.07f * a, p / 2, 0f, p * 3 / 4, 0.07f * a, p, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, p / 4, -3f * a, p / 2, 0f, p * 3 / 4, 3f * a, p, 0f);
                    Key(clip, "localEulerAnglesRaw.z", 0, 0f, p / 4, 2f * a, p * 3 / 4, -2f * a, p, 0f);
                    break;
                }
                case Style.Hopper:
                {
                    float p = 0.55f / e.tempo;
                    Key(clip, "m_LocalPosition.y", 0, 0f, p * 0.5f, 0.32f * a, p, 0f);
                    Key(clip, "m_LocalScale.y", 0, 0.85f, p * 0.15f, 1.1f, p * 0.5f, 1f, p * 0.9f, 0.95f, p, 0.85f);
                    Key(clip, "m_LocalScale.x", 0, 1.08f, p * 0.15f, 0.95f, p * 0.5f, 1f, p, 1.08f);
                    Key(clip, "m_LocalScale.z", 0, 1.08f, p * 0.15f, 0.95f, p * 0.5f, 1f, p, 1.08f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, p * 0.3f, -8f * a, p * 0.7f, 6f * a, p, 0f);
                    break;
                }
                case Style.Waddler:
                {
                    float p = 0.5f / e.tempo;
                    Key(clip, "localEulerAnglesRaw.z", 0, 0f, p / 4, 7f * a, p / 2, 0f, p * 3 / 4, -7f * a, p, 0f);
                    Key(clip, "m_LocalPosition.y", 0, 0f, p / 4, 0.04f * a, p / 2, 0f, p * 3 / 4, 0.04f * a, p, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 4f * a, p, 4f * a);
                    break;
                }
                case Style.Flyer:
                {
                    float p = 0.5f / e.tempo;
                    Key(clip, "m_LocalPosition.y", 0, 0f, p / 2, 0.12f * a, p, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 12f, p, 12f);
                    Key(clip, "localEulerAnglesRaw.z", 0, 0f, p / 4, 6f * a, p * 3 / 4, -6f * a, p, 0f);
                    break;
                }
                case Style.Slither:
                {
                    float p = 0.8f / e.tempo;
                    Key(clip, "localEulerAnglesRaw.y", 0, 0f, p / 4, 16f * a, p * 3 / 4, -16f * a, p, 0f);
                    Key(clip, "m_LocalPosition.x", 0, 0f, p / 4, -0.07f * a, p * 3 / 4, 0.07f * a, p, 0f);
                    Key(clip, "m_LocalScale.z", 0, 1f, p / 2, 1.06f, p, 1f);
                    break;
                }
            }
        }

        private static void BuildAttack(AnimationClip clip, Entry e)
        {
            float a = e.amount;
            switch (e.style)
            {
                case Style.Flyer: // dive at the target
                    Key(clip, "m_LocalPosition.y", 0, 0f, 0.15f, 0.15f, 0.3f, -0.25f, 0.5f, 0f);
                    Key(clip, "m_LocalPosition.z", 0, 0f, 0.15f, -0.1f, 0.3f, 0.4f * a, 0.5f, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, 0.15f, -10f, 0.3f, 25f, 0.5f, 0f);
                    break;
                case Style.Slither: // coil back, then strike
                    Key(clip, "m_LocalPosition.z", 0, 0f, 0.18f, -0.15f, 0.26f, 0.55f * a, 0.5f, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, 0.18f, -15f, 0.26f, 10f, 0.5f, 0f);
                    Key(clip, "m_LocalScale.z", 0, 1f, 0.18f, 0.9f, 0.26f, 1.15f, 0.5f, 1f);
                    break;
                case Style.Waddler: // peck
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, 0.15f, -12f, 0.27f, 25f * a, 0.5f, 0f);
                    Key(clip, "m_LocalPosition.z", 0, 0f, 0.15f, -0.08f, 0.27f, 0.3f * a, 0.5f, 0f);
                    break;
                case Style.Hopper: // pounce
                    Key(clip, "m_LocalPosition.y", 0, 0f, 0.27f, 0.35f * a, 0.5f, 0f);
                    Key(clip, "m_LocalPosition.z", 0, 0f, 0.15f, -0.1f, 0.3f, 0.45f, 0.5f, 0f);
                    Key(clip, "m_LocalScale.y", 0, 1f, 0.12f, 0.85f, 0.27f, 1.1f, 0.5f, 1f);
                    break;
                default: // lunge
                    Key(clip, "m_LocalPosition.z", 0, 0f, 0.15f, -0.12f, 0.27f, 0.45f * a, 0.5f, 0f);
                    Key(clip, "localEulerAnglesRaw.x", 0, 0f, 0.15f, -10f, 0.27f, 12f, 0.5f, 0f);
                    Key(clip, "m_LocalScale.y", 0, 1f, 0.15f, 0.9f, 0.27f, 1.08f, 0.5f, 1f);
                    break;
            }
        }

        private static void BuildHit(AnimationClip clip, Entry e)
        {
            Key(clip, "m_LocalPosition.z", 0, 0f, 0.06f, -0.22f, 0.3f, 0f);
            Key(clip, "localEulerAnglesRaw.x", 0, 0f, 0.06f, -10f, 0.3f, 0f);
            Key(clip, "m_LocalScale.y", 0, 1f, 0.06f, 0.85f, 0.18f, 1.05f, 0.3f, 1f);
            Key(clip, "m_LocalScale.x", 0, 1f, 0.06f, 1.1f, 0.18f, 0.97f, 0.3f, 1f);
            Key(clip, "m_LocalScale.z", 0, 1f, 0.06f, 1.1f, 0.18f, 0.97f, 0.3f, 1f);
            if (e.style == Style.Flyer)
            {
                Key(clip, "m_LocalPosition.y", 0, 0f, 0.06f, -0.15f, 0.3f, 0f);
            }
        }

        // Pairs of (time, value) for one Body property.
        private static void Key(AnimationClip clip, string property, params float[] timeValues)
        {
            var keys = new Keyframe[timeValues.Length / 2];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new Keyframe(timeValues[i * 2], timeValues[i * 2 + 1]);
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(BodyPath, typeof(Transform), property), curve);
        }

        private static AnimationClip SaveClip(string path, bool loop, System.Action<AnimationClip> fill)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            fill(clip);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ---------- shared controller ----------

        private class BaseController
        {
            public AnimatorController controller;
            public AnimationClip idle;
            public AnimationClip move;
            public AnimationClip attack;
            public AnimationClip hit;
        }

        // Locomotion blend (Idle <-> Move by Speed), plus Attack and Hit played from any state.
        // Each species overrides the four placeholder clips.
        private static BaseController GetOrCreateBaseController()
        {
            string folder = $"{AnimationRoot}/Base";
            EnsureFolder(folder);
            var result = new BaseController
            {
                idle = SaveClip($"{folder}/Base_Idle.anim", true, _ => { }),
                move = SaveClip($"{folder}/Base_Move.anim", true, _ => { }),
                attack = SaveClip($"{folder}/Base_Attack.anim", false, _ => { }),
                hit = SaveClip($"{folder}/Base_Hit.anim", false, _ => { }),
            };

            string path = $"{AnimationRoot}/MoaBase.controller";
            result.controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (result.controller != null)
            {
                return result;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(result.idle, 0f);
            tree.AddChild(result.move, 1f);
            machine.defaultState = locomotion;

            AnimatorState attack = machine.AddState("Attack");
            attack.motion = result.attack;
            AnimatorState hit = machine.AddState("Hit");
            hit.motion = result.hit;

            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.duration = 0.05f;
            toAttack.canTransitionToSelf = false;

            AnimatorStateTransition toHit = machine.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.duration = 0.03f;
            toHit.canTransitionToSelf = true;

            foreach (AnimatorState state in new[] { attack, hit })
            {
                AnimatorStateTransition back = state.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }

            result.controller = controller;
            return result;
        }

        // ---------- helpers ----------

        private static MoaSpecies FindSpecies(string speciesId)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MoaSpecies", new[] { SpeciesRoot }))
            {
                var species = AssetDatabase.LoadAssetAtPath<MoaSpecies>(AssetDatabase.GUIDToAssetPath(guid));
                if (species != null && species.speciesId == speciesId)
                {
                    return species;
                }
            }
            return null;
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
