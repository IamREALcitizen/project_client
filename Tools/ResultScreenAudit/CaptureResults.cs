// Copy into an isolated Unity project's Assets/Editor with the real GameResultPanel and Game/Core dependencies.
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;

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
            var skins = Resources.LoadAll<ChibiCharacterSkin>("DayTable/Skins");
            Array.Sort(skins, (a, b) => string.CompareOrdinal(a.name, b.name));
            Check(skins.Length >= 12, "Missing test skins");
            panel.SkinOf = id => skins[(int)(id - 1) % skins.Length];
            Bind(panel, "winnerText", winnerText); Bind(panel, "outcomeText", outcome);
            Bind(panel, "playersText", players); Bind(panel, "backButton", button);
            typeof(GameResultPanel).GetMethod("Awake", Private).Invoke(panel, null);
            int backCount = 0; panel.BackRequested += () => backCount++;
            root.gameObject.SetActive(false); // Match scenes whose result panel starts hidden.

            string[] names = { "CrewVictory", "PirateVictory", "PersonalVictory", "Defeat" };
            for (int i = 0; i < names.Length; i++)
            {
                string winningFaction = i == 3 ? Factions.Pirate : factions[i];
                string myFaction = i == 3 ? Factions.Crew : factions[i];
                panel.Show(Result(winningFaction), new MyRoleDto { playerId = 1, faction = myFaction });
                Check(panel.IsIntroPlaying && !Counting(panel), "Countdown must wait for intro");
                var sequence = root.GetComponent<GameResultSequence>();
                Step(sequence, .25f);
                Check(winnerText.GetComponent<CanvasGroup>().alpha == 0f && players.GetComponent<CanvasGroup>().alpha == 0f, "Early text reveal");
                if (i == 0)
                {
                    Save(camera, target, Path.Combine(output, "Intro-025.png"));
                    Step(sequence, .55f);
                    Save(camera, target, Path.Combine(output, "Intro-080.png"));
                    Step(sequence, .45f);
                    Save(camera, target, Path.Combine(output, "Intro-125.png"));
                    Step(sequence, .75f);
                }
                else panel.SkipIntro();
                Check(!panel.IsIntroPlaying && Counting(panel), "Intro completion or skip did not start countdown");
                Bind(panel, "remaining", 17f); panel.SkipIntro();
                Check((float)typeof(GameResultPanel).GetField("remaining", Private).GetValue(panel) == 17f, "Repeated skip reset countdown");
                int expectedFigures = i == 2 || i == 3 ? 1 : i == 1 ? 4 : 9;
                Check(root.Find("ResultCharacters").childCount == expectedFigures, "Wrong winning team / solo figures");
                foreach (Transform holder in root.Find("ResultCharacters"))
                {
                    long id = long.Parse(holder.name.Substring("Winner_".Length));
                    Check(holder.Find("Character").GetComponent<Image>().sprite == panel.SkinOf(id).Standing, "Worn skin changed");
                }
                Canvas.ForceUpdateCanvases(); Step(sequence, 0f);
                CheckUniformLineup(root);
                Image art = root.Find("ResultBackground").GetComponent<Image>();
                Check(art.sprite != null && art.sprite.name == names[i], "Wrong or missing resource: " + names[i]);
                Transform cards = root.Find("Card/ResultPlayerCards/Players");
                Check(cards.childCount == 12 && !players.gameObject.activeSelf, "Portrait cards must replace the text roster");
                var catalog = Resources.Load<DesignSpriteCatalog>("GameHud/Portraits");
                var buttonCorners = new Vector3[4]; buttonRect.GetWorldCorners(buttonCorners);
                foreach (Transform row in cards)
                {
                    long id = long.Parse(row.name.Substring("Player_".Length));
                    Sprite expectedPortrait = catalog.Find(panel.SkinOf(id).DesignId);
                    if (expectedPortrait == null) expectedPortrait = panel.SkinOf(id).Standing;
                    Check(row.Find("PortraitBase/Portrait").GetComponent<Image>().sprite == expectedPortrait, "Card portrait differs from worn skin: " + id);
                    var rowCorners = new Vector3[4]; ((RectTransform)row).GetWorldCorners(rowCorners);
                    Check(rowCorners[0].y > buttonCorners[2].y, "Result card overlaps the return button");
                    Check(row.Find("Status").GetComponent<TextMeshProUGUI>().text == ((id - 1) % 3 != 0 ? "생존" : "사망"), "Wrong alive/dead badge");
                    foreach (string field in new[] { "Nickname", "Role", "Faction", "Status" })
                    {
                        var label = row.Find(field).GetComponent<TextMeshProUGUI>(); label.ForceMeshUpdate();
                        Check(label.textInfo.meshInfo[0].vertexCount > 0, "Card text is not visible: " + field);
                    }
                }
                if (i == 2) Check(winnerText.text == "개인 승리", "Personal victory headline exposes backend faction code");
                Check(art.transform.GetSiblingIndex() == 0, "Background covers controls");
                Check(root.childCount == 6, "Repeated Show duplicated presentation layers");
                var a = new Vector3[4]; var b = new Vector3[4];
                root.GetWorldCorners(a); art.rectTransform.GetWorldCorners(b);
                Check(b[0].x <= a[0].x + .1f && b[0].y <= a[0].y + .1f && b[2].x >= a[2].x - .1f && b[2].y >= a[2].y - .1f,
                    "Background does not cover panel");
                Save(camera, target, Path.Combine(output, names[i] + ".png"));
                button.onClick.Invoke();
                button.onClick.Invoke(); // Return must only fire once for this presentation.
                panel.Hide(); Check(!root.gameObject.activeSelf, "Hide failed");
            }
            Check(backCount == 4, "Back callback failed or duplicate listeners");
            var fullCrew = Result(Factions.Crew);
            foreach (var player in fullCrew.players) player.role = RoleCodes.CrewSailor;
            panel.Show(fullCrew, new MyRoleDto { playerId = 1, faction = Factions.Crew }, false);
            panel.SkipIntro();
            Check(!Counting(panel), "Fake game auto-return enabled");
            Check(root.Find("ResultCharacters").childCount == 12, "Full 12-player lineup missing");
            Canvas.ForceUpdateCanvases(); Step(root.GetComponent<GameResultSequence>(), 0f); CheckUniformLineup(root);
            Save(camera, target, Path.Combine(output, "TwelveWinners.png"));
            panel.SkinOf = id => skins[((int)(id - 1) + 12) % skins.Length];
            panel.Show(fullCrew, new MyRoleDto { playerId = 1, faction = Factions.Crew }, false);
            panel.SkipIntro(); Canvas.ForceUpdateCanvases(); Step(root.GetComponent<GameResultSequence>(), 0f);
            CheckUniformLineup(root); // Includes wide kraken/mermaid/animal skins as well as humans.
            Save(camera, target, Path.Combine(output, "MixedWinners.png"));
            panel.SkinOf = id => skins[(int)(id - 1) % skins.Length];
            panel.Show(Result(Factions.Crew), new MyRoleDto { playerId = 1, faction = Factions.Crew }, false);
            string frames = Path.Combine(output, "IntroFrames"); Directory.CreateDirectory(frames);
            Save(camera, target, Path.Combine(frames, "Frame000.png"));
            for (int frame = 1; frame <= 40; frame++)
            {
                Step(root.GetComponent<GameResultSequence>(), .05f);
                Save(camera, target, Path.Combine(frames, "Frame" + frame.ToString("D3") + ".png"));
            }
            panel.Show(fullCrew, new MyRoleDto { playerId = 1, faction = Factions.Crew });
            Step(root.GetComponent<GameResultSequence>(), .7f); panel.Hide();
            Step(root.GetComponent<GameResultSequence>(), 10f);
            Check(!Counting(panel) && !panel.IsIntroPlaying && backCount == 4, "Hidden intro sent delayed callbacks");
            panel.Show(fullCrew, new MyRoleDto { playerId = 1, faction = Factions.Crew });
            button.onClick.Invoke();
            Check(!panel.IsIntroPlaying && backCount == 4, "First click should skip rather than leave");
            Bind(panel, "remaining", 0f);
            typeof(GameResultPanel).GetMethod("Update", Private).Invoke(panel, null);
            typeof(GameResultPanel).GetMethod("Update", Private).Invoke(panel, null);
            Check(backCount == 5, "Automatic return fired incorrectly");
            var cancellation = Result(null); cancellation.endReason = EndReasons.CancelledNoDeaths;
            panel.Show(cancellation, new MyRoleDto { faction = Factions.Crew });
            Check(!root.Find("ResultBackground").gameObject.activeSelf, "Stale defeat background after cancellation");
            Check(winnerText.text == "게임 취소", "Cancellation headline");
            Check(!panel.IsIntroPlaying && !root.Find("ResultCharacters").gameObject.activeSelf, "Cancellation celebrated");
            Check(players.gameObject.activeSelf && !root.Find("Card/ResultPlayerCards").gameObject.activeSelf, "Cancellation kept stale player cards");
            panel.Show(Result(Factions.Crew), null);
            Check(!root.Find("ResultBackground").gameObject.activeSelf, "Unknown player should not be labelled defeated");
            // Include all three factions together when reviewing the result palette.
            for (int faction = 0; faction < factions.Length; faction++)
            {
                var mixed = Result(factions[faction]);
                mixed.players[8].role = "NEUTRAL_MERMAID"; mixed.players[8].roleName = "인어";
                mixed.players[9].role = "NEUTRAL_KRAKEN"; mixed.players[9].roleName = "크라켄";
                mixed.players[11].role = "NEUTRAL_GHOST_CAPTAIN"; mixed.players[11].roleName = "유령 선장";
                panel.Show(mixed, new MyRoleDto { playerId = faction == 2 ? 9 : faction + 1, faction = factions[faction] }, false);
                panel.SkipIntro(); Canvas.ForceUpdateCanvases(); Step(root.GetComponent<GameResultSequence>(), 0f);
                Save(camera, target, Path.Combine(output, victories[faction] + "_FactionColors.png"));
            }
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: result mappings, 4 backgrounds, 12-player text/lineup, worn skins, staged reveal, skip, repeated click, 20-second countdown after intro, fake-game return disabled, interrupted intro cleanup and cancellations; previews rendered.");
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
        string[] codes = { "CREW_CAPTAIN", "PIRATE_RAIDER", "CREW_DOCTOR", "CREW_LOOKOUT", "CREW_BOATSWAIN", "CREW_DRUNK", "PIRATE_PARROT", "CREW_MONKEY", "CREW_SAILOR", "CREW_SAILOR", "PIRATE_RAIDER", "CREW_SAILOR" };
        for (int i = 0; i < 12; i++) result.players.Add(new PlayerResultDto { playerId = i + 1, nickname = "플레이어 " + (i + 1), role = codes[i], roleName = roles[i], alive = i % 3 != 0 });
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
    static bool Counting(GameResultPanel panel) => (bool)typeof(GameResultPanel).GetField("counting", Private).GetValue(panel);
    static void Step(GameResultSequence sequence, float delta) { typeof(GameResultSequence).GetMethod("Step", Private).Invoke(sequence, new object[] { delta }); }
    static void CheckUniformLineup(RectTransform root)
    {
        Transform stage = root.Find("ResultCharacters");
        float height = -1f;
        for (int i = 0; i < stage.childCount; i++)
        {
            Transform holder = stage.GetChild(i);
            Image avatar = holder.Find("Character").GetComponent<Image>(); Rect rect = avatar.rectTransform.rect;
            if (height < 0f) height = rect.height;
            Check(Mathf.Abs(rect.height - height) < .01f, "Character heights differ");
            Check(Mathf.Abs(rect.width / rect.height - avatar.sprite.rect.width / avatar.sprite.rect.height) < .001f, "Character stretched");
            Check(Mathf.Abs(avatar.rectTransform.pivot.y - avatar.sprite.pivot.y / avatar.sprite.rect.height) < .001f, "Feet pivot changed");
            for (int j = 0; j < i; j++)
            {
                var other = (RectTransform)stage.GetChild(j);
                if (Mathf.Abs(((RectTransform)holder).anchoredPosition.y - other.anchoredPosition.y) > .1f) continue;
                var left = new Vector3[4]; var right = new Vector3[4];
                other.Find("Character").GetComponent<RectTransform>().GetWorldCorners(left);
                avatar.rectTransform.GetWorldCorners(right);
                Check(left[2].x < right[0].x, "Characters overlap at uniform height");
            }
        }
    }
    static void Save(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.active = null;
    }
}
