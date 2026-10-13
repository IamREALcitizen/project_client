using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.LobbyTest;

namespace WhoisntCitizen.LobbyTest.Editor
{
    // Tools > Setup Auth & Lobby Test Scene : 현재 열려 있는 씬에 로그인/로비 테스트 UI를 만들고 LobbyTestController에 바인딩한다.
    public static class LobbySceneSetupTool
    {
        private const string CanvasName = "LobbyTestCanvas";
        private const string SystemName = "LobbyTestSystem";
        private const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

        private static readonly Color PanelColor = new Color(0.18f, 0.21f, 0.27f, 1f);
        private static readonly Color BoxColor = new Color(0.08f, 0.09f, 0.12f, 1f);

        [MenuItem("Tools/Setup Auth & Lobby Test Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LobbySceneSetupTool] Play 모드를 종료한 뒤 실행해 주세요.");
                return;
            }

            // TMP Essentials(기본 셰이더/스타일)가 없으면 임포트 후 재실행 요청
            if (TMP_Settings.instance == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.LogWarning("[LobbySceneSetupTool] TMP Essential Resources를 임포트했습니다. 완료 후 메뉴를 다시 실행해 주세요.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();

            GameObject oldCanvas = GameObject.Find(CanvasName);
            GameObject oldSystem = GameObject.Find(SystemName);
            if (oldCanvas != null || oldSystem != null)
            {
                if (!EditorUtility.DisplayDialog("Auth & Lobby Test", "이미 생성된 테스트 UI가 있습니다. 삭제하고 다시 만들까요?", "다시 만들기", "취소"))
                    return;
                if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);
                if (oldSystem != null) Undo.DestroyObjectImmediate(oldSystem);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
                Debug.LogWarning("[LobbySceneSetupTool] 한글 폰트를 찾지 못했습니다. 먼저 Tools > Setup Auth UI in Current Scene 을 한 번 실행하면 " + KoreanFontPath + " 가 생성됩니다. 기본 폰트로는 한글이 깨집니다.");

            Canvas canvas = CreateCanvas();
            EnsureEventSystem();
            TMP_DefaultControls.Resources res = BuildResources();

            Image background = CreateImage("Background", canvas.transform, new Color(0.11f, 0.13f, 0.17f, 1f));
            Stretch(background.rectTransform);

            // ---------- 1) AuthPanel (상단) ----------
            Image authPanel = CreateImage("AuthPanel", canvas.transform, PanelColor);
            SetRect(authPanel.rectTransform, new Vector2(0, 600), new Vector2(1000, 640));

            TextMeshProUGUI authTitle = CreateLabel("Title", authPanel.transform, "회원가입 / 로그인", 44, font, res);
            authTitle.fontStyle = FontStyles.Bold;
            SetRect(authTitle.rectTransform, new Vector2(0, 275), new Vector2(940, 60));

            TMP_InputField usernameInput = CreateInput("UsernameInput", authPanel.transform, "아이디 (영문 소문자+숫자 4~12자)", font, res);
            SetRect((RectTransform)usernameInput.transform, new Vector2(0, 190), new Vector2(940, 76));

            TMP_InputField passwordInput = CreateInput("PasswordInput", authPanel.transform, "비밀번호 (영문+숫자+!@#$%^&* 8~20자)", font, res);
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            SetRect((RectTransform)passwordInput.transform, new Vector2(0, 100), new Vector2(940, 76));

            TMP_InputField nicknameInput = CreateInput("NicknameInput", authPanel.transform, "닉네임 (2~10자, 회원가입 시 필수)", font, res);
            SetRect((RectTransform)nicknameInput.transform, new Vector2(0, 10), new Vector2(940, 76));

            Button signupButton = CreateButton("SignUpButton", authPanel.transform, "SignUp", font, res);
            SetRect((RectTransform)signupButton.transform, new Vector2(-315, -85), new Vector2(300, 76));

            Button loginButton = CreateButton("LoginButton", authPanel.transform, "Login", font, res);
            SetRect((RectTransform)loginButton.transform, new Vector2(0, -85), new Vector2(300, 76));

            Button logoutButton = CreateButton("LogoutButton", authPanel.transform, "Logout", font, res);
            SetRect((RectTransform)logoutButton.transform, new Vector2(315, -85), new Vector2(300, 76));

            TextMeshProUGUI authStatus = CreateLabel("AuthStatusText", authPanel.transform, "로그인 필요", 32, font, res);
            authStatus.textWrappingMode = TextWrappingModes.Normal;
            SetRect(authStatus.rectTransform, new Vector2(0, -215), new Vector2(940, 150));

            // ---------- 2) LobbyPanel (중단) ----------
            Image lobbyPanel = CreateImage("LobbyPanel", canvas.transform, PanelColor);
            SetRect(lobbyPanel.rectTransform, new Vector2(0, -150), new Vector2(1000, 820));

            TextMeshProUGUI lobbyTitle = CreateLabel("Title", lobbyPanel.transform, "Lobby", 44, font, res);
            lobbyTitle.fontStyle = FontStyles.Bold;
            SetRect(lobbyTitle.rectTransform, new Vector2(0, 365), new Vector2(940, 60));

            // 행 1: 방 만들기
            TMP_InputField roomTitleInput = CreateInput("RoomTitleInput", lobbyPanel.transform, "Room Title", font, res);
            SetRect((RectTransform)roomTitleInput.transform, new Vector2(-260, 285), new Vector2(420, 76));

            TMP_InputField maxPlayersInput = CreateInput("MaxPlayersInput", lobbyPanel.transform, "인원", font, res);
            maxPlayersInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            maxPlayersInput.text = "6";
            SetRect((RectTransform)maxPlayersInput.transform, new Vector2(25, 285), new Vector2(130, 76));

            Button createRoomButton = CreateButton("CreateRoomButton", lobbyPanel.transform, "Create Room", font, res);
            SetRect((RectTransform)createRoomButton.transform, new Vector2(285, 285), new Vector2(370, 76));

            // 행 2: 방 번호로 입장 / 퇴장 / 참가자 조회
            TMP_InputField roomIdInput = CreateInput("RoomIdInput", lobbyPanel.transform, "Room ID", font, res);
            roomIdInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            SetRect((RectTransform)roomIdInput.transform, new Vector2(-345, 200), new Vector2(250, 76));

            Button joinRoomButton = CreateButton("JoinRoomButton", lobbyPanel.transform, "Join", font, res);
            SetRect((RectTransform)joinRoomButton.transform, new Vector2(-105, 200), new Vector2(210, 76));

            Button leaveRoomButton = CreateButton("LeaveRoomButton", lobbyPanel.transform, "Leave", font, res);
            SetRect((RectTransform)leaveRoomButton.transform, new Vector2(115, 200), new Vector2(210, 76));

            Button playersButton = CreateButton("PlayersButton", lobbyPanel.transform, "Players", font, res);
            SetRect((RectTransform)playersButton.transform, new Vector2(350, 200), new Vector2(240, 76));

            // 행 3: 목록 새로고침
            Button refreshRoomsButton = CreateButton("RefreshRoomsButton", lobbyPanel.transform, "Refresh Rooms", font, res);
            SetRect((RectTransform)refreshRoomsButton.transform, new Vector2(0, 115), new Vector2(940, 76));

            TextMeshProUGUI currentRoomText = CreateLabel("CurrentRoomText", lobbyPanel.transform, "현재 방: 없음", 34, font, res);
            currentRoomText.color = new Color(1f, 0.85f, 0.35f);
            SetRect(currentRoomText.rectTransform, new Vector2(0, 50), new Vector2(940, 50));

            ScrollRect roomScroll = CreateScrollText("RoomListScroll", lobbyPanel.transform, font, 30, out TextMeshProUGUI roomListText);
            SetRect((RectTransform)roomScroll.transform, new Vector2(0, -95), new Vector2(940, 290));

            ScrollRect playersScroll = CreateScrollText("PlayersScroll", lobbyPanel.transform, font, 30, out TextMeshProUGUI playersText);
            SetRect((RectTransform)playersScroll.transform, new Vector2(0, -325), new Vector2(940, 150));

            // ---------- 3) Status / Log (하단) ----------
            Image logPanel = CreateImage("LogPanel", canvas.transform, PanelColor);
            SetRect(logPanel.rectTransform, new Vector2(0, -760), new Vector2(1000, 360));

            ScrollRect logScroll = CreateScrollText("LogScroll", logPanel.transform, font, 26, out TextMeshProUGUI logText);
            SetRect((RectTransform)logScroll.transform, Vector2.zero, new Vector2(960, 320));

            // ---------- 컨트롤러 부착 + 바인딩 ----------
            GameObject system = new GameObject(SystemName);
            Undo.RegisterCreatedObjectUndo(system, "Create Lobby Test System");
            LobbyTestController controller = system.AddComponent<LobbyTestController>();

            SerializedObject so = new SerializedObject(controller);
            Bind(so, "authPanel", authPanel.gameObject);
            Bind(so, "usernameInput", usernameInput);
            Bind(so, "passwordInput", passwordInput);
            Bind(so, "nicknameInput", nicknameInput);
            Bind(so, "signupButton", signupButton);
            Bind(so, "loginButton", loginButton);
            Bind(so, "logoutButton", logoutButton);
            Bind(so, "authStatusText", authStatus);
            Bind(so, "lobbyPanel", lobbyPanel.gameObject);
            Bind(so, "roomTitleInput", roomTitleInput);
            Bind(so, "maxPlayersInput", maxPlayersInput);
            Bind(so, "createRoomButton", createRoomButton);
            Bind(so, "roomIdInput", roomIdInput);
            Bind(so, "joinRoomButton", joinRoomButton);
            Bind(so, "leaveRoomButton", leaveRoomButton);
            Bind(so, "playersButton", playersButton);
            Bind(so, "refreshRoomsButton", refreshRoomsButton);
            Bind(so, "currentRoomText", currentRoomText);
            Bind(so, "roomListText", roomListText);
            Bind(so, "playersText", playersText);
            Bind(so, "logText", logText);
            Bind(so, "logScroll", logScroll);
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(signupButton.onClick, controller.OnSignupClicked);
            UnityEventTools.AddPersistentListener(loginButton.onClick, controller.OnLoginClicked);
            UnityEventTools.AddPersistentListener(logoutButton.onClick, controller.OnLogoutClicked);
            UnityEventTools.AddPersistentListener(createRoomButton.onClick, controller.OnCreateRoomClicked);
            UnityEventTools.AddPersistentListener(joinRoomButton.onClick, controller.OnJoinRoomClicked);
            UnityEventTools.AddPersistentListener(leaveRoomButton.onClick, controller.OnLeaveRoomClicked);
            UnityEventTools.AddPersistentListener(playersButton.onClick, controller.OnPlayersClicked);
            UnityEventTools.AddPersistentListener(refreshRoomsButton.onClick, controller.OnRefreshRoomsClicked);

            // 로그인 전에는 로비 패널을 숨긴 상태로 저장 (런타임에서 로그인 성공 시 켜진다)
            lobbyPanel.gameObject.SetActive(false);

            Selection.activeGameObject = system;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[LobbySceneSetupTool] Auth & Lobby 테스트 UI 배치 완료. Ctrl+S로 씬을 저장하고, 서버(8080) 실행 후 Play(▶)하세요.");
        }

        private static void Bind(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[LobbySceneSetupTool] LobbyTestController에 '{field}' 필드가 없습니다.");
                return;
            }
            p.objectReferenceValue = value;
        }

