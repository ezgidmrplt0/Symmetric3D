using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GridSpawner))]
public class GridSpawnerEditor : Editor
{
    private LevelData GetCurrentLevel(GridSpawner spawner)
    {
        if (spawner == null) return null;
        if (spawner.levels != null && spawner.currentLevelIndex >= 0 && spawner.currentLevelIndex < spawner.levels.Count)
        {
            return spawner.levels[spawner.currentLevelIndex];
        }
        if (spawner.sequence != null && spawner.sequence.levels != null && spawner.sequence.levels.Count > 0)
        {
            return spawner.sequence.levels[0];
        }
        return null;
    }

    private void OnSceneGUI()
    {
        GridSpawner spawner = (GridSpawner)target;
        if (spawner == null) return;

        LevelData currentLevel = GetCurrentLevel(spawner);
        if (currentLevel == null || currentLevel.pieces == null) return;

        // Sahne görünümünde her şişeyi doğrudan raf üzerinde taşıyabilmek için Scene View Handles
        int count = currentLevel.pieces.Count;
        for (int i = 0; i < count; i++)
        {
            var piece = currentLevel.pieces[i];
            Vector3 worldPos;

            // Eğer oyun çalışıyorsa ve objeler spawn edilmişse o objenin konumunu baz al
            if (Application.isPlaying && spawner.transform.childCount > i)
            {
                Transform child = spawner.transform.GetChild(i);
                worldPos = child.position;
            }
            else
            {
                Vector3 localPos = GridSpawner.GetBottlePositionForLevel(currentLevel, i, count);
                worldPos = spawner.transform.position + localPos;
            }

            // Şişe numarası etiketi
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.85f);
            Handles.DrawWireDisc(worldPos, Vector3.forward, 0.35f);
            Handles.Label(worldPos + new Vector3(0f, 0.6f, 0f), $"Şişe #{i + 1}", EditorStyles.boldLabel);

            // Sadece CustomPositions modunda hareket handle'ı göster
            if (currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.CustomPositions)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newTargetPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(currentLevel, "Move Bottle Position");
                    Vector3 newLocalPos = newTargetPos - spawner.transform.position;
                    piece.customPosition = new Vector2(newLocalPos.x, newLocalPos.y);

                    if (Application.isPlaying && spawner.transform.childCount > i)
                    {
                        spawner.transform.GetChild(i).position = newTargetPos;
                    }

                    EditorUtility.SetDirty(currentLevel);
                }
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GridSpawner spawner = (GridSpawner)target;
        if (spawner == null) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎨 Magic Sort Hızlı Araçlar", EditorStyles.boldLabel);

        if (GUILayout.Button("🧪 Level Tasarımcısını Aç (Görsel & Serbest Editör)", GUILayout.Height(34)))
        {
            LevelDesignerWindow.ShowWindow();
        }

        LevelData curLvl = GetCurrentLevel(spawner);
        if (curLvl != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox($"Aktif Seviye: {curLvl.levelDisplayName} | Dizilim: {curLvl.flatLayoutMode} | Şişe Sayısı: {curLvl.pieces?.Count ?? 0}", MessageType.Info);

            if (curLvl.flatLayoutMode != LevelData.FlatLayoutMode.CustomPositions)
            {
                if (GUILayout.Button("🎯 Bu Seviyeyi Serbest Pozisyona Çevir (Bake to Custom)", GUILayout.Height(28)))
                {
                    Undo.RecordObject(curLvl, "Convert to Custom Positions");
                    int total = curLvl.pieces != null ? curLvl.pieces.Count : 0;
                    for (int i = 0; i < total; i++)
                    {
                        Vector3 p = GridSpawner.GetBottlePositionForLevel(curLvl, i, total);
                        curLvl.pieces[i].customPosition = new Vector2(p.x, p.y);
                    }
                    curLvl.flatLayoutMode = LevelData.FlatLayoutMode.CustomPositions;
                    EditorUtility.SetDirty(curLvl);
                }
            }
        }
    }
}
