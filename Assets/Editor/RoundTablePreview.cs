using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;

namespace WhoisntCitizen.EditorTools
{
    // Tools > Game Scene > 원탁 미리보기 (Edit 모드)
    // GameScene3(GameScreen의 layout이 Tabs인 씬)을 편집하는 동안, Play 때 코드가 만드는 무대를 같은 자리·같은 모습으로 띄운다.
    // 화면을 구운 씬(게임 화면 굽기)이면 Play 때와 같은 순서로 끼워 넣는다:
    //   GameCanvas
    //   ├─ Background
    //   ├─ [미리보기] 갑판 (저장 안 됨)   ← Deck: 갑판 배경 (GameLayoutTabs.PlaceDeck). Play 때는 Background가 갑판이 된다
    //   ├─ GameHud, BottomBackdrop, ...   (구운 틀)
    //   ├─ ChatLogPanel
    //   ├─ [미리보기] 원탁 (저장 안 됨)   ← DayRoundTable: 원탁과 캐릭터 (DayRoundTableView, 낮). Play 때처럼 채팅 기록 바로 위
    //   ├─ RoleChip, Notice
    //   └─ ...
    // 굽기 전이면 원탁 쪽 하나에 갑판, 무대 아래 바탕(GameLayoutTabs.BuildBackdrop), 원탁을 차례로 넣는다.
    // - 씬에 저장되지 않는다(HideFlags.DontSave). 인스펙터에서 고칠 수 없고 Scene 뷰에서 눌러도 선택되지 않는다(아래 UI를 고를 수 있게).
    //   만들고 지우는 것만으로 씬이 '변경됨'(*)이 되지 않는다(그 전에 깨끗했던 씬은 다시 깨끗하게 둔다).
    // - Play에 들어가기 전에 지우고(Play 화면은 GameScreen이 직접 만든다), Edit 모드로 돌아오면 다시 만든다.
    // - Resources/DayTable/RoundTableLayout 값을 바꾸면 바로 다시 그린다. 앉히는 인원은 그 에셋의 previewPlayers.
    // - 켜고 끄기는 사람마다 따로 저장된다(EditorPrefs).
    [InitializeOnLoad]
    internal static class RoundTablePreview
    {
        private const string MenuPath = "Tools/Game Scene/원탁 미리보기 (Edit 모드)";
        private const string EnabledKey = "WhoisntCitizen.RoundTablePreview.Enabled";
        private const string Prefix = "[미리보기] ";
        private const string StageName = Prefix + "원탁 (저장 안 됨)", DeckName = Prefix + "갑판 (저장 안 됨)";
        private const HideFlags PreviewFlags = HideFlags.DontSave | HideFlags.NotEditable;
        // 가짜 서버의 나와 봇 7명(FakeGameApi), 그 뒤로 12명까지
        private static readonly string[] Names = { "나", "철수", "영희", "민수", "지훈", "수진", "현우", "유나", "서연", "도윤", "하준", "지아" };
        // 씬의 '변경됨' 표시를 지우는 Unity 내부 함수 (internal). 버전이 바뀌어 없으면 지우지 않는다.
        private static readonly MethodInfo ClearSceneDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);
        private static bool queued;

        static RoundTablePreview()
        {
            if (Application.isBatchMode) return;
            EditorSceneManager.sceneOpened += (scene, mode) => Queue();
            EditorSceneManager.sceneClosing += (scene, removingScene) => WithoutDirtying(() => Remove(scene));
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += () => WithoutDirtying(() => Remove(default));
            RoundTableLayout.EditModeChanged += Queue;
            Queue();
        }

        private static bool Enabled
        {
            get { return EditorPrefs.GetBool(EnabledKey, true); }
            set { EditorPrefs.SetBool(EnabledKey, value); }
        }

        /// <summary>곧 다시 만든다 (씬을 바꾼 도구가 부른다).</summary>
        internal static void Refresh()
        {
            Queue();
        }

