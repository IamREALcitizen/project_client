using TMPro;
using UnityEditor;
using UnityEngine;
using WhoisntCitizen.Game;
using WhoisntCitizen.GameUI;

namespace WhoisntCitizen.EditorTools
{
    // 낮·밤 전환 컷신(DayTransitionView)의 구간별 시간을 고치고 바로 미리 보는 창. Tools > 낮·밤 전환 연출 시간
    //   - 시간: Resources/DayTransition/DayTransitionSettings에 저장된다. 플레이 중에 바꿔도 남고, 다음 컷신부터 적용된다.
    //   - 미리보기(플레이 중): 고른 장면을 게임 화면 위에서 재생하거나, 시간 막대를 끌어 그 순간을 멈춰 본다.
    public sealed class DayTransitionTuner : EditorWindow
    {
        private const string SettingsPath = "Assets/Resources/DayTransition/DayTransitionSettings.asset";
        private const string FontPath = "Assets/Fonts/MalgunGothic SDF.asset";
        private const float ResultPhaseSeconds = 5f; // 가짜 서버의 밤 결과·처형 페이즈 길이 (FakeGameApi)
        private static readonly string[] KindNames = { "밤사이 사망", "선의 보호로 생존", "아무 일 없음", "투표로 처형", "처형 없음 (동점·투표 없음)", "크라켄에게 끌려감" };

        private DayTransitionSettings settings;
        private SerializedObject serialized;
        private DayTransitionKind kind;
        private string playerName = "지훈";
        private int skinIndex;
        private bool mine, gameOver;
        private float scrub;
        private DayTransitionView view;
        private ChibiCharacterSkin[] skins;
        private string[] skinNames;
        private DesignSpriteCatalog portraits;
        private Vector2 scroll;

        [MenuItem("Tools/낮·밤 전환 연출 시간")]
        private static void Open()
        {
            GetWindow<DayTransitionTuner>("전환 연출 시간");
        }

        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            view = null; // 미리보기 오브젝트는 플레이가 끝나면 씬과 함께 사라진다
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (view != null && view.IsPlaying) Repaint();
        }

        private void LoadSettings()
        {
            settings = AssetDatabase.LoadAssetAtPath<DayTransitionSettings>(SettingsPath);
            serialized = settings != null ? new SerializedObject(settings) : null;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("시간 설정 파일이 없습니다: " + SettingsPath + "\n없으면 기본 시간으로 재생됩니다.", MessageType.Warning);
                if (GUILayout.Button("설정 파일 만들기"))
                {
                    AssetDatabase.CreateAsset(CreateInstance<DayTransitionSettings>(), SettingsPath);
                    AssetDatabase.SaveAssets();
                    LoadSettings();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            serialized.Update();
            kind = (DayTransitionKind)EditorGUILayout.Popup("장면", (int)kind, KindNames);
            EditorGUILayout.Space();
            DrawBeats();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통", EditorStyles.boldLabel);
            Field("outLength", "걷힘 (레터박스가 열림)");
            Field("maxHold", "다음 페이즈 최대 대기");
            if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(settings);

            EditorGUILayout.Space();
            DrawPreview();
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ 시간

        private void DrawBeats()
        {
            EditorGUILayout.LabelField("구간별 시간 (초)", EditorStyles.boldLabel);
            (string field, string label)[] fields = DayTransitionSettings.BeatFields(kind);
            float[] defaults = DayTransitionSettings.DesignBeats(kind);
            float hold = 0f;
            for (int i = 0; i < fields.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                SerializedProperty beat = Field(fields[i].field, (i + 1) + ". " + fields[i].label);
                GUILayout.Label("기본 " + defaults[i].ToString("0.0#"), EditorStyles.miniLabel, GUILayout.Width(52f));
                EditorGUILayout.EndHorizontal();
                if (!gameOver || i < fields.Length - 1) hold += beat.floatValue;
            }
            float outLength = serialized.FindProperty("outLength").floatValue;
            EditorGUILayout.LabelField(" ", "마지막 명패까지 " + hold.ToString("0.0#") + "초 + 걷힘 " + outLength.ToString("0.0#")
                + "초 = " + (hold + outLength).ToString("0.0#") + "초", EditorStyles.miniBoldLabel);
            if (hold > ResultPhaseSeconds)
            {
                EditorGUILayout.HelpBox("가짜 서버의 결과 페이즈(" + ResultPhaseSeconds + "초)보다 깁니다. 다음 페이즈가 먼저 시작되면 명패에서 기다리지 않고 "
                    + "끝까지 재생한 뒤 걷히며, 그동안 다음 페이즈는 컷신 아래에서 이미 진행됩니다.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 0.8배 (빠르게)")) Scale(fields, 0.8f);
            if (GUILayout.Button("전체 1.25배 (느리게)")) Scale(fields, 1.25f);
            if (GUILayout.Button("이 장면 기본값"))
            {
                for (int i = 0; i < fields.Length; i++) serialized.FindProperty(fields[i].field).floatValue = defaults[i];
            }
            EditorGUILayout.EndHorizontal();
        }

        private SerializedProperty Field(string path, string label)
        {
            SerializedProperty property = serialized.FindProperty(path);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
            float min = path == "maxHold" ? 0f : DayTransitionSettings.MinBeat;
            if (property.floatValue < min) property.floatValue = min;
            return property;
        }

        private void Scale((string field, string label)[] fields, float factor)
        {
            foreach ((string field, string label) beat in fields)
            {
                SerializedProperty property = serialized.FindProperty(beat.field);
                property.floatValue = Mathf.Max(DayTransitionSettings.MinBeat, Mathf.Round(property.floatValue * factor * 100f) / 100f);
            }
        }

        // ------------------------------------------------------------------ 미리보기

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 미리 볼 수 있습니다. GameScene3(또는 캔버스가 있는 씬)을 플레이한 뒤 여기서 재생하세요.\n"
                    + "시간 값은 플레이 중에 바꿔도 플레이를 멈춘 뒤 그대로 남습니다.", MessageType.Info);
                return;
            }
            LoadPreviewArt();
            playerName = EditorGUILayout.TextField("이름", playerName);
            if (skinNames.Length > 0) skinIndex = EditorGUILayout.Popup("스킨", Mathf.Clamp(skinIndex, 0, skinNames.Length - 1), skinNames);
            mine = EditorGUILayout.Toggle("내가 죽은 경우", mine);
            gameOver = EditorGUILayout.Toggle("게임이 끝난 결과", gameOver);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ 재생", GUILayout.Height(28f)) && EnsureView()) view.Play(Info(), false);
            using (new EditorGUI.DisabledScope(view == null || !view.IsPlaying))
            {
                if (GUILayout.Button("다음 페이즈 시작", GUILayout.Height(28f))) view.Release();
            }
            if (GUILayout.Button("■ 멈춤", GUILayout.Height(28f)) && view != null) view.Stop();
            EditorGUILayout.EndHorizontal();
            if (view != null && view.IsPlaying)
            {
                EditorGUILayout.HelpBox("재생 중. 마지막 명패에서 \"다음 페이즈 시작\"을 누르거나 최대 대기 시간이 지나면 걷힙니다. 화면을 눌러 건너뛸 수도 있습니다.", MessageType.None);
            }

