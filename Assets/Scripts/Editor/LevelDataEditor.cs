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

        // Rotation levelleri için parça bazlı canRotate editörü
        bool isRotation = (level.levelType & LevelData.LevelType.Rotation) != 0;
        if (isRotation && level.pieces != null && level.pieces.Count > 0)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Döndürülebilir Parçalar", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < level.pieces.Count; i++)
            {
                var piece = level.pieces[i];

                // Renk önizlemesi için küçük renkli kutu
                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                Rect colorRect = new Rect(rect.x, rect.y + 1, 12, rect.height - 2);
                Rect toggleRect = new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height);

                EditorGUI.DrawRect(colorRect, piece.liquidColor);
                string label = $"Parça {i}  →  ({piece.gridPosition.x}, {piece.gridPosition.y})  |  {piece.currentSlices} dilim";
                piece.canRotate = EditorGUI.ToggleLeft(toggleRect, label, piece.canRotate);
            }
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(level);

            EditorGUILayout.EndVertical();
        }

        // Donuk Gridler (Frozen Cells) Editörü
        if (level.frozenCells != null && level.frozenCells.Count > 0)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"❄️ Donuk Gridler ({level.frozenCells.Count} Adet)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < level.frozenCells.Count; i++)
            {
                var fc = level.frozenCells[i];
                EditorGUILayout.BeginHorizontal();
                string faceStr = level.boardMode == LevelData.BoardMode.Shape3D ? $" [Yüz {fc.faceIndex}]" : "";
                EditorGUILayout.LabelField($"📍 ({fc.gridPosition.x}, {fc.gridPosition.y}){faceStr}", GUILayout.Width(110));
                fc.requiredMatches = EditorGUILayout.IntSlider("Sayaç", fc.requiredMatches, 1, 20);
                if (GUILayout.Button("🗑️ Sil", GUILayout.Width(45)))
                {
                    level.frozenCells.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(level);

            EditorGUILayout.EndVertical();
        }

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
