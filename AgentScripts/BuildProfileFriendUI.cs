using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Common;
using WhoisntCitizen.Friend;
using WhoisntCitizen.Lobby;
using WhoisntCitizen.Profile;

// Lobby 씬에 프로필/친구 UI를 만들고 컨트롤러를 연결하는 일회성 빌더 (run_script로 실행).
// 여러 번 실행해도 Popup_Profile / Popup_Avatar / Popup_Friend / Popup_Setting / ProfileManager 를 지우고 다시 만든다.
//   - 상단바 Profile_Button(사람 아이콘) = 친구 창 버튼, Setting_Button(톱니바퀴) = 설정 창(로그아웃)
//   - 로비 상단 정보 패널(Rooms/UserInfoArea) 전체를 누르면 프로필 창, 프로필 창의 내 아바타를 누르면 아바타 선택 창
public static class BuildProfileFriendUI
{
    const string PrefabFolder = "Assets/03_Prefabs/Friend";

    static Transform tplRoot, tplBg, tplTitle, tplLabel, tplInput, tplBtn, tplClose, tplStatus;
    static readonly StringBuilder log = new StringBuilder();

    public static string Build()
    {
        log.Clear();

        GameObject canvas = GameObject.Find("Canvas");
        Transform popupRoot = canvas.transform.Find("Popup");
        Transform template = popupRoot.Find("Popup_RoomPassward");
        if (template == null) return "Popup_RoomPassward 템플릿을 찾지 못했습니다.";

        tplRoot = template;
        tplBg = template.Find("Background");
        tplTitle = tplBg.Find("TitleImage");
        tplLabel = tplBg.Find("Area_PasswordText/Text (TMP)");
        tplInput = tplBg.Find("Area_PasswordText/InputField (TMP) (1)");
        tplBtn = tplBg.Find("Button_Enter");
        tplClose = tplBg.Find("Button_Close");
        tplStatus = tplBg.Find("StatusMessageText");

        // 다시 실행해도 되도록 이전 결과물 삭제
        foreach (string n in new[] { "Popup_Profile", "Popup_Avatar", "Popup_Friend", "Popup_Setting" })
        {
            Transform old = popupRoot.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        GameObject oldMgr = GameObject.Find("ProfileManager");
        if (oldMgr != null) Object.DestroyImmediate(oldMgr);

        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/03_Prefabs", "Friend");

        // 항목 프리팹 (팝업보다 먼저 만들어 둔다)
        FriendItemView friendItemPrefab = BuildFriendItemPrefab();
        FriendRequestItemView requestItemPrefab = BuildRequestItemPrefab();

        // 팝업
        ProfileRefs pr = BuildProfilePopup(popupRoot);
        AvatarRefs ar = BuildAvatarPopup(popupRoot);
        FriendRefs fr = BuildFriendPopup(popupRoot, friendItemPrefab, requestItemPrefab);
        SettingRefs sr = BuildSettingPopup(popupRoot);

        // 상단바: Profile_Button(사람 아이콘)이 친구 창 버튼이다. 이전 실행에서 만든 Friend_Button / Avatar 는 정리한다.
        Transform topBar = canvas.transform.Find("SafeArea/TopBar");
        Transform oldFriendButton = topBar.Find("Friend_Button");
        if (oldFriendButton != null) Object.DestroyImmediate(oldFriendButton.gameObject);
        Transform friendButtonTf = topBar.Find("Profile_Button");
        Transform oldAvatar = friendButtonTf.Find("Avatar");
        if (oldAvatar != null) Object.DestroyImmediate(oldAvatar.gameObject);
        Button friendButton = friendButtonTf.GetComponent<Button>();
        Button settingButton = topBar.Find("Setting_Button").GetComponent<Button>();

        // 로비 상단 정보 패널 전체가 프로필 창 버튼. 패널의 프로필 칸(profileImage) 안에 아바타를 표시한다.
        Transform infoArea = canvas.transform.Find("SafeArea/Rooms/UserInfoArea");
        Button profileOpenButton = EnsureInfoPanelButton(infoArea);
        Image panelAvatar = EnsureAvatar(infoArea.Find("profileImage"), "Avatar");
        panelAvatar.enabled = false; // 프로필을 불러와 AvatarCatalog가 채울 때까지 흰 사각형이 보이지 않게
        pr.avatar.enabled = false;

        // 매니저 + 컨트롤러 연결
        GameObject mgr = new GameObject("ProfileManager");
        LobbyProfileController lpc = mgr.AddComponent<LobbyProfileController>();
        FriendUIController fui = mgr.AddComponent<FriendUIController>();

        Set(lpc, "profileButton", profileOpenButton);
        Set(lpc, "profilePopup", pr.root);
        Set(lpc, "closeButton", pr.close);
        Set(lpc, "nicknameText", pr.nickname);
        Set(lpc, "profileButtonAvatar", panelAvatar);
        Set(lpc, "avatarImage", pr.avatar);
        Set(lpc, "avatarEditButton", pr.avatarEditButton);
        Set(lpc, "avatarPopup", ar.root);
        Set(lpc, "avatarConfirmButton", ar.confirm);
        Set(lpc, "avatarCancelButton", ar.cancel);
        Set(lpc, "avatarStatusMessage", ar.status);
        SetArray(lpc, "avatarButtons", ar.buttons);
        Set(lpc, "levelText", pr.level);
        Set(lpc, "goldText", pr.gold);
        Set(lpc, "recordText", pr.record);
        Set(lpc, "winRateText", pr.winRate);
        Set(lpc, "nicknameInput", pr.nicknameInput);
        Set(lpc, "changeNicknameButton", pr.changeButton);
        Set(lpc, "statusMessage", pr.status);

        Set(fui, "openButton", friendButton); // TopBar 의 사람 아이콘 버튼
        Set(fui, "friendPopup", fr.root);
        Set(fui, "closeButton", fr.close);
        Set(fui, "friendsTabButton", fr.friendsTab);
        Set(fui, "requestsTabButton", fr.requestsTab);
        Set(fui, "addTabButton", fr.addTab);
        Set(fui, "friendsPanel", fr.friendsPanel);
        Set(fui, "requestsPanel", fr.requestsPanel);
        Set(fui, "addPanel", fr.addPanel);
        Set(fui, "requestCountText", fr.requestBadge);
        Set(fui, "friendListContent", fr.friendContent);
        Set(fui, "friendItemPrefab", friendItemPrefab);
        Set(fui, "friendEmptyNotice", fr.friendEmpty);
        Set(fui, "requestListContent", fr.requestContent);
        Set(fui, "requestItemPrefab", requestItemPrefab);
        Set(fui, "requestEmptyNotice", fr.requestEmpty);
        Set(fui, "searchInput", fr.searchInput);
        Set(fui, "searchButton", fr.searchButton);
        Set(fui, "resultRoot", fr.resultRoot);
        Set(fui, "resultAvatarImage", fr.resultAvatar);
        Set(fui, "resultNicknameText", fr.resultNickname);
        Set(fui, "resultInfoText", fr.resultInfo);
        Set(fui, "sendRequestButton", fr.sendButton);
        Set(fui, "statusMessage", fr.status);

        LobbySettingsPopup sp = mgr.AddComponent<LobbySettingsPopup>();
        Set(sp, "openButton", settingButton);
        Set(sp, "popup", sr.root);
        Set(sp, "closeButton", sr.close);
        Set(sp, "logoutButton", sr.logout);

        pr.root.SetActive(false);
        ar.root.SetActive(false);
        fr.root.SetActive(false);
        sr.root.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        return "OK\n" + log;
    }

    // ------------------------------------------------------------------
    // 프로필 팝업
    // ------------------------------------------------------------------

    class ProfileRefs
    {
        public GameObject root;
        public Button close, changeButton, avatarEditButton;
        public Image avatar;
        public TMP_Text nickname, level, gold, record, winRate;
        public TMP_InputField nicknameInput;
        public StatusMessageView status;
    }

    static ProfileRefs BuildProfilePopup(Transform popupRoot)
    {
        ProfileRefs r = new ProfileRefs();
        Transform bg = NewPopupShell(popupRoot, "Popup_Profile", 650f, out r.root);

        Title(bg, "내 프로필");

        // 내 아바타: 누르면 아바타 선택 창이 뜬다
        r.avatar = AvatarImage(bg, "Avatar_Current", 120f);
        r.avatar.raycastTarget = true;
        r.avatarEditButton = r.avatar.gameObject.AddComponent<Button>();
        r.avatarEditButton.targetGraphic = r.avatar;
        TMP_Text hint = Label(bg, "Text_AvatarHint", "이미지를 눌러 변경", 24f, TextAlignmentOptions.Center, -1f, 1f);
        LE(hint.gameObject, -1f, 30f, 1f, -1f);
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        r.nickname = InfoRow(bg, "닉네임");
        r.level = InfoRow(bg, "레벨");
        r.gold = InfoRow(bg, "골드");
        r.record = InfoRow(bg, "전적");
        r.winRate = InfoRow(bg, "승률");

        Spacer(bg, 6);

        TMP_Text changeLabel = Label(bg, "Text_ChangeNickname", "닉네임 변경", 28f, TextAlignmentOptions.MidlineLeft, -1f, 1f);
        LE(changeLabel.gameObject, -1f, 36f, 1f, -1f);
        RectTransform change = HRow(bg, "Area_ChangeNickname", 54f, 10f);
        r.nicknameInput = Input(change, "InputField_Nickname", 330f);
        r.changeButton = Button(change, "Button_ChangeNickname", "변경", tplBtn, 150f, 54f, 28f);

        r.status = Status(bg);
        r.close = Close(bg);
        return r;
    }

    // ------------------------------------------------------------------
    // 아바타 선택 팝업 / 설정 팝업
    // ------------------------------------------------------------------

    class AvatarRefs
    {
        public GameObject root;
        public Button confirm, cancel;
        public StatusMessageView status;
        public Button[] buttons;
    }

    static AvatarRefs BuildAvatarPopup(Transform popupRoot)
    {
        AvatarRefs r = new AvatarRefs();
        Transform bg = NewPopupShell(popupRoot, "Popup_Avatar", 900f, out r.root);
        Title(bg, "프로필 이미지");
        r.buttons = AvatarGrid(bg);
        r.status = Status(bg);

        // 왼쪽 확인 / 오른쪽 취소
        RectTransform row = HRow(bg, "Buttons", 64f, 40f);
        r.confirm = Button(row, "Button_Confirm", "확인", tplBtn, 220f, 64f, 32f);
        r.cancel = Button(row, "Button_Cancel", "취소", tplBtn, 220f, 64f, 32f);
        return r;
    }

    class SettingRefs
    {
        public GameObject root;
        public Button close, logout;
    }

    static SettingRefs BuildSettingPopup(Transform popupRoot)
    {
        SettingRefs r = new SettingRefs();
        Transform bg = NewPopupShell(popupRoot, "Popup_Setting", 520f, out r.root);
        Title(bg, "설정");
        Spacer(bg, 10);
        r.logout = Button(bg, "Button_Logout", "로그아웃", tplBtn, 260f, 60f, 30f);
        Spacer(bg, 10);
        r.close = Close(bg);
        return r;
    }

    static TMP_Text InfoRow(Transform parent, string title)
    {
        RectTransform row = HRow(parent, "Row_" + title, 46f, 10f);
        Label(row, "Label", title, 34f, TextAlignmentOptions.MidlineLeft, 160f, -1f);
        return Label(row, "Value", "-", 34f, TextAlignmentOptions.MidlineRight, -1f, 1f);
    }

    // ------------------------------------------------------------------
    // 친구 팝업
    // ------------------------------------------------------------------

    class FriendRefs
    {
        public GameObject root, friendsPanel, requestsPanel, addPanel, friendEmpty, requestEmpty, resultRoot;
        public Button close, friendsTab, requestsTab, addTab, searchButton, sendButton;
        public Image resultAvatar;
        public TMP_Text requestBadge, resultNickname, resultInfo;
        public Transform friendContent, requestContent;
        public TMP_InputField searchInput;
        public StatusMessageView status;
    }

    static FriendRefs BuildFriendPopup(Transform popupRoot, FriendItemView friendItem, FriendRequestItemView requestItem)
    {
        FriendRefs r = new FriendRefs();
        Transform bg = NewPopupShell(popupRoot, "Popup_Friend", 700f, out r.root);

        Title(bg, "친구");

        RectTransform tabs = HRow(bg, "Tabs", 56f, 8f);
        r.friendsTab = Button(tabs, "Tab_Friends", "내 친구", tplBtn, 190f, 56f, 28f);
        r.requestsTab = Button(tabs, "Tab_Requests", "받은 요청", tplBtn, 190f, 56f, 28f);
        r.addTab = Button(tabs, "Tab_Add", "친구 추가", tplBtn, 190f, 56f, 28f);
        r.requestBadge = Badge(r.requestsTab.transform);

        RectTransform panels = NewRect("Panels", bg);
        LE(panels.gameObject, -1f, 520f, 1f, -1f);

        // 내 친구
        RectTransform fp = Panel(panels, "Panel_Friends");
        r.friendsPanel = fp.gameObject;
        r.friendContent = ScrollList(fp);
        r.friendEmpty = EmptyNotice(fp, "친구가 없습니다.");

        // 받은 요청
        RectTransform rp = Panel(panels, "Panel_Requests");
        r.requestsPanel = rp.gameObject;
        r.requestContent = ScrollList(rp);
        r.requestEmpty = EmptyNotice(rp, "받은 친구 요청이 없습니다.");

        // 친구 추가
        RectTransform ap = Panel(panels, "Panel_Add");
        r.addPanel = ap.gameObject;
        VLG(ap.gameObject, 10, 10, 10, 10, 16f, TextAnchor.UpperCenter);
        RectTransform searchRow = HRow(ap, "SearchRow", 54f, 10f);
        r.searchInput = Input(searchRow, "InputField_Search", 330f);
        LE(r.searchInput.gameObject, 330f, 54f, 1f, -1f);
        r.searchButton = Button(searchRow, "Button_Search", "검색", tplBtn, 140f, 54f, 28f);

        RectTransform result = NewRect("Result", ap);
        r.resultRoot = result.gameObject;
        Image resultBg = result.gameObject.AddComponent<Image>();
        resultBg.color = new Color(1f, 1f, 1f, 0.12f);
        VLG(result.gameObject, 18, 18, 16, 16, 8f, TextAnchor.UpperCenter);
        LE(result.gameObject, -1f, -1f, 1f, -1f);
        r.resultAvatar = AvatarImage(result, "Avatar", 96f);
        r.resultNickname = Label(result, "Text_Nickname", "-", 38f, TextAlignmentOptions.Center, -1f, 1f);
        LE(r.resultNickname.gameObject, -1f, 46f);
        r.resultInfo = Label(result, "Text_Info", "-", 28f, TextAlignmentOptions.Center, -1f, 1f);
        LE(r.resultInfo.gameObject, -1f, 36f);
        r.sendButton = Button(result, "Button_SendRequest", "친구 요청 보내기", tplBtn, 300f, 54f, 28f);
        result.gameObject.SetActive(false);

        r.status = Status(bg);
        r.close = Close(bg);
        return r;
    }

    // 팝업 공통 틀: 어두운 배경 + Background(VerticalLayout + 자동 높이)
    static Transform NewPopupShell(Transform popupRoot, string name, float width, out GameObject root)
    {
        GameObject go = Object.Instantiate(tplRoot.gameObject, popupRoot, false);
        go.name = name;
        go.SetActive(true);

        // 템플릿 전용 컴포넌트 제거
        RoomPasswordPopup old = go.GetComponent<RoomPasswordPopup>();
        if (old != null) Object.DestroyImmediate(old);

        Transform bg = go.transform.Find("Background");
        for (int i = bg.childCount - 1; i >= 0; i--) Object.DestroyImmediate(bg.GetChild(i).gameObject);

        RectTransform brt = (RectTransform)bg;
        brt.sizeDelta = new Vector2(width, 400f);
        VerticalLayoutGroup v = VLG(bg.gameObject, 50, 50, 70, 40, 14f, TextAnchor.UpperCenter);
        ContentSizeFitter fit = bg.gameObject.GetComponent<ContentSizeFitter>();
        if (fit == null) fit = bg.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        root = go;
        return bg;
    }

    static void Title(Transform parent, string text)
    {
        GameObject go = Object.Instantiate(tplTitle.gameObject, parent, false);
        go.name = "TitleImage";
        go.GetComponentInChildren<TMP_Text>().text = text;
        LE(go, 344f, 66f);
    }

    static void Spacer(Transform parent, float height)
    {
        RectTransform s = NewRect("Spacing", parent);
        LE(s.gameObject, -1f, height);
    }

    static StatusMessageView Status(Transform parent)
    {
        GameObject go = Object.Instantiate(tplStatus.gameObject, parent, false);
        go.name = "StatusMessageText";
        go.GetComponent<TMP_Text>().text = string.Empty;
        LE(go, -1f, 40f, 1f, -1f);
        return go.GetComponent<StatusMessageView>();
    }

    static Button Close(Transform parent)
    {
        GameObject go = Object.Instantiate(tplClose.gameObject, parent, false);
        go.name = "Button_Close";
        LE(go, 70f, 70f).ignoreLayout = true; // 템플릿처럼 팝업 하단 가운데에 고정
        Button b = go.GetComponent<Button>();
        ClearPersistentListeners(b);
        return b;
    }

    static TMP_Text Badge(Transform tabButton)
    {
        GameObject go = Object.Instantiate(tplLabel.gameObject, tabButton, false);
        go.name = "Badge";
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(44f, 36f);
        rt.anchoredPosition = new Vector2(-4f, -2f);
        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = "0";
        t.fontSize = 26f;
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(1f, 0.35f, 0.3f);
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        go.SetActive(false);
        return t;
    }

    static GameObject EmptyNotice(Transform parent, string text)
    {
        GameObject go = Object.Instantiate(tplLabel.gameObject, parent, false);
        go.name = "EmptyNotice";
        Object.DestroyImmediate(go.GetComponent<LayoutElement>());
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 60f);
        rt.anchoredPosition = Vector2.zero;
        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.fontSize = 32f;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        go.SetActive(false);
        return go;
    }

