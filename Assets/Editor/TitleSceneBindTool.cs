using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.Title;

namespace WhoisntCitizen.Title.Editor
{
    // Tools > Bind Title Scene : 열려 있는 Title 씬의 UI 오브젝트를 하이어라키 경로로 찾아
    // TitleController에 연결하고, 버튼 OnClick을 연결한 뒤 씬들을 Build Settings에 등록한다.
    public static class TitleSceneBindTool
    {
        private const string SystemName = "TitleSystem";

        // Canvas 아래 경로. (Regi_Panel 안의 입력칸 이름이 모두 Regi_ID_Input이라 부모 이름까지 포함해 구분한다)
        private const string LoginId = "Login/ID/ID_Input";
        private const string LoginPassword = "Login/Password/Password_Input";
        private const string LoginButton = "Login/Button_Login";
        private const string OpenRegisterButton = "Login/Button_Regi";
        private const string LoginMessage = "Login/ErrorMessage";
        private const string RegisterPanel = "Regi_Panel";
        private const string RegisterId = "Regi_Panel/Regi_ID/Regi_ID_Input";
        private const string RegisterPassword = "Regi_Panel/Regi_Password/Regi_ID_Input";
        private const string RegisterPasswordConfirm = "Regi_Panel/Regi_Password_re/Regi_ID_Input";
        private const string RegisterNickname = "Regi_Panel/Regi_Nickname/Regi_ID_Input";
        private const string RegisterButton = "Regi_Panel/Regi_button";
        private const string RegisterMessage = "Regi_Panel/Regi_ErrorMessage";

        private static readonly string[] BuildScenes =
        {
            "Assets/01_Scenes/Title.unity",
            "Assets/01_Scenes/Lobby.unity",
            "Assets/01_Scenes/Game.unity",
        };

