using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WhoisntCitizen.Lobby;

namespace WhoisntCitizen.Title.Editor
{
    // 로그인 플로우에 필요한 UI 오브젝트를 만들어 주는 에디터 도구.
    //   - Title 씬: Tools > Bind Title Scene 실행 시 EnsureTitleUi가 자동 호출된다. (게스트/로그인 메인, 로그인 수단 선택 팝업, 계정 패널 뒤로가기)
    //   - Lobby 씬: Tools > Setup Lobby Account Link UI (설정 버튼 + 설정 팝업의 구글/카카오 연동 버튼)
    // 기존 버튼/텍스트를 복제해서 만들기 때문에 폰트(한글)와 스타일이 기존 UI와 같다.
    // 이미 있는 오브젝트는 다시 만들지 않으므로 여러 번 실행해도 안전하다.
    public static class AuthFlowUiBuilder
    {
        // ------------------------------------------------------------------
        // Title
        // ------------------------------------------------------------------

        public const string MainPanel = "Main_Panel";
        public const string LoginSelectPanel = "LoginSelect_Panel";

        /// <summary>Title 씬 Canvas에 새 로그인 플로우 UI가 없으면 만든다. 템플릿(Login/Button_Login)이 없으면 false.</summary>
        public static bool EnsureTitleUi(Transform canvas)
        {
            Transform buttonTemplate = canvas.Find("Login/Buttons/Button_Login");
            Transform textTemplate = canvas.Find("Login/ErrorMessage");
            if (buttonTemplate == null || textTemplate == null)
            {
                Debug.LogError("[AuthFlowUiBuilder] 복제할 템플릿(Login/Button_Login, Login/ErrorMessage)을 찾지 못했습니다.");
                return false;
            }

            if (canvas.Find(MainPanel) == null)
            {
                Transform main = CreatePanel(canvas, MainPanel, dim: false);
                CreateButton(main, "Button_Guest", "게스트 로그인", buttonTemplate, new Vector2(0, -60));
                CreateButton(main, "Button_OpenLogin", "로그인", buttonTemplate, new Vector2(0, -150));
                CreateMessage(main, "Message", textTemplate, new Vector2(0, -230));
            }

            if (canvas.Find(LoginSelectPanel) == null)
            {
                Transform select = CreatePanel(canvas, LoginSelectPanel, dim: true);
                CreateButton(select, "Button_Google", "구글 로그인", buttonTemplate, new Vector2(0, 90));
                CreateButton(select, "Button_Kakao", "카카오 로그인", buttonTemplate, new Vector2(0, 0));
                CreateButton(select, "Button_Account", "계정 로그인", buttonTemplate, new Vector2(0, -90));
                CreateButton(select, "Button_Close", "닫기", buttonTemplate, new Vector2(0, -180));
                CreateMessage(select, "Message", textTemplate, new Vector2(0, -260));
            }

            Transform login = canvas.Find("Login");
            if (login.Find("Button_Back") == null)
            {
                // Login 그룹이 레이아웃 그룹이어도 자리를 차지하지 않도록 하고, 그룹 바로 아래에 둔다.
                RectTransform back = CreateButton(login, "Button_Back", "뒤로", buttonTemplate, new Vector2(0, -20));
                back.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                back.anchorMin = back.anchorMax = new Vector2(0.5f, 0f);
                back.pivot = new Vector2(0.5f, 1f);
                back.anchoredPosition = new Vector2(0, -20);
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Lobby
        // ------------------------------------------------------------------

        [MenuItem("Tools/Setup Lobby Account Link UI")]
        public static void SetupLobby()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AuthFlowUiBuilder] Play 모드를 종료한 뒤 실행해 주세요.");
                return;
            }

            LobbyUIController lobby = Object.FindFirstObjectByType<LobbyUIController>();
            GameObject canvasGo = GameObject.Find("Canvas");
            if (lobby == null || canvasGo == null)
            {
                Debug.LogError("[AuthFlowUiBuilder] Lobby 씬을 열고 실행해 주세요. (Canvas / LobbyUIController를 찾지 못했습니다)");
                return;
            }
            Transform canvas = canvasGo.transform;

            Button refresh = FindByName<Button>(canvas, "Button_Refresh");
            // StatusMessageText가 여러 개(팝업마다 하나씩)라서, Canvas 바로 아래 것을 우선 쓰고 없으면 아무거나 쓴다.
            Transform statusTemplate = canvas.Find("StatusMessageText");
            if (statusTemplate == null)
            {
                var view = FindByName<WhoisntCitizen.Common.StatusMessageView>(canvas, "StatusMessageText");
                if (view != null) statusTemplate = view.transform;
            }
            if (refresh == null || statusTemplate == null)
            {
                Debug.LogError("[AuthFlowUiBuilder] 복제할 템플릿(Button_Refresh, Canvas/StatusMessageText)을 찾지 못했습니다.");
                return;
            }

            // 설정 버튼: 새로고침 버튼 옆(같은 부모)에 복제
            Button settingButton = FindByName<Button>(canvas, "Button_Setting");
            if (settingButton == null)
            {
                GameObject go = Object.Instantiate(refresh.gameObject, refresh.transform.parent, false);
                go.name = "Button_Setting";
                Undo.RegisterCreatedObjectUndo(go, "Create Setting Button");
                settingButton = go.GetComponent<Button>();
                ResetListeners(settingButton.onClick);
                SetButtonLabel(go.transform, "설정");
                RectTransform rt = (RectTransform)go.transform;
                if (refresh.transform.parent.GetComponent<LayoutGroup>() == null)
                    rt.anchoredPosition += new Vector2(0, -(rt.rect.height + 10f)); // 레이아웃이 없으면 새로고침 버튼 아래로
            }