        /// <summary>지금 지운다 (씬을 바꾸기 전에, 미리보기가 그 사이에 끼지 않게).</summary>
        internal static void Clear()
        {
            WithoutDirtying(() => Remove(default));
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Queue();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) WithoutDirtying(() => Remove(default));
            else if (change == PlayModeStateChange.EnteredEditMode) Queue();
        }

        // 슬라이더를 끄는 동안처럼 값이 연달아 바뀌어도 에디터가 지금 일을 마친 뒤 한 번만 다시 만든다.
        private static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += Rebuild;
        }

        private static void Rebuild()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            WithoutDirtying(() =>
            {
                Remove(default);
                if (!Enabled) return;
                foreach (GameScreen screen in Object.FindObjectsByType<GameScreen>(FindObjectsSortMode.None))
                {
                    if (screen.Layout == HudLayout.Tabs && !EditorSceneManager.IsPreviewScene(screen.gameObject.scene)) Build(screen);
                }
            });
        }

        // 저장되지 않는 미리보기를 만들거나 지운 것만으로 씬이 '변경됨'(*)이 되지 않게 한다.
        // 그 전에 깨끗했던 씬만 되돌리므로, 사람이 고친 씬의 표시는 그대로 남는다.
        private static void WithoutDirtying(System.Action change)
        {
            var clean = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !scene.isDirty) clean.Add(scene);
            }
            change();
            if (ClearSceneDirtiness == null) return;
            foreach (Scene scene in clean)
            {
                if (scene.isLoaded && scene.isDirty) ClearSceneDirtiness.Invoke(null, new object[] { scene });
            }
        }

        private static void Build(GameScreen screen)
        {
            var canvas = (RectTransform)screen.transform;
            GameHudMetrics.Use(HudLayout.Tabs); // 무대 자리 (RoundTableLayout의 stageTop·stageHeight)
            GameLayoutTabs tabs = screen.GetComponent<GameLayoutTabs>();
            bool baked = tabs != null && tabs.IsBaked;

            RectTransform stage = NewRoot(StageName, canvas, screen.ChatPanel);
            RectTransform deckRoot = baked ? NewRoot(DeckName, canvas, canvas.Find("Background")) : stage;
            var deck = new GameObject("Deck", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            deck.layer = canvas.gameObject.layer;
            deck.transform.SetParent(deckRoot, false);
            Image deckImage = deck.GetComponent<Image>();
            deckImage.sprite = Resources.Load<Sprite>("DayTable/Deck");
            deckImage.raycastTarget = false;
            // 캔버스 크기는 처음 그려질 때 정해진다. 그 전이면 기준 폭(1080, 폭 맞춤이라 늘 이 값)으로 둔다
            float width = canvas.rect.width > 0f ? canvas.rect.width : 1080f;
            GameLayoutTabs.PlaceDeck((RectTransform)deck.transform, width);
            if (!baked) GameLayoutTabs.BuildBackdrop(stage); // 구운 씬에는 진짜가 있다

            int count = Mathf.Clamp(RoundTableLayout.Current.previewPlayers, 4, Names.Length);
            var players = new List<PlayerViewDto>(count);
            for (int i = 0; i < count; i++) players.Add(new PlayerViewDto { playerId = i + 1, nickname = Names[i], alive = true });
            DayRoundTableView table = stage.gameObject.AddComponent<DayRoundTableView>();
            table.UseSnugLayout();
            table.Initialize(null, null, screen.LabelFont, null, count);
            table.SetPlayers(players);
            table.SetPhase(GamePhases.Day);

            Seal(stage);
            if (deckRoot != stage) Seal(deckRoot);
        }

        // 캔버스를 덮는 빈 미리보기 칸. after 바로 위 형제로 놓는다 (없으면 맨 위).
        private static RectTransform NewRoot(string name, RectTransform canvas, Transform after)
        {
            var go = new GameObject(name, typeof(RectTransform)) { hideFlags = PreviewFlags };
            go.layer = canvas.gameObject.layer;
            var root = (RectTransform)go.transform;
            root.SetParent(canvas, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            if (after != null && after.parent == canvas) root.SetSiblingIndex(after.GetSiblingIndex() + 1);
            return root;
        }

        // 저장되지 않고, 인스펙터에서 고칠 수 없고, Scene 뷰에서 눌러도 선택되지 않게 한다.
        private static void Seal(RectTransform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = PreviewFlags;
                foreach (Component component in t.GetComponents<Component>()) component.hideFlags = PreviewFlags;
            }
            SceneVisibilityManager.instance.DisablePicking(root.gameObject, true);
        }

        // 그 씬의 미리보기를 지운다 (default면 열린 모든 씬). 스크립트를 다시 불러온 뒤에 남은 것도 이름과 표시로 찾는다.
        private static void Remove(Scene scene)
        {
            foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rect == null || !rect.name.StartsWith(Prefix, System.StringComparison.Ordinal)
                    || (rect.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0) continue;
                if (scene.IsValid() && rect.gameObject.scene != scene) continue;
                Object.DestroyImmediate(rect.gameObject);
            }
        }
    }
}
