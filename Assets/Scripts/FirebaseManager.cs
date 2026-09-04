using UnityEngine;
using System;
using System.Collections.Generic;
#if ENABLE_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

/// <summary>
/// Firebase Analytics + Crashlytics köprüsü.
///
/// Firebase init'i asenkron olduğu için (CheckAndFixDependenciesAsync) uygulamanın ilk
/// karesinde gelen olaylar SDK hazır olmadan tetikleniyor. Eskiden bunlar "if (!isReady)
/// return;" ile sessizce atılıyordu — yani HER açılışın ilk level_start'ı kayboluyordu.
/// Şimdi hazır olana kadar kuyruğa alınıp init biter bitmez sırayla gönderiliyor.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;

    private bool isReady = false;

    /// <summary>SDK hazır olana kadar bekleyen olaylar.</summary>
    private readonly List<Action> pendingEvents = new List<Action>();
    private const int MAX_PENDING_EVENTS = 64;

    // Aktif level ve oturum için zaman takibi (level_quit için)
    private int   activeLevel        = -1;
    private float levelStartRealtime = 0f;
    private bool  levelActive        = false;

    // Oturum bazlı sayaçlar
    private float sessionStartRealtime = 0f;
    private int   sessionLevelsPlayed  = 0;

    // Arka plan takibi — Time.realtimeSinceStartup arka planda da işlediği için
    // uygulama dışında geçen süre level ve oturum sürelerinden düşülür.
    private float pauseStartRealtime = 0f;
    private bool  isPaused           = false;

    // Awake sırası garanti olmadığı için FirestoreAnalytics.Instance henüz atanmamış
    // olabilir; ilk erişimde sahneden çözülür ve önbelleğe alınır.
    private FirestoreAnalytics firestoreCache;
    private FirestoreAnalytics Firestore
    {
        get
        {
            if (firestoreCache == null)
            {
                firestoreCache = FirestoreAnalytics.Instance != null
                    ? FirestoreAnalytics.Instance
                    : FindObjectOfType<FirestoreAnalytics>();
            }
            return firestoreCache;
        }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeFirebase();
    }

    void InitializeFirebase()
    {
#if ENABLE_FIREBASE
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                sessionStartRealtime = Time.realtimeSinceStartup;

                // Firestore analitiğini başlat, sonra bekleyen olayları boşalt
                Firestore?.Initialize();
                MarkReady();

                Debug.Log("[Firebase] Başarıyla başlatıldı.");
            }
            else
            {
                Debug.LogError("[Firebase] Bağımlılık hatası: " + task.Result);
                pendingEvents.Clear();
            }
        });
#else
        Debug.Log("[Firebase] ENABLE_FIREBASE tanımlı değil veya Firebase SDK yüklü değil. Mock modunda çalışıyor.");
        sessionStartRealtime = Time.realtimeSinceStartup;
        Firestore?.Initialize();
        MarkReady();
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // OLAY KUYRUĞU
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// SDK hazır değilse olayı kuyruğa alır ve true döner (çağıran metot çıkmalıdır).
    /// </summary>
    private bool Defer(Action action)
    {
        if (isReady) return false;

        if (pendingEvents.Count < MAX_PENDING_EVENTS)
            pendingEvents.Add(action);
        else
            Debug.LogWarning("[Firebase] Olay kuyruğu doldu, olay atlandı.");

        return true;
    }

    private void MarkReady()
    {
        isReady = true;

        if (pendingEvents.Count > 0)
            Debug.Log("[Firebase] " + pendingEvents.Count + " bekleyen olay gönderiliyor.");

        // Replay sırasında kuyruğa yeni olay eklenmesin diye kopya üzerinden ilerle
        var queued = new List<Action>(pendingEvents);
        pendingEvents.Clear();

        foreach (var action in queued)
        {
            try { action(); }
            catch (Exception e) { Debug.LogWarning("[Firebase] Bekleyen olay gönderilemedi: " + e.Message); }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // LEVEL OLAYLARI
    // ─────────────────────────────────────────────────────────────

    public void LogLevelStart(int levelIndex)
    {
        // Yerel durum SDK'dan bağımsız olarak hemen güncellenir —
        // aksi halde init sırasında başlayan level "aktif değil" sanılır.
        activeLevel        = levelIndex;
        levelStartRealtime = Time.realtimeSinceStartup;
        levelActive        = true;
        sessionLevelsPlayed++;

        if (Defer(() => SendLevelStart(levelIndex))) return;
        SendLevelStart(levelIndex);
    }

    private void SendLevelStart(int levelIndex)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_start",
            new Parameter("level_index",         levelIndex),
            new Parameter("session_level_count", sessionLevelsPlayed));
#endif
        Firestore?.LogLevelStart(levelIndex);
    }

    public void LogLevelComplete(int levelIndex, float durationSeconds, float timeRemaining = 0f)
    {
        levelActive = false;

        if (Defer(() => SendLevelComplete(levelIndex, durationSeconds, timeRemaining))) return;
        SendLevelComplete(levelIndex, durationSeconds, timeRemaining);
    }

    private void SendLevelComplete(int levelIndex, float durationSeconds, float timeRemaining)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_complete",
            new Parameter("level_index",      levelIndex),
            new Parameter("duration_seconds", durationSeconds),
            new Parameter("time_remaining",   timeRemaining));
