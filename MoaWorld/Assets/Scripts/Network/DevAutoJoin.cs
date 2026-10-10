using System;
using UnityEngine;

namespace MoaWorld
{
    // Development builds only: launching with "-autojoin" joins the first LAN room found,
    // so a second game window can be tested without clicking through the lobby.
    public class DevAutoJoin : MonoBehaviour
    {
        private const string Argument = "-autojoin";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (Debug.isDebugBuild && !Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), Argument) >= 0)
            {
                new GameObject(nameof(DevAutoJoin)).AddComponent<DevAutoJoin>();
            }
        }

        private void Update()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.IsInSession)
            {
                return;
            }
            foreach (LanDiscovery.RoomInfo room in session.Discovery.Rooms)
            {
                Debug.Log($"[DevAutoJoin] Joining {room.hostName}");
                session.Join(room);
                Destroy(gameObject);
                return;
            }
        }
    }
}
