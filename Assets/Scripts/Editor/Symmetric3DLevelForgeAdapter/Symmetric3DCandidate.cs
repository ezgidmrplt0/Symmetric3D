// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Aday (TCandidate)
//  LevelForge Setup Wizard İSKELET olarak üretti; bu proje için elle dolduruldu (bkz.
//  Symmetric3DDifficultyEvaluator başındaki heuristik notu). Bkz.
//  Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md.
// ═══════════════════════════════════════════════════════════════════

// Bir "aday", var olan (ya da henüz kaydedilmemiş bir kopyası olan) bir LevelData asset'ini
// sarar. LevelForge.DifficultySearchEngine bu tipin içeriğini hiç bilmez, sadece opak bir
// TCandidate olarak taşır — Symmetric3DDifficultyEvaluator onu somutlaştırır.
public class Symmetric3DCandidate
{
    public LevelData levelAsset;
}