#endif
        Firestore?.LogLevelComplete(levelIndex, durationSeconds);
    }

    public void LogLevelFail(int levelIndex, float durationSeconds)
    {
        levelActive = false;

        if (Defer(() => SendLevelFail(levelIndex, durationSeconds))) return;
        SendLevelFail(levelIndex, durationSeconds);
    }

    private void SendLevelFail(int levelIndex, float durationSeconds)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_fail",
            new Parameter("level_index",      levelIndex),
            new Parameter("duration_seconds", durationSeconds));
#endif
        Firestore?.LogLevelFail(levelIndex, durationSeconds);
    }

    public void LogLevelRetry(int levelIndex)
    {
        levelStartRealtime = Time.realtimeSinceStartup;
        levelActive        = true;

        if (Defer(() => SendLevelRetry(levelIndex))) return;
        SendLevelRetry(levelIndex);
    }

    private void SendLevelRetry(int levelIndex)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_retry", new Parameter("level_index", levelIndex));
#endif
        Firestore?.LogLevelRetry(levelIndex);
    }

    public void LogLevelReset(int levelIndex)
    {
        levelStartRealtime = Time.realtimeSinceStartup;
        levelActive        = true;

        if (Defer(() => SendLevelReset(levelIndex))) return;
        SendLevelReset(levelIndex);
    }

    private void SendLevelReset(int levelIndex)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_reset", new Parameter("level_index", levelIndex));
