using TMPro;
using UnityEngine;

namespace MoaWorld
{
    // Player HP and party list (bottom left), selected target (top center), time of day (top right).
    public class PlayerStatusHud : LocalPlayerView
    {
        private const float RefreshInterval = 0.1f;
        private const float PartyRowSpacing = 38f;
        private const float PartyBottomRowY = 120f;

        private static readonly Color ActiveColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color FaintedColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color TargetHpColor = new Color(0.9f, 0.3f, 0.3f);

        private PlayerHealth health;
        private PlayerParty party;
        private PlayerCombat combat;
        private PlayerTargeting targeting;

        private GameObject root;
        private TextMeshProUGUI hpLabel;
        private UiBar hpBar;
        private TextMeshProUGUI[] partyLabels;
        private UiBar[] partyBars;
        private TextMeshProUGUI clockText;
        private TextMeshProUGUI targetLabel;
        private UiBar targetBar;
        private float nextRefreshTime;

        private void Awake()
        {
            Build();
            root.SetActive(false);
        }

        protected override void Bind(GameObject player)
        {
            health = player.GetComponent<PlayerHealth>();
            party = player.GetComponent<PlayerParty>();
            combat = player.GetComponent<PlayerCombat>();
            targeting = player.GetComponent<PlayerTargeting>();
            root.SetActive(true);
        }

        protected override void Unbind()
        {
            root.SetActive(false);
        }

        private void Update()
        {
            if (!HasPlayer || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }
            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            RefreshPlayer();
            RefreshParty();
            RefreshTarget();
            RefreshClock();
        }

        private void RefreshPlayer()
        {
            hpLabel.text = $"체력 {Mathf.CeilToInt(health.CurrentHp)} / {health.MaxHp}";
            hpBar.Set(health.CurrentHp / health.MaxHp);
        }

        private void RefreshParty()
        {
            MoaUnit active = combat.ActiveMoa != null ? combat.ActiveMoa.Unit : null;
            for (int i = 0; i < partyLabels.Length; i++)
            {
                MoaInstance moa = party.Get(i);
                bool hasMoa = moa != null;
                partyBars[i].Root.SetActive(hasMoa);
                if (!hasMoa)
                {
                    partyLabels[i].text = string.Empty;
                    continue;
                }

                bool isActive = active != null && active.Moa == moa;
                partyLabels[i].text = $"[{i + 1}] {moa.Species.displayName} Lv.{moa.level}{(moa.IsFainted ? "  기절" : string.Empty)}";
                partyLabels[i].color = moa.IsFainted ? FaintedColor : isActive ? ActiveColor : Color.white;
                partyBars[i].Set(moa.currentHp / moa.MaxHp);
            }
        }

        private void RefreshTarget()
        {
            Targetable target = targeting.CurrentTarget;
            Combatant combatant = target != null ? target.GetComponent<Combatant>() : null;
            bool show = combatant != null && !combatant.IsSuspended;
            targetLabel.gameObject.SetActive(show);
            targetBar.Root.SetActive(show);
            if (!show)
            {
                return;
            }

            if (combatant is MoaUnit unit)
            {
                targetLabel.text = $"{unit.Moa.Species.displayName} Lv.{unit.Moa.level}";
                targetBar.Set(unit.Moa.currentHp / unit.Moa.MaxHp);
            }
            else if (combatant is PlayerHealth player)
            {
                targetLabel.text = "플레이어";
                targetBar.Set(player.CurrentHp / player.MaxHp);
            }
        }

        private void RefreshClock()
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
            {
                clockText.text = string.Empty;
                return;
            }
            int remaining = Mathf.CeilToInt(clock.PhaseRemainingSeconds);
            string now = clock.IsNight ? "밤" : "낮";
            string next = clock.IsNight ? "낮" : "밤";
            clockText.text = $"{now}  ·  {next}까지 {remaining / 60}:{remaining % 60:00}";
        }

        private void Build()
        {
            root = new GameObject("StatusHud", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            UiFactory.Stretch((RectTransform)root.transform, 0f, 0f);
            Transform parent = root.transform;
            Vector2 bottomLeft = new Vector2(0f, 0f);

            hpLabel = UiFactory.CreateText("PlayerHpLabel", parent, 24, TextAlignmentOptions.Left, bottomLeft, new Vector2(250f, 80f), new Vector2(420f, 30f));
            UiFactory.AddOutline(hpLabel);
            hpBar = UiFactory.CreateBar("PlayerHpBar", parent, bottomLeft, new Vector2(250f, 52f), new Vector2(420f, 22f), UiFactory.HpColor);

            int slots = GameConfig.Instance.maxPartySize;
            partyLabels = new TextMeshProUGUI[slots];
            partyBars = new UiBar[slots];
            for (int i = 0; i < slots; i++)
            {
                float y = PartyBottomRowY + (slots - 1 - i) * PartyRowSpacing;
                partyLabels[i] = UiFactory.CreateText($"Party{i}", parent, 20, TextAlignmentOptions.Left, bottomLeft, new Vector2(155f, y), new Vector2(230f, 30f));
                UiFactory.AddOutline(partyLabels[i]);
                partyBars[i] = UiFactory.CreateBar($"Party{i}Hp", parent, bottomLeft, new Vector2(370f, y), new Vector2(160f, 12f), UiFactory.HpColor);
            }

            Vector2 topCenter = new Vector2(0.5f, 1f);
            targetLabel = UiFactory.CreateText("TargetLabel", parent, 26, TextAlignmentOptions.Center, topCenter, new Vector2(0f, -40f), new Vector2(600f, 36f));
            UiFactory.AddOutline(targetLabel);
            targetBar = UiFactory.CreateBar("TargetHpBar", parent, topCenter, new Vector2(0f, -72f), new Vector2(400f, 18f), TargetHpColor);

            clockText = UiFactory.CreateText("Clock", parent, 26, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-200f, -40f), new Vector2(360f, 36f));
            UiFactory.AddOutline(clockText);
        }
    }
}
