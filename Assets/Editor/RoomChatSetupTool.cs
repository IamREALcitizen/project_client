using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.Chat;
using WhoisntCitizen.GameUI;

namespace WhoisntCitizen.EditorTools
{
    // Tools > Chat > Room 채팅 구성
    // 열려 있는 Room 씬(대기실)에 GameScene과 같은 서버 채팅을 붙인다.
    //   Canvas
    //   ├─ PlayerList      (기존) 아래쪽을 채팅 영역만큼 올린다 (처음 구성할 때만)
    //   ├─ Buttons         (기존) 채팅 영역 위로 올린다 (처음 구성할 때만)
    //   ├─ RoomChat        (새로) 어두운 배경 + ChatLogPanel(프리팹, 줄 프리팹은 Room 크기용 RoomChatLogLine)
    //   └─ ChatInputBar    (새로) GameBottomSheet의 TabBar를 복제해 [+] 버튼을 뺀 것: [채팅 입력] [전송]
    //   GameChat           (새로) ChatApiClient + GameChatController (chatLog·inputBar 연결)
    // 크기는 GameScene(1080 기준)의 TabBar를 Room 캔버스(600 기준) 비율로 줄여 쓴다.
    // 여러 번 실행해도 된다. 이미 있는 오브젝트는 다시 만들지 않고 연결만 다시 한다. Ctrl+Z로 되돌릴 수 있고 저장은 직접 한다.
    public static class RoomChatSetupTool
    {
        private const string RoomScenePath = "Assets/01_Scenes/Room.unity";
        private const string BottomSheetPrefabPath = "Assets/03_Prefabs/GameUI/GameBottomSheet.prefab";
        private const string ChatLogPanelPrefabPath = "Assets/03_Prefabs/GameUI/ChatLogPanel.prefab";
        private const string GameLinePrefabPath = "Assets/03_Prefabs/GameUI/ChatLogLine.prefab";
        private const string RoomLinePrefabPath = "Assets/03_Prefabs/Room/RoomChatLogLine.prefab";

        // Room 캔버스 기준 크기 (GameScene 1080 → Room 600, 약 0.56배)
        private const float Scale = 600f / 1080f;
        private const float BarHeight = 140f * Scale;          // 78
        private const float Margin = 20f * Scale;              // 11
        private const float SendWidth = 200f * Scale;          // 111
        private const float SendHeight = 104f * Scale;         // 58
        private const float InputFontSize = 34f * Scale;       // 19
        private const float PlaceholderFontSize = 32f * Scale; // 18
        private const float SendFontSize = 38f * Scale;        // 21
        private const float LineFontSize = 20f;
        private const float ChatHeight = 230f;
        private const float Gap = 8f;
        private static readonly Color ChatBackground = new Color32(0x1B, 0x1E, 0x26, 0xD9);
        private static readonly Color SystemGreen = new Color(0.4f, 0.9f, 0.45f);

