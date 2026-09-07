using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Seviye sıralama, akış yönetimi ve Assets/Levels klasörü senkronizasyonu için ortak yardımcı sınıf.
/// </summary>
public static class LevelSequenceHelper
{
    public const string SEQUENCE_PATH = "Assets/LevelSequence.asset";
    public const string LEVELS_FOLDER = "Assets/Levels";

    public static LevelSequenceData GetOrCreateSequence()
    {
        LevelSequenceData seq = AssetDatabase.LoadAssetAtPath<LevelSequenceData>(SEQUENCE_PATH);
        if (seq == null)
        {
            seq = ScriptableObject.CreateInstance<LevelSequenceData>();
            AssetDatabase.CreateAsset(seq, SEQUENCE_PATH);
            AssetDatabase.SaveAssets();
        }
        return seq;
    }

    /// <summary>
    /// Assets/Levels klasöründeki tüm geçerli LevelData asset'lerini bulur,
    /// silinmiş olanları temizler, eksik olanları ekler ve doğal sayısal sıraya göre dizer.
    /// </summary>
    public static void SyncAndSortSequence(LevelSequenceData seq = null)
    {
        if (seq == null) seq = GetOrCreateSequence();
        if (seq == null) return;

        Undo.RecordObject(seq, "Seviyeleri Numaraya Göre Sırala ve Eşitle");

        string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LEVELS_FOLDER });
        List<LevelData> foundLevels = new List<LevelData>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelData ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (ld != null && !foundLevels.Contains(ld))
            {
                foundLevels.Add(ld);
            }
        }

        // Doğal sayısal sıralama (Level_01, Level_02, Level_10...)
        foundLevels.Sort((a, b) =>
        {
            int numA = ExtractLevelNumber(a != null ? a.name : "");
            int numB = ExtractLevelNumber(b != null ? b.name : "");
            if (numA != numB) return numA.CompareTo(numB);
            return string.Compare(a != null ? a.name : "", b != null ? b.name : "", System.StringComparison.OrdinalIgnoreCase);
        });

        seq.levels = foundLevels;
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
    }

    public static int ExtractLevelNumber(string name)
    {
        if (string.IsNullOrEmpty(name)) return int.MaxValue;
        Match match = Regex.Match(name, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int num))
        {
            return num;
        }
        return int.MaxValue;
    }

    public static void MoveLevel(LevelSequenceData seq, int fromIndex, int toIndex)
    {
        if (seq == null || seq.levels == null) return;
        if (fromIndex < 0 || fromIndex >= seq.levels.Count) return;
        if (toIndex < 0 || toIndex >= seq.levels.Count) return;
        if (fromIndex == toIndex) return;

        Undo.RecordObject(seq, "Seviye Sırasını Değiştir");
        LevelData item = seq.levels[fromIndex];
        seq.levels.RemoveAt(fromIndex);
        seq.levels.Insert(toIndex, item);
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
    }

    public static int GetLevelIndex(LevelSequenceData seq, LevelData level)
    {
        if (seq == null || seq.levels == null || level == null) return -1;
        return seq.levels.IndexOf(level);
    }

    public static bool AddLevel(LevelSequenceData seq, LevelData level, int targetIndex = -1)
    {
        if (seq == null || level == null) return false;
        if (seq.levels == null) seq.levels = new List<LevelData>();
        if (seq.levels.Contains(level)) return false;

        Undo.RecordObject(seq, "Seviyeyi Sıraya Ekle");
        if (targetIndex >= 0 && targetIndex <= seq.levels.Count)
            seq.levels.Insert(targetIndex, level);
        else
            seq.levels.Add(level);

        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
        return true;
    }

    public static bool RemoveLevel(LevelSequenceData seq, LevelData level)
    {
        if (seq == null || seq.levels == null || level == null) return false;
        if (!seq.levels.Contains(level)) return false;

        Undo.RecordObject(seq, "Seviyeyi Sıradan Çıkar");
        seq.levels.Remove(level);
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
        return true;
    }

    public static void ShiftLevel(LevelSequenceData seq, LevelData level, int delta)
    {
        if (seq == null || seq.levels == null || level == null) return;
        int idx = seq.levels.IndexOf(level);
        if (idx < 0) return;
        int newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= seq.levels.Count) return;
        MoveLevel(seq, idx, newIdx);
    }
}

