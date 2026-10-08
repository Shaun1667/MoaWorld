using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoaWorld
{
    // Start screen: host a world or join one from the list of rooms found on the LAN.
    public class LobbyUI : MonoBehaviour
    {
        private const int MaxListedRooms = 6;
        private const float RefreshInterval = 0.5f;
        private const float RoomButtonHeight = 64f;
        private const float RoomButtonSpacing = 10f;

        private readonly List<LanDiscovery.RoomInfo> listedRooms = new List<LanDiscovery.RoomInfo>();

        private GameObject panel;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI emptyText;
        private Button[] roomButtons;
        private TextMeshProUGUI[] roomLabels;
        private float nextRefreshTime;
        private bool ownsMenuState;

        private void Awake()
        {
            Build();
            panel.SetActive(false); // first Update shows it and takes the menu (cursor) state
        }

        private void Start()
        {
            GameSession.Instance.SessionEnded += OnSessionEnded;
        }

        private void OnDestroy()
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.SessionEnded -= OnSessionEnded;
            }
        }

        private void Update()
        {
            bool show = GameSession.Instance != null && !GameSession.Instance.IsInSession;
            if (panel.activeSelf != show)
            {
                panel.SetActive(show);
                SetMenuState(show);
            }
            if (show && Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + RefreshInterval;
                RefreshRooms();
            }
        }

        private void SetMenuState(bool open)
        {
            if (open)
            {
                UiState.IsMenuOpen = true;
                ownsMenuState = true;
            }
            else if (ownsMenuState)
            {
                UiState.IsMenuOpen = false;
                ownsMenuState = false;
            }
        }

        private void RefreshRooms()
        {
            listedRooms.Clear();
            foreach (LanDiscovery.RoomInfo room in GameSession.Instance.Discovery.Rooms)
            {
                if (listedRooms.Count < MaxListedRooms)
                {
                    listedRooms.Add(room);
                }
            }

            for (int i = 0; i < roomButtons.Length; i++)
            {
                bool hasRoom = i < listedRooms.Count;
                roomButtons[i].gameObject.SetActive(hasRoom);
                if (hasRoom)
                {
                    LanDiscovery.RoomInfo room = listedRooms[i];
                    roomLabels[i].text = $"{room.hostName}의 월드      {room.players} / {room.maxPlayers}명";
                }
            }
            emptyText.gameObject.SetActive(listedRooms.Count == 0);
        }

        private void OnHostClicked()
        {
            statusText.text = GameSession.Instance.HostWorld() ? string.Empty : "방을 열 수 없어요. 포트가 이미 사용 중인지 확인해 주세요";
        }

        private void OnRoomClicked(int index)
        {
            if (index >= listedRooms.Count)
            {
                return;
            }
            LanDiscovery.RoomInfo room = listedRooms[index];
            if (room.players >= room.maxPlayers)
            {
                statusText.text = "방이 가득 찼어요";
                return;
            }
            statusText.text = GameSession.Instance.Join(room) ? string.Empty : "방에 참가할 수 없어요";
        }

        private void OnSessionEnded(string reason)
        {
            statusText.text = reason;
        }

        private void Build()
        {
            panel = UiFactory.CreatePanel("LobbyPanel", transform, new Vector2(900f, 720f));
            Transform root = panel.transform;

            UiFactory.CreateText("Title", root, 56, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 80f))
                .text = "MOA WORLD";

            UiFactory.CreateButton("HostButton", root, new Vector2(450f, -170f), new Vector2(400f, 70f), 28, OnHostClicked, out TextMeshProUGUI hostLabel);
            hostLabel.text = "방 만들기";

            UiFactory.CreateText("RoomsHeader", root, 24, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(450f, -250f), new Vector2(800f, 36f))
                .text = "같은 네트워크의 방";

            roomButtons = new Button[MaxListedRooms];
            roomLabels = new TextMeshProUGUI[MaxListedRooms];
            for (int i = 0; i < MaxListedRooms; i++)
            {
                int index = i;
                float y = -300f - i * (RoomButtonHeight + RoomButtonSpacing);
                roomButtons[i] = UiFactory.CreateButton($"Room{i}", root, new Vector2(450f, y), new Vector2(800f, RoomButtonHeight), 24,
                    () => OnRoomClicked(index), out roomLabels[i]);
                roomButtons[i].gameObject.SetActive(false);
            }

            emptyText = UiFactory.CreateText("Empty", root, 22, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(450f, -300f), new Vector2(800f, 40f));
            emptyText.text = "찾는 중... 같은 네트워크에서 열린 방이 여기에 나타나요";
            emptyText.color = new Color(0.75f, 0.75f, 0.75f);

            statusText = UiFactory.CreateText("Status", root, 22, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(800f, 40f));
            statusText.color = new Color(1f, 0.8f, 0.4f);
            statusText.text = string.Empty;
        }
    }
}
