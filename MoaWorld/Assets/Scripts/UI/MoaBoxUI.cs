using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoaWorld
{
    // Moa Box screen. Clicking a party moa stores it, clicking a stored moa moves it to the party.
    public class MoaBoxUI : ProximityMenu
    {
        private const int Columns = 6;
        private const float CellWidth = 142f;
        private const float CellHeight = 108f;
        private const float CellSpacing = 8f;
        private const float PartySlotHeight = 90f;

        private static readonly Color EmptySlotColor = new Color(0.2f, 0.2f, 0.22f, 1f);
        private static readonly Color FaintedSlotColor = new Color(0.35f, 0.15f, 0.15f, 1f);

        private PlayerParty party;
        private PlayerMoaBox box;

        private class SlotView
        {
            public Image background;
            public TextMeshProUGUI label;
        }

        private TextMeshProUGUI partyHeader;
        private TextMeshProUGUI pageHeader;
        private SlotView[] partySlots;
        private SlotView[] boxSlots;
        private int page;

        protected override bool IsPlayerNear => box.IsNearBox;
        protected override string PromptText => "[E] 모아 박스 열기";

        protected override void Bind(GameObject player)
        {
            party = player.GetComponent<PlayerParty>();
            box = player.GetComponent<PlayerMoaBox>();
            party.Changed += RefreshNow;
            box.Changed += RefreshNow;
        }

        protected override void Unbind()
        {
            base.Unbind();
            party.Changed -= RefreshNow;
            box.Changed -= RefreshNow;
            party = null;
            box = null;
        }

        protected override void Refresh()
        {
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
            RefreshNow();
        }

        protected override GameObject BuildPanel()
        {
            GameConfig config = GameConfig.Instance;
            GameObject panel = UiFactory.CreatePanel("MoaBoxPanel", transform, new Vector2(1300f, 780f));
            Transform root = panel.transform;

            UiFactory.CreateText("Title", root, 36, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 50f))
                .text = "모아 박스";
            partyHeader = UiFactory.CreateText("PartyHeader", root, 24, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(180f, -105f), new Vector2(300f, 40f));
            pageHeader = UiFactory.CreateText("PageHeader", root, 24, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(670f, -105f), new Vector2(600f, 40f));

            UiFactory.CreateButton("PrevPage", root, new Vector2(1130f, -105f), new Vector2(70f, 40f), 24, () => ChangePage(-1), out TextMeshProUGUI prevLabel);
            prevLabel.text = "<";
            UiFactory.CreateButton("NextPage", root, new Vector2(1210f, -105f), new Vector2(70f, 40f), 24, () => ChangePage(1), out TextMeshProUGUI nextLabel);
            nextLabel.text = ">";

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
                float x = 370f + (i % Columns) * (CellWidth + CellSpacing) + CellWidth / 2f;
                float y = -130f - (i / Columns) * (CellHeight + CellSpacing) - CellHeight / 2f;
                boxSlots[i] = CreateSlot($"Box{i}", root, new Vector2(x, y), new Vector2(CellWidth, CellHeight), () => OnBoxSlotClicked(slot));
            }

            UiFactory.CreateText("Hint", root, 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(1200f, 40f))
                .text = "파티 모아를 누르면 보관, 보관함 모아를 누르면 파티로 꺼내요   ·   E / Esc 닫기";
            return panel;
        }

        private static SlotView CreateSlot(string name, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(name, parent, position, size, 18, onClick, out TextMeshProUGUI label);
            return new SlotView { background = (Image)button.targetGraphic, label = label };
        }
    }
}
