using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MoaWorld
{
    // Hosting and joining. The host PC is the server; others join from the LAN room list.
    // Each PC has a unique player ID that is sent when connecting, so the host can find that player's save.
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport), typeof(LanDiscovery))]
    [RequireComponent(typeof(WorldSave))]
    public class GameSession : MonoBehaviour
    {
        private const string PlayerIdKey = "MoaWorld.PlayerId";
        private const int MaxPlayerIdLength = 64;

        private readonly Dictionary<ulong, string> playerIds = new Dictionary<ulong, string>();

        private NetworkManager network;
        private UnityTransport transport;
        private LanDiscovery discovery;
        private WorldSave worldSave;
        private bool leftByChoice;

        public static GameSession Instance { get; private set; }

        public LanDiscovery Discovery => discovery;
        public bool IsInSession => network != null && network.IsListening;

        // Korean reason shown in the lobby when a session ends.
        public event Action<string> SessionEnded;

        private static string HostDisplayName => Environment.MachineName;

        // Created once per PC and kept in PlayerPrefs.
        private static string LocalPlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(PlayerIdKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        private void Awake()
        {
            Instance = this;
            network = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            discovery = GetComponent<LanDiscovery>();
            worldSave = GetComponent<WorldSave>();

            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientStopped += OnClientStopped;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            discovery.PlayerCountProvider = () => network.IsServer ? network.ConnectedClientsIds.Count : 0;
        }

        private void Start()
        {
            discovery.StartListening();
        }

        private void OnDestroy()
        {
            if (network != null)
            {
                network.OnClientStopped -= OnClientStopped;
                network.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HostWorld()
        {
            playerIds.Clear();
            worldSave.Load();

            // Listen on all interfaces so other PCs on the LAN can connect.
            transport.SetConnectionData("127.0.0.1", GameConfig.Instance.gamePort, "0.0.0.0");
            network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(LocalPlayerId);
            if (!network.StartHost())
            {
                return false;
            }
            discovery.StopListening();
            discovery.StartBroadcasting(HostDisplayName);
            return true;
        }

        public bool Join(LanDiscovery.RoomInfo room)
        {
            transport.SetConnectionData(room.address, room.port);
            network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(LocalPlayerId);
            discovery.StopListening();
            if (network.StartClient())
            {
                return true;
            }
            discovery.StartListening();
            return false;
        }

        // Closing a hosted room saves the world (WorldSave listens for the server stopping).
        public void Leave()
        {
            leftByChoice = true;
            network.Shutdown();
        }

        // Server only.
        public string GetPlayerId(ulong clientId)
        {
            return playerIds.TryGetValue(clientId, out string id) ? id : null;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasRoom = network.ConnectedClientsIds.Count < GameConfig.Instance.maxPlayers;
            string playerId = ReadPlayerId(request.Payload);
            bool valid = playerId != null;

            response.Approved = hasRoom && valid;
            response.CreatePlayerObject = response.Approved;
            response.Position = MoaBox.Instance != null ? MoaBox.Instance.SpawnPosition : (Vector3?)null;
            response.Reason = !hasRoom ? "방이 가득 찼어요" : !valid ? "접속 정보가 올바르지 않아요" : null;
            response.Pending = false;

            if (response.Approved)
            {
                playerIds[request.ClientNetworkId] = MakeUnique(playerId);
            }
        }

        private static string ReadPlayerId(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxPlayerIdLength)
            {
                return null;
            }
            string id = Encoding.UTF8.GetString(payload).Trim();
            return id.Length > 0 ? id : null;
        }

        // Two game windows on the same PC share a player ID (handy for testing); the second one
        // gets its own save slot instead of being refused.
        private string MakeUnique(string playerId)
        {
            string candidate = playerId;
            for (int n = 2; playerIds.ContainsValue(candidate); n++)
            {
                candidate = $"{playerId}#{n}";
            }
            return candidate;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (network.IsServer)
            {
                playerIds.Remove(clientId);
            }
        }

        private void OnClientStopped(bool wasHost)
        {
            playerIds.Clear();
            discovery.StopBroadcasting();
            discovery.StartListening();

            string reason;
            if (wasHost)
            {
                reason = "방을 닫았어요. 월드가 저장됐어요";
            }
            else if (leftByChoice)
            {
                reason = "방에서 나왔어요";
            }
            else if (!string.IsNullOrEmpty(network.DisconnectReason))
            {
                reason = network.DisconnectReason;
            }
            else
            {
                reason = "방에 연결할 수 없거나 연결이 끊어졌어요";
            }
            leftByChoice = false;
            SessionEnded?.Invoke(reason);
        }
    }
}
