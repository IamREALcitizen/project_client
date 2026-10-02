using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.Chat;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;
using WhoisntCitizen.Vote;

namespace WhoisntCitizen.EditorTools
{
    // Tools > Game Scene > 게임 화면 구성 (G)
    // 열려 있는 GameScene(Vote_test 복사본)에 G(게임 진행 화면)를 붙인다. 안내문 3-1 ~ 3-10을 그대로 한다.
    //   GameCanvas                [GameScreen 추가]
    //   ├─ Background
    //   ├─ TopBar_Placeholder     Label → 페이즈 표시, TimerText·DisconnectedBanner(꺼 둠) 추가
    //   ├─ RoleCard               (새로) 내 직업 카드. 채팅 기록은 그만큼 아래로 내린다
    //   ├─ ChatLogPanel           (klik075)
    //   ├─ GameBottomSheet        (klik075) Drawer에 NightActionPanel(꺼 둠) 추가, VotePanel 자기 투표 허용
    //   └─ GameResultPanel        (새로, 꺼 둠) 결과 + "대기실로"(Room 씬으로, 가짜 서버면 새 판)
    //   GameFlow                  (새로) WaitingRoomController(씬 들어오기·나가기) + GameController(가짜 서버 켬)
    //   GameChat                  (새로) ChatApiClient + GameChatController (서버 채팅: ChatLogPanel·하단 탭 입력에 연결)
    //   WaitingRoomPanel          삭제 (예전 도구가 만든 씬 안 대기실. 대기실은 Room 씬이 맡는다)
    //   VoteTestSystem            삭제 (가짜 데이터·투표 처리가 G와 부딪힌다)
    // 여러 번 실행해도 된다. 이미 있는 오브젝트는 다시 만들지 않고 연결만 다시 한다(손으로 고친 배치는 그대로 둔다).
    // Ctrl+Z 한 번으로 되돌릴 수 있다. 저장은 직접 한다(Ctrl+S).
    public static class GameSceneSetupTool
    {
        private const string GameScenePath = "Assets/01_Scenes/GameScene.unity";
        private const string FontPath = "Assets/Fonts/MalgunGothic SDF.asset";
        private const string VotePanelPrefabPath = "Assets/03_Prefabs/Vote/VotePanel.prefab";
        private const string ItemPrefabPath = "Assets/03_Prefabs/Vote/PlayerProfileItem.prefab";

        // VoteTestSceneSetupTool(klik075)과 같은 크기·색
        private const float TopBarHeight = 120f;
        private const float RoleCardHeight = 110f;
        private static readonly Color PanelColor = new Color32(0x1B, 0x1E, 0x26, 0xF5);
        private static readonly Color CardColor = new Color32(0x2B, 0x2F, 0x3A, 0xFF);
        private static readonly Color AccentColor = new Color32(0xF2, 0xC1, 0x4E, 0xFF);
        private static readonly Color DarkText = new Color32(0x1B, 0x1E, 0x26, 0xFF);
        private static readonly Color DangerColor = new Color32(0xD9, 0x4B, 0x4B, 0xFF);
        private static readonly Color MutedText = new Color(1f, 1f, 1f, 0.6f);

        private static TMP_FontAsset font;
        private static TMP_DefaultControls.Resources res;
        private static int uiLayer;

