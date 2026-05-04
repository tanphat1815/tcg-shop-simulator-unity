// Assets/Editor/FurnitureDefinitionEditor.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for FurnitureDefinition ScriptableObjects.
/// Displays footprint preview grid and validation status.
/// </summary>
[CustomEditor(typeof(FurnitureDefinition))]
public class FurnitureDefinitionEditor : Editor
{
    private static readonly int[] CELL_COLORS = new int[16];

    public override void OnInspectorGUI()
    {
        var def = (FurnitureDefinition)target;

        EditorGUILayout.LabelField("Furniture Definition", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        DrawValidation(def);

        EditorGUILayout.Space();
        DrawFootprintPreview(def);
    }

    private void DrawValidation(FurnitureDefinition def)
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        bool valid = def.IsValid();
        GUI.color = valid ? Color.green : Color.red;
        EditorGUILayout.LabelField($"Status: {(valid ? "VALID" : "INVALID")}");
        GUI.color = Color.white;

        if (!valid)
        {
            if (def.furniturePrefab == null)
                EditorGUILayout.HelpBox("furniturePrefab is required.", MessageType.Warning);
            if (string.IsNullOrEmpty(def.displayName))
                EditorGUILayout.HelpBox("displayName is required.", MessageType.Warning);
            if (def.footprintWidth <= 0 || def.footprintHeight <= 0)
                EditorGUILayout.HelpBox("footprintWidth/Height must be > 0.", MessageType.Warning);
        }
    }

    private void DrawFootprintPreview(FurnitureDefinition def)
    {
        EditorGUILayout.LabelField("Footprint Preview (rotation=0)", EditorStyles.boldLabel);

        int w = def.footprintWidth;
        int h = def.footprintHeight;

        if (w <= 0 || h <= 0 || w > 4 || h > 4)
        {
            EditorGUILayout.HelpBox("Invalid footprint dimensions.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        for (int y = h - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < w; x++)
            {
                bool occupied = (x < w && y < h);
                GUI.color = occupied ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
                GUILayout.Button("",
                    GUILayout.Width(20),
                    GUILayout.Height(20));
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.LabelField($"Total cells: {def.TotalCells}");
        EditorGUILayout.LabelField($"Can rotate: {(def.canRotate ? "Yes" : "No")}");
    }
}
#endif
