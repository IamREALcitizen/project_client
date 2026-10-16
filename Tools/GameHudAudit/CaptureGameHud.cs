// Run only in the isolated project produced by Prepare.ps1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;

[InitializeOnLoad]
public static class CaptureGameHud
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Running = "GameHudAudit.Running";
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Output"));
    static readonly List<string> checks = new List<string>();
    static readonly List<string> runtimeErrors = new List<string>();
    static Camera camera;
    static TMP_FontAsset font;
    static RectTransform canvasRect, strip;
    static GameLayoutTabs tabs;
    static GameSession session;
    static int step;
    static double next;

    static CaptureGameHud() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Running, true);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.unscaledTime < 0.4f || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (runtimeErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", runtimeErrors));
            switch (step++)
            {
                case 0: Setup(); Build(4, true); break;
                case 1: CheckRoster(4, true); Shoot("Roster-4"); Build(8, true); break;
                case 2: CheckRoster(8, true); Shoot("Roster-8"); Build(12, true); break;
                case 3: CheckRoster(12, true); Shoot("Roster-12"); Build(4, false); break;
                case 4: CheckRoster(4, false); Shoot("Revived"); Build(4, true); break;
                case 5:
                    CheckRoster(4, true);
                    Call(tabs, "RebuildStrip", session);
                    Require(strip.childCount == 4, "Unchanged roster does not duplicate players");
                    Shoot("Roster-4");
                    Finish("PASS\n" + string.Join("\n", checks));
                    break;
            }
            next = EditorApplication.timeSinceStartup + 0.35;
        }
        catch (Exception e) { Finish("FAIL\n" + string.Join("\n", checks) + "\n" + e); }
    }
    static void Setup()
    {
        // The isolated project uses precompiled TMP, so source-script GUIDs in TMP Settings.asset do not resolve.
        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        Set(settings, "assetVersion", "2");
        Set(settings, "m_leadingCharacters", Resources.Load<TextAsset>("LineBreaking Leading Characters"));
        Set(settings, "m_followingCharacters", Resources.Load<TextAsset>("LineBreaking Following Characters"));
        typeof(TMP_Settings).GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, settings);
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
        font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/malgun.ttf"));
        TMP_Settings.defaultFontAsset = font;
        font.TryAddCharacters("사망전체시스템일반귓속말채팅필터선원유령선의습격자선장복구아이콘낮밤…_ ", out string missing);
        Require(string.IsNullOrEmpty(missing), "Korean labels have font glyphs");
        Application.logMessageReceived += (message, stack, type) =>
        {
            // Unity's background search index can fail in this minimal project; it does not render the HUD.
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeErrors.Add(message);
        };

        string[] icons = { "anchor", "arrow_down", "arrow_next", "arrow_up", "bottle", "chat_1", "compass", "emoji_smiley",
            "energy", "exclamation", "feather", "flag_1", "foot_print", "friend", "info", "knife_and_fork", "lantern",
            "mark_question", "moon", "music", "notification", "rope", "shield", "skull", "star_and_moon", "sun", "sword_1", "target_1", "wind_wave" };
        foreach (string icon in icons)
        {
            var sprite = Resources.Load<Sprite>("Sprites/flaticon/white/128/" + icon + "_128");
            Require(sprite != null, "Resources.Load: " + icon);
            Require(sprite.texture.width % 4 == 0 && sprite.texture.height % 4 == 0, "Multiple-of-four dimensions: " + icon);
            var bytes = File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite));
            var texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);
            var pixels = texture.GetPixels32();
            Require(pixels.Any(p => p.a == 0) && pixels.Any(p => p.a > 200), "Icon silhouette alpha: " + icon);
            UnityEngine.Object.Destroy(texture);
        }
        camera = new GameObject("AuditCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(0, 8, 14, 255);
        camera.targetTexture = new RenderTexture(1080, 640, 24);
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10;
        canvasRect = (RectTransform)canvas.transform;
        Canvas.ForceUpdateCanvases();

        var hud = canvas.gameObject.AddComponent<GameHudLayout>();
        hud.enabled = false;
        var catalog = ScriptableObject.CreateInstance<DesignSpriteCatalog>();
        var serialized = new SerializedObject(catalog);
        var entries = serialized.FindProperty("entries");
        string[] designs = { "CREW_GHOST", "CREW_DOCTOR", "PIRATE_RAIDER", "CREW_CAPTAIN" };
        var skins = new ChibiCharacterSkin[4];
        entries.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Portraits/" + designs[i] + ".png").OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).First();
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("designId").stringValue = designs[i];
            entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            skins[i] = ScriptableObject.CreateInstance<ChibiCharacterSkin>();
            Set(skins[i], "designId", designs[i]);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Set(hud, "portraits", catalog);
        tabs = canvas.gameObject.AddComponent<GameLayoutTabs>();
        tabs.enabled = false;
        var context = new GameLayoutContext { Canvas = canvasRect, Font = font, Hud = hud, SkinOf = id => skins[((int)id - 1) % 4] };
        typeof(GameLayoutBody).GetField("ctx", Private).SetValue(tabs, context);

        var chat = canvas.gameObject.AddComponent<GameHudChat>();
        chat.enabled = false;
        Set(chat, "font", font);
        Call(chat, "BuildTabs", canvasRect, 25f, 26f);
        foreach (var image in canvas.GetComponentsInChildren<Image>())
            if (image.name == "Icon" || image.name == "Arrow")
                Require(image.sprite != null, "Rendered chat sprite: " + image.transform.parent.name);

        Text("낮", 35, 115);
        Icon("sun", 86, 110);
        Text("밤", 155, 115);
        Icon("star_and_moon", 207, 110);
        for (int i = 0; i < icons.Length; i++) Icon(icons[i], 26 + i * 35, 185);

        var panel = Rect("NightPanel", canvasRect, 12, 270, 1056, 350);
        var frame = panel.gameObject.AddComponent<Image>();
        frame.sprite = Resources.Load<Sprite>("GameHud/Nautical/NightPanel");
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 2172f / 1080f;
        frame.raycastTarget = false;
        strip = Rect("Strip", canvasRect, 0, 345, 1080, 236);
        Set(tabs, "strip", strip);
        session = new GameSession(new FakeGameApi(new FakeGameOptions(), () => 0d), "audit", new LogGameView(), () => 0d, 1d);
    }
    static void Build(int count, bool dead)
    {
        var state = new GameStateDto { gameId = "audit", phase = GamePhases.Day, day = 2, phaseVersion = step };
        string[] names = { "유령", "선의", "습격자", "선장" };
        for (int i = 0; i < count; i++) state.players.Add(new PlayerViewDto { playerId = i + 1, nickname = names[i % 4], alive = !dead || i != 1 });
        ((PhaseTracker)Get(session, "tracker")).Update(state);
        Call(tabs, "RebuildStrip", session);
    }
    static void CheckRoster(int count, bool dead)
    {
        Canvas.ForceUpdateCanvases();
        Require(strip.childCount == count, "Roster count: " + count);
        var player = strip.Find("Player2");
        var ribbon = player.Find("Avatar/DeathRibbon");
        Require((ribbon != null) == dead, "Death/revival ribbon state: " + count + " dead=" + dead);
        Require(player.Find("Dead") == null, "Old square death image removed");
        Require(strip.Find("Player1/Avatar/DeathRibbon") == null, "Living player has no ribbon");
        if (dead)
        {
            var label = ribbon.Find("Label").GetComponent<TMP_Text>();
            label.ForceMeshUpdate();
            Require(label.text == "사망" && label.textInfo.characterCount == 2, "Death ribbon Korean text");
            Require(label.textInfo.characterInfo.Take(label.textInfo.characterCount).All(c => c.isVisible), "Death label fits at roster count " + count);
            Require(ribbon.parent.GetComponent<Mask>() != null, "Ribbon clipped by avatar circle");
            Require(!ribbon.GetComponent<Image>().raycastTarget && !label.raycastTarget, "Ribbon does not intercept clicks");
            Require(player.Find("Plate/Name").GetComponent<TMP_Text>().text == "선의", "Nickname remains visible");
        }
    }
    static void Icon(string icon, float x, float y)
    {
        var image = Rect(icon, canvasRect, x, y, 28, 28).gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("Sprites/flaticon/white/128/" + icon + "_128");
        image.color = new Color(0.98f, 0.8f, 0.42f);
        image.preserveAspect = true;
    }
    static void Text(string value, float x, float y)
    {
        var text = Rect("Label", canvasRect, x, y, 60, 40).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = 25; text.color = Color.white;
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    static void Shoot(string name)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(1080, 640, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1080, 640), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
    }
    static object Get(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
    }
    static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        File.WriteAllText(Path.Combine(Output, "status.txt"), result);
        EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
    }
}

