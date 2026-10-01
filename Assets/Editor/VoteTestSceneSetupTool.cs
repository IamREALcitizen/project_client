using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.GameUI;
using WhoisntCitizen.Vote;

namespace WhoisntCitizen.EditorTools
{
    // Tools > Vote Test > Build Prefabs & Scene
    //  - Assets/03_Prefabs/Vote/PlayerProfileItem.prefab
    //  - Assets/03_Prefabs/Vote/VotePanel.prefab
    //  - Assets/03_Prefabs/GameUI/ChatLogLine, ChatLogPanel, ChatKeyboardPanel, GameBottomSheet.prefab
    //  - Assets/01_Scenes/Vote_test.unity
    //      GameCanvas
    //      ├─ Background / TopBar_Placeholder
    //      ├─ ChatLogPanel     (채팅 기록, 아래 끝이 시트 윗변을 따라 올라감)
    //      └─ GameBottomSheet  (TabBar + Drawer[ChatKeyboardPanel, VotePanel])
    // 이미 있는 파일은 덮어쓰지 않는다. (수작업 수정을 보호) 다시 만들려면 해당 파일을 지우고 실행.
    public static class VoteTestSceneSetupTool
    {
        private const string PrefabDir = "Assets/03_Prefabs/Vote";
        private const string ItemPrefabPath = PrefabDir + "/PlayerProfileItem.prefab";
        private const string VotePanelPrefabPath = PrefabDir + "/VotePanel.prefab";
        private const string GameUIPrefabDir = "Assets/03_Prefabs/GameUI";
        private const string ChatLogLinePrefabPath = GameUIPrefabDir + "/ChatLogLine.prefab";
        private const string ChatLogPanelPrefabPath = GameUIPrefabDir + "/ChatLogPanel.prefab";
        private const string ChatKeyboardPanelPrefabPath = GameUIPrefabDir + "/ChatKeyboardPanel.prefab";
        private const string BottomSheetPrefabPath = GameUIPrefabDir + "/GameBottomSheet.prefab";
        private const string ScenePath = "Assets/01_Scenes/Vote_test.unity";
        private const string FontPath = "Assets/Fonts/MalgunGothic SDF.asset";
        private const string PortraitDir = "Assets/09_Image/Portrait";

        private const float TopBarHeight = 120f;
        private const float TabBarHeight = 140f;
        private const float DrawerHeight = 800f;

        private static readonly Color PanelColor = new Color32(0x1B, 0x1E, 0x26, 0xF5);
        private static readonly Color TabBarColor = new Color32(0x23, 0x26, 0x2F, 0xFF);
        private static readonly Color CardColor = new Color32(0x2B, 0x2F, 0x3A, 0xFF);
        private static readonly Color AccentColor = new Color32(0xF2, 0xC1, 0x4E, 0xFF);

        private static TMP_FontAsset font;
        private static TMP_DefaultControls.Resources res;

        [MenuItem("Tools/Vote Test/Build Prefabs & Scene")]
        public static void BuildAll()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[VoteTestSetup] 저장하지 않은 씬이 있습니다. 먼저 저장한 뒤 다시 실행하세요.");
                    return;
                }
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            res = BuildResources();
            EnsureFolder(PrefabDir);
            EnsureFolder(GameUIPrefabDir);

            bool sceneExists = File.Exists(ScenePath);
            if (!sceneExists)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PlayerProfileItem itemPrefab = LoadOrBuild(ItemPrefabPath, BuildItemPrefab);
            VotePanelController votePanelPrefab = LoadOrBuild(VotePanelPrefabPath, () => BuildVotePanelPrefab(itemPrefab));
            TextMeshProUGUI linePrefab = LoadOrBuild(ChatLogLinePrefabPath, BuildChatLogLinePrefab);
            ChatLogView chatLogPrefab = LoadOrBuild(ChatLogPanelPrefabPath, () => BuildChatLogPanelPrefab(linePrefab));
            GameObject chatKeyboardPrefab = LoadOrBuild(ChatKeyboardPanelPrefabPath, BuildChatKeyboardPanelPrefab);

