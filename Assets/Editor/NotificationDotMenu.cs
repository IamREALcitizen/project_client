using UnityEditor;
using UnityEngine;

/// <summary>
/// Hierarchy 오른쪽 클릭 → UI → Notification Dot (알림 점)
/// 선택한 UI 오브젝트 아래에 알림 점 프리팹을 붙인다. (프리팹 연결 유지, Ctrl+Z로 되돌리기 가능)
/// </summary>
public static class NotificationDotMenu
{
    private const string MenuPath = "GameObject/UI/Notification Dot (알림 점)";

    [MenuItem(MenuPath, false, 2200)]
    private static void Create(MenuCommand command)
    {
        var parent = command.context as GameObject;
        if (parent == null) parent = Selection.activeGameObject;
        if (parent == null || !(parent.transform is RectTransform))
        {
            EditorUtility.DisplayDialog("알림 점", "알림 점을 붙일 UI 오브젝트(버튼·아이콘 등)를 먼저 선택하세요.", "확인");
            return;
        }

        var prefab = Resources.Load<GameObject>(NotificationDot.ResourcePath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("알림 점", "프리팹을 찾을 수 없습니다: Assets/Resources/" + NotificationDot.ResourcePath + ".prefab", "확인");
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        go.transform.SetAsLastSibling();
        Undo.RegisterCreatedObjectUndo(go, "Create Notification Dot");
        Selection.activeGameObject = go;
    }
}
