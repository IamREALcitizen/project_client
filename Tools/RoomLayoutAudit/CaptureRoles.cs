// Isolated Unity editor audit: real Lobby hierarchy/resources, fixture data, no network.
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
using WhoisntCitizen.Game;
using WhoisntCitizen.Lobby;

public static class CaptureRoles
{
    static string output;
    static Camera camera;
    static Canvas canvas;
    static CreateRoomRoleEditor editor;
    static RoleCompositionLayout layout;
    static RoleSetupDto source;
    static RoleSetupOptionsDto options;
    static int commits;
    static Transform Root => layout.transform.Find("SafeArea/VoyageLog");
    static Transform Rows => Root.Find("Viewport/Roles");

    public static void Run()
    {
        output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RolesOutput");
        Directory.CreateDirectory(output);
        try
        {
            EditorSceneManager.OpenScene("Assets/Lobby.unity");
            foreach (var old in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) old.enabled = false;
            var host = Resources.FindObjectsOfTypeAll<Transform>().First(t => t.name == "Popup_JobCustom" && t.gameObject.scene.IsValid());
            canvas = host.GetComponentInParent<Canvas>(true).rootCanvas;
            for (Transform t = host.parent; t != null; t = t.parent) t.gameObject.SetActive(true);
            camera = new GameObject("RoleAuditCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = new RenderTexture(1080, 1920, 24); camera.targetTexture.Create();
            if (EventSystem.current == null)
            {
                var events = new GameObject("AuditEvents").AddComponent<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(events, null);
            }
            Canvas.ForceUpdateCanvases();
            editor = host.gameObject.AddComponent<CreateRoomRoleEditor>();
            editor.Initialize(message => { throw new Exception(message); }, () => commits++);
            options = Options();
            source = new RoleSetupDto { mode = RoleSetupModes.Recommended, randomCandidates = new List<string>(options.randomCandidates) };
            Open(8);
            Save("Roles-8-recommended.png");
            Check(Rows.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("Role_")) == 14, "All 14 roles must be shown");
            Check(Root.Find("Viewport").GetComponent<ScrollRect>().content.rect.height > 830, "Ledger should scroll");
            Check(Row("NEUTRAL_SIREN").gameObject.activeSelf, "Neutral role hidden");
            foreach (var r in Rows.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.name.StartsWith("Role_")))
                Check(r.Find("PortraitMask/Portrait").GetComponent<Image>().sprite != null, "Missing portrait: " + r.name);
            CheckRaycast(Row("CREW_CAPTAIN").Find("Amount/Plus").GetComponent<Button>());
            ClickRole("PIRATE_COOK", "Plus");
            Check(source.mode == RoleSetupModes.Recommended && source.customCompositions.Count == 0, "Draft leaked before apply");
            editor.Close();
            Check(commits == 0 && !layout.gameObject.activeSelf, "Cancel committed or left overlay visible");
            Open(8);
            Check(Number("PIRATE_COOK") == "0", "Cancelled edit survived");
            ClickRole("CREW_BOATSWAIN", "Plus");
            Click("Apply");
            Check(commits == 1 && !editor.IsOpen && source.mode == RoleSetupModes.Custom, "Apply must commit once and close");
            Check(RoleSetupRules.Count(RoleSetupRules.FindCustom(source, 8).roles, "CREW_BOATSWAIN") == 1, "Custom role not committed");
            Open(8);
            Save("Roles-8-custom.png");

            Random(true);
            Check(!Row("CREW_CAPTAIN").Find("Amount/Plus").GetComponent<Button>().interactable, "Random allows quantity editing");
            Check(Number("CREW_CAPTAIN") == "—", "Random displays predetermined quantity");
            Check(Row("NEUTRAL_SIREN").GetComponent<CanvasGroup>().alpha < 1, "Neutral state unclear");
            Check(Row("CREW_SAILOR").Find("CandidateArea/AlwaysIncluded") != null, "Base candidate is not locked");
            Toggle("CREW_DOCTOR").isOn = false;
            Check(source.randomCandidates.Contains("CREW_DOCTOR"), "Candidate leaked before apply");
            Save("Roles-random.png");
            Random(false);
            Check(Number("CREW_BOATSWAIN") == "1", "Switching modes lost manual draft");
            Random(true);
            Click("Apply");
            Check(commits == 2 && source.mode == RoleSetupModes.Random && !source.randomCandidates.Contains("CREW_DOCTOR"), "Random save failed");

            Open(8); Random(false);
            ClickRole("PIRATE_RAIDER", "Minus"); ClickRole("PIRATE_RAIDER", "Minus");
            Check(!Root.Find("Apply").GetComponent<Button>().interactable, "Invalid composition can apply");
            Check(Root.Find("Validation").GetComponent<TMP_Text>().text.Contains("공격"), "Missing actionable validation");
            Save("Roles-invalid.png");
            Click("Apply");
            Check(commits == 2 && editor.IsOpen, "Invalid draft escaped apply guard");
            Click("Recommend");
            Check(Number("CREW_BOATSWAIN") == "0" && Root.Find("Apply").GetComponent<Button>().interactable, "Recommendation failed to restore selected count");

            for (int i = 0; i < 8; i++) Click("PreviousCount");
            Check(Root.Find("PlayerCount").GetComponent<TMP_Text>().text == "4명", "Minimum bound incorrect");
            ClickRole("CREW_CAPTAIN", "Plus");
            Check(Number("CREW_CAPTAIN") == "2", "4 player custom edit failed");
            for (int i = 0; i < 8; i++) Click("NextCount");
            Check(Root.Find("PlayerCount").GetComponent<TMP_Text>().text == "12명", "Maximum bound incorrect");
            ClickRole("PIRATE_COOK", "Plus");
            Click("Apply");
            Check(commits == 3 && RoleSetupRules.FindCustom(source, 4) != null && RoleSetupRules.FindCustom(source, 12) != null, "Per-count custom table not preserved");

            Open(12);
            Root.Find("Viewport").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            Save("Roles-12-neutral.png");
            Check(Root.Find("Apply").gameObject.activeInHierarchy, "Sticky apply disappeared after scrolling");
            editor.Close();
            // Invalid hidden count must also be explained and blocked.
            var invalid = source.Clone();
            RoleSetupRules.SetCustom(invalid, 4, new List<string> { "CREW_SAILOR", "CREW_SAILOR", "CREW_SAILOR", "CREW_SAILOR" });
            editor.Open(invalid, options, 8);
            Check(!Root.Find("Apply").GetComponent<Button>().interactable, "Invalid different count can save");
            Check(Root.Find("Validation").GetComponent<TMP_Text>().text.Contains("4인"), "Invalid count is not identified");
            editor.Close();

            Open(8);
            SetTarget(390, 844); Save("Roles-mobile.png");
            SetTarget(1920, 1080); Save("Roles-landscape.png");
            editor.Close();
            Check(!layout.gameObject.activeInHierarchy, "Overlay remains active after closing");
            // A parent reset disables the legacy host instead of calling Close.
            Open(8); editor.gameObject.SetActive(false);
            typeof(CreateRoomRoleEditor).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(editor, null);
            Check(!layout.gameObject.activeSelf, "Parent reset leaves orphan overlay");

            File.WriteAllText(Path.Combine(output, "status.txt"),
                "PASS: real Lobby popup hierarchy; 14 roles and portraits; button raycast; draft/cancel/apply; single callback; random and manual restoration; candidate persistence; base locks; neutral roles; selected and other-count validation; 4-12 count bounds and independent custom tables; recommendation; scrolling and fixed footer; Korean glyphs; 7 portrait/mobile/landscape renders. No network APIs were invoked.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(output, "status.txt"), e.ToString());
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }

    static void Open(int count)
    {
        editor.Open(source, options, count);
        layout = canvas.GetComponentInChildren<RoleCompositionLayout>(true);
        Check(editor.IsOpen, "Editor did not open");
    }
    static Transform Row(string code) => Rows.Cast<Transform>().First(t => t.gameObject.activeSelf && t.name == "Role_" + code);
    static string Number(string code) => Row(code).Find("Amount/Count").GetComponent<TMP_Text>().text;
    static Toggle Toggle(string code) => Row(code).Find("CandidateArea").GetComponent<Toggle>();
    static void Click(string name) => Root.Find(name).GetComponent<Button>().onClick.Invoke();
    static void ClickRole(string code, string name) => Row(code).Find("Amount/" + name).GetComponent<Button>().onClick.Invoke();
    static void Random(bool enabled) => Root.Find("Random").GetComponent<Toggle>().isOn = enabled;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void CheckRaycast(Button button)
    {
        // Event/raycast behaviours do not receive play-mode OnEnable during this editor-only render.
        foreach (var raycaster in canvas.GetComponentsInChildren<GraphicRaycaster>())
            typeof(BaseRaycaster).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(raycaster, null);
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Role plus button raycast failed: " + string.Join(", ", hits.Select(h => h.gameObject.name + "@" + h.sortingOrder)));
    }
    static void SetTarget(int width, int height)
    {
        camera.targetTexture.Release(); UnityEngine.Object.DestroyImmediate(camera.targetTexture);
        camera.targetTexture = new RenderTexture(width, height, 24); camera.targetTexture.Create();
    }
    static void Save(string name)
    {
        for (int i = 0; i < 3; i++)
        {
            camera.Render(); Canvas.ForceUpdateCanvases();
            typeof(RoleCompositionLayout).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(layout, null);
        }
        Canvas.ForceUpdateCanvases();
        foreach (var text in layout.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            Check(text.font.HasCharacters(text.text.Replace("\n", ""), out uint[] missing, true, true), "Missing glyph in " + text.name);
        }
        camera.Render(); RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = null;
    }
    static RoleSetupOptionsDto Options()
    {
        var o = new RoleSetupOptionsDto { minPlayers = 4, maxPlayers = 12 };
        string[] crew = { "CAPTAIN", "DOCTOR", "LOOKOUT", "BOATSWAIN", "DRUNK", "MONKEY", "SAILOR" };
        string[] crewNames = { "선장", "선의", "망루지기", "갑판장", "주정뱅이", "원숭이", "선원" };
        string[] pirate = { "RAIDER", "PARROT", "COOK" }; string[] pirateNames = { "해적", "앵무새", "요리사" };
        string[] neutral = { "SIREN", "KRAKEN", "GHOST_CAPTAIN", "MERMAID" }; string[] neutralNames = { "세이렌", "크라켄", "유령 선장", "인어" };
        for (int i = 0; i < crew.Length; i++) o.roles.Add(new RoleDto { code = "CREW_" + crew[i], name = crewNames[i], faction = Factions.Crew });
        for (int i = 0; i < pirate.Length; i++) o.roles.Add(new RoleDto { code = "PIRATE_" + pirate[i], name = pirateNames[i], faction = Factions.Pirate });
        for (int i = 0; i < neutral.Length; i++) o.roles.Add(new RoleDto { code = "NEUTRAL_" + neutral[i], name = neutralNames[i], faction = Factions.Neutral });
        o.randomCandidates = o.roles.Where(r => r.faction != Factions.Neutral && !RoleSetupRules.IsBaseRole(r.code)).Select(r => r.code).ToList();
        for (int count = 4; count <= 12; count++)
        {
            var roles = new List<string> { "PIRATE_RAIDER", "CREW_CAPTAIN", "CREW_DOCTOR" };
            if (count >= 7) roles.Add("PIRATE_RAIDER");
            if (count >= 8) { roles.Add("PIRATE_PARROT"); roles.Add("CREW_LOOKOUT"); }
            while (roles.Count < count) roles.Add("CREW_SAILOR");
            o.recommended.Add(new RoleCompositionDto { playerCount = count, roles = roles });
        }
        return o;
    }
}