    // 목록용 스크롤뷰(CommonScroll 프리팹)를 패널 가득 채워 만들고 Content를 돌려준다.
    static Transform ScrollList(RectTransform panel)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/CommonScroll.prefab");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "CommonScroll";
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Transform content = FindDeep(go.transform, "Content");
        VerticalLayoutGroup v = content.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(6, 6, 6, 6);
        v.spacing = 8f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter f = content.GetComponent<ContentSizeFitter>();
        if (f == null) f = content.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 밝은 회색 배경이면 흰 글씨가 안 보이므로 어둡게
        foreach (Image img in go.GetComponentsInChildren<Image>(true))
            if (img.sprite != null && img.sprite.name == "Background") img.color = new Color(0f, 0f, 0f, 0.4f);

        // 프리팹에 들어 있던 샘플 항목 제거
        for (int i = content.childCount - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject);

        log.AppendLine("ScrollList content: " + content.name + " under " + content.parent.name);
        return content;
    }

    // ------------------------------------------------------------------
    // 항목 프리팹
    // ------------------------------------------------------------------

    static FriendItemView BuildFriendItemPrefab()
    {
        GameObject root = NewItemRoot("FriendItem");
        Image avatar = AvatarImage(root.transform, "Avatar", 64f);
        Transform info = ItemInfo(root.transform, out TMP_Text nick, out TMP_Text record);
        Button remove = Button(root.transform, "Button_Remove", "삭제", tplClose, 96f, 54f, 26f);

        FriendItemView view = root.AddComponent<FriendItemView>();
        Set(view, "nicknameText", nick);
        Set(view, "recordText", record);
        Set(view, "avatarImage", avatar);
        Set(view, "removeButton", remove);
        return SavePrefab<FriendItemView>(root, PrefabFolder + "/FriendItem.prefab");
    }

    static FriendRequestItemView BuildRequestItemPrefab()
    {
        GameObject root = NewItemRoot("FriendRequestItem");
        Transform info = ItemInfo(root.transform, out TMP_Text nick, out TMP_Text at);
        Button accept = Button(root.transform, "Button_Accept", "수락", tplBtn, 96f, 54f, 26f);
        Button reject = Button(root.transform, "Button_Reject", "거절", tplClose, 96f, 54f, 26f);

        FriendRequestItemView view = root.AddComponent<FriendRequestItemView>();
        Set(view, "nicknameText", nick);
        Set(view, "requestedAtText", at);
        Set(view, "acceptButton", accept);
        Set(view, "rejectButton", reject);
        return SavePrefab<FriendRequestItemView>(root, PrefabFolder + "/FriendRequestItem.prefab");
    }

    static GameObject NewItemRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.layer = 5;
        ((RectTransform)root.transform).sizeDelta = new Vector2(560f, 90f);
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        LE(root, -1f, 90f);

        HorizontalLayoutGroup h = root.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 12, 8, 8);
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        return root;
    }

    static Transform ItemInfo(Transform parent, out TMP_Text top, out TMP_Text bottom)
    {
        RectTransform info = NewRect("Info", parent);
        LE(info.gameObject, -1f, -1f, 1f, -1f);
        VLG(info.gameObject, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
        top = Label(info, "Text_Nickname", "-", 32f, TextAlignmentOptions.MidlineLeft, -1f, 1f);
        LE(top.gameObject, -1f, 40f, 1f, -1f);
        bottom = Label(info, "Text_Record", "-", 24f, TextAlignmentOptions.MidlineLeft, -1f, 1f);
        bottom.color = new Color(1f, 1f, 1f, 0.75f);
        LE(bottom.gameObject, -1f, 30f, 1f, -1f);
        return info;
    }

    static T SavePrefab<T>(GameObject root, string path) where T : Component
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine("prefab: " + path);
        return saved.GetComponent<T>();
    }

    // ------------------------------------------------------------------
    // Profile_Button 텍스트
    // ------------------------------------------------------------------

    static void ConfigureProfileButtonText(TMP_Text t)
    {
        t.transform.SetAsLastSibling(); // 아이콘(Image) 위에 그려지도록
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-8f, 44f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        t.font = tplLabel.GetComponent<TMP_Text>().font; // 기본 LiberationSans 에는 한글이 없다
        t.enableAutoSizing = true;
        t.fontSizeMin = 12f;
        t.fontSizeMax = 20f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Bottom;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "닉네임\nLv.1";
    }

    // ------------------------------------------------------------------
    // UI 조립 도우미
    // ------------------------------------------------------------------

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Panel(Transform parent, string name)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform HRow(Transform parent, string name, float height, float spacing)
    {
        RectTransform rt = NewRect(name, parent);
        HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        LE(rt.gameObject, -1f, height, 1f, -1f);
        return rt;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, TextAlignmentOptions align, float prefW, float flexW)
    {
        GameObject go = Object.Instantiate(tplLabel.gameObject, parent, false);
        go.name = name;
        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        LE(go, prefW, -1f, flexW, -1f);
        return t;
    }

    static TMP_InputField Input(Transform parent, string name, float width)
    {
        GameObject go = Object.Instantiate(tplInput.gameObject, parent, false);
        go.name = name;
        LE(go, width, 54f);
        TMP_InputField f = go.GetComponent<TMP_InputField>();
        f.text = string.Empty;
        return f;
    }

    static Button Button(Transform parent, string name, string text, Transform template, float width, float height, float fontSize)
    {
        GameObject go = Object.Instantiate(template.gameObject, parent, false);
        go.name = name;
        TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
        t.text = text;
        t.fontSize = fontSize;
        LE(go, width, height);
        Button b = go.GetComponent<Button>();
        ClearPersistentListeners(b);
        return b;
    }

    // 템플릿 Button_Close 에는 Popup_JobCustom.SetActive 가 OnClick 으로 남아 있다.
    // 복제본에 남아 있으면 컨트롤러의 BindIfEmpty 가 코드 연결을 건너뛰므로 지운다.
    static void ClearPersistentListeners(Button b)
    {
        for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(b.onClick, i);
    }

    static LayoutElement LE(GameObject go, float pw = -1f, float ph = -1f, float fw = -1f, float fh = -1f)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = pw;
        le.preferredHeight = ph;
        le.flexibleWidth = fw;
        le.flexibleHeight = fh;
        le.ignoreLayout = false; // 템플릿 Button_Close 는 ignoreLayout=true 라서 복제본도 레이아웃을 무시한다
        return le;
    }

    static VerticalLayoutGroup VLG(GameObject go, int left, int right, int top, int bottom, float spacing, TextAnchor align)
    {
        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(left, right, top, bottom);
        v.spacing = spacing;
        v.childAlignment = align;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        return v;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new System.Exception(name + " 를 " + root.name + " 아래에서 찾지 못했습니다.");
    }

    // 아바타 표시용 정사각 Image (스프라이트는 런타임에 AvatarCatalog가 채운다)
    static Image AvatarImage(Transform parent, string name, float size)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        LE(rt.gameObject, size, size);
        return img;
    }

    // 프로필 팝업의 아바타 선택 그리드 (버튼 순서 = 아바타 번호)
    // 칸마다 테두리(금색 선) + 어두운 바탕 + 초상화를 겹쳐서 서로 구분되게 하고, 칸 사이도 넉넉히 띄운다.
    static Button[] AvatarGrid(Transform parent)
    {
        const float cell = 140f, gap = 16f, border = 3f, inset = 8f;
        RectTransform grid = NewRect("Avatar_Grid", parent);
        GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(cell, cell);
        g.spacing = new Vector2(gap, gap);
        g.childAlignment = TextAnchor.UpperCenter;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        int rows = (AvatarCatalog.Count + g.constraintCount - 1) / g.constraintCount;
        LE(grid.gameObject, -1f, rows * cell + (rows - 1) * gap, 1f, -1f);

        var buttons = new Button[AvatarCatalog.Count];
        for (int i = 0; i < buttons.Length; i++)
        {
            // 바깥: 테두리 색 / 눌림 영역
            RectTransform root = NewRect("Avatar_" + i, grid);
            Image frame = root.gameObject.AddComponent<Image>();
            frame.color = new Color(0.85f, 0.68f, 0.30f, 0.9f);

            RectTransform fill = Stretch(NewRect("Fill", root), border);
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.07f, 0.09f, 0.12f, 0.9f);
            fillImg.raycastTarget = false;

            RectTransform portrait = Stretch(NewRect("Portrait", root), inset);
            Image img = portrait.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            buttons[i] = root.gameObject.AddComponent<Button>();
            buttons[i].targetGraphic = img; // 컨트롤러가 초상화 Image에 아바타를 넣고 선택 여부를 밝기로 표시한다
        }
        return buttons;
    }

    static RectTransform Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    // 부모를 꽉 채우는 아바타 Image 를 (없으면) 만든다. 부모(프로필 칸 틀) 안쪽으로 약간 들여 놓는다.
    static Image EnsureAvatar(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -10f);
            t = rt;
            Image created = t.gameObject.AddComponent<Image>();
            created.preserveAspect = true;
            created.raycastTarget = false;
        }
        return t.GetComponent<Image>();
    }

    // 정보 패널(UserInfoArea) 전체를 덮는 투명 버튼을 (없으면) 만든다. 패널 배경(Background) 크기에 맞춘다.
    static Button EnsureInfoPanelButton(Transform infoArea)
    {
        Transform t = infoArea.Find("OpenProfileButton");
        if (t == null)
        {
            RectTransform rt = NewRect("OpenProfileButton", infoArea);
            RectTransform bgRt = (RectTransform)infoArea.Find("Background");
            rt.anchorMin = bgRt.anchorMin;
            rt.anchorMax = bgRt.anchorMax;
            rt.pivot = bgRt.pivot;
            rt.anchoredPosition = bgRt.anchoredPosition;
            rt.sizeDelta = bgRt.sizeDelta;
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // 안 보이지만 클릭은 받는다
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            t = rt;
        }
        t.SetAsLastSibling(); // 다른 자식들 위에서 클릭을 받는다
        return t.GetComponent<Button>();
    }

    static void SetArray(Component c, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception(c.GetType().Name + "." + field + " 필드를 찾지 못했습니다.");
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // SerializedObject로 private [SerializeField] 연결 (문자열/오브젝트)
    static void Set(Component c, string field, Object value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception(c.GetType().Name + "." + field + " 필드를 찾지 못했습니다.");

        // Component 대신 GameObject 필드이거나 그 반대인 경우를 허용
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (value == null) log.AppendLine("WARN null: " + c.GetType().Name + "." + field);
    }
}
