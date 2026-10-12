using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;

namespace WhoisntCitizen.EditorTools
{
    // Tools > Game Scene > 게임 화면 굽기 (GameScene3)
    // 열려 있는 GameScene3(GameScreen의 layout이 Tabs)에, Play 때 코드가 만들던 고정된 틀을 씬 오브젝트로 만들어 넣는다
    // (GameScreen.BakeLayout). 저장하면 Play 때는 만들지 않고 연결만 하므로, 위치·크기·색을 에디터에서 고칠 수 있다.
    //   GameCanvas                 GameHudLayout · GameLayoutTabs · GameHudChat 추가 (만든 오브젝트를 기억했다가 Play 때 연결한다)
    //   ├─ Background              그대로 (Play 때 갑판이 된다. 무대는 RoundTableLayout을 따른다)
    //   ├─ GameHud                 (새로) 페이즈 패, 남은 시간, 설정·지도 버튼. 페이즈 글자·시간 글자를 이 안으로 옮긴다
    //   ├─ TopBar_Placeholder      바탕 투명, 높이 236 (연결 끊김 띠만 남는다)
    //   ├─ RoleCard                꺼 둔다 (내 역할 칩이 대신한다)
    //   ├─ BottomBackdrop          (새로) 무대 아래 바탕: ChatBox(채팅 상자), ActionLine(행동 줄), PlayerStrip(명단 칸)
    //   ├─ BottomSheetLift         (새로) GameBottomSheet를 이 안으로 옮긴다. 입력 바만 남긴다 (서랍 높이 0)
    //   ├─ ChatLogPanel            채팅 기록. 채팅 상자 안에 놓는다
    //   ├─ RoleChip                (새로) 내 역할 칩
    //   ├─ Notice                  (새로, 꺼 둠) 공지 두루마리. 켜 보고 위치를 고친 뒤 꺼 두지 않아도 된다(Play 때 끈다)
    //   └─ GameResultPanel
    // - 하단 시트가 채팅 기록을 밀어 올리지 않게 한다(GameBottomSheet의 pushTargets를 비운다). 채팅 기록 자리는 씬이 정한다.
    // - 원탁·캐릭터·밤 방, 명단 안의 사람, 내 역할 칩의 초상, 채팅 줄처럼 사람·게임에 따라 바뀌는 것은 Play 때 그대로 만든다.
    // - 프리팹 인스턴스(ChatLogPanel, GameBottomSheet)를 고친 것은 그 인스턴스의 오버라이드로 씬에 남는다.
    // - 한 번만 한다. 되돌리려면 저장하지 않고 씬을 다시 열거나(Don't Save) git으로 되돌린다.
    internal static class GameLayoutBakeTool
    {
        private const string MenuPath = "Tools/Game Scene/게임 화면 굽기 (GameScene3)";
        private const string Title = "게임 화면 굽기";

        [MenuItem(MenuPath)]
        private static void Bake()
        {
            GameScreen screen = FindTabsScreen();
            if (screen == null)
            {
                EditorUtility.DisplayDialog(Title, "열린 씬에 Tabs 배치의 GameScreen이 없습니다. GameScene3을 열고 다시 실행하세요.", "확인");
                return;
            }
            if (IsBaked(screen))
            {
                EditorUtility.DisplayDialog(Title, "이 씬은 이미 구워져 있습니다. 씬의 오브젝트를 직접 고치세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog(Title,
                    screen.gameObject.scene.name + "에 상단 HUD, 공지, 내 역할 칩, 채팅·행동 줄·명단 칸을 씬 오브젝트로 만듭니다.\n\n" +
                    "저장하면 Play 때는 이 오브젝트를 그대로 쓰므로 에디터에서 위치와 모양을 고칠 수 있습니다.\n" +
                    "되돌리려면 저장하지 않고 씬을 다시 열거나 git으로 되돌리세요.", "굽기", "취소"))
            {
                return;
            }

            RoundTablePreview.Clear(); // 미리보기가 구운 틀 사이에 끼지 않게
            StopPushingChat(screen);
            screen.BakeLayout();
            RecordPrefabInstances(screen.transform);
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            RoundTablePreview.Refresh();
            Selection.activeGameObject = screen.gameObject;
            EditorUtility.DisplayDialog(Title, "구웠습니다. Ctrl+S로 씬을 저장하세요.\n\n" +
                "무대(갑판·원탁)의 자리와 크기는 Resources/DayTable/RoundTableLayout에서 고칩니다.", "확인");
        }

        [MenuItem(MenuPath, true)]
        private static bool BakeValidate()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static GameScreen FindTabsScreen()
        {
            foreach (GameScreen screen in Object.FindObjectsByType<GameScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (screen.Layout == HudLayout.Tabs && !EditorSceneManager.IsPreviewScene(screen.gameObject.scene)) return screen;
            }
            return null;
        }

        private static bool IsBaked(GameScreen screen)
        {
            GameLayoutTabs tabs = screen.GetComponent<GameLayoutTabs>();
            return tabs != null && tabs.IsBaked;
        }

        // Play 때는 채팅 기록을 GameHudChat이 매 프레임 제자리로 되돌렸다. 구우면 씬이 자리를 정하므로 시트가 밀지 않게 한다.
        private static void StopPushingChat(GameScreen screen)
        {
            BottomTabController sheet = screen.GetComponentInChildren<BottomTabController>(true);
            if (sheet == null) return;
            var so = new SerializedObject(sheet);
            SerializedProperty targets = so.FindProperty("pushTargets");
            if (targets == null) return;
            targets.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 프리팹 인스턴스 안을 스크립트로 바꾼 것은 기록해야 오버라이드로 씬에 남는다 (안 하면 다시 열 때 프리팹 값으로 돌아간다).
        private static void RecordPrefabInstances(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                foreach (Component component in t.GetComponents<Component>())
                {
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
        }
    }
}
