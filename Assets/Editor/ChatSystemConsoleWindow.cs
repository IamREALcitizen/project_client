using System.Linq;
using UnityEditor;
using UnityEngine;
using WhoisntCitizen.Chat;

/// <summary>
/// Tools > Chat > System Message Console
/// Play 모드에서 현재 씬의 채팅 컨트롤러(GameScene: GameChatController)를 통해
/// 시스템 메시지(공지)를 서버로 보냅니다. (IChatSystemSender를 구현한 컴포넌트를 찾음)
///  - 보낸 메시지는 서버에 type=SYSTEM으로 저장되어 모든 클라이언트 채팅창에 녹색 [시스템] 메시지로 표시됩니다.
///  - 채팅창에 표시되는 시스템 메시지(입장·퇴장 알림, 공지)는 Unity 콘솔에도 [Chat][시스템]으로 출력됩니다.
/// Enter: 전송 / Shift+Enter: 줄바꿈
/// </summary>
public class ChatSystemConsoleWindow : EditorWindow
{
    string _text = "";
    string _result = "";
    MessageType _resultType = MessageType.None;
    bool _sending;

    [MenuItem("Tools/Chat/System Message Console")]
    public static void Open()
    {
        var w = GetWindow<ChatSystemConsoleWindow>("Chat System Console");
        w.minSize = new Vector2(360, 160);
    }

    void OnInspectorUpdate()
    {
        Repaint(); // Play 상태/연결 상태 표시 갱신
    }

    void OnGUI()
    {
        var chat = Application.isPlaying ? FindSender() : null;

        // 상태
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Play 모드에서 사용할 수 있습니다. (GameChatController가 있는 씬)", MessageType.Info);
        else if (chat == null)
            EditorGUILayout.HelpBox("현재 씬에 채팅 컨트롤러(GameChatController)가 없습니다.", MessageType.Warning);
        else
            EditorGUILayout.LabelField("대상",
                "room " + chat.RoomId + (chat.IsReady ? "  (연결됨)" : "  (연결 중...)"),
                EditorStyles.boldLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("시스템 메시지 (Enter 전송 / Shift+Enter 줄바꿈)");

        // Enter 처리는 TextArea가 이벤트를 먹기 전에 확인
        bool submit = false;
        var e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            && !e.shift && GUI.GetNameOfFocusedControl() == "SystemMessageInput")
        {
            submit = true;
            e.Use();
        }

        GUI.SetNextControlName("SystemMessageInput");
        _text = EditorGUILayout.TextArea(_text, GUILayout.MinHeight(60), GUILayout.ExpandHeight(true));

        bool canSend = chat != null && chat.IsReady && !_sending && !string.IsNullOrWhiteSpace(_text);
        using (new EditorGUI.DisabledScope(!canSend))
        {
            if (GUILayout.Button(_sending ? "보내는 중..." : "시스템 메시지로 전송", GUILayout.Height(28)))
                submit = true;
        }

        if (submit && canSend) Send(chat);

        if (!string.IsNullOrEmpty(_result))
            EditorGUILayout.HelpBox(_result, _resultType);
    }

    static IChatSystemSender FindSender()
    {
        return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(m => m != null && m.isActiveAndEnabled)
            .OfType<IChatSystemSender>()
            .FirstOrDefault();
    }

    void Send(IChatSystemSender chat)
    {
        string text = _text;
        _sending = true;
        _result = "";
        chat.SendSystemMessage(text, (ok, err) =>
        {
            _sending = false;
            if (ok)
            {
                _text = "";
                _result = "전송 완료: " + text;
                _resultType = MessageType.Info;
                GUI.FocusControl("SystemMessageInput");
            }
            else
            {
                _result = err;
                _resultType = MessageType.Error;
            }
            Repaint();
        });
    }
}