#endif
        Firestore?.LogLevelReset(levelIndex);
    }

    /// <summary>
    /// Kullanıcı level ortasında uygulamayı kapattığında tetiklenir.
    /// </summary>
    private void LogLevelQuit(int levelIndex, float durationSeconds)
    {
        if (levelIndex < 0) return;

        if (Defer(() => SendLevelQuit(levelIndex, durationSeconds))) return;
        SendLevelQuit(levelIndex, durationSeconds);
    }

    private void SendLevelQuit(int levelIndex, float durationSeconds)
    {
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("level_quit",
            new Parameter("level_index",      levelIndex),
            new Parameter("duration_seconds", durationSeconds));
#endif
        Firestore?.LogLevelQuit(levelIndex, durationSeconds);
    }

    // ─────────────────────────────────────────────────────────────
    // USER PROPERTIES
    // ─────────────────────────────────────────────────────────────

    public void SetCurrentLevel(int levelIndex)
    {
        if (Defer(() => SetCurrentLevel(levelIndex))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.SetUserProperty("current_level", levelIndex.ToString());
        FirebaseAnalytics.SetUserProperty("last_level_quit", levelIndex.ToString());
#endif
    }

    public void SetTotalLevelsCompleted(int count)
    {
        if (Defer(() => SetTotalLevelsCompleted(count))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.SetUserProperty("total_levels_completed", count.ToString());
#endif
    }

    public void SetFarthestLevel(int levelIndex)
    {
        if (Defer(() => SetFarthestLevel(levelIndex))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.SetUserProperty("farthest_level_reached", levelIndex.ToString());
#endif
    }

    public void SetTotalPlayTimeMinutes(int minutes)
    {
        if (Defer(() => SetTotalPlayTimeMinutes(minutes))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.SetUserProperty("total_play_time_minutes", minutes.ToString());
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // ATTRIBUTION & BUSINESS EVENTS
    // ─────────────────────────────────────────────────────────────

    public void SetAttribution(string source, string medium = "organic", string campaign = "direct")
    {
        Firestore?.SetAttribution(source, medium, campaign);
        if (Defer(() => SetAttribution(source, medium, campaign))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.SetUserProperty("attr_source", source);
        FirebaseAnalytics.SetUserProperty("attr_medium", medium);
        FirebaseAnalytics.SetUserProperty("attr_campaign", campaign);
#endif
    }

    public void LogBusinessEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        Firestore?.LogBusinessEvent(eventName, parameters);
        if (Defer(() => SendBusinessEvent(eventName, parameters))) return;
        SendBusinessEvent(eventName, parameters);
    }

    private void SendBusinessEvent(string eventName, Dictionary<string, object> parameters)
    {
#if ENABLE_FIREBASE
        if (parameters != null && parameters.Count > 0)
        {
            var pList = new List<Parameter>();
            foreach (var kvp in parameters)
            {
                if (kvp.Value is long l) pList.Add(new Parameter(kvp.Key, l));
                else if (kvp.Value is int i) pList.Add(new Parameter(kvp.Key, i));
                else if (kvp.Value is double d) pList.Add(new Parameter(kvp.Key, d));
                else if (kvp.Value is float f) pList.Add(new Parameter(kvp.Key, f));
                else pList.Add(new Parameter(kvp.Key, kvp.Value?.ToString() ?? ""));
            }
            FirebaseAnalytics.LogEvent(eventName, pList.ToArray());
        }
        else
        {
            FirebaseAnalytics.LogEvent(eventName);
        }
#endif
    }

    public void LogRegistration(string method = "device_id")
    {
        Firestore?.LogRegistration(method);
        if (Defer(() => LogRegistration(method))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventSignUp,
            new Parameter(FirebaseAnalytics.ParameterSignUpMethod, method));
#endif
    }

    public void LogLogin(string method = "auto")
    {
        Firestore?.LogLogin(method);
        if (Defer(() => LogLogin(method))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin,
            new Parameter(FirebaseAnalytics.ParameterMethod, method));
#endif
    }

    public void LogCampaignView(string campaignId, string placement = "app_open")
    {
        Firestore?.LogCampaignView(campaignId, placement);
        if (Defer(() => LogCampaignView(campaignId, placement))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("campaign_view",
            new Parameter("campaign_id", campaignId),
            new Parameter("placement", placement));
#endif
    }

    public void LogConversion(string conversionType, double value = 0.0, string currency = "TRY")
    {
        Firestore?.LogConversion(conversionType, value, currency);
        if (Defer(() => LogConversion(conversionType, value, currency))) return;
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("conversion",
            new Parameter("conversion_type", conversionType),
            new Parameter("value", value),
            new Parameter("currency", currency));
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // UYGULAMA ARKA PLAN / KAPATMA
    // ─────────────────────────────────────────────────────────────

    void OnApplicationPause(bool pauseStatus)
    {
        if (!isReady) return;

        if (pauseStatus)
        {
            pauseStartRealtime = Time.realtimeSinceStartup;
            isPaused           = true;

            // Uygulama arka plana alındı
            if (levelActive && activeLevel >= 0)
            {
                float timeOnLevel = Time.realtimeSinceStartup - levelStartRealtime;
                LogLevelQuit(activeLevel, timeOnLevel);
                levelActive = false;
            }

            // Oturum süresi kaydı
            float sessionDuration = Time.realtimeSinceStartup - sessionStartRealtime;
#if ENABLE_FIREBASE
            FirebaseAnalytics.LogEvent("session_end",
                new Parameter("duration_seconds",  sessionDuration),
                new Parameter("levels_played",     sessionLevelsPlayed));
#endif
        }
        else
        {
            // Gerçekten arka plana düşülmediyse (bazı platformlar açılışta focus
            // olayı gönderir) sayaçlara dokunma.
            if (!isPaused) return;
            isPaused = false;

            // Arka planda geçen süreyi oturum ve level sayaçlarından düş
            float awaySeconds = Time.realtimeSinceStartup - pauseStartRealtime;
            sessionStartRealtime += awaySeconds;
            levelStartRealtime   += awaySeconds;
        }
    }

    void OnApplicationQuit()
    {
        if (!isReady) return;

        if (levelActive && activeLevel >= 0)
        {
            float timeOnLevel = Time.realtimeSinceStartup - levelStartRealtime;
            LogLevelQuit(activeLevel, timeOnLevel);
        }

        float sessionDuration = Time.realtimeSinceStartup - sessionStartRealtime;
#if ENABLE_FIREBASE
        FirebaseAnalytics.LogEvent("session_end",
            new Parameter("duration_seconds", sessionDuration),
            new Parameter("levels_played",    sessionLevelsPlayed));
#endif
    }
}
