using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Symmetric3D/Level Data")]
public class LevelData : ScriptableObject
{
    // ── Level Türleri ────────────────────────────────────────────
    public enum LevelType
    {
        Classic,        // Kaydır-birleştir (her zaman açık)
        QuarterFill,    // Çeyrek dolu obje mekaniği (%100'de açılır)
        ColorMix        // Renk karıştırma — farklı renkler birleşip yeni renk yapar
    }


    // ── Level Meta Bilgileri ─────────────────────────────────────
    [Header("Level Bilgileri")]
    [Tooltip("Level adı (seviye seçim ekranında görünecek)")]
    public string levelDisplayName = "Yeni Level";

    [Tooltip("Bu level'ın mechanic türü")]
    public LevelType levelType = LevelType.Classic;

    // ── Grid Boyutu ──────────────────────────────────────────────
    [Header("Grid Boyutu")]
    public int gridX = 3;
    public int gridY = 3;

    // ── Parçalar ────────────────────────────────────────────────
    [System.Serializable]
    public class PieceData
    {
        public Vector2Int gridPosition;
        public Color liquidColor = Color.white;
        public int currentSlices = 1;
        public float rotationZ = 0f;
    }

    [Header("Parçalar")]
    public List<PieceData> pieces = new List<PieceData>();

    /// <summary>
    /// Mevcut progress ile bu level'ın türünün açık olup olmadığını döner.
    /// </summary>
    public bool IsUnlocked(LevelSequenceData sequence, int currentProgress)
    {
        if (sequence == null) return true;
        return currentProgress >= sequence.GetUnlockProgress(levelType);
    }
}
