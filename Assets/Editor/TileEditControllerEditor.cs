using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

[CustomEditor(typeof(TileEditController))]
public class TileEditControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Auto-fill Palette (from folder)", EditorStyles.boldLabel);
        SerializedProperty resourcesPathProp = serializedObject.FindProperty("resourcesPalettePath");
        string resourcesPath = resourcesPathProp != null ? resourcesPathProp.stringValue : "Tiles";
        string scanPath = string.IsNullOrWhiteSpace(resourcesPath) ? "Assets/Resources" : "Assets/Resources/" + resourcesPath.Trim('/');
        EditorGUILayout.HelpBox($"Scan 경로: {scanPath}\n(Resources Palette Path와 동일한 폴더를 스캔합니다.)", MessageType.None);

        if (GUILayout.Button("Scan folder and fill palette"))
        {
            LoadPaletteFromFolder(scanPath);
        }
    }

    private void LoadPaletteFromFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Debug.LogWarning("Folder path is empty.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets") && !folderPath.StartsWith("Assets"))
        {
            Debug.LogWarning("Path must be under Assets (e.g. Assets/Tile).");
            return;
        }

        // Search for TileBase or Tile assets inside the folder (including subfolders)
        string[] guids = AssetDatabase.FindAssets("t:TileBase", new[] { folderPath });
        if (guids.Length == 0)
            guids = AssetDatabase.FindAssets("t:Tile", new[] { folderPath });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"No Tile/TileBase assets found in folder: {folderPath}");
            return;
        }

        var controller = (TileEditController)target;
        SerializedObject so = serializedObject;
        SerializedProperty paletteProp = so.FindProperty("palette");
        if (paletteProp == null)
        {
            Debug.LogError("Could not find 'palette' SerializedProperty.");
            return;
        }

        paletteProp.ClearArray();
        int index = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile == null) continue;

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string id = nameWithoutExtension;
            string parentFolder = Path.GetFileName(Path.GetDirectoryName(path) ?? "").ToLowerInvariant();
            TileLayerType layer = LayerFromFolderName(parentFolder);

            paletteProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = paletteProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("displayName").stringValue = id;
            element.FindPropertyRelative("icon").objectReferenceValue = null;
            element.FindPropertyRelative("tile").objectReferenceValue = tile;
            element.FindPropertyRelative("layer").enumValueIndex = (int)layer;
            index++;
        }

        so.ApplyModifiedProperties();
        Debug.Log($"Added {index} tiles to palette. (Folder: {folderPath})");
    }

    private static TileLayerType LayerFromFolderName(string folderName)
    {
        if (string.IsNullOrEmpty(folderName)) return TileLayerType.Solid;
        if (folderName.Contains("gimmick")) return TileLayerType.Gimmick;
        if (folderName.Contains("oneway") || folderName.Contains("one_way")) return TileLayerType.OneWay;
        if (folderName.Contains("background") || folderName.Contains("back")) return TileLayerType.BackGround;
        if (folderName.Contains("hazard")) return TileLayerType.Hazard;
        return TileLayerType.Solid; // ground, default
    }
}
