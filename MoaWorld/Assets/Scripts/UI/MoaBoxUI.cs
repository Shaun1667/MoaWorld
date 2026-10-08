using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MoaWorld
{
    // Moa Box screen, opened with E near the box. Clicking a party moa stores it,
    // clicking a stored moa moves it to the party. Builds its own layout at startup.
    public class MoaBoxUI : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;
        private const int Columns = 6;
        private const float CellWidth = 142f;
        private const float CellHeight = 108f;
        private const float CellSpacing = 8f;
        private const float PartySlotHeight = 90f;

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        private static readonly Color EmptySlotColor = new Color(0.2f, 0.2f, 0.22f, 1f);
        private static readonly Color FaintedSlotColor = new Color(0.35f, 0.15f, 0.15f, 1f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);

        [SerializeField] private PlayerParty party;
        [SerializeField] private PlayerMoaBox box;

        private class SlotView
        {
            public Image background;
            public Text label;
        }

        private Font font;
        private GameObject panel;
        private Text prompt;
        private Text partyHeader;
        private Text pageHeader;
        private SlotView[] partySlots;
        private SlotView[] boxSlots;
        private int page;
        private float nextRefreshTime;

        private bool IsOpen => panel.activeSelf;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            party.Changed += Refresh;
            box.Changed += Refresh;
        }

        private void OnDisable()
        {
            party.Changed -= Refresh;
            box.Changed -= Refresh;
            if (IsOpen)
            {
                Close();
            }
        }

        private void Update()
        {
            bool near = box.IsNearBox;
            if (IsOpen)
            {
                if (!near || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }
                else if (Time.unscaledTime >= nextRefreshTime)
                {
                    Refresh();
                }
            }
            else if (near && !UiState.IsMenuOpen && Input.GetKeyDown(KeyCode.E))
            {
                Open();
            }
            prompt.enabled = near && !IsOpen;
        }

        public void Open()
        {
            panel.SetActive(true);
            UiState.IsMenuOpen = true;
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            UiState.IsMenuOpen = false;
        }

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }
            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            GameConfig config = GameConfig.Instance;
            partyHeader.text = $"파티 {party.Members.Count}/{config.maxPartySize}";
            for (int i = 0; i < partySlots.Length; i++)
            {
                SetSlot(partySlots[i], party.Get(i), null);
            }

            pageHeader.text = $"보관함  {page + 1}/{config.storagePageCount} 페이지   ({box.Stored.Count}/{box.Capacity})";
            for (int i = 0; i < boxSlots.Length; i++)
            {
                int index = page * config.storagePageSize + i;
                PlayerMoaBox.StoredMoa entry = index < box.Stored.Count ? box.Stored[index] : null;
                SetSlot(boxSlots[i], entry?.moa, entry);
            }
        }

        private void SetSlot(SlotView view, MoaInstance moa, PlayerMoaBox.StoredMoa storedEntry)
        {
            if (moa == null)
            {
                view.background.color = EmptySlotColor;
                view.label.text = string.Empty;
                return;
            }

            Color elementColor = GameConfig.Instance.GetElementColor(moa.Species.element);
            view.background.color = moa.IsFainted ? FaintedSlotColor : Color.Lerp(elementColor, Color.black, 0.55f);

            string status = string.Empty;
            if (storedEntry != null && moa.currentHp < moa.MaxHp)
            {
                status = $"\n회복까지 {FormatTime(box.HealRemainingSeconds(storedEntry))}";
            }
            else if (storedEntry == null && moa.IsFainted)
            {
                status = "\n기절";
            }
            view.label.text = $"{moa.Species.displayName} Lv.{moa.level}\nHP {Mathf.CeilToInt(moa.currentHp)}/{moa.MaxHp}{status}";
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        private void OnPartySlotClicked(int slot)
        {
            MoaInstance moa = party.Get(slot);
            if (moa != null)
            {
                box.Deposit(moa);
            }
        }

        private void OnBoxSlotClicked(int slot)
        {
            int index = page * GameConfig.Instance.storagePageSize + slot;
            if (index < box.Stored.Count)
            {
                box.Withdraw(index);
            }
        }

        private void ChangePage(int delta)
        {
            page = Mathf.Clamp(page + delta, 0, GameConfig.Instance.storagePageCount - 1);
            Refresh();
        }

        // ---- Layout (reference resolution 1920x1080) ----

        private void Build()
        {
            GameConfig config = GameConfig.Instance;

            prompt = CreateText("BoxPrompt", transform, 28, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(600f, 50f));
            prompt.text = "[E] 모아 박스 열기";

            panel = new GameObject("MoaBoxPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            panel.GetComponent<Image>().color = PanelColor;
            Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 780f));
            Transform root = panel.transform;

            Text title = CreateText("Title", root, 36, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 50f));
            title.text = "모아 박스";

            partyHeader = CreateText("PartyHeader", root, 24, TextAnchor.MiddleLeft);
            Place(partyHeader.rectTransform, new Vector2(0f, 1f), new Vector2(180f, -105f), new Vector2(300f, 40f));

            pageHeader = CreateText("PageHeader", root, 24, TextAnchor.MiddleLeft);
            Place(pageHeader.rectTransform, new Vector2(0f, 1f), new Vector2(670f, -105f), new Vector2(600f, 40f));

            CreateButton("PrevPage", root, "<", new Vector2(1130f, -105f), new Vector2(70f, 40f), () => ChangePage(-1));
            CreateButton("NextPage", root, ">", new Vector2(1210f, -105f), new Vector2(70f, 40f), () => ChangePage(1));

            partySlots = new SlotView[config.maxPartySize];
            for (int i = 0; i < partySlots.Length; i++)
            {
                int slot = i;
                float y = -130f - i * (PartySlotHeight + CellSpacing) - PartySlotHeight / 2f;
                partySlots[i] = CreateSlot($"Party{i}", root, new Vector2(180f, y), new Vector2(300f, PartySlotHeight), () => OnPartySlotClicked(slot));
            }

            boxSlots = new SlotView[config.storagePageSize];
            for (int i = 0; i < boxSlots.Length; i++)
            {
                int slot = i;
                int column = i % Columns;
                int row = i / Columns;
                float x = 370f + column * (CellWidth + CellSpacing) + CellWidth / 2f;
                float y = -130f - row * (CellHeight + CellSpacing) - CellHeight / 2f;
                boxSlots[i] = CreateSlot($"Box{i}", root, new Vector2(x, y), new Vector2(CellWidth, CellHeight), () => OnBoxSlotClicked(slot));
            }

            Text hint = CreateText("Hint", root, 20, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(1200f, 40f));
            hint.text = "파티 모아를 누르면 보관, 보관함 모아를 누르면 파티로 꺼내요   ·   E / Esc 닫기";
        }

        private SlotView CreateSlot(string name, Transform parent, Vector2 position, Vector2 size, UnityAction onClick)
        {
            Image background = CreateButtonBase(name, parent, position, size, onClick);
            Text label = CreateText("Label", background.transform, 18, TextAnchor.MiddleCenter);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 4f);
            rect.offsetMax = new Vector2(-6f, -4f);
            return new SlotView { background = background, label = label };
        }

        private void CreateButton(string name, Transform parent, string text, Vector2 position, Vector2 size, UnityAction onClick)
        {
            Image background = CreateButtonBase(name, parent, position, size, onClick);
            background.color = ButtonColor;
            Text label = CreateText("Label", background.transform, 24, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
        }

        private static Image CreateButtonBase(string name, Transform parent, Vector2 position, Vector2 size, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0f, 1f), position, size);
            Image image = go.GetComponent<Image>();
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return image;
        }

        private Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
