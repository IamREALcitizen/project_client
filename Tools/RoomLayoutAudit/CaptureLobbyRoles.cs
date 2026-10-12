// Actual Lobby.unity CreateRoomPopup and role entry. Only network/scene transitions are mocked.
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

public static class CaptureLobbyRoles
{
    static CreateRoomPopup popup;
    static CreateRoomRoleEditor editor;
    static RoleCompositionLayout layout;
    static Canvas canvas;
    static Camera camera;
    static string output;
    static Transform Root => layout.transform.Find("SafeArea/VoyageLog");
    static Transform Rows => Root.Find("Viewport/Roles");
    static T Field<T>(string name) => (T)typeof(CreateRoomPopup).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);

    public static void Run()
    {
        output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LobbyRolesOutput");
        Directory.CreateDirectory(output);
        try
        {
            EditorSceneManager.OpenScene("Assets/Lobby.unity");
            popup = Resources.FindObjectsOfTypeAll<CreateRoomPopup>().Single(p => p.gameObject.scene.IsValid());
            canvas = popup.GetComponentInParent<Canvas>(true).rootCanvas;
            foreach (var old in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) old.enabled = false;
            for (var t = popup.transform.parent; t != null; t = t.parent) t.gameObject.SetActive(true);
            camera = new GameObject("LobbyRoleAuditCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = new RenderTexture(1080, 1920, 24); camera.targetTexture.Create();
            if (EventSystem.current == null)
            {
                var es = new GameObject("AuditEvents").AddComponent<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(es, null);
            }
            var options = (RoleSetupOptionsDto)typeof(CaptureRoles).GetMethod("Options", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            popup.Open();
            editor = popup.GetComponentInChildren<CreateRoomRoleEditor>(true);
            var entry = popup.transform.Find("Background/GameMode/Button_Custom").GetComponent<Button>();
            Check(entry.gameObject.activeInHierarchy && entry.IsInteractable(), "Lobby entry is hidden");
            foreach (Transform child in entry.transform.parent)
                if (child != entry.transform) Check(!child.gameObject.activeSelf, "Old role tabs remain");
            for (int i = 0; i < entry.onClick.GetPersistentEventCount(); i++)
                Check(entry.onClick.GetPersistentListenerState(i) == UnityEngine.Events.UnityEventCallState.Off, "Old SetActive still enabled");
            Raycast(entry);
            RoomApi.PendingLoad(new ApiResult<RoleSetupOptionsDto> { message = "load failed", statusCode = 503 });
            Check(Field<StatusMessageView>("statusMessage").CurrentMessage.Contains("다시"), "Missing retry instruction");
            entry.onClick.Invoke(); entry.onClick.Invoke();
            Check(RoomApi.Loads == 2 && !editor.IsOpen, "Duplicate fetch or premature old popup");
            var staleLoad = RoomApi.PendingLoad;
            popup.Close(); popup.Open();
            staleLoad(new ApiResult<RoleSetupOptionsDto> { success = true, data = options });
            Check(Field<RoleSetupOptionsDto>("roleOptions") == null && !editor.IsOpen, "Old request changed reopened popup");
            var title = Field<TMP_InputField>("titleInput");
            var capacity = Field<TMP_InputField>("maxPlayersInput");
            title.text = "로비 항해일지"; capacity.text = "8";
            entry.onClick.Invoke();
            RoomApi.PendingLoad(new ApiResult<RoleSetupOptionsDto> { success = true, data = options });
            layout = canvas.GetComponentInChildren<RoleCompositionLayout>(true);
            Check(editor.IsOpen && RoomApi.Loads == 3, "Queued entry did not open after fetch");
            Check(Root.Find("PlayerCount").GetComponent<TMP_Text>().text == "8명", "Wrong count from create-room input");
            Check(Rows.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("Role_")) == 14, "Missing catalog roles");
            Raycast(Root.Find("Apply").GetComponent<Button>());
            Save("Lobby-role-logbook.png");
            ClickRole("CREW_BOATSWAIN", "Plus");
            popup.OnCreateClicked();
            Check(RoomApi.Creates == 0, "Enter created a room behind editor");
            popup.OnCancelClicked();
            Check(!editor.IsOpen && popup.IsOpen, "Cancel should return to create-room");
            entry.onClick.Invoke();
            Check(Number("CREW_BOATSWAIN") == "0", "Cancelled draft leaked");
            ClickRole("CREW_BOATSWAIN", "Plus"); Click("Apply");
            Check(RoomApi.Saves == 0 && RoomApi.Creates == 0 && popup.IsOpen && !editor.IsOpen, "Lobby apply must only update room draft");
            Check(Field<TMP_Text>("modeInfoText").text.Contains("직접"), "Summary did not update");
            Check(title.text == "로비 항해일지" && capacity.text == "8", "Room inputs lost");
            Save("Lobby-role-entry.png");
            entry.onClick.Invoke();
            Root.Find("Random").GetComponent<Toggle>().isOn = true;
            Row("CREW_DOCTOR").Find("CandidateArea").GetComponent<Toggle>().isOn = false;
            Click("Apply");
            entry.onClick.Invoke();
            Check(Root.Find("Random").GetComponent<Toggle>().isOn && !Row("CREW_DOCTOR").Find("CandidateArea").GetComponent<Toggle>().isOn,
                "Random mode/candidates not restored");
            Root.Find("Random").GetComponent<Toggle>().isOn = false;
            Check(Number("CREW_BOATSWAIN") == "1", "Custom draft lost by mode switch");
            Click("Apply");

            popup.OnCreateClicked(); popup.OnCreateClicked();
            Check(RoomApi.Creates == 1 && RoomApi.CreatedRequest.maxPlayers == 8, "Duplicate/wrong create request");
            entry.onClick.Invoke(); Check(!editor.IsOpen, "Can edit while creating");
            RoomApi.PendingCreate(new ApiResult<RoomResponse> { success = true, data = new RoomResponse { id = 301, title = title.text, maxPlayers = 8 } });
            Check(RoomApi.Saves == 1 && RoomApi.SavedRoomId == 301 && RoomApi.SavedSetup.mode == RoleSetupModes.Custom, "Applied setup omitted from created room");
            Check(RoleSetupRules.Count(RoleSetupRules.FindCustom(RoomApi.SavedSetup, 8).roles, "CREW_BOATSWAIN") == 1, "Custom role omitted from PUT");
            RoomApi.PendingSave(new ApiResult { statusCode = 503, message = "save failed" });
            Check(RoomApi.Leaves == 1 && RoomApi.LeftRoomId == 301 && SceneLoader.Loads == 0, "Failed save should clean created room before entry");
            RoomApi.PendingLeave(new ApiResult { success = true });
            Check(!popup.IsRequesting && Field<RoleSetupDto>("roleSetup").mode == RoleSetupModes.Custom, "Retry lost edited setup");
            popup.OnCreateClicked();
            RoomApi.PendingCreate(new ApiResult<RoomResponse> { success = true, data = new RoomResponse { id = 302, title = title.text, maxPlayers = 8 } });
            RoomApi.PendingSave(new ApiResult { success = true });
            Check(RoomSession.RoomId == 302 && SceneLoader.Loads == 1, "Successful create/save did not enter room once");
            // A new dialog must discard the prior creation draft while retaining the catalog cache.
            popup.Open(); entry.onClick.Invoke();
            Check(Number("CREW_BOATSWAIN") == "0" && Field<RoleSetupDto>("roleSetup").mode == RoleSetupModes.Recommended, "New popup inherited old setup");
            popup.gameObject.SetActive(false);
            typeof(CreateRoomPopup).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(popup, null);
            Check(!layout.gameObject.activeSelf, "Parent disabled but overlay remained");
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: real Lobby CreateRoomPopup/button, hidden tabs, legacy callback disabled, raycasts, catalog failure/retry and stale response, all 14 roles, input count, cancel/apply, random/custom restoration, parent input preservation, no premature API call, created-room payload and cleanup/retry, single transition, fresh-open reset, parent close.");
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