            if (sceneExists)
            {
                Debug.Log($"[VoteTestSetup] 기존 씬 유지: {ScenePath} (다시 만들려면 씬 파일을 지우고 실행)");
                return;
            }

            BuildScene(votePanelPrefab, chatLogPrefab, chatKeyboardPrefab);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[VoteTestSetup] 완료: {ScenePath}");
        }

        // 이미 있으면 그대로 쓰고(수작업 수정 보호), 없을 때만 만든다.
        private static T LoadOrBuild<T>(string path, System.Func<T> build) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log($"[VoteTestSetup] 기존 프리팹 유지: {path}");
                return existing;
            }
            return build();
        }

        // ================= PlayerProfileItem =================

        private static PlayerProfileItem BuildItemPrefab()
        {
            RectTransform root = NewRect("PlayerProfileItem", null, typeof(Image), typeof(Button), typeof(CanvasGroup));
            root.sizeDelta = new Vector2(200, 250);
            Image bg = root.GetComponent<Image>();
            bg.sprite = res.standard;
            bg.type = Image.Type.Sliced;
            bg.color = CardColor;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock cb = button.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = cb;

            RectTransform portrait = NewRect("Portrait", root, typeof(Image));
            SetRect(portrait, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(170, 170));
            Image portraitImg = portrait.GetComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;

            TextMeshProUGUI nickname = NewText("Nickname", root, "닉네임", 30);
            RectTransform nickRt = nickname.rectTransform;
            nickRt.anchorMin = new Vector2(0, 0);
            nickRt.anchorMax = new Vector2(1, 0);
            nickRt.pivot = new Vector2(0.5f, 0);
            nickRt.anchoredPosition = new Vector2(0, 8);
            nickRt.sizeDelta = new Vector2(-12, 52);
            nickname.enableAutoSizing = true;
            nickname.fontSizeMin = 16;
            nickname.fontSizeMax = 30;

            // 선택 표시: 속이 빈 노란 테두리 (Sliced + fillCenter=false, 배율을 낮춰 테두리를 두껍게)
            RectTransform selected = NewRect("SelectedFrame", root, typeof(Image));
            Stretch(selected);
            selected.offsetMin = new Vector2(-6, -6);
            selected.offsetMax = new Vector2(6, 6);
            Image selImg = selected.GetComponent<Image>();
            selImg.sprite = res.standard;
            selImg.type = Image.Type.Sliced;
            selImg.fillCenter = false;
            selImg.pixelsPerUnitMultiplier = 0.25f;
            selImg.color = AccentColor;
            selImg.raycastTarget = false;
            selected.gameObject.SetActive(false);

            // "나" 배지
            RectTransform me = NewRect("MeBadge", root, typeof(Image));
            SetRect(me, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -8), new Vector2(56, 38));
            Image meImg = me.GetComponent<Image>();
            meImg.sprite = res.standard;
            meImg.type = Image.Type.Sliced;
            meImg.color = new Color32(0x3B, 0x82, 0xF6, 0xFF);
            meImg.raycastTarget = false;
            TextMeshProUGUI meText = NewText("Text", me, "나", 24);
            Stretch(meText.rectTransform);
            me.gameObject.SetActive(false);

            // 득표 수 배지
            RectTransform badge = NewRect("VoteCountBadge", root, typeof(Image));
            SetRect(badge, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(56, 56));
            Image badgeImg = badge.GetComponent<Image>();
            badgeImg.sprite = res.knob;
            badgeImg.color = new Color32(0xE5, 0x48, 0x4D, 0xFF);
            badgeImg.raycastTarget = false;
            TextMeshProUGUI countText = NewText("Count", badge, "0", 30);
            Stretch(countText.rectTransform);
            countText.fontStyle = FontStyles.Bold;
            badge.gameObject.SetActive(false);

            // 사망 덮개
            RectTransform dead = NewRect("DeadOverlay", root, typeof(Image));
            Stretch(dead);
            Image deadImg = dead.GetComponent<Image>();
            deadImg.sprite = res.standard;
            deadImg.type = Image.Type.Sliced;
            deadImg.color = new Color(0, 0, 0, 0.6f);
            deadImg.raycastTarget = false;
            TextMeshProUGUI deadText = NewText("Text", dead, "사망", 40);
            Stretch(deadText.rectTransform);
            deadText.color = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
            deadText.fontStyle = FontStyles.Bold;
            dead.gameObject.SetActive(false);

            PlayerProfileItem item = root.gameObject.AddComponent<PlayerProfileItem>();
            Bind(item, "button", button);
            Bind(item, "canvasGroup", root.GetComponent<CanvasGroup>());
            Bind(item, "portraitImage", portraitImg);
            Bind(item, "nicknameText", nickname);
            Bind(item, "selectedFrame", selected.gameObject);
            Bind(item, "meBadge", me.gameObject);
            Bind(item, "voteCountBadge", badge.gameObject);
            Bind(item, "voteCountText", countText);
            Bind(item, "deadOverlay", dead.gameObject);

            return SavePrefab(root.gameObject, ItemPrefabPath).GetComponent<PlayerProfileItem>();
        }

        // ================= VotePanel =================

        private static VotePanelController BuildVotePanelPrefab(PlayerProfileItem itemPrefab)
        {
            // Drawer 안에서 stretch로 꽉 채운다. (세로 1080 x 1920 기준, 단독 크기는 1080 x DrawerHeight)
            RectTransform root = NewRect("VotePanel", null, typeof(Image));
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            Image bg = root.GetComponent<Image>();
            bg.color = PanelColor;

            TextMeshProUGUI title = NewText("Title", root, "투표", 32);
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0, 1);
            titleRt.offsetMin = new Vector2(40, -80);
            titleRt.offsetMax = new Vector2(-40, -10);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.fontStyle = FontStyles.Bold;

            // 스크롤 + Grid
            RectTransform scroll = NewRect("Scroll View", root, typeof(Image), typeof(ScrollRect));
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(20, 170);
            scroll.offsetMax = new Vector2(-20, -80);
            Image scrollBg = scroll.GetComponent<Image>();
            scrollBg.color = new Color(1, 1, 1, 0.03f);

            RectTransform viewport = NewRect("Viewport", scroll, typeof(RectMask2D), typeof(Image));
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0); // 드래그 영역 확보용

            RectTransform content = NewRect("Content", viewport, typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200, 250);
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect sr = scroll.GetComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;

            // 하단 줄: 상태 안내(왼쪽) + 확정 버튼(오른쪽)
            RectTransform side = NewRect("SideArea", root);
            side.anchorMin = new Vector2(0, 0);
            side.anchorMax = new Vector2(1, 0);
            side.pivot = new Vector2(0.5f, 0);
            side.offsetMin = new Vector2(40, 20);
            side.offsetMax = new Vector2(-40, 150);

            TextMeshProUGUI status = NewText("StatusText", side, "처형할 플레이어를\n선택하세요.", 30);
            RectTransform statusRt = status.rectTransform;
            statusRt.anchorMin = new Vector2(0, 0);
            statusRt.anchorMax = new Vector2(1, 1);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = new Vector2(-360, 0);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            status.textWrappingMode = TextWrappingModes.Normal;
            status.richText = true;

            Button confirm = NewButton("ConfirmButton", side, "투표하기", 38);
            RectTransform confirmRt = (RectTransform)confirm.transform;
            SetRect(confirmRt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(330, 0));
            Image confirmImg = confirm.GetComponent<Image>();
            confirmImg.color = AccentColor;
            TextMeshProUGUI confirmText = confirm.GetComponentInChildren<TextMeshProUGUI>();
            confirmText.color = new Color32(0x1B, 0x1E, 0x26, 0xFF);
            confirmText.fontStyle = FontStyles.Bold;

            VotePanelController panel = root.gameObject.AddComponent<VotePanelController>();
            Bind(panel, "itemPrefab", itemPrefab);
            Bind(panel, "gridRoot", content);
            Bind(panel, "confirmButton", confirm);
            Bind(panel, "confirmButtonText", confirmText);
            Bind(panel, "statusText", status);

            return SavePrefab(root.gameObject, VotePanelPrefabPath).GetComponent<VotePanelController>();
        }

        // ================= Chat (자리 표시용 기본 프리팹) =================

        private static TextMeshProUGUI BuildChatLogLinePrefab()
        {
            TextMeshProUGUI line = NewText("ChatLogLine", null, "메시지", 32);
            line.alignment = TextAlignmentOptions.TopLeft;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.richText = true;
            line.rectTransform.sizeDelta = new Vector2(1000, 44);
            return SavePrefab(line.gameObject, ChatLogLinePrefabPath).GetComponent<TextMeshProUGUI>();
        }

        private static ChatLogView BuildChatLogPanelPrefab(TextMeshProUGUI linePrefab)
        {
            RectTransform root = NewRect("ChatLogPanel", null, typeof(Canvas), typeof(GraphicRaycaster), typeof(ScrollRect));
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;

            RectTransform viewport = NewRect("Viewport", root, typeof(RectMask2D), typeof(Image));
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0); // 탭/드래그를 받기 위한 투명 영역

            // Content는 Viewport 하단에 붙인다 → Viewport가 줄어도 최신 메시지가 보인다.
            RectTransform content = NewRect("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(1, 0);
            content.pivot = new Vector2(0.5f, 0);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 24, 24);
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = root.GetComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40;

            ChatLogView view = root.gameObject.AddComponent<ChatLogView>();
            Bind(view, "scrollRect", sr);
            Bind(view, "content", content);
            Bind(view, "linePrefab", linePrefab);
            return SavePrefab(root.gameObject, ChatLogPanelPrefabPath).GetComponent<ChatLogView>();
        }

        private static GameObject BuildChatKeyboardPanelPrefab()
        {
            RectTransform root = NewRect("ChatKeyboardPanel", null, typeof(Image));
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;
            root.GetComponent<Image>().color = PanelColor;
            TextMeshProUGUI label = NewText("Label", root, "채팅 / 키보드 영역\n(채팅 UI 합칠 자리)", 36);
            Stretch(label.rectTransform);
            label.color = new Color(1, 1, 1, 0.4f);
            return SavePrefab(root.gameObject, ChatKeyboardPanelPrefabPath);
        }

        // ================= Scene =================

        private static void BuildScene(VotePanelController votePanelPrefab, ChatLogView chatLogPrefab, GameObject chatKeyboardPrefab)
        {
            // Camera
            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x10, 0x12, 0x18, 0xFF);
            camGo.transform.position = new Vector3(0, 0, -10);

            // EventSystem
            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            // GameCanvas
            GameObject canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 세로 모바일 기준
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 가로 폭 기준으로 맞춤
            Transform canvasT = canvasGo.transform;

            // 배경
            RectTransform bg = NewRect("Background", canvasT, typeof(Image));
            Stretch(bg);
            bg.GetComponent<Image>().color = new Color32(0x14, 0x17, 0x1F, 0xFF);

            // 상단 바 자리 (페이즈 / 남은 시간 등)
            RectTransform top = NewRect("TopBar_Placeholder", canvasT, typeof(Image));
            SetRect(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, TopBarHeight));
            top.GetComponent<Image>().color = TabBarColor;
            TextMeshProUGUI topLabel = NewText("Label", top, "낮 1일차 · 투표   (상단 바 자리)", 36);
            Stretch(topLabel.rectTransform);
            topLabel.color = new Color(1, 1, 1, 0.6f);

            // 채팅 기록: 상단 바 아래 ~ 시트 윗변. 아래 끝은 BottomTabController가 시트를 따라 움직인다.
            ChatLogView chatLog = ((GameObject)PrefabUtility.InstantiatePrefab(chatLogPrefab.gameObject, canvasT)).GetComponent<ChatLogView>();
            RectTransform logRt = (RectTransform)chatLog.transform;
            logRt.anchorMin = Vector2.zero;
            logRt.anchorMax = Vector2.one;
            logRt.offsetMin = new Vector2(0, TabBarHeight);
            logRt.offsetMax = new Vector2(0, -TopBarHeight);

            // 하단 시트 (기존 프리팹이 있으면 그대로 배치, 없으면 만들고 프리팹으로 저장)
            BottomTabController tab;
            GameObject sheetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottomSheetPrefabPath);
            if (sheetPrefab != null)
            {
                tab = ((GameObject)PrefabUtility.InstantiatePrefab(sheetPrefab, canvasT)).GetComponent<BottomTabController>();
                Debug.Log($"[VoteTestSetup] 기존 프리팹 유지: {BottomSheetPrefabPath}");
            }
            else
            {
                tab = BuildBottomSheet(canvasT, votePanelPrefab, chatKeyboardPrefab);
            }

            // 시트 밖(프리팹 바깥) 참조는 씬 인스턴스에서 연결한다.
            var so = new SerializedObject(tab);
            SerializedProperty push = so.FindProperty("pushTargets");
            push.arraySize = 1;
            push.GetArrayElementAtIndex(0).objectReferenceValue = logRt;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 채팅 기록을 탭하면 패널을 내린다. (Dimmer 대신)
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(chatLog.OnTapped, tab.Close);
            PrefabUtility.RecordPrefabInstancePropertyModifications(chatLog); // 프리팹 인스턴스 오버라이드로 기록

            SetLayerRecursive(canvasGo, LayerMask.NameToLayer("UI"));

            // 가짜 데이터 주입
            GameObject sys = new GameObject("VoteTestSystem");
            VoteTestBootstrap boot = sys.AddComponent<VoteTestBootstrap>();
            Bind(boot, "bottomTab", tab);
            Bind(boot, "votePanel", tab.GetComponentInChildren<VotePanelController>(true));
            Bind(boot, "chatLog", chatLog);
            BindSprites(boot, "portraits", LoadPortraits());
        }

        private static BottomTabController BuildBottomSheet(Transform parent, VotePanelController votePanelPrefab, GameObject chatKeyboardPrefab)
        {
            // GameBottomSheet: 하단 기준 가로 stretch, 높이 = 탭 바 + Drawer
            RectTransform sheet = NewRect("GameBottomSheet", parent);
            sheet.anchorMin = new Vector2(0, 0);
            sheet.anchorMax = new Vector2(1, 0);
            sheet.pivot = new Vector2(0.5f, 0);
            sheet.sizeDelta = new Vector2(0, TabBarHeight + DrawerHeight);
            sheet.anchoredPosition = new Vector2(0, -DrawerHeight); // 닫힌 상태

            // TabBar (위쪽)
            RectTransform tabBar = NewRect("TabBar", sheet, typeof(Image));
            SetRect(tabBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, TabBarHeight));
            tabBar.GetComponent<Image>().color = TabBarColor;

            Button plus = NewButton("PlusButton", tabBar, "+", 64);
            SetRect((RectTransform)plus.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(110, 110));
            plus.GetComponent<Image>().color = new Color32(0x3A, 0x3F, 0x4D, 0xFF);
            plus.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;

            TMP_InputField input = NewInput("ChatInput", tabBar, "메시지를 입력하세요");
            RectTransform inputRt = (RectTransform)input.transform;
            inputRt.anchorMin = new Vector2(0, 0);
            inputRt.anchorMax = new Vector2(1, 1);
            inputRt.offsetMin = new Vector2(150, 18);
            inputRt.offsetMax = new Vector2(-240, -18);

            Button send = NewButton("SendButton", tabBar, "전송", 38);
            SetRect((RectTransform)send.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(200, 104));
            send.GetComponent<Image>().color = AccentColor;
            send.GetComponentInChildren<TextMeshProUGUI>().color = new Color32(0x1B, 0x1E, 0x26, 0xFF);

            // Drawer (아래쪽, 채팅 키보드/투표 공유 영역)
            RectTransform drawer = NewRect("Drawer", sheet);
            drawer.anchorMin = Vector2.zero;
            drawer.anchorMax = Vector2.one;
            drawer.offsetMin = Vector2.zero;
            drawer.offsetMax = new Vector2(0, -TabBarHeight);

            GameObject chatPanel = (GameObject)PrefabUtility.InstantiatePrefab(chatKeyboardPrefab, drawer);
            Stretch((RectTransform)chatPanel.transform);
            GameObject votePanel = (GameObject)PrefabUtility.InstantiatePrefab(votePanelPrefab.gameObject, drawer);
            Stretch((RectTransform)votePanel.transform);

            BottomTabController tab = sheet.gameObject.AddComponent<BottomTabController>();
            Bind(tab, "sheet", sheet);
            BindFloat(tab, "drawerHeight", DrawerHeight);
            Bind(tab, "chatInput", input);
            Bind(tab, "plusButton", plus);
            Bind(tab, "sendButton", send);
            Bind(tab, "chatPanel", chatPanel);
            Bind(tab, "votePanel", votePanel);

            chatPanel.SetActive(false);
            votePanel.SetActive(false);
            SetLayerRecursive(sheet.gameObject, LayerMask.NameToLayer("UI"));

            // 씬 인스턴스와 연결된 채로 프리팹 저장 (VotePanel, ChatKeyboardPanel은 중첩 프리팹)
            PrefabUtility.SaveAsPrefabAssetAndConnect(sheet.gameObject, BottomSheetPrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[VoteTestSetup] 프리팹 생성: {BottomSheetPrefabPath}");
            return tab;
        }

        // ================= Helpers =================

        private static Sprite[] LoadPortraits()
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                // Sprite Mode가 Multiple이라 조각이 여러 개일 수 있으므로 가장 큰 조각을 초상화로 쓴다.
                .Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()
                    .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault())
                .Where(s => s != null)
                .ToArray();
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            SetLayerRecursive(go, LayerMask.NameToLayer("UI"));
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[VoteTestSetup] 프리팹 생성: {path}");
            return asset;
        }

        private static RectTransform NewRect(string name, Transform parent, params System.Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            foreach (System.Type t in components) go.AddComponent(t);
            if (parent != null) go.transform.SetParent(parent, false);
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
            if (font != null) tmp.font = font;
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
            if (font != null) tmp.font = font;
            Button b = go.GetComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            return b;
        }

        private static TMP_InputField NewInput(string name, Transform parent, string placeholder)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TMP_InputField input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;
            input.pointSize = 34;
            input.textComponent.fontSize = 34;
            TMP_Text ph = (TMP_Text)input.placeholder;
            ph.text = placeholder;
            ph.fontSize = 32;
            ph.fontStyle = FontStyles.Normal;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            if (font != null)
            {
                ph.font = font;
                input.textComponent.font = font;
                input.fontAsset = font;
            }
            return input;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Bind(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[VoteTestSetup] 필드 없음: {target.GetType().Name}.{field}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindSprites(Object target, string field, Sprite[] sprites)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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
