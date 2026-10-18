using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WhoisntCitizen.Common;

/// <summary>
/// 하이어라키에서 고른 UI들을 PortraitFrame(9:16 폭 제한) 오브젝트 안으로 옮긴다.
/// 같은 Canvas의 Canvas Scaler도 1080x1920 / Expand로 맞춘다. Ctrl+Z로 한 번에 되돌릴 수 있다.
/// </summary>
public static class PortraitFrameMenu
{
    private const string MenuPath = "Tools/UI/선택한 UI를 세로 프레임으로 감싸기";

    [MenuItem(MenuPath)]
    private static void Wrap()
    {
        var selected = Selection.transforms.OfType<RectTransform>().ToList();
        if (selected.Count == 0)
        {
            EditorUtility.DisplayDialog("세로 프레임", "Canvas 바로 아래의 UI 오브젝트를 먼저 선택하세요.", "확인");
            return;
        }

        Transform parent = selected[0].parent;
        if (parent == null || selected.Any(t => t.parent != parent))
        {
            EditorUtility.DisplayDialog("세로 프레임", "같은 부모(보통 Canvas) 아래의 오브젝트만 한 번에 감쌀 수 있습니다.", "확인");
            return;
        }

        var canvas = parent.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("세로 프레임", "선택한 오브젝트가 Canvas 아래에 있지 않습니다.", "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("세로 프레임으로 감싸기");

        // 1) Canvas Scaler: 넓은 화면은 높이 1920 고정, 세로로 긴 화면은 폭 1080 고정
        var scaler = canvas.rootCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
        }

        // 2) 프레임 생성: 선택한 것 중 가장 위 순서 자리에
        var ordered = selected.OrderBy(t => t.GetSiblingIndex()).ToList();
        int index = ordered[0].GetSiblingIndex();

        var go = new GameObject("PortraitFrame", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "PortraitFrame 생성");
        var frame = (RectTransform)go.transform;
        frame.SetParent(parent, false);
        frame.localScale = Vector3.one;
        frame.SetSiblingIndex(index);
        // 일단 부모를 꽉 채운 상태에서 옮겨야 앵커 값이 그대로 유지된다
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;

        // 3) 선택한 UI를 순서 그대로 프레임 안으로 (앵커/오프셋 값 유지)
        foreach (var t in ordered)
        {
            Undo.SetTransformParent(t, frame, false, "프레임 안으로 이동");
        }

        // 4) 마지막에 폭 제한 컴포넌트 부착
        Undo.AddComponent<PortraitFrame>(go);

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    [MenuItem(MenuPath, true)]
    private static bool WrapValidate() => Selection.transforms.OfType<RectTransform>().Any();
}