            EditorGUI.BeginChangeCheck();
            scrub = EditorGUILayout.Slider("시간 (멈춰 보기)", scrub, 0f, PreviewDuration());
            if (EditorGUI.EndChangeCheck() && EnsureView()) view.Show(Info(), scrub);
        }

        private float PreviewDuration()
        {
            float[] beats = settings.Beats(kind);
            float hold = 0f;
            for (int i = 0; i < beats.Length - (gameOver ? 1 : 0); i++) hold += beats[i];
            return hold + Mathf.Max(DayTransitionSettings.MinBeat, settings.outLength);
        }

        private void LoadPreviewArt()
        {
            if (skins != null) return;
            skins = Resources.LoadAll<ChibiCharacterSkin>("DayTable/Skins");
            System.Array.Sort(skins, (a, b) => string.CompareOrdinal(a.name, b.name));
            skinNames = new string[skins.Length];
            for (int i = 0; i < skins.Length; i++) skinNames[i] = skins[i].DesignId;
            portraits = Resources.Load<DesignSpriteCatalog>("GameHud/Portraits");
        }

        private DayTransitionInfo Info()
        {
            ChibiCharacterSkin skin = skins.Length > 0 ? skins[Mathf.Clamp(skinIndex, 0, skins.Length - 1)] : null;
            ChibiCharacterSkin other = skins.Length > 1 ? skins[(Mathf.Clamp(skinIndex, 0, skins.Length - 1) + 1) % skins.Length] : null;
            Sprite portrait = skin != null && portraits != null ? portraits.Find(skin.DesignId) : null;
            return new DayTransitionInfo
            {
                Kind = kind,
                Day = 1,
                Name = playerName,
                Figure = skin != null ? skin.Bound : null,
                Portrait = portrait,
                Votes = 4,
                TotalVotes = 7,
                Reason = "동점이라 아무도 처형되지 않았습니다.",
                Mine = mine,
                // 크라켄 장면: 고른 스킨과 그다음 스킨, 두 사람이 끌려간 밤
                Victims = new[]
                {
                    new DayTransitionVictim { Name = playerName, Portrait = portrait, Mine = mine, Kraken = true },
                    new DayTransitionVictim { Name = "민수", Portrait = other != null && portraits != null ? portraits.Find(other.DesignId) : null, Kraken = true },
                },
                GameOver = gameOver,
            };
        }

        /// <summary>미리보기용 컷신을 게임 화면 캔버스(없으면 아무 캔버스) 위에 만든다. 게임의 컷신과 따로 움직인다.</summary>
        private bool EnsureView()
        {
            if (view != null) return true;
            GameScreen screen = FindFirstObjectByType<GameScreen>();
            RectTransform canvas = screen != null ? screen.transform as RectTransform : null;
            if (canvas == null)
            {
                Canvas any = FindFirstObjectByType<Canvas>();
                canvas = any != null ? any.transform as RectTransform : null;
            }
            if (canvas == null)
            {
                ShowNotification(new GUIContent("씬에 캔버스가 없습니다"));
                return false;
            }
            view = DayTransitionView.Create(canvas, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
            view.name = "DayTransitionPreview";
            view.Settings = settings;
            NightSceneBackgroundCatalog nights = Resources.Load<NightSceneBackgroundCatalog>("NightScene/NightSceneBackgrounds");
            view.NightBackdrop = nights != null ? nights.PirateShared : null;
            return true;
        }
    }
}