        [MenuItem("Tools/Game Scene/게임 화면 구성 (G)")]
        public static void SetupFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameScenePath
                && !EditorUtility.DisplayDialog("게임 화면 구성",
                    "지금 열린 씬은 " + scene.path + " 입니다.\nGameScene이 아닌데 이 씬에 구성할까요?", "구성", "취소"))
            {
                return;
            }
            string error;
            string summary = Apply(out error);
            if (error != null)
            {
                EditorUtility.DisplayDialog("게임 화면 구성", error, "확인");
                return;
            }
            EditorUtility.DisplayDialog("게임 화면 구성",
                summary + "\n확인한 뒤 Ctrl+S로 씬을 저장하세요. (Ctrl+Z로 되돌릴 수 있습니다)", "확인");
        }

        /// <summary>열려 있는 씬에 G를 구성한다. 실패하면 error에 이유를 넣고 null을 돌려준다. 성공하면 한 일 요약.</summary>
        public static string Apply(out string error)
        {
            error = null;
            BottomTabController bottomTab = Object.FindFirstObjectByType<BottomTabController>(FindObjectsInactive.Include);
            ChatLogView chatLog = Object.FindFirstObjectByType<ChatLogView>(FindObjectsInactive.Include);
            VotePanelController votePanel = bottomTab != null ? bottomTab.GetComponentInChildren<VotePanelController>(true) : null;
            if (bottomTab == null || chatLog == null || votePanel == null)
            {
                error = "GameBottomSheet(+VotePanel)과 ChatLogPanel이 있는 씬에서 실행하세요. (Assets/01_Scenes/GameScene.unity)";
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("게임 화면 구성 (G)");
            int undoGroup = Undo.GetCurrentGroup();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            res = BuildResources();
            Transform canvas = bottomTab.GetComponentInParent<Canvas>().rootCanvas.transform;
            uiLayer = canvas.gameObject.layer;
            var log = new StringBuilder();

            RemoveTestBootstrap(log);                                                     // 3-1
            GameObject flow = EnsureGameFlow(log);                                        // 3-2
            Transform topBar = EnsureTopBar(canvas, log);                                 // 3-3
            TextMeshProUGUI phaseText = EnsurePhaseText(topBar);
            TextMeshProUGUI timerText = EnsureTimerText(topBar, log);
            GameObject banner = EnsureDisconnectedBanner(topBar, log);
            AllowSelfVote(votePanel, log);                                                // 3-4
            NightActionPanel nightPanel = EnsureNightPanel(votePanel.transform.parent, log); // 3-5
            RoleCardView roleCard = EnsureRoleCard(canvas, topBar, chatLog, log);         // 3-6
            GameResultPanel resultPanel = EnsureResultPanel(canvas, log);                 // 3-7
            RemoveOldWaitingRoom(canvas, flow, log);                                      // 3-8
            EnsureGameChat(chatLog, bottomTab, log);                                      // 3-11 서버 채팅

            GameScreen screen = canvas.GetComponent<GameScreen>();                        // 3-9
            if (screen == null)
            {
                screen = Undo.AddComponent<GameScreen>(canvas.gameObject);
                log.AppendLine("· GameCanvas에 GameScreen 추가");
            }
            Bind(screen, "controller", flow.GetComponent<GameController>());
            Bind(screen, "waitingRoom", flow.GetComponent<WaitingRoomController>());
            Bind(screen, "phaseText", phaseText);
            Bind(screen, "timerText", timerText);
            Bind(screen, "disconnectedBanner", banner);
            Bind(screen, "chatLog", chatLog);
            Bind(screen, "bottomTab", bottomTab);
            Bind(screen, "votePanel", votePanel);
            Bind(screen, "nightPanel", nightPanel);
            Bind(screen, "roleCard", roleCard);
            Bind(screen, "resultPanel", resultPanel);

            Bind(flow.GetComponent<GameController>(), "gameView", screen);              // 3-10
            log.AppendLine("· GameScreen ↔ GameController 연결");

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = flow;
            return log.ToString();
        }

        // ================================================================ 3-1 ~ 3-4

        private static void RemoveTestBootstrap(StringBuilder log)
        {
            foreach (VoteTestBootstrap boot in Object.FindObjectsByType<VoteTestBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                log.AppendLine("· " + boot.gameObject.name + " 삭제 (가짜 데이터)");
                Undo.DestroyObjectImmediate(boot.gameObject);
            }
        }

        private static GameObject EnsureGameFlow(StringBuilder log)
        {
            WaitingRoomController existing = Object.FindFirstObjectByType<WaitingRoomController>(FindObjectsInactive.Include);
            GameObject flow;
            if (existing != null)
            {
                flow = existing.gameObject;
            }
            else
            {
                flow = new GameObject("GameFlow");
                Undo.RegisterCreatedObjectUndo(flow, "GameFlow");
                Undo.AddComponent<WaitingRoomController>(flow); // RequireComponent로 GameController도 붙는다
                GameController controller = flow.GetComponent<GameController>();
                BindBool(controller, "useFakeServer", true);    // 처음 만들 때만. 다시 실행할 때는 직접 바꾼 값을 둔다
                BindString(controller, "fakeMyRole", RoleCodes.CrewCaptain);
                BindBool(controller, "fakeBotsAct", true);
                log.AppendLine("· GameFlow 생성 (WaitingRoomController + GameController, 가짜 서버 켬)");
            }
            return flow;
        }

        private static Transform EnsureTopBar(Transform canvas, StringBuilder log)
        {
            Transform top = canvas.Find("TopBar_Placeholder");
            if (top != null)
            {
                return top;
            }
            RectTransform rt = NewRect("TopBar_Placeholder", canvas, typeof(Image));
            SetRect(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, TopBarHeight));
            rt.GetComponent<Image>().color = CardColor;
            Created(rt.gameObject, log, "TopBar_Placeholder 생성");
            return rt;
        }

        private static TextMeshProUGUI EnsurePhaseText(Transform topBar)
        {
            Transform t = topBar.Find("Label");
            TextMeshProUGUI label = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            if (label == null)
            {
                label = NewText("Label", topBar, string.Empty, 36);
                Undo.RegisterCreatedObjectUndo(label.gameObject, "Label");
            }
            Undo.RecordObject(label, "Label");
            Undo.RecordObject(label.rectTransform, "Label");
            label.text = "1일차 밤"; // 실행하면 GameScreen이 페이즈로 바꾼다
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            SetOffsets(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 0), new Vector2(-300, 0));
            return label;
        }

        private static TextMeshProUGUI EnsureTimerText(Transform topBar, StringBuilder log)
        {
            Transform t = topBar.Find("TimerText");
            if (t != null && t.GetComponent<TextMeshProUGUI>() != null)
            {
                return t.GetComponent<TextMeshProUGUI>();
            }
            TextMeshProUGUI timer = NewText("TimerText", topBar, "00:00", 44);
            SetRect(timer.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(240, 0));
            timer.alignment = TextAlignmentOptions.MidlineRight;
            timer.fontStyle = FontStyles.Bold;
            timer.color = AccentColor;
            Created(timer.gameObject, log, "TimerText 생성 (상단 오른쪽)");
            return timer;
        }

        private static GameObject EnsureDisconnectedBanner(Transform topBar, StringBuilder log)
        {
            Transform t = topBar.Find("DisconnectedBanner");
            if (t != null)
            {
                return t.gameObject;
            }
            RectTransform banner = NewRect("DisconnectedBanner", topBar, typeof(Image));
            SetRect(banner, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));
            banner.GetComponent<Image>().color = DangerColor;
            TextMeshProUGUI text = NewText("Text", banner, "서버에 연결할 수 없습니다. 다시 연결하는 중…", 28);
            Stretch(text.rectTransform);
            banner.gameObject.SetActive(false);
            Created(banner.gameObject, log, "DisconnectedBanner 생성 (꺼 둠)");
            return banner.gameObject;
        }

        private static void AllowSelfVote(VotePanelController votePanel, StringBuilder log)
        {
            var so = new SerializedObject(votePanel);
            SerializedProperty p = so.FindProperty("allowSelfVote");
            if (p == null || p.boolValue)
            {
                return;
            }
            p.boolValue = true; // 결정: 자기 자신에게도 투표할 수 있다 (서버 규칙과 같음)
            so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(votePanel);
            log.AppendLine("· VotePanel 자기 투표 허용 (Allow Self Vote)");
        }

        // ================================================================ 3-5 밤 능력 패널

        private static NightActionPanel EnsureNightPanel(Transform drawer, StringBuilder log)
        {
            Transform existing = drawer.Find("NightActionPanel");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                // VotePanel 프리팹을 연결 없이 복제해 같은 배치를 쓴다. 투표용 컴포넌트는 뺀다.
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VotePanelPrefabPath);
                go = Object.Instantiate(prefab, drawer, false);
                go.name = "NightActionPanel";
                Object.DestroyImmediate(go.GetComponent<VotePanelController>());

                Transform title = go.transform.Find("Title");
                if (title != null && title.GetComponent<TextMeshProUGUI>() != null)
                {
                    title.GetComponent<TextMeshProUGUI>().text = "밤 능력";
                }
                Transform side = go.transform.Find("SideArea");
                Transform confirm = side.Find("ConfirmButton");
                confirm.GetComponentInChildren<TextMeshProUGUI>().text = "능력";
                side.Find("StatusText").GetComponent<TextMeshProUGUI>().text = "밤 능력 대상을 고르세요.";
                RectTransform statusRt = (RectTransform)side.Find("StatusText");
                statusRt.offsetMax = new Vector2(-700, statusRt.offsetMax.y); // 버튼 두 개 자리

                GameObject skip = Object.Instantiate(confirm.gameObject, side, false);
                skip.name = "SkipButton";
                ((RectTransform)skip.transform).anchoredPosition = new Vector2(-350, 0); // 확정 버튼 왼쪽
                skip.GetComponent<Image>().color = CardColor;
                TextMeshProUGUI skipText = skip.GetComponentInChildren<TextMeshProUGUI>();
                skipText.text = "넘기기";
                skipText.color = Color.white;

                go.AddComponent<NightActionPanel>();
                go.SetActive(false); // 밤에 GameScreen이 켠다
                Created(go, log, "NightActionPanel 생성 (Drawer 안, VotePanel 복제, 꺼 둠)");
            }

            NightActionPanel panel = go.GetComponent<NightActionPanel>();
            if (panel == null)
            {
                panel = Undo.AddComponent<NightActionPanel>(go);
            }
            Transform sideArea = go.transform.Find("SideArea");
            Bind(panel, "itemPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath).GetComponent<PlayerProfileItem>());
            Bind(panel, "gridRoot", go.transform.Find("Scroll View/Viewport/Content"));
            Bind(panel, "confirmButton", sideArea.Find("ConfirmButton").GetComponent<Button>());
            Bind(panel, "confirmButtonText", sideArea.Find("ConfirmButton").GetComponentInChildren<TextMeshProUGUI>(true));
            Bind(panel, "skipButton", sideArea.Find("SkipButton") != null ? sideArea.Find("SkipButton").GetComponent<Button>() : null);
            Bind(panel, "statusText", sideArea.Find("StatusText").GetComponent<TextMeshProUGUI>());
            return panel;
        }

        // ================================================================ 3-6 직업 카드

        private static RoleCardView EnsureRoleCard(Transform canvas, Transform topBar, ChatLogView chatLog, StringBuilder log)
        {
            Transform existing = canvas.Find("RoleCard");
            RectTransform card;
            if (existing != null)
            {
                card = (RectTransform)existing;
            }
            else
            {
                card = NewRect("RoleCard", canvas, typeof(Image));
                SetRect(card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -TopBarHeight), new Vector2(0, RoleCardHeight));
                card.GetComponent<Image>().color = CardColor;
                card.SetSiblingIndex(topBar.GetSiblingIndex() + 1);

                TextMeshProUGUI roleName = NewText("RoleNameText", card, "직업", 40);
                SetOffsets(roleName.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 1), new Vector2(40, 0), new Vector2(0, -6));
                roleName.alignment = TextAlignmentOptions.MidlineLeft;
                roleName.fontStyle = FontStyles.Bold;
                roleName.color = AccentColor;

                TextMeshProUGUI faction = NewText("FactionText", card, "진영", 28);
                SetOffsets(faction.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1, 1), Vector2.zero, new Vector2(-40, -6));
                faction.alignment = TextAlignmentOptions.MidlineRight;
                faction.color = MutedText;

                TextMeshProUGUI ability = NewText("AbilityText", card, "능력", 28);
                SetOffsets(ability.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(40, 6), Vector2.zero);
                ability.alignment = TextAlignmentOptions.MidlineLeft;

                TextMeshProUGUI teammates = NewText("TeammatesText", card, string.Empty, 28);
                SetOffsets(teammates.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(0, 6), new Vector2(-40, 0));
                teammates.alignment = TextAlignmentOptions.MidlineRight;
                teammates.color = AccentColor;
                teammates.textWrappingMode = TextWrappingModes.NoWrap;
                teammates.overflowMode = TextOverflowModes.Ellipsis;

                RectTransform dead = NewRect("DeadBadge", card, typeof(Image));
                SetRect(dead, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 56));
                dead.GetComponent<Image>().color = DangerColor;
                TextMeshProUGUI deadText = NewText("Text", dead, "사망", 30);
                Stretch(deadText.rectTransform);
                deadText.fontStyle = FontStyles.Bold;
                dead.gameObject.SetActive(false);

                card.gameObject.AddComponent<RoleCardView>();
                Created(card.gameObject, log, "RoleCard 생성 (상단 바 아래)");

                // 채팅 기록 위쪽을 직업 카드 아래로 내린다 (처음 만들 때만)
                RectTransform logRt = (RectTransform)chatLog.transform;
                Undo.RecordObject(logRt, "ChatLogPanel");
                logRt.offsetMax = new Vector2(logRt.offsetMax.x, -(TopBarHeight + RoleCardHeight));
                PrefabUtility.RecordPrefabInstancePropertyModifications(logRt);
            }

            RoleCardView view = card.GetComponent<RoleCardView>();
            if (view == null)
            {
                view = Undo.AddComponent<RoleCardView>(card.gameObject);
            }
            Bind(view, "roleNameText", Text(card, "RoleNameText"));
            Bind(view, "factionText", Text(card, "FactionText"));
            Bind(view, "abilityText", Text(card, "AbilityText"));
            Bind(view, "teammatesText", Text(card, "TeammatesText"));
            Bind(view, "deadBadge", card.Find("DeadBadge") != null ? card.Find("DeadBadge").gameObject : null);
            return view;
        }

        // ================================================================ 3-7 게임 결과

        private static GameResultPanel EnsureResultPanel(Transform canvas, StringBuilder log)
        {
            Transform existing = canvas.Find("GameResultPanel");
            RectTransform root;
            if (existing != null)
            {
                root = (RectTransform)existing;
            }
            else
            {
                root = NewRect("GameResultPanel", canvas, typeof(Image));
                Stretch(root);
                root.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f); // 뒤를 누르지 못하게 덮는다

                RectTransform panel = NewRect("Card", root, typeof(Image));
                SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 1300));
                panel.GetComponent<Image>().color = PanelColor;

                TextMeshProUGUI winner = NewText("WinnerText", panel, "선원 진영 승리", 60);
                SetRect(winner.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(-80, 90));
                winner.fontStyle = FontStyles.Bold;
                winner.color = AccentColor;

                TextMeshProUGUI outcome = NewText("OutcomeText", panel, "승리", 48);
                SetRect(outcome.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(-80, 70));

                TextMeshProUGUI players = NewText("PlayersText", panel, string.Empty, 34);
                SetOffsets(players.rectTransform, Vector2.zero, Vector2.one, new Vector2(60, 220), new Vector2(-60, -280));
                players.alignment = TextAlignmentOptions.TopLeft;
                players.textWrappingMode = TextWrappingModes.Normal;

                Button back = NewButton("BackButton", panel, "대기실로", 40);
                SetRect((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(400, 120));
                StyleAccentButton(back);

                root.gameObject.AddComponent<GameResultPanel>();
                root.gameObject.SetActive(false);
                Created(root.gameObject, log, "GameResultPanel 생성 (꺼 둠)");
            }
            root.SetAsLastSibling();

            GameResultPanel result = root.GetComponent<GameResultPanel>();
            if (result == null)
            {
                result = Undo.AddComponent<GameResultPanel>(root.gameObject);
            }
            Bind(result, "winnerText", Text(root, "Card/WinnerText"));
            Bind(result, "outcomeText", Text(root, "Card/OutcomeText"));
            Bind(result, "playersText", Text(root, "Card/PlayersText"));
            Bind(result, "backButton", root.Find("Card/BackButton") != null ? root.Find("Card/BackButton").GetComponent<Button>() : null);
            return result;
        }

        // ================================================================ 3-8 예전 대기실 정리

        // 예전 도구가 GameScene 안에 만든 대기실을 지운다. 대기실은 Room 씬(RoomUIController)이 맡는다.
        // WaitingRoomView.cs를 지운 뒤라 GameFlow에 Missing Script로 남은 컴포넌트도 지운다.
        private static void RemoveOldWaitingRoom(Transform canvas, GameObject flow, StringBuilder log)
        {
            Transform panel = canvas.Find("WaitingRoomPanel");
            if (panel != null)
            {
                Undo.DestroyObjectImmediate(panel.gameObject);
                log.AppendLine("· WaitingRoomPanel 삭제 (대기실은 Room 씬)");
            }
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(flow);
            if (missing > 0)
            {
                Undo.RegisterCompleteObjectUndo(flow, "Missing Script");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(flow);
                log.AppendLine("· GameFlow의 Missing Script " + missing + "개 삭제 (예전 WaitingRoomView)");
            }
        }

        // ================================================================ 3-11 서버 채팅

        // ChatScene의 서버 채팅 기능(GameChatController)을 붙인다. 브랜치 병합 때 씬에서 빠져도 이 도구를 다시 실행하면 복구된다.
        // 처음 만들 때만 ChatLogView의 [시스템] 색을 채팅과 같은 녹색으로 맞춘다. (다시 실행할 때는 직접 바꾼 값을 둔다)
        private static void EnsureGameChat(ChatLogView chatLog, BottomTabController bottomTab, StringBuilder log)
        {
            GameChatController chat = Object.FindFirstObjectByType<GameChatController>(FindObjectsInactive.Include);
            if (chat == null)
            {
                GameObject go = new GameObject("GameChat");
                Undo.RegisterCreatedObjectUndo(go, "GameChat");
                Undo.AddComponent<ChatApiClient>(go);
                chat = Undo.AddComponent<GameChatController>(go);

                var so = new SerializedObject(chatLog);
                SerializedProperty color = so.FindProperty("systemColor");
                if (color != null)
                {
                    color.colorValue = chat.systemColor;
                    so.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(chatLog);
                }
                log.AppendLine("· GameChat 생성 (ChatApiClient + GameChatController, [시스템] 색을 채팅 녹색으로)");
            }
            ChatApiClient api = chat.GetComponent<ChatApiClient>();
            if (api == null)
            {
                api = Undo.AddComponent<ChatApiClient>(chat.gameObject);
            }
            Bind(chat, "api", api);
            Bind(chat, "chatLog", chatLog);
            Bind(chat, "bottomTab", bottomTab);
        }

        // ================================================================ 도우미 (VoteTestSceneSetupTool과 같은 방식)

        private static void Created(GameObject go, StringBuilder log, string message)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
            }
            Undo.RegisterCreatedObjectUndo(go, go.name);
            log.AppendLine("· " + message);
        }

        private static TextMeshProUGUI Text(Transform root, string path)
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private static void StyleAccentButton(Button button)
        {
            button.GetComponent<Image>().color = AccentColor;
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.color = DarkText;
            text.fontStyle = FontStyles.Bold;
        }

        private static RectTransform NewRect(string name, Transform parent, params System.Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            foreach (System.Type t in components)
            {
                go.AddComponent(t);
            }
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size)
        {
            GameObject go = TMP_DefaultControls.CreateText(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (font != null)
            {
                tmp.font = font;
            }
            return tmp;
        }

        private static Button NewButton(string name, Transform parent, string label, float size)
        {
            GameObject go = TMP_DefaultControls.CreateButton(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = size;
            if (font != null)
            {
                tmp.font = font;
            }
            Button b = go.GetComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            return b;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SetOffsets(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rt)
        {
            SetOffsets(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void Bind(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[GameSceneSetup] 필드 없음: " + target.GetType().Name + "." + field);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void BindBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedProperties();
        }

        private static void BindString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedProperties();
        }

        private static TMP_DefaultControls.Resources BuildResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
        }
    }
}
