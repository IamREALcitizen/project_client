// Copy into an isolated Unity project's Assets/Editor with the real GameResultPanel and Game/Core dependencies.
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Game;

public static class CaptureResults
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Output");
        Directory.CreateDirectory(output);
        try
        {
            string[] factions = { Factions.Crew, Factions.Pirate, Factions.Neutral };
            string[] victories = { "CrewVictory", "PirateVictory", "PersonalVictory" };
            for (int winner = 0; winner < 3; winner++)
                for (int me = 0; me < 3; me++)
                    Check(GameResultPanel.BackgroundName(Result(factions[winner]), factions[me]) ==
                          (winner == me ? victories[winner] : "Defeat"), "Wrong result mapping");
            Check(GameResultPanel.BackgroundName(null, Factions.Crew) == null, "Null result");
            Check(GameResultPanel.BackgroundName(Result(Factions.Crew), null) == null, "Missing local faction");
            Check(GameResultPanel.BackgroundName(Result("UNKNOWN"), Factions.Crew) == null, "Unknown winner");
            var pending = Result(Factions.Crew); pending.ended = false;
            Check(GameResultPanel.BackgroundName(pending, Factions.Crew) == null, "Not ended");
            foreach (string reason in new[] { EndReasons.CancelledError, EndReasons.CancelledAllDisconnected, EndReasons.CancelledNoDeaths })
            {
                var cancelled = Result(Factions.Crew); cancelled.endReason = reason;
                Check(GameResultPanel.BackgroundName(cancelled, Factions.Pirate) == null, "Cancellation displayed as defeat");
            }

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 5;
            var target = new RenderTexture(1080, 1920, 24); target.Create(); camera.targetTexture = target;
            RectTransform canvasRect = Rect("Canvas", null);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            RectTransform root = Rect("GameResultPanel", canvasRect);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            RectTransform card = Rect("Card", root);
            card.sizeDelta = new Vector2(920, 1300);
            card.gameObject.AddComponent<Image>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MalgunGothic SDF.asset");
            var winnerText = Text("WinnerText", card, font, new Color(1f, 0.82f, 0.38f));
            var outcome = Text("OutcomeText", card, font, Color.white);
            var players = Text("PlayersText", card, font, Color.white);
            players.textWrappingMode = TextWrappingModes.Normal;
            RectTransform buttonRect = Rect("BackButton", card);
            var buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.86f, 0.66f, 0.27f);
            var button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage;
            var buttonText = Text("Text", buttonRect, font, new Color(0.09f, 0.065f, 0.025f));
            Stretch(buttonText.rectTransform); buttonText.text = "대기실로";
            buttonText.fontSize = 36; buttonText.alignment = TextAlignmentOptions.Center;
            var panel = root.gameObject.AddComponent<GameResultPanel>();
            Bind(panel, "winnerText", winnerText); Bind(panel, "outcomeText", outcome);
            Bind(panel, "playersText", players); Bind(panel, "backButton", button);
            typeof(GameResultPanel).GetMethod("Awake", Private).Invoke(panel, null);
            int backCount = 0; panel.BackRequested += () => backCount++;

            string[] names = { "CrewVictory", "PirateVictory", "PersonalVictory", "Defeat" };
            for (int i = 0; i < names.Length; i++)
            {
                string winningFaction = i == 3 ? Factions.Pirate : factions[i];
                string myFaction = i == 3 ? Factions.Crew : factions[i];
                panel.Show(Result(winningFaction), new MyRoleDto { faction = myFaction });
                Canvas.ForceUpdateCanvases(); players.ForceMeshUpdate();
                Image art = root.Find("ResultBackground").GetComponent<Image>();
                Check(art.sprite != null && art.sprite.name == names[i], "Wrong or missing resource: " + names[i]);
                Check(!players.isTextOverflowing, "12-player roster overflow");
                if (i == 2) Check(winnerText.text == "개인 승리", "Personal victory headline exposes backend faction code");
                Check(art.transform.GetSiblingIndex() == 0, "Background covers controls");
                Check(root.childCount == 2, "Repeated Show duplicated background");
                var a = new Vector3[4]; var b = new Vector3[4];
                root.GetWorldCorners(a); art.rectTransform.GetWorldCorners(b);
                Check(b[0].x <= a[0].x + .1f && b[0].y <= a[0].y + .1f && b[2].x >= a[2].x - .1f && b[2].y >= a[2].y - .1f,
                    "Background does not cover panel");
                Save(camera, target, Path.Combine(output, names[i] + ".png"));
                button.onClick.Invoke();
                panel.Hide(); Check(!root.gameObject.activeSelf, "Hide failed");
            }
            Check(backCount == 4, "Back callback failed or duplicate listeners");
            var cancellation = Result(null); cancellation.endReason = EndReasons.CancelledNoDeaths;
            panel.Show(cancellation, new MyRoleDto { faction = Factions.Crew });
            Check(!root.Find("ResultBackground").gameObject.activeSelf, "Stale defeat background after cancellation");
            Check(winnerText.text == "게임 취소", "Cancellation headline");
            panel.Show(Result(Factions.Crew), null);
            Check(!root.Find("ResultBackground").gameObject.activeSelf, "Unknown player should not be labelled defeated");
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: 9 win/loss mappings, null/unknown/pending and 3 cancellations, 4 imported backgrounds, 12-player text fit, aspect cover, repeat show/hide, back button; 4 previews rendered.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "status.txt"), ex.ToString());
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }

    static GameResultDto Result(string winner)
    {
        var result = new GameResultDto { ended = true, endReason = EndReasons.Win, winner = winner, lastDay = 4 };
        string[] roles = { "선장", "해적", "선의", "망루지기", "갑판장", "주정뱅이", "앵무새", "원숭이", "선원", "선원", "해적", "선원" };
        for (int i = 0; i < 12; i++) result.players.Add(new PlayerResultDto { playerId = i + 1, nickname = "플레이어 " + (i + 1), roleName = roles[i], alive = i % 3 != 0 });
        return result;
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
        var rect = (RectTransform)obj.transform; rect.SetParent(parent, false);
        return rect;
    }
    static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, Color color)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.color = color; text.raycastTarget = false; return text;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static void Bind(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
    static void Save(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.active = null;
    }
}
