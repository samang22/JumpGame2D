using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

[CustomEditor(typeof(TileEditController))]
public class TileEditControllerEditor : Editor
{
    private const string FolderPathKey = "TileEditController.PaletteFolderPath";
    private string _folderPath = "Assets/Tile";

    private void OnEnable()
    {
        _folderPath = EditorPrefs.GetString(FolderPathKey, "Assets/Tile");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Auto-fill Palette (from folder)", EditorStyles.boldLabel);
        _folderPath = EditorGUILayout.TextField("Folder Path", _folderPath);

        if (GUILayout.Button("Scan folder and fill palette"))
        {
            LoadPaletteFromFolder(_folderPath);
            EditorPrefs.SetString(FolderPathKey, _folderPath);
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

            paletteProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = paletteProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("displayName").stringValue = id;
            element.FindPropertyRelative("icon").objectReferenceValue = null;
            element.FindPropertyRelative("tile").objectReferenceValue = tile;
            element.FindPropertyRelative("layer").enumValueIndex = (int)TileLayerType.Solid;
            index++;
        }

        so.ApplyModifiedProperties();
        Debug.Log($"Added {index} tiles to palette. (Folder: {folderPath})");
    }
}
