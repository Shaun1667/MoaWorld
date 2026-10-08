using TMPro;
using UnityEngine;

namespace MoaWorld
{
    // Floating name, level and HP bar above a moa. Hidden when far away or inside a moa ball.
    [RequireComponent(typeof(MoaUnit))]
    public class MoaNameplate : MonoBehaviour
    {
        private const float RefreshInterval = 0.1f;
        private const float MaxVisibleDistance = 30f;
        private const float HeightAboveHead = 0.45f;
        private const float WorldScale = 0.01f;
        private const float ReferenceDistance = 8f;
        private const float MinScaleFactor = 0.35f;
        private const float MaxScaleFactor = 2f;

        private static readonly Color OwnedNameColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color OwnedHpColor = new Color(1f, 0.8f, 0.25f);

        private MoaUnit unit;
        private Canvas canvas;
        private TextMeshProUGUI label;
        private UiBar hpBar;
        private Camera viewCamera;
        private float nextRefreshTime;

        private void Awake()
        {
            unit = GetComponent<MoaUnit>();
            Build();
        }

        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            float distance = viewCamera != null ? Vector3.Distance(viewCamera.transform.position, transform.position) : float.MaxValue;
            bool visible = unit.Moa != null && unit.IsAttackable && distance <= MaxVisibleDistance;
            if (canvas.enabled != visible)
            {
                canvas.enabled = visible;
            }
            if (!visible)
            {
                return;
            }

            // Grow with distance so the label keeps a readable, roughly constant screen size.
            float scale = WorldScale * Mathf.Clamp(distance / ReferenceDistance, MinScaleFactor, MaxScaleFactor);
            canvas.transform.localScale = Vector3.one * scale;
            canvas.transform.rotation = viewCamera.transform.rotation;

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }
            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            bool owned = unit.OwnerRoot != null;
            label.text = $"{unit.Moa.Species.displayName} Lv.{unit.Moa.level}";
            label.color = owned ? OwnedNameColor : Color.white;
            hpBar.SetColor(owned ? OwnedHpColor : UiFactory.HpColor);
            hpBar.Set(unit.Moa.currentHp / unit.Moa.MaxHp);
        }

        private void Build()
        {
            float bodyHeight = GetComponent<CharacterController>().height;

            var plate = new GameObject("Nameplate", typeof(RectTransform), typeof(Canvas));
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = Vector3.up * (bodyHeight + HeightAboveHead);
            plate.transform.localScale = Vector3.one * WorldScale;
            ((RectTransform)plate.transform).sizeDelta = new Vector2(240f, 70f);
            canvas = plate.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            label = UiFactory.CreateText("Name", plate.transform, 26, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(240f, 36f));
            UiFactory.AddOutline(label);
            hpBar = UiFactory.CreateBar("Hp", plate.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(180f, 14f), UiFactory.HpColor);
        }
    }
}
