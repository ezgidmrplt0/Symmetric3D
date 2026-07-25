// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Doğrulama
//  Var olan (elle tasarlanmış) Assets/Levels/*.asset dosyalarını Symmetric3DDifficultyEvaluator
//  ile puanlayıp Console'a yazar — üretilen adapter'ın gerçek proje verisi üzerinde çalıştığının
//  somut kanıtı. Bkz. README_NEXT_STEPS.md.
// ═══════════════════════════════════════════════════════════════════

using UnityEditor;
using UnityEngine;

public static class Symmetric3DHeuristicSelfTest
{
    [MenuItem("Symmetric3D/LevelForge/Mevcut Levelleri Heuristik ile Puanla")]
    public static void RunOnExistingLevels()
    {
        var evaluator = new Symmetric3DDifficultyEvaluator();
        var guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Levels" });

        Debug.Log("═══════════════════════════════════════");
        Debug.Log("  SYMMETRIC3D — MEVCUT LEVELLER HEURİSTİK ZORLUK TAHMİNİ");
        Debug.Log($"  ({guids.Length} level bulundu, Assets/Levels altında)");
        Debug.Log("═══════════════════════════════════════");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) continue;

            var candidate = new Symmetric3DCandidate { levelAsset = level };
            var result = evaluator.Evaluate(candidate);

            if (!result.isValid)
            {
                Debug.LogWarning($"{path} → GEÇERSİZ: {result.diagnosticMessage}");
                continue;
            }

            // Symmetric3DDifficultyTiers'daki hedef skorların (Kolay .15 / Orta .40 / Zor .65 /
            // Uzman .85) tam ortası — sadece bu log'da "en yakın hangisi" diye göstermek için,
            // gerçek eşleştirme DifficultyTier.IsScoreWithinTolerance ile yapılır.
            string tierGuess = result.difficultyScore < 0.275f ? "Kolay"
                : result.difficultyScore < 0.525f ? "Orta"
                : result.difficultyScore < 0.75f ? "Zor"
                : "Uzman";

            Debug.Log($"{level.levelDisplayName} ({path}) → skor={result.difficultyScore:F2}, en yakın seviye={tierGuess}, " +
                      $"parça={result.metrics["pieceCount"]:F0}, renk/tarif={result.metrics["colorComplexity"]:F1}, " +
                      $"mekanik={result.metrics["mechanicComplexity"]:F1}, ort.dilim ihtiyacı={result.metrics["avgSlicesNeeded"]:F2}");
        }

        Debug.Log("═══════════════════════════════════════");
    }
}
