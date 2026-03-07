using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Progress Ayarları")]
    [Tooltip("Her level tamamlandığında artacak yüzde miktarı")]
    public int progressPerLevel = 10;

    [Tooltip("%100 dolunca hangi levelden itibaren yeni mechanic gelsin")]
    public int newMechanicStartLevelIndex = 10;

    // PlayerPrefs key'leri
    private const string PROGRESS_KEY = "TotalProgress";
    private const string MECHANIC_UNLOCKED_KEY = "NewMechanicUnlocked";

    [HideInInspector] public int totalProgress;
    [HideInInspector] public int previousTotalProgress; // Artıştan önceki değer, panel animasyonu için
    [HideInInspector] public bool newMechanicUnlocked;

    // Aynı level içinde çift tetiklenmeyi önler
    private bool levelCompleting = false;

    // Level tamamlandığında tetiklenecek event
    public static UnityEvent OnLevelCompleted = new UnityEvent();

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
        totalProgress = PlayerPrefs.GetInt(PROGRESS_KEY, 0);
        newMechanicUnlocked = PlayerPrefs.GetInt(MECHANIC_UNLOCKED_KEY, 0) == 1;
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt(PROGRESS_KEY, totalProgress);
        PlayerPrefs.SetInt(MECHANIC_UNLOCKED_KEY, newMechanicUnlocked ? 1 : 0);
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

        // Artıştan önce önceki değeri kaydet (panel animasyonu için)
        previousTotalProgress = totalProgress;

        // Progress artır
        totalProgress += progressPerLevel;

        // %100'e ulaşınca yeni mechanic aç ve sıfırla
        if (totalProgress >= 100)
        {
            if (!newMechanicUnlocked)
            {
                newMechanicUnlocked = true;
                Debug.Log("Yeni Mechanic Açıldı!");
            }
            totalProgress = 0;
        }

        SaveProgress();

        // Panel'i tetikle
        OnLevelCompleted.Invoke();
    }

    /// <summary>
    /// Yeni level başlarken flag'ı sıfırla (GridSpawner.NextLevel() çağırır)
    /// </summary>
    public void ResetLevelState()
    {
        levelCompleting = false;
    }

    /// <summary>
    /// Verilen level index'i için yeni mechanic mi, normal mi diye kontrol eder.
    /// </summary>
    public bool ShouldUseNewMechanic(int levelIndex)
    {
        if (!newMechanicUnlocked) return false;
        // Unlock olduktan sonra her 5 level'da bir yeni mechanic
        return (levelIndex - newMechanicStartLevelIndex) % 5 == 0;
    }

    // --- Debug / Test ---
    [ContextMenu("Progress Sıfırla")]
    public void ResetProgress()
    {
        totalProgress = 0;
        newMechanicUnlocked = false;
        SaveProgress();
        Debug.Log("Progress sıfırlandı.");
    }
}
