using UnityEditor.SceneManagement;
using WhoisntCitizen.Chat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ChatPrac의 ChatUIBuilder를 옮긴 것입니다.
///  - Tools > Chat > Build Chat UI    : 현재 씬에 채팅 UI를 생성합니다. (기존 ChatCanvas는 교체)
///  - Tools > Chat > Create ChatScene : Assets/01_Scenes/ChatScene.unity를 새로 만들고 채팅 UI를 넣어 저장합니다.
/// 한글 표시를 위해 Assets/Fonts/NanumGothic SDF(Dynamic, OFL 라이선스)를 사용하고,
/// 없으면 AuthUISetupTool이 만드는 Assets/Fonts/MalgunGothic SDF를 사용합니다.
/// </summary>
public static class ChatUIBuilder
{
    const string FontPath = "Assets/Fonts/NanumGothic SDF.asset";
    const string FallbackFontPath = "Assets/Fonts/MalgunGothic SDF.asset";
    const string ScenePath = "Assets/01_Scenes/ChatScene.unity";

    // 채팅 박스: 화면 중앙, 가로·세로 각각 화면의 약 83.7%(√0.7) → 전체 면적의 70%
    // (기준 해상도 1920x1080에서 약 1606 x 904)
    const float AreaRatio = 0.7f;
    // 박스·글자 크기 배율 (예전 400 x 251.5 박스 → 새 박스 높이 기준 약 3.6배)
    const float UiScale = 3.6f;

    const float FontSize = 15f * UiScale;           // 54
    const float InputHeight = FontSize * 1.5f;      // 81
    const float Spacing = 4f * UiScale;             // 14.4
    const float SendButtonWidth = 48f * UiScale;    // 172.8
    const float Pad = 6f * UiScale;                 // 21.6 (좌우 여백)
    const float PadV = 4f * UiScale;                // 14.4 (위아래 여백)
    const float ScrollSensitivity = 20f * UiScale;  // 72

    [MenuItem("Tools/Chat/Create ChatScene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ChatUIBuilder] Play 모드를 종료한 뒤 실행해 주세요.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("ChatScene", ScenePath + " 가 이미 있습니다. 새로 만들까요?", "새로 만들기", "취소"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        Build();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[ChatUIBuilder] " + ScenePath + " 생성 완료");
    }

    static void CreateCamera()
    {
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
    }

