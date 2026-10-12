// Copy into an isolated Unity project's Assets/Editor with DayTransitionView, NightFxSprites and the art listed in README.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.GameUI;

public static class CaptureTransitions
{
    static readonly string[] Kinds = { "Killed", "Protected", "Quiet", "Executed", "NotExecuted" };

    // Moments to draw per scene (seconds after it began): one per beat of the storyboard.
    static readonly Dictionary<string, float[]> Moments = new Dictionary<string, float[]>
    {
        { "Killed", new[] { 0.45f, 1.0f, 2.3f, 3.9f, 4.3f } },
        { "Protected", new[] { 0.45f, 1.0f, 1.62f, 2.2f, 3.6f } },
        { "Quiet", new[] { 0.45f, 1.2f, 2.5f, 2.85f } },
        { "Executed", new[] { 0.5f, 1.55f, 2.4f, 3.45f, 4.45f } },
        { "NotExecuted", new[] { 0.5f, 1.5f, 2.15f, 2.75f } },
    };

    public static void Run()
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Output");
        Directory.CreateDirectory(output);
        var report = new StringBuilder();
        try
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MalgunGothic SDF.asset");
            Check(font != null, "font");
            Sprite night = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PIRATE_SHARED_Night.png");
            Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CREW_DRUNK.png");
            Sprite figure = null;
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/CrewDrunk_BaseSheet.png"))
            {
                if (o is Sprite s && s.name == "CrewDrunk_Standing") figure = s;
            }
            Check(night != null && portrait != null && figure != null, "test art");
            foreach (string path in new[] { "DayTable/Deck", "GameHud/PhasePlaque", "GameHud/ParchmentPanel",
                "DayTransition/ExecutionPlank", "DayTransition/ExecutionSplash" })
            {
                Check(Resources.Load<Sprite>(path) != null, "Resources sprite missing: " + path);
            }
            Check(Resources.Load<Sprite>("GameHud/ParchmentPanel").border != Vector4.zero, "parchment is not 9-sliced");

            foreach (Vector2Int size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2340) })
            {
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 5;
                var target = new RenderTexture(size.x, size.y, 24);
                target.Create();
                camera.targetTexture = target;
                RectTransform canvasRect = Rect("Canvas", null);
                var canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0f;
                // A stand-in for the game screen under the cut-scene: the deck, a bit dark.
                Image game = Rect("Game", canvasRect).gameObject.AddComponent<Image>();
                game.sprite = Resources.Load<Sprite>("DayTable/Deck");
                Stretch(game.rectTransform);
                game.color = new Color(0.55f, 0.55f, 0.6f);
                Canvas.ForceUpdateCanvases();

                DayTransitionView view = DayTransitionView.Create(canvasRect, font);
                view.NightBackdrop = night;
                bool tall = size.y != 1920;
                foreach (string kind in Kinds)
                {
                    if (tall && kind != "Executed") continue;
                    var info = new DayTransitionInfo
                    {
                        Kind = (DayTransitionKind)Enum.Parse(typeof(DayTransitionKind), kind),
                        Day = 1, Name = "지훈", Portrait = portrait, Figure = figure, Votes = 4, TotalVotes = 7,
                        Reason = "동점이라 아무도 처형되지 않았습니다.",
                    };
                    if (kind == "NotExecuted") info.Portrait = info.Figure = null;
                    if (kind == "Killed") info.Name = "민수";
                    view.Prepare(info);
                    Check(view.transform.GetSiblingIndex() == canvasRect.childCount - 1, "cut-scene is not on top");
                    float[] moments = tall ? new[] { 2.4f, 3.45f } : Moments[kind];
                    for (int i = 0; i < moments.Length; i++)
                    {
                        view.Evaluate(moments[i]);
                        foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
                        string name = kind + (tall ? "_tall" : string.Empty) + "_" + i + ".png";
                        Save(camera, target, Path.Combine(output, name));
                        report.AppendLine(name + " t=" + moments[i].ToString("0.00"));
                    }
                    // After the hold the overlay must be gone (the next phase shows through).
                    view.Evaluate(view.Duration + 0.01f);
                    Check(view.GetComponent<CanvasGroup>().alpha <= 0.001f, kind + ": still visible after its end");
                    report.AppendLine(kind + ": hold " + view.HoldAt.ToString("0.0") + "s, end " + view.Duration.ToString("0.0") + "s");
                }
                foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!string.IsNullOrEmpty(text.text)) CheckGlyphs(text, report);
                }
                UnityEngine.Object.DestroyImmediate(canvasRect.gameObject);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                target.Release();
            }
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS\n" + report);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "status.txt"), ex + "\n" + report);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    static void CheckGlyphs(TMP_Text text, StringBuilder report)
    {
        foreach (char c in text.text)
        {
            if (c == ' ' || c == '<' || c == '>' || char.IsControl(c)) continue;
            if (!text.font.HasCharacter(c, true, true)) report.AppendLine("MISSING GLYPH '" + c + "' in " + text.name);
        }
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
    }

    static void Save(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels);
        RenderTexture.active = null;
    }
}
