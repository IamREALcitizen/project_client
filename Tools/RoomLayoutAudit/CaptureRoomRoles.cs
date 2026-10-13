// Actual Room.unity settings entry + shared role editor, isolated API responses, no live network.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WhoisntCitizen.Common;
using WhoisntCitizen.Game;
using WhoisntCitizen.Lobby;
using WhoisntCitizen.Network;

public static class CaptureRoomRoles
{
    static RoomSettingsPopup popup;
    static CreateRoomRoleEditor editor;
    static RoleCompositionLayout layout;
    static Canvas canvas;
    static Camera camera;
    static string output;
    static int commits;
    static Transform Root => layout.transform.Find("SafeArea/VoyageLog");
    static Transform Rows => Root.Find("Viewport/Roles");
    static RoleSetupOptionsDto options;
    static RoomDetailResponse room;

    public static void Run()
    {
        output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RoomRolesOutput");
        Directory.CreateDirectory(output);
        try
        {
            EditorSceneManager.OpenScene("Assets/Room.unity");
            popup = Resources.FindObjectsOfTypeAll<RoomSettingsPopup>().Single(p => p.gameObject.scene.IsValid());
            Check(popup.name == "Popup_Settings", "Wrong room settings host");
            canvas = popup.GetComponentInParent<Canvas>(true).rootCanvas;
            foreach (var old in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) old.enabled = false;
            for (var t = popup.transform.parent; t != null; t = t.parent) t.gameObject.SetActive(true);
            camera = new GameObject("RoomRoleAuditCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = new RenderTexture(1080, 1920, 24); camera.targetTexture.Create();
            if (EventSystem.current == null)
            {
                var es = new GameObject("AuditEvents").AddComponent<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(es, null);
            }
            var cabin = canvas.gameObject.AddComponent<RoomLobbyLayout>();
            cabin.Build(canvas, Resources.Load<TMP_FontAsset>("RoomLobby/CabinFont"), canvas.transform.Find("SafeArea/contents"),
                canvas.GetComponentInChildren<WhoisntCitizen.GameUI.ChatLogView>(true),
                canvas.GetComponentInChildren<WhoisntCitizen.GameUI.ChatInputBar>(true));

            options = (RoleSetupOptionsDto)typeof(CaptureRoles).GetMethod("Options", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            room = JsonUtility.FromJson<RoomDetailResponse>("{\"id\":2813,\"title\":\"바다의 수수께끼\",\"hostUserId\":6,\"maxPlayers\":8,\"status\":\"WAITING\",\"roleSetup\":{\"mode\":\"CUSTOM\",\"customCompositions\":[{\"playerCount\":8,\"roles\":[\"PIRATE_RAIDER\",\"PIRATE_RAIDER\",\"PIRATE_PARROT\",\"CREW_CAPTAIN\",\"CREW_DOCTOR\",\"CREW_LOOKOUT\",\"CREW_BOATSWAIN\",\"CREW_SAILOR\"]}],\"randomCandidates\":[\"CREW_CAPTAIN\",\"CREW_DOCTOR\"]}}");
            Check(room.roleSetup.mode == RoleSetupModes.Custom, "Room JSON loses roleSetup");
            AuthSession.SetSession(1, 6, "audit", "바다냥이", "audit");
            RoomSession.Set(room);
            cabin.SettingsButton.onClick.AddListener(() => popup.Open(room, _ => commits++));
            cabin.SettingsButton.onClick.Invoke();
            editor = popup.GetComponentInChildren<CreateRoomRoleEditor>(true);
            var entry = popup.transform.Find("Background/GameMode/Button_Custom").GetComponent<Button>();
            Check(entry.gameObject.activeInHierarchy && entry.IsInteractable(), "Room role entry is hidden");
            foreach (Transform child in entry.transform.parent)
                if (child != entry.transform) Check(!child.gameObject.activeSelf, "Old role tabs remain visible");
            for (int i = 0; i < entry.onClick.GetPersistentEventCount(); i++)
                Check(entry.onClick.GetPersistentListenerState(i) == UnityEngine.Events.UnityEventCallState.Off, "Legacy popup callback still enabled");
            Settle(); Raycast(entry);
            Save("Room-role-entry.png");
            entry.onClick.Invoke(); entry.onClick.Invoke();
            Check(RoomApi.Loads == 1 && popup.IsRequesting && !editor.IsOpen, "Load duplicates or old popup opens");
            RoomApi.PendingLoad(new ApiResult<RoleSetupOptionsDto> { message = "목록 로딩 실패", statusCode = 503 });
            Check(!popup.IsRequesting && !editor.IsOpen && entry.IsInteractable(), "Load failure cannot retry");
            entry.onClick.Invoke();
            var staleLoad = RoomApi.PendingLoad;
            popup.Close(); popup.Open(room, _ => commits++);
            staleLoad(new ApiResult<RoleSetupOptionsDto> { success = true, data = options });
            Check(!editor.IsOpen, "Stale load reopened editor");
            entry.onClick.Invoke();
            RoomApi.PendingLoad(new ApiResult<RoleSetupOptionsDto> { success = true, data = options });
            layout = canvas.GetComponentInChildren<RoleCompositionLayout>(true);
            Check(editor.IsOpen && !popup.IsRequesting, "Room entry never opened logbook");
            Check(Number("CREW_BOATSWAIN") == "1", "Saved custom setup was replaced by recommendation");
            Check(Rows.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("Role_")) == 14, "Missing roles");
            Save("Room-role-logbook.png");
            Raycast(Root.Find("Apply").GetComponent<Button>());
            popup.transform.Find("Background/Items/Area_Title").GetComponentInChildren<TMP_InputField>().text = "아직 저장하지 않은 방 이름";
            ClickRole("CREW_BOATSWAIN", "Plus");
            popup.OnSaveClicked(); // input Enter must not submit parent behind overlay
            Click("Apply"); Click("Apply");
            Check(RoomApi.Saves == 1 && RoomApi.SavedRoomId == 2813 && editor.IsSaving && popup.IsRequesting, "Save not locked / wrong room");
            Check(!Root.Find("Apply").GetComponent<Button>().IsInteractable(), "Saving controls are active");
            popup.OnCancelClicked();
            Check(editor.IsOpen && commits == 0, "Save closed before success");
            RoomApi.PendingSave(new ApiResult { statusCode = 503, message = "연결을 확인한 뒤 다시 적용해 주세요." });
            Check(editor.IsOpen && !editor.IsSaving && Number("CREW_BOATSWAIN") == "2", "Failure lost draft");
            Check(Root.Find("Validation").GetComponent<TMP_Text>().text.Contains("연결"), "Save error invisible");
            Save("Room-role-save-error.png");
            Click("Apply"); RoomApi.PendingSave(new ApiResult { success = true });
            Check(!editor.IsOpen && popup.IsOpen && commits == 1, "Successful save must return to settings once");
            Check(popup.transform.Find("Background/Items/Area_Title").GetComponentInChildren<TMP_InputField>().text == "아직 저장하지 않은 방 이름", "Role save erased room input draft");
            entry.onClick.Invoke();
            Check(Number("CREW_BOATSWAIN") == "2", "Saved composition not retained on reopen");
            Root.Find("Random").GetComponent<Toggle>().isOn = true;
            Row("CREW_DOCTOR").Find("CandidateArea").GetComponent<Toggle>().isOn = false;
            Click("Apply");
            Check(RoomApi.SavedSetup.mode == RoleSetupModes.Random && !RoomApi.SavedSetup.randomCandidates.Contains("CREW_DOCTOR"), "Random changes missing in PUT");
            var staleSave = RoomApi.PendingSave;
            popup.Close(); popup.Open(room, _ => commits++);
            staleSave(new ApiResult { success = true });
            Check(!editor.IsOpen && !popup.IsRequesting && commits == 1, "Old save affected reopened popup");
            entry.onClick.Invoke(); popup.OnCancelClicked();
            Check(!editor.IsOpen && popup.IsOpen, "ESC must close child first");
            entry.onClick.Invoke(); popup.Close();
            Check(!layout.gameObject.activeSelf && !popup.IsOpen, "Lifecycle close leaves overlay visible");
            AuthSession.SetSession(2, 7, "guest", "참가자", "audit");
            popup.Open(room, _ => commits++); entry.onClick.Invoke();
            Check(!editor.IsOpen, "Guest can edit roles");
            popup.Close();
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: real Room settings entry, 14 roles, saved JSON, legacy callbacks off, load failure/retry, stale load, async save failure/retry/success, duplicate save, random payload, parent draft preservation, stale save, child cancel, lifecycle close, host guard, raycasts.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "status.txt"), "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    static Transform Row(string code) => Rows.Cast<Transform>().First(t => t.gameObject.activeSelf && t.name == "Role_" + code);
    static string Number(string code) => Row(code).Find("Amount/Count").GetComponent<TMP_Text>().text;
    static void Click(string name) => Root.Find(name).GetComponent<Button>().onClick.Invoke();
    static void ClickRole(string code, string name) => Row(code).Find("Amount/" + name).GetComponent<Button>().onClick.Invoke();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Settle()
    {
        for (int i = 0; i < 3; i++)
        {
            camera.Render(); Canvas.ForceUpdateCanvases();
            if (layout != null) typeof(RoleCompositionLayout).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(layout, null);
        }
    }
    static void Raycast(Button button)
    {
        Settle();
        foreach (var raycaster in canvas.GetComponentsInChildren<GraphicRaycaster>())
            typeof(BaseRaycaster).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(raycaster, null);
        var rect = (RectTransform)button.transform;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
        }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Button blocked: " + button.name + " / " + string.Join(", ", hits.Select(h => h.gameObject.name)));
    }
    static void Save(string name)
    {
        Settle(); camera.Render(); RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = null;
    }
}
