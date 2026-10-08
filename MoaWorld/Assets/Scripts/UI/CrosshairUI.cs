using UnityEngine;
using UnityEngine.UI;

namespace MoaWorld
{
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private PlayerTargeting targeting;
        [SerializeField] private Graphic[] parts;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.3f, 0.3f, 1f);

        private void Update()
        {
            bool visible = Cursor.lockState == CursorLockMode.Locked;
            Color color = targeting != null && targeting.HoveredTarget != null ? hoverColor : normalColor;
            foreach (Graphic part in parts)
            {
                part.enabled = visible;
                part.color = color;
            }
        }
    }
}
