using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(GridGuideFiller))]
public class GridGuideFillerEditor : Editor
{
    private SerializedProperty _gridGuideTilemap;
    private SerializedProperty _gridGuideTile;
    private SerializedProperty _fillBoundsMin;
    private SerializedProperty _fillBoundsSize;

    private void OnEnable()
    {
        _gridGuideTilemap = serializedObject.FindProperty("gridGuideTilemap");
        _gridGuideTile = serializedObject.FindProperty("gridGuideTile");
        _fillBoundsMin = serializedObject.FindProperty("fillBoundsMin");
        _fillBoundsSize = serializedObject.FindProperty("fillBoundsSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_gridGuideTilemap);
        EditorGUILayout.PropertyField(_gridGuideTile);
        EditorGUILayout.PropertyField(_fillBoundsMin);
        EditorGUILayout.PropertyField(_fillBoundsSize);
        serializedObject.ApplyModifiedProperties();

        var filler = (GridGuideFiller)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Grid Guide 채우기", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(filler.gridGuideTilemap == null || filler.gridGuideTile == null);
        if (GUILayout.Button("Fill Grid Guide (한 번에 채우기)"))
        {
            FillGridGuide(filler);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("영역 복사 (다른 타일맵 기준)", EditorStyles.miniLabel);
        if (GUILayout.Button("Bounds를 다른 타일맵에서 가져오기"))
        {
            var grid = filler.GetComponentInParent<Grid>();
            if (grid != null)
            {
                var all = grid.GetComponentsInChildren<Tilemap>();
                Tilemap other = null;
                foreach (var tm in all)
                {
                    if (tm != filler.gridGuideTilemap) { other = tm; break; }
                }
                if (other != null)
                {
                    filler.SetBoundsFromTilemap(other);
                    EditorUtility.SetDirty(filler);
                    Debug.Log($"Bounds 복사됨: {other.name} → fillBoundsMin/Size");
                }
                else
                    Debug.LogWarning("같은 Grid 아래 GridGuide가 아닌 다른 Tilemap을 찾지 못했습니다.");
            }
        }
    }

    private static void FillGridGuide(GridGuideFiller filler)
    {
        if (filler.gridGuideTilemap == null)
        {
            Debug.LogWarning("Grid Guide Tilemap이 할당되지 않았습니다.");
            return;
        }
        if (filler.gridGuideTile == null)
        {
            Debug.LogWarning("Grid Guide Tile이 할당되지 않았습니다.");
            return;
        }

        var bounds = filler.GetFillBounds();
        int count = 0;
        Undo.RegisterCompleteObjectUndo(filler.gridGuideTilemap, "Fill Grid Guide");

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                filler.gridGuideTilemap.SetTile(pos, filler.gridGuideTile);
                count++;
            }
        }

        Debug.Log($"Grid Guide 채움 완료: {count} 셀 (bounds: {bounds})");
    }
}