            // 설정 팝업
            Transform popupParent = canvas.Find("Popup") != null ? canvas.Find("Popup") : canvas;
            Transform popup = popupParent.Find("Popup_Setting");
            AccountLinkPopup popupComp;
            if (popup == null)
            {
                popup = CreatePanel(popupParent, "Popup_Setting", dim: true);
                Transform box = CreateBox(popup, "Box", new Vector2(640, 560));

                RectTransform title = CreateMessage(box, "Title", statusTemplate, new Vector2(0, 220));
                TMP_Text titleText = title.GetComponent<TMP_Text>();
                titleText.text = "설정 - 계정 연동";
                titleText.color = Color.white;
                Object.DestroyImmediate(title.GetComponent<WhoisntCitizen.Common.StatusMessageView>());

                RectTransform info = CreateMessage(box, "AccountInfo", statusTemplate, new Vector2(0, 140));
                info.GetComponent<TMP_Text>().color = Color.white;
                Object.DestroyImmediate(info.GetComponent<WhoisntCitizen.Common.StatusMessageView>());

                CreateButton(box, "Button_LinkGoogle", "구글 연동", refresh.transform, new Vector2(0, 50));
                CreateButton(box, "Button_LinkKakao", "카카오 연동", refresh.transform, new Vector2(0, -40));
                CreateMessage(box, "StatusMessageText", statusTemplate, new Vector2(0, -130));
                CreateButton(box, "Button_Close", "닫기", refresh.transform, new Vector2(0, -220));
            }
            Transform b = popup.Find("Box");
            popupComp = popup.GetComponent<AccountLinkPopup>();
            if (popupComp == null) popupComp = popup.gameObject.AddComponent<AccountLinkPopup>();

            var pso = new SerializedObject(popupComp);
            SetRef(pso, "accountInfoText", b.Find("AccountInfo").GetComponent<TMP_Text>());
            SetRef(pso, "googleLinkButton", b.Find("Button_LinkGoogle").GetComponent<Button>());
            SetRef(pso, "kakaoLinkButton", b.Find("Button_LinkKakao").GetComponent<Button>());
            SetRef(pso, "closeButton", b.Find("Button_Close").GetComponent<Button>());
            SetRef(pso, "statusMessage", b.Find("StatusMessageText").GetComponent<WhoisntCitizen.Common.StatusMessageView>());
            pso.ApplyModifiedPropertiesWithoutUndo();

            var lso = new SerializedObject(lobby);
            SetRef(lso, "settingButton", settingButton);
            SetRef(lso, "accountLinkPopup", popupComp);
            lso.ApplyModifiedPropertiesWithoutUndo();

            // 인스펙터에서 보기 편하도록 팝업은 꺼 둔다. (런타임 Start에서도 닫는다)
            popup.gameObject.SetActive(false);

            Selection.activeGameObject = popup.gameObject;
            EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
            Debug.Log("[AuthFlowUiBuilder] Lobby 설정/계정 연동 UI 생성 및 연결 완료. Ctrl+S로 씬을 저장하세요.");
        }

        // ------------------------------------------------------------------
        // UI 생성 도우미
        // ------------------------------------------------------------------

        // 부모를 가득 채우는 패널. dim이면 반투명 검정 배경(뒤쪽 클릭 차단)을 깐다.
        private static Transform CreatePanel(Transform parent, string name, bool dim)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (dim)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.6f);
            }
            return go.transform;
        }

        // 가운데 고정 크기의 상자(팝업 본체)
        private static Transform CreateBox(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
            return go.transform;
        }

        // 템플릿 버튼을 복제한다. 복제본의 기존 OnClick(영구 리스너)은 지운다.
        private static RectTransform CreateButton(Transform parent, string name, string label, Transform template, Vector2 pos)
        {
            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);

            var button = go.GetComponent<Button>();
            if (button != null) ResetListeners(button.onClick);
            foreach (var le in go.GetComponents<LayoutElement>()) Object.DestroyImmediate(le);
            SetButtonLabel(go.transform, label);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            return rt;
        }

        // 템플릿 텍스트를 복제해서 가운데 정렬 메시지 칸으로 쓴다.
        private static RectTransform CreateMessage(Transform parent, string name, Transform template, Vector2 pos)
        {
            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);

            var text = go.GetComponent<TMP_Text>();
            text.text = "";
            text.alignment = TextAlignmentOptions.Center;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            if (rt.sizeDelta.x < 400f) rt.sizeDelta = new Vector2(560f, Mathf.Max(rt.sizeDelta.y, 50f));
            return rt;
        }

        private static void SetButtonLabel(Transform button, string label)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
        }

        private static T FindByName<T>(Transform root, string name) where T : Component
        {
            foreach (T c in root.GetComponentsInChildren<T>(true))
                if (c.name == name) return c;
            return null;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[AuthFlowUiBuilder] '{so.targetObject.GetType().Name}'에 '{field}' 필드가 없습니다.");
                return;
            }
            p.objectReferenceValue = value;
        }

        private static void ResetListeners(UnityEvent evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt, i);
        }
    }
}
