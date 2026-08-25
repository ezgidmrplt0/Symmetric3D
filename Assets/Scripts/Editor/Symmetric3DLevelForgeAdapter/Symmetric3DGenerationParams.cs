// ═══════════════════════════════════════════════════════════════════
//  SYMMETRIC3D LEVELFORGE ADAPTER — Üretim Parametreleri (TParams)
//  LevelForge Setup Wizard tarafından üretildi (Packages/com.fogboundgames.levelforge/
//  Editor/SetupWizard/LevelForgeAdapterCodeGenerator.cs). Elle düzenleyebilirsin —
//  sihirbazı TEKRAR çalıştırırsan bu dosyanın üzerine yazılır, elle eklediğin
//  değişiklikler kaybolur. Bkz. Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md.
// ═══════════════════════════════════════════════════════════════════

public struct Symmetric3DGenerationParams
{
    // Levelde kaç sıvı parçası olacağı
    public int pieceCount;
    // Kaç farklı temel renk kullanılacağı
    public int distinctColorCount;
    // Bir parçanın dolması için gereken dilim sayısı (yüksek = daha çok transfer gerekir)
    public int maxSlices;
    // Rotation mekaniği açık mı (LevelData.LevelType.Rotation)
    public bool rotationEnabled;
    // Parçaların ne kadarının birbirine bağlı (linkId>0) olacağı
    public float linkedRatio;

    public override string ToString()
        => $"pieceCount={pieceCount} distinctColorCount={distinctColorCount} maxSlices={maxSlices} rotationEnabled={rotationEnabled} linkedRatio={linkedRatio}";
}