        [MenuItem("Tools/Bind Title Scene")]
        public static void Bind()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TitleSceneBindTool] Play 모드를 종료한 뒤 실행해 주세요.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[TitleSceneBindTool] 활성 씬에 'Canvas'가 없습니다. Title 씬을 열고 다시 실행해 주세요.");
                return;
            }
            Transform canvas = canvasGo.transform;

            // 경로로 모든 대상을 먼저 찾고, 하나라도 없으면 아무것도 바꾸지 않는다.
            var missing = new List<string>();
            TMP_InputField loginId = Find<TMP_InputField>(canvas, LoginId, missing);
            TMP_InputField loginPassword = Find<TMP_InputField>(canvas, LoginPassword, missing);
            Button loginButton = Find<Button>(canvas, LoginButton, missing);
            Button openRegisterButton = Find<Button>(canvas, OpenRegisterButton, missing);
            TextMeshProUGUI loginMessage = Find<TextMeshProUGUI>(canvas, LoginMessage, missing);
            Transform registerPanel = canvas.Find(RegisterPanel);
            if (registerPanel == null) missing.Add(RegisterPanel);
            TMP_InputField registerId = Find<TMP_InputField>(canvas, RegisterId, missing);
            TMP_InputField registerPassword = Find<TMP_InputField>(canvas, RegisterPassword, missing);
            TMP_InputField registerPasswordConfirm = Find<TMP_InputField>(canvas, RegisterPasswordConfirm, missing);
            TMP_InputField registerNickname = Find<TMP_InputField>(canvas, RegisterNickname, missing);
            Button registerButton = Find<Button>(canvas, RegisterButton, missing);
            TextMeshProUGUI registerMessage = Find<TextMeshProUGUI>(canvas, RegisterMessage, missing);
            Button registerCloseButton = FindCloseButton(registerPanel, registerButton, missing);

            if (missing.Count > 0)
            {
                Debug.LogError("[TitleSceneBindTool] 다음 오브젝트(또는 컴포넌트)를 찾지 못했습니다. 이름/계층을 바꿨다면 이 툴의 경로 상수를 맞춰 주세요:\n - "
                               + string.Join("\n - ", missing));
                return;
            }

            // 이미 TitleSystem이 있으면 재사용한다.
            GameObject system = GameObject.Find(SystemName);
            if (system == null)
            {
                system = new GameObject(SystemName);
                Undo.RegisterCreatedObjectUndo(system, "Create Title System");
            }
            TitleController controller = system.GetComponent<TitleController>();
            if (controller == null) controller = system.AddComponent<TitleController>();

            SerializedObject so = new SerializedObject(controller);
            SetRef(so, "loginIdInput", loginId);
            SetRef(so, "loginPasswordInput", loginPassword);
            SetRef(so, "loginButton", loginButton);
            SetRef(so, "openRegisterButton", openRegisterButton);
            SetRef(so, "loginMessageText", loginMessage);
            SetRef(so, "registerPanel", registerPanel.gameObject);
            SetRef(so, "registerIdInput", registerId);
            SetRef(so, "registerPasswordInput", registerPassword);
            SetRef(so, "registerPasswordConfirmInput", registerPasswordConfirm);
            SetRef(so, "registerNicknameInput", registerNickname);
            SetRef(so, "registerButton", registerButton);
            SetRef(so, "registerCloseButton", registerCloseButton);
            SetRef(so, "registerMessageText", registerMessage);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 다시 실행해도 리스너가 중복되지 않도록 기존 영구 리스너를 지우고 연결한다.
            ResetListeners(loginButton.onClick);
            ResetListeners(openRegisterButton.onClick);
            ResetListeners(registerButton.onClick);
            ResetListeners(registerCloseButton.onClick);
            UnityEventTools.AddPersistentListener(loginButton.onClick, controller.OnLoginClicked);
            UnityEventTools.AddPersistentListener(openRegisterButton.onClick, controller.OnOpenRegisterClicked);
            UnityEventTools.AddPersistentListener(registerButton.onClick, controller.OnRegisterClicked);
            UnityEventTools.AddPersistentListener(registerCloseButton.onClick, controller.OnCloseRegisterClicked);

            // 에디터에서 작업하기 편하도록 패널 상태는 건드리지 않는다. (런타임 Start에서 회원가입 패널을 닫는다)
            Selection.activeGameObject = system;
            EditorSceneManager.MarkSceneDirty(scene);

            RegisterBuildScenes();
            Debug.Log("[TitleSceneBindTool] Title 씬 바인딩 완료. Ctrl+S로 씬을 저장하세요.");
        }

        // Regi_Panel 안에서 가입 버튼이 아닌 Button 중 X(닫기) 버튼을 찾는다. 이름은 자유(X, Close 등)이며, 후보가 하나뿐이면 그것을 쓴다.
        private static Button FindCloseButton(Transform panel, Button submit, List<string> missing)
        {
            if (panel == null || submit == null) return null;

            List<Button> candidates = panel.GetComponentsInChildren<Button>(true).Where(b => b != submit).ToList();
            if (candidates.Count == 1) return candidates[0];

            Button byName = candidates.FirstOrDefault(b =>
            {
                string n = b.name.ToLowerInvariant();
                return n == "x" || n.Contains("close") || n.Contains("exit") || n.EndsWith("_x") || n.StartsWith("x_");
            });
            if (byName != null) return byName;

            missing.Add(candidates.Count == 0
                ? "Regi_Panel 안의 X 버튼 (Button 컴포넌트가 있는 오브젝트가 없습니다. 씬을 저장했는지 확인하세요)"
                : "Regi_Panel 안의 X 버튼 후보가 여러 개입니다: " + string.Join(", ", candidates.Select(b => b.name)) + " (X 버튼 이름에 'X' 또는 'Close'를 넣어 주세요)");
            return null;
        }

        private static T Find<T>(Transform root, string path, List<string> missing) where T : Component
        {
            Transform t = root.Find(path);
            T comp = t != null ? t.GetComponent<T>() : null;
            if (comp == null) missing.Add($"{path} ({typeof(T).Name})");
            return comp;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[TitleSceneBindTool] TitleController에 '{field}' 필드가 없습니다.");
                return;
            }
            p.objectReferenceValue = value;
        }

        private static void ResetListeners(UnityEngine.Events.UnityEvent evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt, i);
        }

        // Title(0번) -> Lobby -> Game 순으로 Build Settings에 등록한다. 기존 등록 씬은 유지한다.
        private static void RegisterBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            for (int i = BuildScenes.Length - 1; i >= 0; i--)
            {
                string path = BuildScenes[i];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning($"[TitleSceneBindTool] 씬을 찾지 못했습니다: {path}");
                    continue;
                }
                scenes.RemoveAll(s => s.path == path);
            }

            var ordered = new List<EditorBuildSettingsScene>();
            foreach (string path in BuildScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    ordered.Add(new EditorBuildSettingsScene(path, true));
            }
            ordered.AddRange(scenes);
            EditorBuildSettings.scenes = ordered.ToArray();
            Debug.Log("[TitleSceneBindTool] Build Settings: " + string.Join(" > ", ordered.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
        }
    }
}
