#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LevelData level = (LevelData)target;

        DrawDefaultInspector();

        GUILayout.Space(10);

        if (level.boardMode == LevelData.BoardMode.Shape3D)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Shape Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Sync Shape Faces From Prefab"))
            {
                level.SyncShapeFacesFromPrefab();
            }

            if (level.shapePrefab != null)
            {
                ShapeDefinition def = level.shapePrefab.GetComponent<ShapeDefinition>();
                if (def == null)
                {
                    EditorGUILayout.HelpBox("Shape prefab üzerinde ShapeDefinition component yok.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("Detected Face Count", def.FaceCount.ToString());
                }
            }

            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
