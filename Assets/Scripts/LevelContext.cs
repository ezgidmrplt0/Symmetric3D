/// <summary>
/// Bir analitik olayı anındaki oynanış bağlamı.
///
/// Neden var: eldeki veri "oyuncu Level 2'de 243 kez reset'e bastı" diyordu ama
/// bunun anlamını söyleyemiyordu. Sıfır hamleyle reset (tahtayı anlamadı) ile
/// sekiz hamleyle reset (anladı ama çözemedi) tamamen farklı problemler ve
/// farklı çözümler gerektiriyor. Bu alanlar o ayrımı yapılabilir kılar.
/// </summary>
public struct LevelContext
{
    /// <summary>LevelData.LevelType bayrakları (1=Classic, 2=Rotation, 4=Linked, 8=Frozen).</summary>
    public int levelType;

    /// <summary>Bu denemede yapılan başarılı parça yerleştirme sayısı.</summary>
    public int movesMade;

    /// <summary>Bu denemede yapılan döndürme sayısı — mekaniğin keşfedilip
    /// keşfedilmediğini gösterir.</summary>
    public int rotationsUsed;

    /// <summary>Bu denemede tamamlanan eşleşme sayısı — ne kadar yaklaştığı.</summary>
    public int matchesMade;

    /// <summary>Olay anında tahtada kalan aktif parça sayısı.</summary>
    public int piecesRemaining;

    /// <summary>Bu cihazda bu levelin kaçıncı denemesi (1'den başlar).</summary>
    public int attemptNumber;

    /// <summary>Bu levelde tutorial eli gösteriliyor muydu.</summary>
    public bool tutorialShown;
}
