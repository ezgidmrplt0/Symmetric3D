using UnityEngine;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeFirebase();
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                Debug.Log("[Firebase] Başarıyla başlatıldı.");
            }
            else
            {
                Debug.LogError("[Firebase] Bağımlılık hatası: " + task.Result);
            }
        });
    }

    // --- Analytics Event Metodları ---

    public void LogLevelStart(int levelIndex)
    {
        FirebaseAnalytics.LogEvent("level_start",
            new Parameter("level_index", levelIndex));
    }

    public void LogLevelComplete(int levelIndex, float durationSeconds, float timeRemaining = 0f)
    {
        FirebaseAnalytics.LogEvent("level_complete",
            new Parameter("level_index", levelIndex),
            new Parameter("duration_seconds", durationSeconds),
            new Parameter("time_remaining", timeRemaining));
    }

    public void LogLevelFail(int levelIndex, float durationSeconds)
    {
        FirebaseAnalytics.LogEvent("level_fail",
            new Parameter("level_index", levelIndex),
            new Parameter("duration_seconds", durationSeconds));
    }

    // Kullanıcının şu an hangi levelde olduğunu Firebase'e kaydeder.
    // app_remove eventinde bu değere bakarak "kaçıncı leveldeyken sildi" görülür.
    public void SetCurrentLevel(int levelIndex)
    {
        FirebaseAnalytics.SetUserProperty("current_level", levelIndex.ToString());
    }

    // Fail panelinde "Retry" butonuna basıldığında çağrılır.
    // Hangi level en çok tekrar deneniyor = o level çok zor demek.
    public void LogLevelRetry(int levelIndex)
    {
        FirebaseAnalytics.LogEvent("level_retry",
            new Parameter("level_index", levelIndex));
    }

    // Oyun içi reset butonuna basıldığında çağrılır.
    // Level ortasında sıfırlama = kullanıcı takıldı demek.
    public void LogLevelReset(int levelIndex)
    {
        FirebaseAnalytics.LogEvent("level_reset",
            new Parameter("level_index", levelIndex));
    }

    // Toplam tamamlanan level sayısını user property olarak kaydeder.
    // Segment analizi için: "10 leveli tamamlayanlar" vs "1 leveli tamamlayanlar" kıyaslaması.
    public void SetTotalLevelsCompleted(int count)
    {
        FirebaseAnalytics.SetUserProperty("total_levels_completed", count.ToString());
    }
}
