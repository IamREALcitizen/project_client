// Run in an isolated Unity project containing the real Room UI sources/resources (see README).
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Lobby;

public static class CaptureRoom
{
    static string output;
    static RoomLobbyLayout layout;
    static Camera camera;
    static Canvas canvas;
    public static void Run()
    {
        output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Output");
        Directory.CreateDirectory(output);
        try
        {
            PrepareRoomArt.Run();
            EditorSceneManager.OpenScene("Assets/Room.unity");
            foreach (var oldCamera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) oldCamera.enabled = false;
            AuthSession.SetSession(1, 6, "preview", "바다냥이", "preview");
            camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 5;
            canvas = GameObject.Find("Canvas").GetComponent<Canvas>(); var c = canvas.gameObject;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = new RenderTexture(1080, 1920, 24); camera.targetTexture.Create();
            Canvas.ForceUpdateCanvases();
            layout = c.AddComponent<RoomLobbyLayout>();
            var chatLog = canvas.GetComponentInChildren<WhoisntCitizen.GameUI.ChatLogView>(true);
            var input = canvas.GetComponentInChildren<WhoisntCitizen.GameUI.ChatInputBar>(true);
            layout.Build(canvas, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/KyoboHandwriting2025lyb SDF.asset"),
                canvas.transform.Find("SafeArea/contents"), chatLog, input);
            var room = Room(6);
            Draw(room, true);
            Check(layout.Content.childCount == 12, "Expected 6 players + 6 empty berths");
            Save("Room-6of12.png");
            var hostView = layout.Content.GetChild(0);
            Check(hostView.Find("PortraitMask").GetComponent<Mask>().enabled, "Portrait mask missing");
            Check(hostView.Find("StateIcon").GetComponent<RoomHelmGraphic>().Shape == RoomHelmGraphic.Symbol.Crown, "Host crown missing");
            Check(layout.Content.GetChild(1).Find("StateIcon").GetComponent<RoomHelmGraphic>().Shape == RoomHelmGraphic.Symbol.Ready, "Ready check missing");
            Check(layout.Content.GetChild(3).Find("StateIcon").GetComponent<RoomHelmGraphic>().Shape == RoomHelmGraphic.Symbol.Waiting, "Waiting dot missing");
            var longSelf = new RoomPlayerResponse { userId = 6, nickname = "바다의 열두 번째 선원", ready = false };
            hostView.GetComponent<RoomPlayerView>().Bind(longSelf, true, true);
            Save("Room-long-self.png");
            var selfBadge = hostView.Find("SelfBadge").GetComponent<RectTransform>();
            Check(selfBadge.anchoredPosition.x + selfBadge.rect.width < 650, "Self badge overlaps status");
            hostView.GetComponent<RoomPlayerView>().Bind(room.players[0], true, true);
            var scroll = layout.Content.GetComponentInParent<ScrollRect>();
            Check(layout.Content.rect.height > scroll.viewport.rect.height, "Roster must scroll for 12 slots");
            scroll.verticalNormalizedPosition = 0;
            Save("Room-scroll.png");
            scroll.verticalNormalizedPosition = 1;
            int manage = 0;
            var first = layout.Content.GetChild(1).GetComponent<RoomPlayerView>();
            first.Bind(room.players[1], false, false, true, _ => manage++);
            first.GetComponentInChildren<Button>().onClick.Invoke();
            Check(manage == 1, "Manage callback must fire once");
            layout.Settings.Open(room, true, _ => {});
            Save("Room-settings.png");
            layout.Settings.RefreshPermissions(false);
            var root = layout.Content.GetComponentInParent<Canvas>().transform.Find("SafeArea/CaptainCabinLayout");
            Check(!root.Find("RoomSettingsModal/SettingsCard/Save").gameObject.activeSelf, "Guest must not save settings");
            layout.Settings.Close();
            root.Find("OpenChat").GetComponent<Button>().onClick.Invoke();
            chatLog.AddLine("<b>바다냥이</b>  모두 모이면 출항할게요!");
            chatLog.AddLine("<b>루나</b>  준비 완료! 잘 부탁드려요.");
            Save("Room-chat.png");
            Check(input.gameObject.activeInHierarchy, "Chat input did not open");
            root.Find("CrewChat/CloseChat").GetComponent<Button>().onClick.Invoke();
            Check(!input.gameObject.activeInHierarchy, "Chat input did not close");
            Draw(Room(12), false); Save("Room-12of12-guest.png");
            Check(layout.Content.childCount == 12, "Full room has no extra placeholders");
            foreach (var view in layout.Content.GetComponentsInChildren<RoomPlayerView>())
            {
                Check(!view.transform.Find("ManageCrew").gameObject.activeSelf, "Guest sees host controls");
                Check(!view.transform.Find("Nickname").GetComponent<TMP_Text>().isTextOverflowing, "Name overflow");
            }
            Draw(Room(4), true); Save("Room-4of12.png");
            var small = Room(4); small.maxPlayers = 4; Draw(small, true); Save("Room-4capacity.png");
            Check(layout.Content.childCount == 4, "Capacity reduction retained empty slots");
            camera.targetTexture.Release(); UnityEngine.Object.DestroyImmediate(camera.targetTexture);
            camera.targetTexture = new RenderTexture(1920, 1080, 24); camera.targetTexture.Create();
            Canvas.ForceUpdateCanvases(); typeof(RoomLobbyLayout).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(layout, null);
            Save("Room-landscape.png");
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: 4/6/12 participants, capacity change, empty berths, scrolling, host/guest manage controls, single callback, settings permissions, Korean glyphs, circular portrait masks, crown/check/dot status, long self name badge, existing scene chat open/close; rendered 9 previews. Network APIs were not invoked.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(output, "status.txt"), e.ToString()); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    static RoomDetailResponse Room(int count)
    {
        string[] names = { "바다냥이", "선원 진영", "김유진", "항해사 민재", "장현교", "루나", "해풍", "노을", "마지막 선원", "산호", "돛단배", "바다의 열두 번째 선원" };
        var room = new RoomDetailResponse { id = 2813, title = "바다의 수수께끼", hostUserId = 6, maxPlayers = 12, privateRoom = true, status = RoomStatus.Waiting };
        for (int i = 0; i < count; i++) room.players.Add(new RoomPlayerResponse { userId = 6 + i, nickname = names[i], ready = i > 0 && i != 3 });
        return room;
    }
    static void Draw(RoomDetailResponse room, bool host)
    {
        for (int i = layout.Content.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(layout.Content.GetChild(i).gameObject);
        RoomSession.Set(room); layout.Title.text = room.title; layout.MemberCount.text = $"{room.players.Count} / {room.maxPlayers}";
        layout.RenderInfo(room); layout.StartButton.gameObject.SetActive(host); layout.ReadyButton.gameObject.SetActive(!host);
        foreach (var p in room.players)
            layout.CreatePlayer(p, p.userId == room.hostUserId, host && p.userId == room.hostUserId).Bind(p, p.userId == room.hostUserId, host && p.userId == room.hostUserId, host, _ => {});
        layout.AddEmptySlots(room.maxPlayers - room.players.Count);
        string ancestors = "";
        for (Transform t = layout.Content; t != null; t = t.parent) ancestors += t.name + ":" + t.gameObject.activeSelf + "/";
        Check(layout.Content.gameObject.activeInHierarchy, "Inactive roster: " + ancestors);
        layout.Content.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Save(string name)
    {
        // ScreenSpaceCamera updates its rect during rendering. Settle the new target before fitting the design root.
        for (int pass = 0; pass < 3; pass++)
        {
            camera.Render(); Canvas.ForceUpdateCanvases();
            typeof(RoomLobbyLayout).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layout, null);
        }
        Canvas.ForceUpdateCanvases();
        typeof(RoomLobbyLayout).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layout, null);
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>())
            Check(text.font.HasCharacters(text.text.Replace("\n", ""), out uint[] missing, true, true), "Missing glyphs in " + text.name);
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = null;
    }
}
