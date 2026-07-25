// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Zorluk Seviyeleri (DifficultyTier fabrikası) — mekanik, ÇALIŞIR haldedir
//  LevelForge Setup Wizard tarafından üretildi (Packages/com.fogboundgames.levelforge/
//  Editor/SetupWizard/LevelForgeAdapterCodeGenerator.cs). Elle düzenleyebilirsin —
//  sihirbazı TEKRAR çalıştırırsan bu dosyanın üzerine yazılır, elle eklediğin
//  değişiklikler kaybolur. Bkz. Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md.
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;
using LevelForge;

public static class Symmetric3DDifficultyTiers
{
    private static readonly Dictionary<string, DifficultyTier> cache = new Dictionary<string, DifficultyTier>();

    public static DifficultyTier GetTier(string tierName)
    {
        if (cache.TryGetValue(tierName, out var existing) && existing != null) return existing;

        float targetScore;
        float tolerance;
        switch (tierName)
        {
            case "Kolay": targetScore = 0.15f; tolerance = 0.08f; break;
            case "Orta": targetScore = 0.4f; tolerance = 0.08f; break;
            case "Zor": targetScore = 0.65f; tolerance = 0.08f; break;
            case "Uzman": targetScore = 0.85f; tolerance = 0.08f; break;
            default:
                Debug.LogWarning($"{nameof(Symmetric3DDifficultyTiers)}: bilinmeyen tier adı '{tierName}', varsayılan (0.5, 0.1) kullanılıyor.");
                targetScore = 0.5f; tolerance = 0.1f;
                break;
        }

        var tier = ScriptableObject.CreateInstance<DifficultyTier>();
        tier.tierName = tierName;
        tier.targetScore = targetScore;
        tier.scoreTolerance = tolerance;

        cache[tierName] = tier;
        return tier;
    }
}
