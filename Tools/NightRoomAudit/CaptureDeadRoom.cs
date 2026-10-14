// Isolated Play-mode audit. See README.md; never run this against an open working scene.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Game;
using WhoisntCitizen.Game.Tests;
using WhoisntCitizen.GameUI;

[InitializeOnLoad]
public static class CaptureDeadRoom
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../NightDeadRoomOutput"));
    static readonly List<string> checks = new List<string>();
    static Camera camera;
    static NightRoomView view;

    static CaptureDeadRoom()
    {
        if (SessionState.GetBool("DeadRoomAudit", false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool("DeadRoomAudit", true);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.unscaledTime < 0.3f) return;
        EditorApplication.update -= Tick;
        try { Rules(); RenderAndCheck(); Finish("PASS\n" + string.Join("\n", checks)); }
        catch (Exception ex) { Finish("FAIL\n" + string.Join("\n", checks) + "\n" + ex); }
    }

    static void Rules()
    {
        int total = 0;
        foreach (var method in typeof(NightRoomRulesTests).GetMethods())
        {
            var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
            if (cases.Length > 0)
                foreach (var test in cases) { method.Invoke(new NightRoomRulesTests(), test.Arguments); total++; }
            else if (method.IsDefined(typeof(TestAttribute), false))
            { method.Invoke(new NightRoomRulesTests(), null); total++; }
        }
        Require(total >= 22, "NightRoomRules: " + total + " NUnit cases passed");
    }

    static void RenderAndCheck()
    {
        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        typeof(TMP_Settings).GetField("assetVersion", Private).SetValue(settings, "2");
        typeof(TMP_Settings).GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, settings);
        var font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/malgun.ttf"));
        Require(font != null, "In-memory Korean font loaded");
        TMP_Settings.defaultFontAsset = font;
        camera = new GameObject("AuditCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        // Allocate the capture size before any room positions are computed.
        camera.targetTexture = new RenderTexture(1080, 1920, 24);
        Canvas.ForceUpdateCanvases();
        GameHudMetrics.Use(HudLayout.Tabs);
        var skins = LoadSkins();
        view = canvas.gameObject.AddComponent<NightRoomView>();
        view.Initialize(null, font, id => skins[(int)(id - 1) % skins.Length]);
        var backgrounds = ScriptableObject.CreateInstance<NightSceneBackgroundCatalog>();
        var serialized = new SerializedObject(backgrounds);
        var entries = serialized.FindProperty("backgrounds");
        entries.arraySize = 1;
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("designId").stringValue = NightRoomRules.DeadShared;
        var ghostDeck = Resources.Load<Sprite>("NightScene/CREW_GHOST_Night");
        entries.GetArrayElementAtIndex(0).FindPropertyRelative("sprite").objectReferenceValue = ghostDeck;
        serialized.FindProperty("pirateShared").objectReferenceValue = Resources.Load<Sprite>("NightScene/PIRATE_SHARED_Night");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "backgrounds", backgrounds);
        view.SetPhase(GamePhases.Night);

        var members = new List<PlayerViewDto>();
        for (int count = 1; count <= 12; count++)
        {
            members.Add(new PlayerViewDto { playerId = count, nickname = count == 1 ? "나" : "떠난 선원 " + count, alive = false });
            view.SetRoom(NightRoomRules.DeadShared, members, 1);
            var figures = Figures();
            Require(figures.Length == count && figures.All(f => !(bool)Get(f, "alive")), "Dead room count " + count + ": only dead members");
            Require(figures.Select(f => (Vector2)Get(f, "to")).Distinct().Count() == count, "Distinct slots for " + count);
            if (count > 4)
            {
                Require(figures.GroupBy(f => ((Vector2)Get(f, "to")).y).All(row => row.Count() <= 4), "At most four per row for " + count);
                var mine = figures.Single(f => (bool)Get(f, "mine"));
                Require(((Vector2)Get(mine, "to")).y == figures.Min(f => ((Vector2)Get(f, "to")).y), "Self in front for " + count);
                Require(figures.All(f => ((Vector2)Get(f, "to")).y + (float)Get(f, "height") + 12f < 370f), "Floating heads clear title for " + count);
            }
            Settle();
            if (count == 1 || count == 4 || count == 8 || count == 12) Shoot("Dead_" + count);
            float appear = (float)Get(figures[0], "appearStart");
            view.SetRoom(NightRoomRules.DeadShared, members, 1);
            Require((float)Get(figures[0], "appearStart") == appear, "Roster refresh keeps animation for " + count);
        }
        Require(((Image)Get(view, "backdrop")).sprite == ghostDeck, "Shared ghost-deck background selected");
        view.PlayAbility("PIRATE_RAIDER", ActionCodes.SelectAttackTarget, "습격자", "공격", true);
        Require(!(bool)Get(view, "abilityRunning"), "Dead players cannot start an ability cut-in");
        view.SetPhase(GamePhases.Day);
        Require(!((RectTransform)Get(view, "stage")).gameObject.activeSelf, "Hidden during day");
        view.SetPhase(GamePhases.NightResult);
        Require(((RectTransform)Get(view, "stage")).gameObject.activeSelf, "Shown during night result");

        var alive = new List<PlayerViewDto> {
            new PlayerViewDto { playerId = 1, nickname = "나", alive = true },
            new PlayerViewDto { playerId = 20, nickname = "동료", alive = true }
        };
        view.SetRoom(NightRoomRules.PirateShared, alive, 1);
        Settle();
        Require(!((TMP_Text)Get(view, "deadRoomTitle")).gameObject.activeSelf, "Ghost title hidden in living room");
        Set(view, "abilityRunning", true);
        Set(view, "hasPending", true);
        Set(view, "pendingDesign", NightRoomRules.PirateShared);
        view.SetRoom(NightRoomRules.DeadShared, members, 1);
        Require(!(bool)Get(view, "abilityRunning") && !(bool)Get(view, "hasPending"), "Death cancels pending ability and room changes");
        Require(Figures().All(f => !(bool)Get(f, "alive")), "Living former teammate removed immediately");
        Settle();
        members.RemoveAt(4); // a dead teammate revived/left
        view.SetRoom(NightRoomRules.DeadShared, members, 1);
        Settle();
        Require(Figures().Length == 11, "Removed member leaves shared room");
        view.Clear();
        Require(Figures().Length == 0 && !((RectTransform)Get(view, "stage")).gameObject.activeSelf, "New game clears shared room");
    }

    static void Settle()
    {
        float now = Time.unscaledTime;
        Set(view, "crossfadeStart", now - 3f);
        foreach (object f in Figures())
        {
            Set(f, "appearStart", now - 3f);
            Set(f, "moveStart", now - 3f);
            if ((float)Get(f, "leaveStart") >= 0) Set(f, "leaveStart", now - 0.6f);
        }
        typeof(NightRoomView).GetMethod("Update", Private).Invoke(view, null);
    }

    static ChibiCharacterSkin[] LoadSkins()
    {
        return Directory.GetDirectories("Assets/Characters").Select(folder => {
            var skin = ScriptableObject.CreateInstance<ChibiCharacterSkin>();
            Sprite[] sprites = Directory.GetFiles(folder, "*.png").SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p.Replace('\\', '/')).OfType<Sprite>()).ToArray();
            Sprite standing = sprites.FirstOrDefault(s => s.name.EndsWith("_Standing"));
            if (standing == null)
            {
                string path = Directory.GetFiles(folder, "*_Standing.png").First();
                standing = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<Sprite>().First();
            }
            Set(skin, "standing", standing);
            return skin;
        }).ToArray();
    }

    static object[] Figures() => ((IEnumerable)Get(view, "figures")).Cast<object>().ToArray();
    static object Get(object obj, string name) => obj.GetType().GetField(name, Private | BindingFlags.Public).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private | BindingFlags.Public).SetValue(obj, value);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
    static void Shoot(string name)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        var image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(image);
    }
    static void Finish(string status)
    {
        SessionState.SetBool("DeadRoomAudit", false);
        File.WriteAllText(Path.Combine(Output, "status.txt"), status);
        EditorApplication.Exit(status.StartsWith("PASS") ? 0 : 1);
    }
}
