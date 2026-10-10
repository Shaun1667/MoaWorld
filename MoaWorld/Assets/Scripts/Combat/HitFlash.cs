using UnityEngine;

namespace MoaWorld
{
    // Briefly flashes the body white when the combatant takes damage.
    [RequireComponent(typeof(Combatant))]
    public class HitFlash : MonoBehaviour
    {
        private const float Duration = 0.12f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer bodyRenderer;

        private MaterialPropertyBlock block;
        private Color restoreColor;
        private bool flashing;
        private float flashEndTime;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            GetComponent<Combatant>().HitShown += OnHitShown;
        }

        private void OnHitShown()
        {
            bodyRenderer.GetPropertyBlock(block);
            if (!flashing)
            {
                restoreColor = block.HasColor(BaseColorId) ? block.GetColor(BaseColorId) : bodyRenderer.sharedMaterial.GetColor(BaseColorId);
            }
            flashing = true;
            flashEndTime = Time.time + Duration;
            block.SetColor(BaseColorId, Color.white);
            bodyRenderer.SetPropertyBlock(block);
        }

        private void Update()
        {
            if (!flashing || Time.time < flashEndTime)
            {
                return;
            }
            flashing = false;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, restoreColor);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