    [MenuItem("Tools/Chat/Build Chat UI")]
    public static void Build()
    {
        var old = GameObject.Find("ChatCanvas");
        if (old != null) Undo.DestroyObjectImmediate(old);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if (font == null)
        {
            Debug.LogWarning("[ChatUIBuilder] 한글 폰트(" + FontPath + ")가 없어 TMP 기본 폰트를 씁니다. (한글이 □로 보일 수 있음)");
            font = TMP_Settings.defaultFontAsset;
        }

        // Canvas
        var canvasGo = new GameObject("ChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Chat UI");
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel (두 박스를 세로로 배치, 가로 폭 동일) - 화면 중앙, 면적 70%
        var panel = CreateUI("ChatPanel", canvasGo.transform);
        float side = Mathf.Sqrt(AreaRatio);             // 0.8367
        float margin = (1f - side) * 0.5f;              // 0.0817
        panel.anchorMin = new Vector2(margin, margin);
        panel.anchorMax = new Vector2(1f - margin, 1f - margin);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = Vector2.zero;
        var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = Spacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ---------- 조회 박스 ----------
        var view = CreateUI("MessageView", panel);
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var viewLe = view.gameObject.AddComponent<LayoutElement>();
        viewLe.minHeight = InputHeight;
        viewLe.flexibleHeight = 1f;   // 입력 박스를 뺀 나머지 높이를 모두 사용
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ScrollSensitivity;

        var viewport = CreateUI("Viewport", view);
        Stretch(viewport, Pad, PadV, Pad, PadV);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateUI("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var msgText = content.gameObject.AddComponent<TextMeshProUGUI>();
        msgText.font = font;
        msgText.fontSize = FontSize;
        msgText.textWrappingMode = TextWrappingModes.Normal;   // 가로 초과 시 자동 줄바꿈
        msgText.overflowMode = TextOverflowModes.Overflow;
        msgText.alignment = TextAlignmentOptions.TopLeft;
        msgText.color = Color.white;
        msgText.richText = true;
        msgText.raycastTarget = false;
        msgText.text = "";
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        // ---------- 입력 박스 ----------
        var inputBox = CreateUI("InputBox", panel);
        var inputBg = inputBox.gameObject.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        var inputLe = inputBox.gameObject.AddComponent<LayoutElement>();
        inputLe.preferredHeight = InputHeight;
        inputLe.minHeight = InputHeight;

        var textArea = CreateUI("TextArea", inputBox);
        Stretch(textArea, Pad, 0, SendButtonWidth + Spacing, 0);
        textArea.gameObject.AddComponent<RectMask2D>();

        var placeholder = CreateUI("Placeholder", textArea);
        Stretch(placeholder, 0, 0, 0, 0);
        var ph = placeholder.gameObject.AddComponent<TextMeshProUGUI>();
        ph.font = font;
        ph.fontSize = FontSize;
        ph.fontStyle = FontStyles.Italic;
        ph.color = new Color(1, 1, 1, 0.35f);
        ph.text = "메시지 입력 (Enter 전송 / Shift+Enter 줄바꿈)";
        ph.textWrappingMode = TextWrappingModes.Normal;
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        ph.raycastTarget = false;

        var inputTextRt = CreateUI("Text", textArea);
        Stretch(inputTextRt, 0, 0, 0, 0);
        var inputText = inputTextRt.gameObject.AddComponent<TextMeshProUGUI>();
        inputText.font = font;
        inputText.fontSize = FontSize;
        inputText.color = Color.white;
        inputText.textWrappingMode = TextWrappingModes.Normal;  // 가로 초과 시 자동 줄바꿈
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        inputText.richText = false;

        var input = inputBox.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = inputBg;
        input.textViewport = textArea;
        input.textComponent = inputText;
        input.placeholder = ph;
        input.fontAsset = font;
        input.pointSize = FontSize;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.richText = false;
        input.caretColor = Color.white;
        input.customCaretColor = true;
        input.onFocusSelectAll = false;
        input.restoreOriginalTextOnEscape = false;
        input.scrollSensitivity = ScrollSensitivity;
        input.caretWidth = 3;

        // 전송 버튼 (입력 박스 우측 끝단)
        var btnRt = CreateUI("SendButton", inputBox);
        btnRt.anchorMin = new Vector2(1, 0);
        btnRt.anchorMax = new Vector2(1, 1);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = Vector2.zero;
        btnRt.sizeDelta = new Vector2(SendButtonWidth, 0);
        var btnImg = btnRt.gameObject.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.5f, 0.9f, 1f);
        var btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var btnLabelRt = CreateUI("Label", btnRt);
        Stretch(btnLabelRt, 0, 0, 0, 0);
        var btnLabel = btnLabelRt.gameObject.AddComponent<TextMeshProUGUI>();
        btnLabel.font = font;
        btnLabel.fontSize = FontSize;
        btnLabel.text = "전송";
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = Color.white;
        btnLabel.raycastTarget = false;

        // ---------- 컨트롤러 ----------
        var api = panel.gameObject.AddComponent<ChatApiClient>();
        var ctrl = panel.gameObject.AddComponent<ChatUIController>();
        ctrl.api = api;
        ctrl.inputField = input;
        ctrl.sendButton = btn;
        ctrl.messagesText = msgText;
        ctrl.messagesScroll = scroll;
        ctrl.inputBoxLayout = inputLe;
        ctrl.baseInputHeight = InputHeight;

        EnsureEventSystem();

        Selection.activeGameObject = panel.gameObject;
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
    }

    static RectTransform CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}
