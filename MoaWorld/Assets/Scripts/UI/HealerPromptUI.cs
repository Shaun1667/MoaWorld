using TMPro;
using UnityEngine;

namespace MoaWorld
{
    // "[E]" hint while the local player stands at the healing machine.
    public class HealerPromptUI : LocalPlayerView
    {
        private PlayerHealerUse healerUse;
        private TextMeshProUGUI prompt;

        private void Awake()
        {
            prompt = UiFactory.CreateText("HealerPrompt", transform, 28, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(600f, 50f));
            UiFactory.AddOutline(prompt);
            prompt.text = "[E] 회복 기계 사용";
            prompt.enabled = false;
        }

        protected override void Bind(GameObject player)
        {
            healerUse = player.GetComponent<PlayerHealerUse>();
        }

        protected override void Unbind()
        {
            healerUse = null;
            prompt.enabled = false;
        }

        private void Update()
        {
            prompt.enabled = healerUse != null && !UiState.IsMenuOpen && healerUse.IsNearHealer;
        }
    }
}
