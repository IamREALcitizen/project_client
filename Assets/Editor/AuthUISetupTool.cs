using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Setup Auth UI in Current Scene : 현재 열려 있는 씬에 로그인/회원가입 UI를 자동 배치하고 스크립트를 연결한다.
public static class AuthUISetupTool
{
    private const string CanvasName = "AuthCanvas";
    private const string SystemName = "AuthSystem";
    private const string FontDir = "Assets/Fonts";
    private const string SystemKoreanFont = @"C:\Windows\Fonts\malgun.ttf";

    [MenuItem("Tools/Setup Auth UI in Current Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AuthUISetupTool] Play 모드를 종료한 뒤 실행해 주세요.");
            return;
        }

        // TMP Essentials(기본 폰트/셰이더)가 없으면 임포트 후 재실행 요청
        if (TMP_Settings.instance == null)
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.LogWarning("[AuthUISetupTool] TMP Essential Resources를 임포트했습니다. 완료 후 메뉴를 다시 실행해 주세요.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        // 이미 생성된 UI가 있으면 교체 여부 확인
        GameObject oldCanvas = GameObject.Find(CanvasName);
        GameObject oldSystem = GameObject.Find(SystemName);
        if (oldCanvas != null || oldSystem != null)
        {
            if (!EditorUtility.DisplayDialog("Auth UI", "이미 생성된 Auth UI가 있습니다. 삭제하고 다시 만들까요?", "다시 만들기", "취소"))
                return;
            if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);
            if (oldSystem != null) Undo.DestroyObjectImmediate(oldSystem);
        }

        TMP_FontAsset font = GetOrCreateKoreanFont();

        Canvas canvas = CreateCanvas();
        EnsureEventSystem();

        // 배경 + 중앙 패널
        Image background = CreateImage("Background", canvas.transform, new Color(0.11f, 0.13f, 0.17f, 1f));
        Stretch(background.rectTransform);
        Image panel = CreateImage("Panel", canvas.transform, new Color(0.18f, 0.21f, 0.27f, 1f));
        SetRect(panel.rectTransform, Vector2.zero, new Vector2(640, 620));

        TMP_DefaultControls.Resources res = BuildResources();

        TextMeshProUGUI title = CreateLabel("Title", panel.transform, "회원가입 / 로그인", 44, font, res);
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, new Vector2(0, 240), new Vector2(560, 70));

        TMP_InputField usernameInput = CreateInput("UsernameInput", panel.transform, "아이디 (영문 소문자+숫자 4~12자)", font, res);
        SetRect((RectTransform)usernameInput.transform, new Vector2(0, 130), new Vector2(560, 70));

        TMP_InputField passwordInput = CreateInput("PasswordInput", panel.transform, "비밀번호 (영문+숫자+!@#$%^&* 8~20자)", font, res);
        passwordInput.contentType = TMP_InputField.ContentType.Password;
        SetRect((RectTransform)passwordInput.transform, new Vector2(0, 40), new Vector2(560, 70));

        Button signupButton = CreateButton("SignUpButton", panel.transform, "회원가입", font, res);
        SetRect((RectTransform)signupButton.transform, new Vector2(-140, -70), new Vector2(260, 70));

        Button loginButton = CreateButton("LoginButton", panel.transform, "로그인", font, res);
        SetRect((RectTransform)loginButton.transform, new Vector2(140, -70), new Vector2(260, 70));

        TextMeshProUGUI status = CreateLabel("StatusText", panel.transform, "", 26, font, res);
        status.textWrappingMode = TextWrappingModes.Normal;
        SetRect(status.rectTransform, new Vector2(0, -200), new Vector2(560, 150));

        // 컨트롤러 부착 + [SerializeField] 바인딩
        GameObject auth = new GameObject(SystemName);
        Undo.RegisterCreatedObjectUndo(auth, "Create Auth System");
        AuthNetworkManager network = auth.AddComponent<AuthNetworkManager>();
        AuthUIController controller = auth.AddComponent<AuthUIController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("network").objectReferenceValue = network;
        so.FindProperty("usernameInput").objectReferenceValue = usernameInput;
        so.FindProperty("passwordInput").objectReferenceValue = passwordInput;
        so.FindProperty("signupButton").objectReferenceValue = signupButton;
        so.FindProperty("loginButton").objectReferenceValue = loginButton;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();

        // onClick 영구 리스너 연결
        UnityEventTools.AddPersistentListener(signupButton.onClick, controller.OnSignupClicked);
        UnityEventTools.AddPersistentListener(loginButton.onClick, controller.OnLoginClicked);

        Selection.activeGameObject = auth;
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[AuthUISetupTool] Auth UI 배치 완료. Ctrl+S로 씬을 저장하고, Spring Boot 서버 실행 후 Play(▶)하세요.");
    }

    // ---------- 생성 헬퍼 ----------

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create Auth Canvas");
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
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
        ph.fontSize = 24;
        ph.fontStyle = FontStyles.Normal;
        input.textComponent.fontSize = 28;
        input.pointSize = 28;
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
        tmp.fontSize = 30;
        ApplyFont(tmp, font);
        return go.GetComponent<Button>();
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

    // ---------- 한글 폰트 ----------

    // 기본 TMP 폰트는 한글이 없으므로 Windows 맑은 고딕으로 다이나믹 폰트 에셋을 만든다.
    private static TMP_FontAsset GetOrCreateKoreanFont()
    {
        string assetPath = $"{FontDir}/MalgunGothic SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        if (!File.Exists(SystemKoreanFont))
        {
            Debug.LogWarning("[AuthUISetupTool] 한글 폰트(malgun.ttf)를 찾지 못해 기본 폰트를 사용합니다. 한글이 깨질 수 있습니다.");
            return null;
        }

        Directory.CreateDirectory(FontDir);
        string ttfPath = $"{FontDir}/malgun.ttf";
        if (!File.Exists(ttfPath))
        {
            File.Copy(SystemKoreanFont, ttfPath);
            AssetDatabase.ImportAsset(ttfPath);
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (source == null) return null;

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.name = "MalgunGothic SDF";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        foreach (Texture2D tex in fa.atlasTextures)
            AssetDatabase.AddObjectToAsset(tex, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}
