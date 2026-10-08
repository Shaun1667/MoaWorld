using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MoaWorld
{
    // Hosting and joining. The host PC is the server; others join from the LAN room list.
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport), typeof(LanDiscovery))]
    public class GameSession : MonoBehaviour
    {
        private NetworkManager network;
        private UnityTransport transport;
        private LanDiscovery discovery;

        public static GameSession Instance { get; private set; }

        public LanDiscovery Discovery => discovery;
        public bool IsInSession => network != null && network.IsListening;

        // Korean reason shown in the lobby when a session ends.
        public event Action<string> SessionEnded;

        private static string HostDisplayName => Environment.MachineName;

        private void Awake()
        {
            Instance = this;
            network = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            discovery = GetComponent<LanDiscovery>();

            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientStopped += OnClientStopped;
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
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HostWorld()
        {
            // Listen on all interfaces so other PCs on the LAN can connect.
            transport.SetConnectionData("127.0.0.1", GameConfig.Instance.gamePort, "0.0.0.0");
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
            discovery.StopListening();
            if (network.StartClient())
            {
                return true;
            }
            discovery.StartListening();
            return false;
        }

        public void Leave()
        {
            network.Shutdown();
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasRoom = network.ConnectedClientsIds.Count < GameConfig.Instance.maxPlayers;
            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            response.Position = MoaBox.Instance != null ? MoaBox.Instance.SpawnPosition : (Vector3?)null;
            response.Reason = hasRoom ? null : "방이 가득 찼어요";
            response.Pending = false;
        }

        private void OnClientStopped(bool wasHost)
        {
            discovery.StopBroadcasting();
            discovery.StartListening();

            string reason;
            if (wasHost)
            {
                reason = "방을 닫았어요";
            }
            else if (!string.IsNullOrEmpty(network.DisconnectReason))
            {
                reason = network.DisconnectReason;
            }
            else
            {
                reason = "방에 연결할 수 없거나 연결이 끊어졌어요";
            }
            SessionEnded?.Invoke(reason);
        }
    }
}
