using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LevelSequence", menuName = "Symmetric3D/Level Sequence")]
public class LevelSequenceData : ScriptableObject
{
    // ── Level Türü Açılma Konfigürasyonu ────────────────────────
    [System.Serializable]
    public class LevelTypeConfig
    {
        public LevelData.LevelType levelType;
        [Range(0, 100)]
        [Tooltip("Bu tür kaç % progress'te açılır? 0 = Her zaman açık")]
        public int unlockAtProgress = 0;
    }

    [Header("Level Türü Ayarları")]
    public List<LevelTypeConfig> typeConfigs = new List<LevelTypeConfig>()
    {
        new LevelTypeConfig { levelType = LevelData.LevelType.Classic,     unlockAtProgress = 0   },
        new LevelTypeConfig { levelType = LevelData.LevelType.QuarterFill, unlockAtProgress = 100 },
        new LevelTypeConfig { levelType = LevelData.LevelType.ColorMix,    unlockAtProgress = 100 },
    };

    // ── Sıralı Level Listesi ─────────────────────────────────────
    [Header("Level Sırası")]
    public List<LevelData> levels = new List<LevelData>();

    // ── Yardımcılar ──────────────────────────────────────────────

    /// <summary>
    /// Verilen tür için açılma progress'ini döner.
    /// Config'de yoksa 0 döner.
    /// </summary>
    public int GetUnlockProgress(LevelData.LevelType type)
    {
        foreach (var cfg in typeConfigs)
            if (cfg.levelType == type) return cfg.unlockAtProgress;
        return 0;
    }

    /// <summary>
    /// Mevcut progress ile ilgili level'ın açık olup olmadığını döner.
    /// </summary>
    public bool IsLevelUnlocked(LevelData level, int currentProgress)
    {
        if (level == null) return false;
        return currentProgress >= GetUnlockProgress(level.levelType);
    }
}
