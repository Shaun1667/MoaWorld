using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MoaWorld
{
    // Finds rooms on the same network with UDP broadcast. A host announces itself every second;
    // the lobby listens and keeps a list of rooms heard from recently.
    public class LanDiscovery : MonoBehaviour
    {
        private const string Magic = "MOAWORLD1";
        private const char Separator = '|';

        public class RoomInfo
        {
            public string sessionId;
            public string address;
            public ushort port;
            public string hostName;
            public int players;
            public int maxPlayers;
            public float lastSeen;
        }

        private readonly Dictionary<string, RoomInfo> rooms = new Dictionary<string, RoomInfo>();
        private readonly List<string> expired = new List<string>();

        private UdpClient sender;
        private UdpClient listener;
        private string sessionId;
        private string hostName;
        private float nextBroadcastTime;

        public Func<int> PlayerCountProvider { get; set; }
        public IEnumerable<RoomInfo> Rooms => rooms.Values;

        public void StartBroadcasting(string displayName)
        {
            StopBroadcasting();
            sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            hostName = displayName;
            sender = new UdpClient { EnableBroadcast = true };
            nextBroadcastTime = 0f;
        }

        public void StopBroadcasting()
        {
            sender?.Close();
            sender = null;
        }

        public void StartListening()
        {
            StopListening();
            rooms.Clear();
            try
            {
                // Several game instances on one PC must be able to listen on the same port.
                listener = new UdpClient { ExclusiveAddressUse = false };
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, GameConfig.Instance.discoveryPort));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[LanDiscovery] Could not listen for rooms: {e.Message}");
                StopListening();
            }
        }

        public void StopListening()
        {
            listener?.Close();
            listener = null;
            rooms.Clear();
        }

        private void Update()
        {
            if (sender != null && Time.unscaledTime >= nextBroadcastTime)
            {
                nextBroadcastTime = Time.unscaledTime + GameConfig.Instance.discoveryBroadcastSeconds;
                Broadcast();
            }
            if (listener != null)
            {
                Receive();
                PruneExpired();
            }
        }

        private void Broadcast()
        {
            GameConfig config = GameConfig.Instance;
            int players = PlayerCountProvider != null ? PlayerCountProvider() : 0;
            string message = string.Join(Separator.ToString(), Magic, sessionId, config.gamePort, players, config.maxPlayers, hostName);
            byte[] data = Encoding.UTF8.GetBytes(message);
            try
            {
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, config.discoveryPort));
                // Loopback too, so a second game instance on the same PC always sees the room.
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, config.discoveryPort));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[LanDiscovery] Broadcast failed: {e.Message}");
            }
        }

        private void Receive()
        {
            try
            {
                while (listener.Available > 0)
                {
                    IPEndPoint from = null;
                    byte[] data = listener.Receive(ref from);
                    TryAddRoom(Encoding.UTF8.GetString(data), from);
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[LanDiscovery] Receive failed: {e.Message}");
            }
        }

        private void TryAddRoom(string message, IPEndPoint from)
        {
            string[] parts = message.Split(new[] { Separator }, 6);
            if (parts.Length != 6 || parts[0] != Magic
                || !ushort.TryParse(parts[2], out ushort port)
                || !int.TryParse(parts[3], out int players)
                || !int.TryParse(parts[4], out int maxPlayers))
            {
                return;
            }

            string id = parts[1];
            if (!rooms.TryGetValue(id, out RoomInfo room))
            {
                room = new RoomInfo { sessionId = id };
                rooms[id] = room;
            }
            // Prefer the real LAN address over loopback when both arrive.
            if (room.address == null || !IPAddress.IsLoopback(from.Address))
            {
                room.address = from.Address.ToString();
            }
            room.port = port;
            room.players = players;
            room.maxPlayers = maxPlayers;
            room.hostName = parts[5];
            room.lastSeen = Time.unscaledTime;
        }

        private void PruneExpired()
        {
            float timeout = GameConfig.Instance.discoveryRoomTimeoutSeconds;
            expired.Clear();
            foreach (KeyValuePair<string, RoomInfo> pair in rooms)
            {
                if (Time.unscaledTime - pair.Value.lastSeen > timeout)
                {
                    expired.Add(pair.Key);
                }
            }
            foreach (string key in expired)
            {
                rooms.Remove(key);
            }
        }

        private void OnDestroy()
        {
            StopBroadcasting();
            StopListening();
        }
    }
}
