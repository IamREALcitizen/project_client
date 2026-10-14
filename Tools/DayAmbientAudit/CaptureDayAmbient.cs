// Run in an isolated audit project with the freshly compiled game DLL and copied art (see README.md).
using System;
using System.Collections;
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
public static class CaptureDayAmbient
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<string> checks = new List<string>();
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Output"));
    static Camera camera;
    static Canvas canvas;
    static Image background;
    static DayRoundTableView table;

    static CaptureDayAmbient()
    {
        if (SessionState.GetBool("DayAmbientAudit", false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool("DayAmbientAudit", true);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.unscaledTime < 0.3f) return;
        EditorApplication.update -= Tick;
        try { Audit(); Finish("PASS\n" + string.Join("\n", checks)); }
        catch (Exception ex) { Finish("FAIL\n" + ex); }
    }

    static void Audit()
    {
        camera = new GameObject("Audit camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        canvas = new GameObject("Audit canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(canvas.transform, false);
        background.sprite = Resources.Load<Sprite>("DayTable/Deck");
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
        background.raycastTarget = false;
        var ocean = background.gameObject.AddComponent<DayOceanMotion>();
        Material original = background.material;
        ocean.SetVisible(true);
        Require(background.material.shader.name == "WhoisntCitizen/UI/DayOcean", "Ocean material applied");
        Require(!ShaderUtil.ShaderHasError(background.material.shader), "Ocean shader compiles");
        background.material.SetFloat("_OceanTime", 0);
        var first = Shoot("Ocean_0", 941, 1672);
        background.material.SetFloat("_OceanTime", 1.7f);
        var second = Shoot("Ocean_1", 941, 1672);
        int waterChanges = 0, shipChanges = 0;
        for (int y = 0; y < 1672; y++)
        for (int x = 0; x < 941; x++)
        {
            int index = y * 941 + x;
            if (first[index].Equals(second[index])) continue;
            float fromTop = 1f - y / 1672f;
            if (fromTop > 0.16f && fromTop < 0.31f && x > 180 && x < 790) waterChanges++;
            else shipChanges++;
        }
        Require(waterChanges > 500, "Water pixels animate: " + waterChanges);
        Require(shipChanges == 0, "Sky, deck and outer rigging stay still");
        ocean.SetVisible(false);
        Require(background.material == original, "Night restores original material");
        ocean.SetVisible(true);
        ocean.enabled = false;
        Require(background.material == original, "Disabling restores original material");
        ocean.enabled = true;
        Require(background.material.shader.name == "WhoisntCitizen/UI/DayOcean", "Re-enabling restores waves");

        var layout = RoundTableLayout.Current;
        layout.tableWidth = 743; layout.tableY = -115; layout.chairSize = 171.3f;
        layout.fewPlayersScale = 1; layout.ringPaddingX = 90; layout.ringPaddingY = -4;
        layout.ringOffsetY = 64; layout.seatRotation = -0.108f; layout.cardSpotRadius = 0.67142f;
        GameHudMetrics.Use(HudLayout.Tabs);
        GameLayoutTabs.PlaceDeck(background.rectTransform, 1080);
        layout.deckRaise = 170;
        GameLayoutTabs.PlaceDeck(background.rectTransform, 1080);
        var skins = LoadSkins();
        Require(skins.Length >= 4, "Four real character skins loaded");
        // The isolated DLL has no package MonoScript GUIDs for serialized TMP settings/fonts.
        // Use a fresh, in-memory font so the audit never rewrites the user's dynamic font assets.
        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        typeof(TMP_Settings).GetField("assetVersion", Private).SetValue(settings, "2");
        typeof(TMP_Settings).GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, settings);
        TMP_FontAsset auditFont = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
        Require(auditFont != null, "Audit font created in memory");
        TMP_Settings.defaultFontAsset = auditFont;
        table = canvas.gameObject.AddComponent<DayRoundTableView>();
        table.UseSnugLayout();
        table.Initialize(background, null, auditFont, skins, 12);
        var players = new List<PlayerViewDto>();
        for (int i = 0; i < 12; i++) players.Add(new PlayerViewDto { playerId = i + 1, nickname = "Crew " + (i + 1), alive = true });
        table.SetPlayers(players);
        table.SetPhase(GamePhases.Day);
        var seats = ((IEnumerable)Get(table, "seats")).Cast<object>().Where(s => s != null).ToArray();
        float now = Time.unscaledTime;
        float arrived = (float)Get(seats[0], "arrivedAt");
        table.SetPlayers(players);
        table.SetPhase(GamePhases.Vote);
        Require((float)Get(seats[0], "arrivedAt") == arrived, "Roster and vote refresh do not restart arrival");
        foreach (object seat in seats) Set(seat, "arrivedAt", now - 5f);
        table.SetPhase(GamePhases.Night);
        Require(background.material == original, "Table night phase disables waves");
        table.SetPhase(GamePhases.Day);
        Require((float)Get(seats[0], "arrivedAt") >= now, "Returning from night restarts seating");
        foreach (object seat in seats) Set(seat, "arrivedAt", now - 5f);

        foreach (object seat in seats)
        {
            Animate(seat, now + 1f);
            var avatar = (Image)Get(seat, "avatar");
            Require(avatar.rectTransform.anchoredPosition == (Vector2)Get(seat, "restingPosition"), "Idle feet stay on cushion: " + Get(seat, "pose"));
            Require(Mathf.Sign(avatar.rectTransform.localScale.x) == Mathf.Sign(((Vector3)Get(seat, "restingScale")).x), "Pose keeps mirror: " + Get(seat, "pose"));
        }
        var front = seats.First(s => (ChibiPose)Get(s, "pose") == ChibiPose.SeatedFront);
        var frontAvatar = (Image)Get(front, "avatar");
        Set(front, "arrivedAt", now);
        Animate(front, now + 0.05f);
        Require(frontAvatar.rectTransform.anchoredPosition.y > ((Vector2)Get(front, "restingPosition")).y, "Arrival lowers onto chair");
        Animate(front, now + 0.35f);
        Require(frontAvatar.rectTransform.localScale.y < 1f, "Arrival compresses on landing");
        Set(front, "arrivedAt", now - 5f);
        var skin = (ChibiCharacterSkin)Get(front, "skin");
        float phase = (float)Get(front, "breathingPhase");
        float period = 3.6f + 0.6f * Mathf.Sin(phase);
        float blinkTime = Mathf.Ceil((now + phase + 1) / period) * period - phase + 0.05f;
        Animate(front, blinkTime);
        Require(frontAvatar.sprite == skin.SeatedFrontBlink, "Front blink uses expression art");
        table.ShowSpeech((long)Get(front, "playerId"), "Let's set sail!");
        bool talked = false;
        for (int i = 0; i < 30; i++) { Animate(front, now + 0.3f + i * 0.05f); talked |= frontAvatar.sprite == skin.SeatedFrontTalk; }
        Require(talked, "Speech uses mouth frames");
        Set(front, "alive", false);
        Animate(front, now + 2f);
        Require(frontAvatar.sprite == skin.SeatedFront && frontAvatar.rectTransform.localRotation == Quaternion.identity && frontAvatar.rectTransform.localScale == (Vector3)Get(front, "restingScale"), "Dead character has no blink, sway or speaking motion");
        Set(front, "alive", true);
        for (int frame = 0; frame < 24; frame++)
        {
            float sample = now + frame / 12f;
            foreach (object seat in seats) Animate(seat, sample);
            background.material.SetFloat("_OceanTime", sample);
            Shoot("Day_" + frame.ToString("00"), 1080, 1920);
        }
        players.RemoveAt(0);
        table.SetPlayers(players);
        Require(table.Capacity == 11, "Roster removal rebuilds to 11 seats");
    }

    static ChibiCharacterSkin[] LoadSkins()
    {
        var skins = new List<ChibiCharacterSkin>();
        foreach (string folder in Directory.GetDirectories("Assets/Characters"))
        {
            var skin = ScriptableObject.CreateInstance<ChibiCharacterSkin>();
            Sprite[] sheetSprites = Directory.GetFiles(folder, "*.png")
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<Sprite>()).ToArray();
            foreach (FieldInfo field in typeof(ChibiCharacterSkin).GetFields(Private))
            {
                if (field.FieldType != typeof(Sprite)) continue;
                string pose = char.ToUpperInvariant(field.Name[0]) + field.Name.Substring(1);
                string path = Directory.GetFiles(folder, "*_" + pose + ".png").FirstOrDefault();
                var sprite = path != null
                    ? AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<Sprite>().FirstOrDefault()
                    : sheetSprites.FirstOrDefault(s => s.name.EndsWith("_" + pose, StringComparison.OrdinalIgnoreCase));
                Require(sprite != null, "Loaded pose: " + Path.GetFileName(folder) + "/" + pose);
                field.SetValue(skin, sprite);
            }
            skins.Add(skin);
        }
        return skins.ToArray();
    }

    static object Get(object o, string name) => o.GetType().GetField(name, Private | BindingFlags.Public).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private | BindingFlags.Public).SetValue(o, value);
    static void Animate(object seat, float now) => typeof(DayRoundTableView).GetMethod("ApplySeatVisual", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { seat, (object)now, true });
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }

    static Color32[] Shoot(string name, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        Color32[] pixels = image.GetPixels32();
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(rt);
        return pixels;
    }

    static void Finish(string status)
    {
        SessionState.SetBool("DayAmbientAudit", false);
        File.WriteAllText(Path.Combine(Output, "status.txt"), status);
        EditorApplication.Exit(status.StartsWith("PASS") ? 0 : 1);
    }
}
