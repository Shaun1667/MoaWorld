using TMPro;
using UnityEngine;

namespace MoaWorld
{
    // A menu opened with E while the local player stands near a world object, closed with E/Esc or by walking away.
    public abstract class ProximityMenu : LocalPlayerView
    {
        private const float RefreshInterval = 0.5f;

        private GameObject panel;
        private TextMeshProUGUI prompt;
        private float nextRefreshTime;

        protected bool IsOpen => panel != null && panel.activeSelf;

        protected abstract bool IsPlayerNear { get; }
        protected abstract string PromptText { get; }

        // Builds the panel contents; called once from Awake.
        protected abstract GameObject BuildPanel();
        protected abstract void Refresh();

        protected virtual void Awake()
        {
            prompt = UiFactory.CreateText("Prompt", transform, 28, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(600f, 50f));
            UiFactory.AddOutline(prompt);
            prompt.text = PromptText;
            prompt.enabled = false;
            panel = BuildPanel();
            panel.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (IsOpen)
            {
                Close();
            }
        }

        protected override void Unbind()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void Update()
        {
            bool near = HasPlayer && IsPlayerNear;
            if (IsOpen)
            {
                if (!near || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }
                else if (Time.unscaledTime >= nextRefreshTime)
                {
                    RefreshNow();
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
            RefreshNow();
        }

        public void Close()
        {
            panel.SetActive(false);
            UiState.IsMenuOpen = false;
        }

        protected void RefreshNow()
        {
            if (!IsOpen)
            {
                return;
            }
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            Refresh();
        }
    }
}
