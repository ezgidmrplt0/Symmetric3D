using UnityEngine;
using UnityEditor;

/// <summary>
/// TutorialManager için Özel Inspector Arayüzü.
/// </summary>
[CustomEditor(typeof(TutorialManager))]
public class TransferTutorialSetupTool : Editor
{
    private bool _showHandSection    = true;
    private bool _showSpecialSection = true;
    private bool _showLevelList      = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        EditorGUILayout.LabelField("Tutorial Manager", header);
        EditorGUILayout.Space(4);

        // ── El Animasyonu ────────────────────────────────────────
        _showHandSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showHandSection, "El Animasyonu");
        if (_showHandSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("handImage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("durationPerSegment"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // ── Özel Panel ───────────────────────────────────────────
        _showSpecialSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showSpecialSection, "Özel Seviye 6 Tutorial (Eski Panel)");
        if (_showSpecialSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("specialTutorialPanel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("specialTutorialText"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // ── Level Listesi ────────────────────────────────────────
        _showLevelList = EditorGUILayout.BeginFoldoutHeaderGroup(_showLevelList, "Seviye Bazlı Eğitimler");
        if (_showLevelList)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelTutorials"), true);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
