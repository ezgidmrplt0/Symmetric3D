using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Progress Ayarları")]
    [Tooltip("Her level tamamlandığında artacak yüzde miktarı")]
    public int progressPerLevel = 20;

    [Tooltip("%100 dolunca hangi levelden itibaren yeni mechanic gelsin")]
    public int newMechanicStartLevelIndex = 10;

    private const string PROGRESS_KEY          = "TotalProgress";
    private const string LIFETIME_PROGRESS_KEY  = "LifetimeProgress";
    private const string MECHANIC_UNLOCKED_KEY  = "NewMechanicUnlocked";
    private const string LEVEL_COMPLETING_KEY   = "LevelCompleting";

    [HideInInspector] public int totalProgress;         // 0-100 döngüsel (bar animasyonu için)
    [HideInInspector] public int previousTotalProgress; // Artıştan önceki değer
    [HideInInspector] public int lifetimeProgress;      // Hiç sıfırlanmayan birikimli sayı
    [HideInInspector] public bool newMechanicUnlocked;

    // Duyurulan (popup çıkan) level türlerini sakla
    private string announcedTypesList = ""; 
    private const string ANNOUNCED_TYPES_KEY = "AnnouncedLevelTypes";

    // Aynı level içinde çift tetiklenmeyi önler
    private bool levelCompleting = false;

    [HideInInspector] public bool hitProgressHundred;   // Bu level'da 100% barajı aşıldı mı?

    public bool IsLevelCompleting => levelCompleting;

    // Level durum eventleri
    public static UnityEvent OnLevelCompleted = new UnityEvent();
    public static UnityEvent OnLevelFailed    = new UnityEvent();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadProgress();
    }

    void LoadProgress()
    {
        totalProgress    = PlayerPrefs.GetInt(PROGRESS_KEY, 0);
        lifetimeProgress = PlayerPrefs.GetInt(LIFETIME_PROGRESS_KEY, 0);
        newMechanicUnlocked = PlayerPrefs.GetInt(MECHANIC_UNLOCKED_KEY, 0) == 1;
        announcedTypesList = PlayerPrefs.GetString(ANNOUNCED_TYPES_KEY, "");
        levelCompleting = PlayerPrefs.GetInt(LEVEL_COMPLETING_KEY, 0) == 1;
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt(PROGRESS_KEY, totalProgress);
        PlayerPrefs.SetInt(LIFETIME_PROGRESS_KEY, lifetimeProgress);
        PlayerPrefs.SetInt(MECHANIC_UNLOCKED_KEY, newMechanicUnlocked ? 1 : 0);
        PlayerPrefs.SetString(ANNOUNCED_TYPES_KEY, announcedTypesList);
        PlayerPrefs.SetInt(LEVEL_COMPLETING_KEY, levelCompleting ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Bir level tamamlandığında çağrılır. Progress'i günceller ve event tetikler.
    /// </summary>
    public void LevelComplete()
    {
        // Aynı level içinde birden fazla tetiklenmeyi önle
        if (levelCompleting) return;
        levelCompleting = true;

        // Artıştan önce önceki değeri kaydet
        previousTotalProgress = totalProgress;

        // Birikimli sayıç (hiç sıfırlanmaz) — unlock için kullanılır
        lifetimeProgress += progressPerLevel;

        // Bu level'da 100 barajı aşıldı mı?
        hitProgressHundred = (previousTotalProgress + progressPerLevel) >= 100;

        // Görsel bar için döngüsel 0-100
        totalProgress += progressPerLevel;
        if (totalProgress >= 100)
            totalProgress -= 100;

        // newMechanicUnlocked artık lifetimeProgress >= 100 ile tetiklenir
        if (lifetimeProgress >= 100 && !newMechanicUnlocked)
        {
            newMechanicUnlocked = true;
        }

        SaveProgress();

        // Panel'i tetikle
        OnLevelCompleted.Invoke();
    }

    /// <summary>
    /// Level başarısız olduğunda (hamle kalmadığında) çağrılır.
    /// </summary>
    public void LevelFail()
    {
        if (levelCompleting) return;
        levelCompleting = true;

        OnLevelFailed.Invoke();
    }

    /// <summary>
    /// Yeni level başlarken flag'ı sıfırla (GridSpawner.NextLevel() çağırır)
    /// </summary>
    public void ResetLevelState()
    {
        levelCompleting = false;
        hitProgressHundred = false;
        PlayerPrefs.SetInt(LEVEL_COMPLETING_KEY, 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Verilen level index'i için yeni mechanic mi, normal mi diye kontrol eder.
    /// </summary>
    /// <summary>
    /// Bu level tipi için daha önce popup gösterildi mi?
    /// </summary>
    public bool WasTypeAnnounced(LevelData.LevelType type)
    {
        return announcedTypesList.Contains(type.ToString());
    }

    /// <summary>
    /// Bu tipi duyurulmuşlar listesine ekle.
    /// </summary>
    public void MarkTypeAsAnnounced(LevelData.LevelType type)
    {
        if (!WasTypeAnnounced(type))
        {
            announcedTypesList += type.ToString() + ",";
            SaveProgress();
        }
    }

    /// <summary>
    /// Verilen progress ile yeni bir türün kilidi açıldı mı kontrol eder.
    /// </summary>
    public bool GetTypeForProgress(int progress, out LevelData.LevelType unlockedType)
    {
        unlockedType = LevelData.LevelType.Classic;
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.sequence == null) return false;

        // En yüksek unlockAtProgress >= progress olanı bul (ama progress barajını aşmamış olmalı)
        // Ya da daha basit: progress tam bir 100 katı ise o katın mekaniğini bul.
        int roundedProgress = (progress / 100) * 100; 

        foreach (var config in spawner.sequence.typeConfigs)
        {
            if (config.unlockAtProgress == roundedProgress)
            {
                unlockedType = config.levelType;
                return true;
            }
        }
        
        // Eğer o 100'lük dilim için özel bir mekanik yoksa, en son açılanı gösterelim?
        // Veya sadece false dönelim (popup çıkmasın ama kullanıcı her 100'de çıksın diyor)
        return false;
    }

    /// <summary>
    /// Bu level tamamlanmadan önce açılmamış mekanik var mıydı?
    /// Bar ve unlock popup yalnızca true ise gösterilmeli.
    /// </summary>
    /// Henüz açılmamış, en yakın eşik değerine sahip mekaniği döner.
    public bool GetNextMechanicToUnlock(out LevelData.LevelType nextType)
    {
        nextType = LevelData.LevelType.Classic;
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.sequence == null) return false;

        int lowestThreshold = int.MaxValue;
        bool found = false;
        foreach (var cfg in spawner.sequence.typeConfigs)
        {
            if (cfg.unlockAtProgress > 0 && cfg.unlockAtProgress > lifetimeProgress
                && cfg.unlockAtProgress < lowestThreshold)
            {
                lowestThreshold = cfg.unlockAtProgress;
                nextType = cfg.levelType;
                found = true;
            }
        }
        return found;
    }

    public bool HadMechanicsToUnlock()
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.sequence == null) return false;
        int progressBefore = lifetimeProgress - progressPerLevel;
        foreach (var cfg in spawner.sequence.typeConfigs)
        {
            if (cfg.unlockAtProgress > 0 && cfg.unlockAtProgress > progressBefore)
                return true;
        }
        return false;
    }

    public bool ShouldUseNewMechanic(int levelIndex)
    {
        if (!newMechanicUnlocked) return false;
        // Unlock olduktan sonra her 5 level'da bir yeni mechanic
        return (levelIndex - newMechanicStartLevelIndex) % 5 == 0;
    }

    [ContextMenu("Progress Sıfırla")]
    public void ResetProgress()
    {
        totalProgress = 0;
        lifetimeProgress = 0;
        newMechanicUnlocked = false;
        announcedTypesList = "";
        PlayerPrefs.DeleteKey("CurrentLevelIndex");
        PlayerPrefs.DeleteKey(ANNOUNCED_TYPES_KEY);
        SaveProgress();
    }
}
