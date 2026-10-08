using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MoaWorld
{
    // Horizontal fill bar built from plain Images (no sprites needed).
    public class UiBar
    {
        private readonly RectTransform fill;
        private readonly Image fillImage;

        public GameObject Root { get; }

        public UiBar(GameObject root, RectTransform fill, Image fillImage)
        {
            Root = root;
            this.fill = fill;
            this.fillImage = fillImage;
        }

        public void Set(float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        public void SetColor(Color color)
        {
            fillImage.color = color;
        }
    }

    // Helpers for building prototype UGUI layouts in code (reference resolution 1920x1080).
    // Text uses TextMeshPro with the project default font (TMP Settings).
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);
        public static readonly Color DisabledButtonColor = new Color(0.18f, 0.18f, 0.2f, 1f);
        public static readonly Color BarBackgroundColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color HpColor = new Color(0.35f, 0.85f, 0.35f);

        public static GameObject CreatePanel(string name, Transform parent, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = PanelColor;
            Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return panel;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 position, Vector2 rectSize)
        {
            TextMeshProUGUI text = CreateText(name, parent, size, alignment);
            Place(text.rectTransform, anchor, position, rectSize);
            return text;
        }

        // Dark outline so HUD text stays readable over bright terrain.
        public static void AddOutline(TMP_Text text)
        {
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);
        }

        // Button anchored to the parent's top-left corner, with a stretched label.
        public static Button CreateButton(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, UnityAction onClick, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0f, 1f), position, size);
            Image image = go.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            label = CreateText("Label", go.transform, fontSize, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 6f, 4f);
            return button;
        }

        public static UiBar CreateBar(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color fillColor)
        {
            var background = new GameObject(name, typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            Place((RectTransform)background.transform, anchor, position, size);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = BarBackgroundColor;
            backgroundImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            return new UiBar(background, fillRect, fillImage);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float paddingX, float paddingY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(paddingX, paddingY);
            rect.offsetMax = new Vector2(-paddingX, -paddingY);
        }
    }
}
