using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Esc menu while in a world: keep playing, or leave the room. For the host, leaving closes the room
    // for everyone (the world is saved), so it asks for a second click first.
    public class GameMenuUI : LocalPlayerView
    {
        private GameObject panel;
        private TextMeshProUGUI leaveLabel;
        private TextMeshProUGUI hintText;
        private bool confirmingClose;

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private void Awake()
        {
            Build();
            panel.SetActive(false);
        }

        protected override void Bind(GameObject player) { }

        protected override void Unbind()
        {
            if (panel.activeSelf)
            {
                Close();
            }
        }

        private void Update()
        {
            if (!HasPlayer || !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }
            if (panel.activeSelf)
            {
                Close();
            }
            else if (!UiState.IsMenuOpen && UiState.ChangedFrame != Time.frameCount)
            {
                Open();
            }
        }

        private void Open()
        {
            confirmingClose = false;
            RefreshTexts();
            panel.SetActive(true);
            UiState.IsMenuOpen = true;
        }

        private void Close()
        {
            panel.SetActive(false);
            UiState.IsMenuOpen = false;
        }

        private void OnLeaveClicked()
        {
            if (IsHost && !confirmingClose)
            {
                confirmingClose = true;
                RefreshTexts();
                return;
            }
            Close();
            GameSession.Instance.Leave();
        }

        private void RefreshTexts()
        {
            if (IsHost)
            {
                leaveLabel.text = confirmingClose ? "정말 닫을까요? 한 번 더 누르세요" : "방 닫기";
                hintText.text = "방을 닫으면 모든 참가자의 연결이 끊어지고 월드가 저장돼요";
            }
            else
            {
                leaveLabel.text = "방 나가기";
                hintText.text = "나가도 모아와 아이템은 호스트의 월드에 저장돼요";
            }
        }

        private void Build()
        {
            panel = UiFactory.CreatePanel("GameMenuPanel", transform, new Vector2(560f, 380f));
            Transform root = panel.transform;

            UiFactory.CreateText("Title", root, 36, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(500f, 50f))
                .text = "메뉴";

            UiFactory.CreateButton("ResumeButton", root, new Vector2(280f, -130f), new Vector2(420f, 70f), 26, Close, out TextMeshProUGUI resumeLabel);
            resumeLabel.text = "계속하기";

            UiFactory.CreateButton("LeaveButton", root, new Vector2(280f, -215f), new Vector2(420f, 70f), 24, OnLeaveClicked, out leaveLabel);

            hintText = UiFactory.CreateText("Hint", root, 18, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(500f, 50f));
            hintText.color = new Color(0.75f, 0.75f, 0.75f);
        }
    }
}
