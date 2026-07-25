// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Parametre Mutasyonu (IParameterSpace) — mekanik, ÇALIŞIR haldedir
//  LevelForge Setup Wizard tarafından üretildi (Packages/com.fogboundgames.levelforge/
//  Editor/SetupWizard/LevelForgeAdapterCodeGenerator.cs). Elle düzenleyebilirsin —
//  sihirbazı TEKRAR çalıştırırsan bu dosyanın üzerine yazılır, elle eklediğin
//  değişiklikler kaybolur. Bkz. Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md.
// ═══════════════════════════════════════════════════════════════════

using UnityEngine;
using LevelForge;

// Bu dosya (Candidate/Evaluator'ın aksine) TAM ÇALIŞIR bir strateji üretir: her sayısal
// alanı, aralığının %10'u kadar hedefe doğru (skor çok yüksekse azalt, çok düşükse artır)
// kaydırıp sınırlar içinde tutar. Bu JENERİK bir strateji — hangi alanın HANGİ
// FailureReasonCode'a nasıl tepki vermesi gerektiğini SEN daha iyi bilirsin; gerekirse
// hint.reason'a göre özelleştir (bkz. BlockMerge3D örneğindeki BlockMerge3DParameterSpace).
public class Symmetric3DParameterSpace : IParameterSpace<Symmetric3DGenerationParams>
{
    public Symmetric3DGenerationParams Mutate(Symmetric3DGenerationParams current, MutationHint hint, System.Random rng)
    {
        var p = current;
        // TooEasy ⇒ zorluğu artıracak yönde (+1), TooHard/Invalid ⇒ azaltacak yönde (-1).
        float direction = hint.direction == MutationDirection.TooEasy ? 1f : -1f;

        p.pieceCount = Mathf.RoundToInt(Mathf.Clamp(p.pieceCount + direction * 3.6f, 4f, 40f));
        p.distinctColorCount = Mathf.RoundToInt(Mathf.Clamp(p.distinctColorCount + direction * 0.5f, 1f, 6f));
        p.maxSlices = Mathf.RoundToInt(Mathf.Clamp(p.maxSlices + direction * 0.3f, 1f, 4f));
        // 'rotationEnabled' bool — yön bilgisi anlamlı değil, deneme indeksine göre keşif amaçlı değiştirilir.
        if (hint.attemptIndex % 2 == 1) p.rotationEnabled = !p.rotationEnabled;
        // 'colorMixEnabled' bool — yön bilgisi anlamlı değil, deneme indeksine göre keşif amaçlı değiştirilir.
        if (hint.attemptIndex % 2 == 1) p.colorMixEnabled = !p.colorMixEnabled;
        p.linkedRatio = Mathf.Clamp(p.linkedRatio + direction * 0.05f, 0f, 0.5f);
        p.shadowTriggerCount = Mathf.RoundToInt(Mathf.Clamp(p.shadowTriggerCount + direction * 0.6f, 0f, 6f));

        return p;
    }
}