        [MenuItem("Tools/Chat/Room 채팅 구성")]
        public static void SetupFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != RoomScenePath
                && !EditorUtility.DisplayDialog("Room 채팅 구성",
                    "지금 열린 씬은 " + scene.path + " 입니다.\nRoom 씬이 아닌데 이 씬에 구성할까요?", "구성", "취소"))
            {
                return;
            }
            string error;
            string summary = Apply(out error);
            EditorUtility.DisplayDialog("Room 채팅 구성",
                error ?? summary + "\n확인한 뒤 Ctrl+S로 씬을 저장하세요. (Ctrl+Z로 되돌릴 수 있습니다)", "확인");
        }

        /// <summary>열려 있는 씬에 Room 채팅을 구성한다. 실패하면 error에 이유를 넣고 null을 돌려준다.</summary>
        public static string Apply(out string error)
        {
            error = null;
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null || canvasGo.GetComponent<Canvas>() == null)
            {
                error = "Canvas가 있는 Room 씬에서 실행하세요. (" + RoomScenePath + ")";
                return null;
            }
            RectTransform canvas = (RectTransform)canvasGo.transform;
            var log = new StringBuilder();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Room 채팅 구성");
            int undoGroup = Undo.GetCurrentGroup();

            TextMeshProUGUI linePrefab = EnsureLinePrefab(log);
            bool firstTime = canvas.Find("RoomChat") == null;
            ChatLogView chatLog = EnsureChatLog(canvas, linePrefab, log);
            ChatInputBar inputBar = EnsureInputBar(canvas, log);
            if (firstTime) MakeRoom(canvas, log);
            EnsureGameChat(chatLog, inputBar, log);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            return log.ToString();
        }

        // ---------------------------------------------------------------- 줄 프리팹 (Room 크기)

        private static TextMeshProUGUI EnsureLinePrefab(StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RoomLinePrefabPath);
            if (existing == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GameLinePrefabPath);
                GameObject copy = Object.Instantiate(source);
                copy.name = "RoomChatLogLine";
                copy.GetComponent<TextMeshProUGUI>().fontSize = LineFontSize;
                existing = PrefabUtility.SaveAsPrefabAsset(copy, RoomLinePrefabPath);
                Object.DestroyImmediate(copy);
                log.AppendLine("· " + RoomLinePrefabPath + " 생성 (ChatLogLine 복제, 글자 " + LineFontSize + ")");
            }
            return existing.GetComponent<TextMeshProUGUI>();
        }

        // ---------------------------------------------------------------- 채팅 기록

        private static ChatLogView EnsureChatLog(RectTransform canvas, TextMeshProUGUI linePrefab, StringBuilder log)
        {
            Transform area = canvas.Find("RoomChat");
            if (area == null)
            {
                var go = new GameObject("RoomChat", typeof(RectTransform), typeof(Image));
                go.layer = canvas.gameObject.layer;
                RectTransform rt = (RectTransform)go.transform;
                rt.SetParent(canvas, false);
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.offsetMin = new Vector2(Margin * 2, BarHeight + Gap);
                rt.offsetMax = new Vector2(-Margin * 2, BarHeight + Gap + ChatHeight);
                go.GetComponent<Image>().color = ChatBackground;
                Undo.RegisterCreatedObjectUndo(go, "RoomChat");
                area = rt;
                log.AppendLine("· RoomChat(채팅 기록 영역) 생성");
            }

            ChatLogView view = area.GetComponentInChildren<ChatLogView>(true);
            if (view == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChatLogPanelPrefabPath);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, area);
                Undo.RegisterCreatedObjectUndo(inst, "ChatLogPanel");
                RectTransform rt = (RectTransform)inst.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                view = inst.GetComponent<ChatLogView>();

                var vlg = inst.transform.Find("Viewport/Content").GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(14, 14, 10, 10);
                vlg.spacing = 6;
                PrefabUtility.RecordPrefabInstancePropertyModifications(vlg);

                var so = new SerializedObject(view);
                so.FindProperty("systemColor").colorValue = SystemGreen; // GameChatController.systemColor와 같은 채팅 녹색
                so.ApplyModifiedProperties();
                log.AppendLine("· ChatLogPanel 배치 (RoomChat 안)");
            }

            var lineSo = new SerializedObject(view);
            lineSo.FindProperty("linePrefab").objectReferenceValue = linePrefab;
            lineSo.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            return view;
        }

        // ---------------------------------------------------------------- 입력 바 ([+] 없음)

        private static ChatInputBar EnsureInputBar(RectTransform canvas, StringBuilder log)
        {
            Transform bar = canvas.Find("ChatInputBar");
            if (bar == null)
            {
                // GameScene 하단 탭 바를 그대로 복제해 모양(색·스프라이트·글꼴)을 맞춘다.
                var sheetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottomSheetPrefabPath);
                GameObject temp = Object.Instantiate(sheetPrefab);
                Transform tab = temp.transform.Find("TabBar");
                GameObject go = Object.Instantiate(tab.gameObject, canvas, false);
                Object.DestroyImmediate(temp);
                go.name = "ChatInputBar";
                Undo.RegisterCreatedObjectUndo(go, "ChatInputBar");

                Transform plus = go.transform.Find("PlusButton");
                if (plus != null) Object.DestroyImmediate(plus.gameObject); // [+] 버튼 제외

                RectTransform rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0, BarHeight);

                RectTransform send = (RectTransform)go.transform.Find("SendButton");
                send.anchoredPosition = new Vector2(-Margin, 0);
                send.sizeDelta = new Vector2(SendWidth, SendHeight);
                send.GetComponentInChildren<TextMeshProUGUI>().fontSize = SendFontSize;

                RectTransform input = (RectTransform)go.transform.Find("ChatInput");
                input.anchorMin = Vector2.zero;
                input.anchorMax = Vector2.one;
                input.offsetMin = new Vector2(Margin, Margin);
                input.offsetMax = new Vector2(-(Margin + SendWidth + Margin), -Margin);
                var field = input.GetComponent<TMP_InputField>();
                field.pointSize = InputFontSize;
                if (field.textComponent != null) field.textComponent.fontSize = InputFontSize;
                if (field.placeholder is TMP_Text ph) ph.fontSize = PlaceholderFontSize;
                RectTransform area = (RectTransform)input.Find("Text Area");
                if (area != null)
                {
                    area.offsetMin = new Vector2(10, 4);
                    area.offsetMax = new Vector2(-10, -4);
                }

                foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = canvas.gameObject.layer;
                go.AddComponent<ChatInputBar>();
                bar = go.transform;
                log.AppendLine("· ChatInputBar 생성 (GameScene 탭 바 복제, [+] 제외)");
            }

            ChatInputBar inputBar = bar.GetComponent<ChatInputBar>();
            if (inputBar == null) inputBar = Undo.AddComponent<ChatInputBar>(bar.gameObject);
            var so = new SerializedObject(inputBar);
            so.FindProperty("chatInput").objectReferenceValue = bar.Find("ChatInput") != null ? bar.Find("ChatInput").GetComponent<TMP_InputField>() : null;
            so.FindProperty("sendButton").objectReferenceValue = bar.Find("SendButton") != null ? bar.Find("SendButton").GetComponent<Button>() : null;
            so.ApplyModifiedProperties();
            bar.SetAsLastSibling();
            return inputBar;
        }

        // ---------------------------------------------------------------- 기존 배치 올리기 (처음만)

        private static void MakeRoom(RectTransform canvas, StringBuilder log)
        {
            float chatTop = BarHeight + Gap + ChatHeight;
            RectTransform buttons = (RectTransform)canvas.Find("Buttons");
            float buttonsTop = chatTop + Gap;
            if (buttons != null)
            {
                Undo.RecordObject(buttons, "Buttons");
                buttons.anchoredPosition = new Vector2(buttons.anchoredPosition.x, chatTop + Gap + buttons.rect.height * buttons.pivot.y);
                buttonsTop = chatTop + Gap + buttons.rect.height;
                log.AppendLine("· Buttons를 채팅 영역 위로 이동");
            }
            RectTransform players = (RectTransform)canvas.Find("PlayerList");
            if (players != null)
            {
                Undo.RecordObject(players, "PlayerList");
                players.offsetMin = new Vector2(players.offsetMin.x, buttonsTop + Gap);
                log.AppendLine("· PlayerList 아래쪽을 버튼 위까지 올림");
            }
        }

        // ---------------------------------------------------------------- 서버 채팅

        private static void EnsureGameChat(ChatLogView chatLog, ChatInputBar inputBar, StringBuilder log)
        {
            GameChatController chat = Object.FindFirstObjectByType<GameChatController>(FindObjectsInactive.Include);
            if (chat == null)
            {
                var go = new GameObject("GameChat");
                Undo.RegisterCreatedObjectUndo(go, "GameChat");
                Undo.AddComponent<ChatApiClient>(go);
                chat = Undo.AddComponent<GameChatController>(go);
                log.AppendLine("· GameChat 생성 (ChatApiClient + GameChatController)");
            }
            ChatApiClient api = chat.GetComponent<ChatApiClient>();
            if (api == null) api = Undo.AddComponent<ChatApiClient>(chat.gameObject);
            var so = new SerializedObject(chat);
            so.FindProperty("api").objectReferenceValue = api;
            so.FindProperty("chatLog").objectReferenceValue = chatLog;
            so.FindProperty("inputBar").objectReferenceValue = inputBar;
            so.ApplyModifiedProperties();
        }
    }
}