        // ---------- 생성 헬퍼 ----------

        private static Canvas CreateCanvas()
        {
            GameObject go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Lobby Test Canvas");
            go.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 세로(Portrait)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 가로 기준으로 맞춤
            return canvas;
        }

        // 씬에 EventSystem이 있으면 재사용하고, 입력 모듈이 없으면 추가한다.
        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.GetComponent<EventSystem>();
            }

            if (es.GetComponent<BaseInputModule>() != null) return;
#if ENABLE_INPUT_SYSTEM
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static TMP_DefaultControls.Resources BuildResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = GetSprite("UI/Skin/UISprite.psd"),
                background = GetSprite("UI/Skin/Background.psd"),
                inputField = GetSprite("UI/Skin/InputFieldBackground.psd"),
                knob = GetSprite("UI/Skin/Knob.psd"),
                checkmark = GetSprite("UI/Skin/Checkmark.psd"),
                dropdown = GetSprite("UI/Skin/DropdownArrow.psd"),
                mask = GetSprite("UI/Skin/UIMask.psd"),
            };
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            Image img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float size,
            TMP_FontAsset font, TMP_DefaultControls.Resources res)
        {
            GameObject go = TMP_DefaultControls.CreateText(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            ApplyFont(tmp, font);
            return tmp;
        }

        private static TMP_InputField CreateInput(string name, Transform parent, string placeholder,
            TMP_FontAsset font, TMP_DefaultControls.Resources res)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TMP_InputField input = go.GetComponent<TMP_InputField>();

            TMP_Text ph = (TMP_Text)input.placeholder;
            ph.text = placeholder;
            ph.fontSize = 28;
            ph.fontStyle = FontStyles.Normal;
            input.textComponent.fontSize = 32;
            input.pointSize = 32;
            ApplyFont(ph, font);
            ApplyFont(input.textComponent, font);
            return input;
        }

        private static Button CreateButton(string name, Transform parent, string label,
            TMP_FontAsset font, TMP_DefaultControls.Resources res)
        {
            GameObject go = TMP_DefaultControls.CreateButton(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 32;
            ApplyFont(tmp, font);
            return go.GetComponent<Button>();
        }

        // 세로 스크롤 + 내용에 맞춰 늘어나는 TMP 텍스트. (배경 박스 / Viewport(RectMask2D) / Content 텍스트)
        private static ScrollRect CreateScrollText(string name, Transform parent, TMP_FontAsset font, float fontSize,
            out TextMeshProUGUI content)
        {
            Image root = CreateImage(name, parent, BoxColor);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(root.transform, false);
            viewportGo.layer = root.gameObject.layer;
            RectTransform viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewport.offsetMin = new Vector2(10, 10);
            viewport.offsetMax = new Vector2(-10, -10);

            GameObject textGo = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            textGo.transform.SetParent(viewport, false);
            textGo.layer = root.gameObject.layer;
            RectTransform rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            content = textGo.GetComponent<TextMeshProUGUI>();
            content.fontSize = fontSize;
            content.color = Color.white;
            content.alignment = TextAlignmentOptions.TopLeft;
            content.textWrappingMode = TextWrappingModes.Normal;
            content.raycastTarget = false;
            content.text = "";
            ApplyFont(content, font);

            textGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = rt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        private static void ApplyFont(TMP_Text text, TMP_FontAsset font)
        {
            if (font != null) text.font = font;
        }

        private static void SetRect(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite GetSprite(string builtinPath)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(builtinPath);
        }
    }
}
