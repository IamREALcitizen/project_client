// Copy ONLY into the isolated audit project. Never import this file into the game Assets.
using System;
using WhoisntCitizen.Game;
using WhoisntCitizen.Network;

namespace WhoisntCitizen.Lobby
{
    public static class RoomApi
    {
        public static int Loads, Saves, Creates, Leaves;
        public static long SavedRoomId, LeftRoomId;
        public static CreateRoomRequest CreatedRequest;
        public static RoleSetupDto SavedSetup;
        public static Action<ApiResult<RoleSetupOptionsDto>> PendingLoad;
        public static Action<ApiResult> PendingSave;
        public static Action<ApiResult<RoomResponse>> PendingCreate;
        public static Action<ApiResult> PendingLeave;
        public static void CreateRoom(string title, int maxPlayers, bool privateRoom, string password, Action<ApiResult<RoomResponse>> done)
        {
            Creates++;
            CreatedRequest = new CreateRoomRequest { title = title, maxPlayers = maxPlayers, privateRoom = privateRoom, password = password };
            PendingCreate = done;
        }
        public static void LeaveRoom(long id, Action<ApiResult> done)
        {
            Leaves++; LeftRoomId = id; PendingLeave = done;
        }
        public static void GetRoleSetupOptions(Action<ApiResult<RoleSetupOptionsDto>> done)
        {
            Loads++;
            PendingLoad = done;
        }
        public static void SetRoleSetup(long roomId, RoleSetupDto setup, Action<ApiResult> done)
        {
            Saves++;
            SavedRoomId = roomId;
            SavedSetup = setup.Clone();
            PendingSave = done;
        }
        public static void UpdateSettings(long id, string title, int max, bool privacy, string password, Action<ApiResult> done)
        {
            throw new Exception("Role editing must not submit unrelated room settings");
        }
    }
}

namespace WhoisntCitizen.Common
{
    public static class SceneLoader
    {
        public static bool IsLoading => false;
        public static int Loads;
        public static bool Load(SceneType scene) { Loads++; return true; }
    }
}

namespace WhoisntCitizen.Chat
{
    public static class ChatNotice
    {
        public static void Post(string message, bool system = true) { }
    }
}

namespace WhoisntCitizen.Network
{
    public static class RealtimeSession
    {
        public static void OnLoggedIn() { }
        public static void OnLoggedOut() { }
    }
    public class RealtimeClient
    {
        public static RealtimeClient ExistingInstance => null;
        public void LeaveRoom() { }
    }
}
