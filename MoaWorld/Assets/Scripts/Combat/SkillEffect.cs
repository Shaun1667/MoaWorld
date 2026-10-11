using UnityEngine;

namespace MoaWorld
{
    // Skill visuals. Species with effects (castEffect / hitEffect) play particle prefabs from the effect packs;
    // others fall back to a small element-colored orb flying from attacker to target. Visual only.
    public class SkillEffect : MonoBehaviour
    {
        private const float OrbDuration = 0.15f;
        private const float OrbSize = 0.3f;
        private const float HitSurfaceOffset = 0.35f; // meters from the target's center toward the attacker
        private const float MinEffectSeconds = 0.5f;
        private const float MaxEffectSeconds = 3f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 from;
        private Vector3 to;
        private float progress;

        public static void Play(MoaSpecies species, Vector3 origin, Vector3 impact, Color fallbackColor)
        {
            if (species == null || (species.castEffect == null && species.hitEffect == null))
            {
                SpawnOrb(origin, impact, fallbackColor);
                return;
            }

            Vector3 direction = impact - origin;
            direction.y = 0f;
            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            Vector3 forward = facing * Vector3.forward;
            if (species.castEffect != null)
            {
                SpawnParticles(species.castEffect, origin + forward * species.castEffectOffset,
                    facing * Quaternion.Euler(species.castEffectEuler), species.castEffectScale);
            }
            if (species.hitEffect != null)
            {
                // Slightly on the attacker's side so the burst shows on the target's surface, not inside it.
                SpawnParticles(species.hitEffect, impact - forward * HitSurfaceOffset, facing, species.hitEffectScale);
            }
        }

        // Plays an effect prefab once: looping systems are made one-shot, the transform scale applies to every
        // particle system, and the object is removed when the longest system is done.
        private static void SpawnParticles(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            GameObject effect = Instantiate(prefab, position, rotation);
            effect.transform.localScale = Vector3.one * scale;
            float seconds = MinEffectSeconds;
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.None;
                seconds = Mathf.Max(seconds, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Play(false);
            }
            Destroy(effect, Mathf.Min(seconds, MaxEffectSeconds));
        }

        private static void SpawnOrb(Vector3 from, Vector3 to, Color color)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.name = "SkillEffect";
            orb.transform.position = from;
            orb.transform.localScale = Vector3.one * OrbSize;

            Renderer orbRenderer = orb.GetComponent<Renderer>();
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            orbRenderer.SetPropertyBlock(block);

            SkillEffect effect = orb.AddComponent<SkillEffect>();
            effect.from = from;
            effect.to = to;
        }

        private void Update()
        {
            progress += Time.deltaTime / OrbDuration;
            transform.position = Vector3.Lerp(from, to, progress);
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
