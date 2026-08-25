// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Zorluk Değerlendirici (IDifficultyEvaluator)
//  LevelForge Setup Wizard İSKELET olarak üretti; bu proje için elle dolduruldu. Bkz.
//  Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md.
// ═══════════════════════════════════════════════════════════════════
//
//  ⚠️ HEURİSTİK — GERÇEK ÇÖZÜCÜ (SOLVER) DEĞİL. BlockMerge3D'nin LevelSolver'ının aksine bu sınıf
//  levelin GERÇEKTEN board'u temizleyip temizleyemeyeceğini ARAMAZ — Symmetric3D'nin transfer
//  kuralları (yüzey-yüzeye bakışma/adjacency, ColorMix zinciri, shadow tetikleyici sırası,
//  LinkedObjectGroup kilitleri) için tam bir move-search çözücü yazmak ayrı, ciddi bir mühendislik
//  işidir (bkz. LevelForge Setup Wizard doğrulama turu, kullanıcı kararı: "heuristik yeterli").
//  Bunun yerine, LevelData'dan ÖLÇÜLEBİLİR yapısal özellikler (parça sayısı, renk/tarif çeşitliliği,
//  aktif mekanik sayısı, ortalama doldurma ihtiyacı) okunup ağırlıklı bir zorluk TAHMİNİ üretilir.
//  Bu skor "levelin muhtemelen ne kadar karmaşık olduğunu" yansıtır, "kesinlikle çözülebilir
//  olduğunu" DEĞİL — LevelForge'un Zorunlu Koruma Kuralı tarzı bir export-engelleme garantisi
//  burada YOKTUR.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LevelForge;

public class Symmetric3DDifficultyEvaluator : IDifficultyEvaluator<Symmetric3DCandidate>
{
    // LiquidTransfer.maxSlices'ın prefab varsayılanı — LevelData.PieceData bu değeri saklamıyor
    // (sadece başlangıç currentSlices'ını saklıyor), bu yüzden "ortalama kaç dilim daha gerekiyor"
    // hesabı bu sabite göre YAKLAŞIK yapılıyor. Projendeki gerçek prefab varsayılanı değişirse burayı
    // güncelle.
    private const int AssumedMaxSlices = 4;

    public EvaluationResult Evaluate(Symmetric3DCandidate candidate)
    {
        var level = candidate?.levelAsset;
        if (level == null || level.pieces == null || level.pieces.Count == 0)
        {
            return EvaluationResult.Invalid(FailureReasonCode.InsufficientContent,
                "LevelData null ya da hiç parça içermiyor.", definitive: true);
        }

        var pieces = level.pieces;
        int pieceCount = pieces.Count;

        // Renk toleranslı karşılaştırma — ColorMixData.ColorsMatch ile aynı kural (gerçek oyunun
        // birleşme/karışım kontrolüyle birebir tutarlı olsun diye kendi eşitliğimizi icat etmiyoruz).
        var distinctColors = new List<Color>();
        foreach (var p in pieces)
        {
            if (!distinctColors.Any(c => ColorMixData.ColorsMatch(c, p.liquidColor)))
                distinctColors.Add(p.liquidColor);
        }

        float colorComplexity = distinctColors.Count;

        int activeMechanicFlags = 0;
        if (level.levelType.HasFlag(LevelData.LevelType.Rotation)) activeMechanicFlags++;
        if (level.levelType.HasFlag(LevelData.LevelType.Linked)) activeMechanicFlags++;

        int linkedCount = pieces.Count(p => p.linkId > 0);
        float linkedRatio = pieceCount > 0 ? (float)linkedCount / pieceCount : 0f;

        float mechanicComplexity = activeMechanicFlags + linkedRatio * 3f
            + (level.boardMode == LevelData.BoardMode.Shape3D ? 1f : 0f);

        float avgSlicesNeeded = pieceCount > 0
            ? (float)pieces.Sum(p => Mathf.Max(0, AssumedMaxSlices - p.currentSlices)) / pieceCount
            : 0f;

        var metrics = new Dictionary<string, float>
        {
            { "pieceCount", pieceCount },
            { "colorComplexity", colorComplexity },
            { "mechanicComplexity", mechanicComplexity },
            { "avgSlicesNeeded", avgSlicesNeeded }
        };

        // Ağırlıklı normalize toplam — bkz. BlockMerge3D'deki LevelSolver.CalculateDifficulty ile
        // aynı desen (her metrik makul bir üst sınıra göre 0-1'e normalize edilip ağırlıklı toplanır).
        float normPieceCount = Mathf.Clamp01(pieceCount / 30f);         // 30 parça ≈ üst sınır
        float normColorComplexity = Mathf.Clamp01(colorComplexity / 8f); // ~8 = 6 renk + birkaç mix çifti
        float normMechanic = Mathf.Clamp01(mechanicComplexity / 6f);
        float normSlices = Mathf.Clamp01(avgSlicesNeeded / 3f);

        float score = 0.30f * normPieceCount + 0.25f * normColorComplexity + 0.30f * normMechanic + 0.15f * normSlices;

        return EvaluationResult.Valid(Mathf.Clamp01(score), metrics);
    }
}
