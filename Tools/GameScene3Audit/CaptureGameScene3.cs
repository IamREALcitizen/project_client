// Copy into Assets/Editor of the project (or a ParrelSync clone) that is not open, then run:
//   Unity.exe -batchmode -projectPath <project> -executeMethod CaptureGameScene3.Run -auditOut <folder> -logFile <log>
// Plays GameScene3 with its fake server (the scene's own setting, 선의) and saves the screen at night, with a night
// target picked, by day and at the vote, at 1080x1920 and 1080x2400. Writes <folder>/status.txt. Remove the copy afterwards.
// -executeMethod CaptureGameScene3.Dress instead dresses the baked GameScene3 in the nautical look (Tools/Game Scene/항해
// 시안 입히기) and saves it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoisntCitizen.Game;

[InitializeOnLoad]
public static class CaptureGameScene3
{
    const string Running = "CaptureGameScene3.Running", OutKey = "CaptureGameScene3.Out";
    static double start = -1;
    static int step;
    static Camera camera;
    static readonly List<string> log = new List<string>();

    static CaptureGameScene3()
    {
        if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }

    public static void Dress()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/GameScene3.unity");
        GameScreen screen = UnityEngine.Object.FindFirstObjectByType<GameScreen>(FindObjectsInactive.Include);
        WhoisntCitizen.EditorTools.GameLayoutBakeTool.Dress(screen);
        bool saved = EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(saved ? 0 : 1);
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "GameScene3Audit");
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-auditOut") output = args[i + 1];
        Directory.CreateDirectory(output);
        SessionState.SetString(OutKey, output);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/01_Scenes/GameScene3.unity");
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (start < 0) start = EditorApplication.timeSinceStartup;
        double t = EditorApplication.timeSinceStartup - start;
        // A new screen height is set a moment before the shot, so the canvas has laid itself out for it.
        try
        {
            if (step == 0 && t > 3.5) { Size(1920); step++; }
            else if (step == 1 && t > 4) { Shoot("Night"); step++; }
            else if (step == 2 && t > 4.3) { Size(2400); step++; }
            else if (step == 3 && t > 4.8) { Shoot("Night"); step++; }
            else if (step == 4 && t > 5) { Size(1920); PickNightTarget(105); step++; }
            else if (step == 5 && t > 6) { Shoot("NightPicked"); step++; }
            else if (step == 6 && t > 34) { Shoot("Day"); step++; }
            else if (step == 7 && t > 34.3) { Size(2400); step++; }
            else if (step == 8 && t > 34.8) { Shoot("Day"); step++; }
            else if (step == 9 && t > 46) { Size(1920); step++; }
            else if (step == 10 && t > 46.5) { Shoot("Vote"); step++; }
            else if (step == 11) Finish("PASS: " + string.Join(", ", log));
        }
        catch (Exception e)
        {
            Finish("FAIL: " + e);
        }
        if (t > 90) Finish("FAIL: timed out at step " + step);
    }

    static void PickNightTarget(long id)
    {
        GameLayoutTabs tabs = UnityEngine.Object.FindFirstObjectByType<GameLayoutTabs>();
        typeof(GameLayoutTabs).GetMethod("OnSlotClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tabs, new object[] { id });
    }

    // Every overlay canvas is drawn by one camera into a render texture this tall (1080 wide).
    static void Size(int height)
    {
        if (camera == null)
        {
            camera = new GameObject("AuditCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }
        camera.targetTexture = new RenderTexture(1080, height, 24);
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
        }
    }

    static void Shoot(string name)
    {
        RenderTexture target = camera.targetTexture;
        int height = target.height;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1080, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1080, height), 0, 0);
        RenderTexture.active = null;
        string file = name + "_" + height + ".png";
        File.WriteAllBytes(Path.Combine(SessionState.GetString(OutKey, "."), file), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        log.Add(file);
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        File.WriteAllText(Path.Combine(SessionState.GetString(OutKey, "."), "status.txt"), status);
        EditorApplication.Exit(status.StartsWith("PASS") ? 0 : 1);
    }
}
