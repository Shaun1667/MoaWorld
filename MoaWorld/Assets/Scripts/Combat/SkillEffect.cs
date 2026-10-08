using UnityEngine;

namespace MoaWorld
{
    // Placeholder skill visual: a small element-colored orb flying from attacker to target.
    public class SkillEffect : MonoBehaviour
    {
        private const float Duration = 0.15f;
        private const float Size = 0.3f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 from;
        private Vector3 to;
        private float progress;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.name = "SkillEffect";
            orb.transform.position = from;
            orb.transform.localScale = Vector3.one * Size;

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
            progress += Time.deltaTime / Duration;
            transform.position = Vector3.Lerp(from, to, progress);
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
