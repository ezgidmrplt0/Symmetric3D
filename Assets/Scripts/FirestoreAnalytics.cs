using UnityEngine;
#if ENABLE_FIREBASE
using Firebase.Firestore;
using Firebase.Extensions;
#endif
using System;
using System.Collections.Generic;

/// <summary>
/// Firestore tabanlı derin kullanıcı analitik sistemi.
/// Her kullanıcı için level bazlı tam geçmiş kaydeder.
///
/// Veri yapısı:
///   users/{deviceId}                            → dashboard'un listelediği özet doküman
///   users/{deviceId}/meta/profile               → aynı özetin detaylı kopyası
///   users/{deviceId}/sessions/{id}              → oturum kayıtları
///   users/{deviceId}/level_history/{levelIndex} → level bazlı detay
///
/// Yazma modeli: her yazma SetAsync(MergeAll) ile yapılır. Böylece hedef doküman
/// henüz oluşmamışsa bile yazma kaybolmaz (UpdateAsync bu durumda NOT_FOUND fırlatıp
/// sessizce veri kaybettiriyordu). Okuma yapılmaz — "en uzak level" ve "en iyi süre"
/// gibi karşılaştırma gerektiren değerler PlayerPrefs'te yerel olarak takip edilir.
/// </summary>
public class FirestoreAnalytics : MonoBehaviour
{
    public static FirestoreAnalytics Instance;

    // ── Sabitler ─────────────────────────────────────────────────
    private const string COLLECTION_USERS    = "users";
    private const string COLLECTION_SESSIONS = "sessions";
    private const string COLLECTION_LEVELS   = "level_history";
    private const string DOC_PROFILE         = "profile";

    // Yerel takip anahtarları (Firestore okumasını ortadan kaldırır)
    private const string PREF_INSTALL_DATE = "fs_install_date";
    private const string PREF_FARTHEST     = "fs_farthest_level";
    private const string PREF_LEVEL_SEEN   = "fs_level_seen_";
    private const string PREF_BEST_TIME    = "fs_best_time_";

    /// <summary>Tek bir olaya yazılabilecek en uzun süre. Arka planda geçen zamanın
    /// level süresine sızmasına karşı son savunma hattı.</summary>
    private const double MAX_EVENT_SECONDS = 3600;

    /// <summary>Bu süreden kısa arka plan kesintileri yeni oturum saymaz.</summary>
    private const double NEW_SESSION_AFTER_SECONDS = 30;

    // ── Özel Durum ────────────────────────────────────────────────
#if ENABLE_FIREBASE
    private FirebaseFirestore db;
#endif
    private bool isReady = false;

    private string userId;
    private string sessionId;
    private DateTime sessionStartTime;
    private int levelsPlayedThisSession = 0;

    // Aktif level takibi
    private int currentLevelIndex = -1;
    private DateTime levelStartTime;
    private bool levelActive = false;

    // Arka plan takibi
    private DateTime pausedAt;
    private bool levelActiveBeforePause = false;

    // ─────────────────────────────────────────────────────────────
    // BAŞLANGIÇ
    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// FirebaseManager başarıyla init ettikten sonra bu metodu çağır.
    /// </summary>
    public void Initialize()
    {
#if ENABLE_FIREBASE
        db = FirebaseFirestore.DefaultInstance;
#endif
        userId = SystemInfo.deviceUniqueIdentifier;
        sessionId = NewSessionId();
        sessionStartTime = DateTime.UtcNow;
        isReady = true;

        EnsureUserProfile();
        StartSession();

        Debug.Log("[Firestore] Başlatıldı. UserID: " + userId + " | Session: " + sessionId);
    }

    private static string NewSessionId()
    {
        return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_") + UnityEngine.Random.Range(1000, 9999);
    }

    // ─────────────────────────────────────────────────────────────
    // YAZMA ALTYAPISI
    // ─────────────────────────────────────────────────────────────

#if ENABLE_FIREBASE
    private DocumentReference RootRef    => db.Collection(COLLECTION_USERS).Document(userId);
    private DocumentReference ProfileRef => RootRef.Collection("meta").Document(DOC_PROFILE);
    private DocumentReference SessionRef => RootRef.Collection(COLLECTION_SESSIONS).Document(sessionId);
    private DocumentReference GetLevelRef(int levelIndex) =>
        RootRef.Collection(COLLECTION_LEVELS).Document(levelIndex.ToString());

    /// <summary>
    /// Doküman yoksa oluşturur, varsa alanları birleştirir. Hata olursa konsola yazar —
    /// eskiden task sonucu atıldığı için başarısız yazmalar görünmüyordu.
    /// </summary>
    private void Merge(DocumentReference doc, Dictionary<string, object> data, string tag)
    {
        doc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("[Firestore] '" + tag + "' yazılamadı: " +
                                 task.Exception?.GetBaseException().Message);
            }
        });
    }

    /// <summary>Aynı özeti hem root dokümana hem meta/profile'a yazar.</summary>
    private void MergeProfileAndRoot(Dictionary<string, object> updates, string tag)
    {
        var now = Timestamp.FromDateTime(DateTime.UtcNow);
        string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var profileData = new Dictionary<string, object>(updates);
        profileData["last_seen"] = now;
        profileData["last_active_date"] = todayStr;
        Merge(ProfileRef, profileData, tag + " → profile");

        var rootData = new Dictionary<string, object>(updates);
        rootData["device_id"] = userId;
        rootData["last_seen"] = now;
        rootData["last_active_date"] = todayStr;
        Merge(RootRef, rootData, tag + " → root");
    }
#endif

    /// <summary>Anormal süreleri kırpar (arka planda geçen zaman, saat değişimi vb.).</summary>
    private static long SaneSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0) return 0;
        return (long)Math.Min(seconds, MAX_EVENT_SECONDS);
    }

    // ─────────────────────────────────────────────────────────────
    // KULLANICI PROFİLİ
    // ─────────────────────────────────────────────────────────────

    private void EnsureUserProfile()
    {
        if (!isReady) return;

#if ENABLE_FIREBASE
        string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Kurulum tarihini yerel olarak tut — Firestore okuması gerekmez.
        string installDate = PlayerPrefs.GetString(PREF_INSTALL_DATE, "");
        bool isNewUser = string.IsNullOrEmpty(installDate);
        if (isNewUser)
        {
            installDate = todayStr;
            PlayerPrefs.SetString(PREF_INSTALL_DATE, installDate);
            PlayerPrefs.Save();
        }

        int retentionDay = 0;
        if (DateTime.TryParse(installDate, out DateTime insDate))
            retentionDay = Mathf.Max(0, (DateTime.UtcNow.Date - insDate.Date).Days);

        var summary = new Dictionary<string, object>
        {
            { "platform",       Application.platform.ToString() },
            { "app_version",    Application.version },
            { "country",        GetDeviceCountry() },
            { "language",       GetDeviceLanguage() },
            { "device_model",   SystemInfo.deviceModel },
            { "install_date",   installDate },
            { "active_dates",   FieldValue.ArrayUnion(todayStr) },
            { "retention_days", FieldValue.ArrayUnion(retentionDay) }
        };

        if (isNewUser)
        {
            // Yalnızca ilk kurulumda yazılır; sonraki açılışlarda üzerine yazılmaz.
            summary["first_open"] = Timestamp.FromDateTime(DateTime.UtcNow);
        }

        MergeProfileAndRoot(summary, "profile");
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // CİHAZ BİLGİSİ
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Cihazın bölge ayarını döner. Dikkat: bu coğrafi konum değil, sistem locale'idir —
    /// yurt dışındaki Türkçe telefon da "TR" raporlar.
    /// </summary>
    private string GetDeviceCountry()
    {
        try
        {
            return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch
        {
            return "unknown";
        }
    }

    private string GetDeviceLanguage()
    {
        try
        {
            return Application.systemLanguage.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    // ─────────────────────────────────────────────────────────────
    // OTURUM YÖNETİMİ
    // ─────────────────────────────────────────────────────────────

    private void StartSession()
    {
        if (!isReady) return;

#if ENABLE_FIREBASE
        string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var data = new Dictionary<string, object>
        {
            { "start_time",       Timestamp.FromDateTime(sessionStartTime) },
            { "date",             todayStr },
            { "duration_seconds", 0 },
            { "levels_played",    0 },
            { "device_model",     SystemInfo.deviceModel },
            { "os_version",       SystemInfo.operatingSystem },
            { "platform",         Application.platform.ToString() },
            { "app_version",      Application.version },
            { "country",          GetDeviceCountry() },
            { "language",         GetDeviceLanguage() }
        };

        Merge(SessionRef, data, "session_start");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_sessions", FieldValue.Increment(1) },
            { "active_dates",   FieldValue.ArrayUnion(todayStr) }
        }, "session_count");
#endif
    }

    private void EndSession(bool fromPause = false)
    {
        if (!isReady) return;

        // Açık bir level varsa önce onu kaydet
        if (levelActive)
        {
            float timeOnLevel = (float)(DateTime.UtcNow - levelStartTime).TotalSeconds;
            LogLevelQuit(currentLevelIndex, timeOnLevel);
        }

        double durationSeconds = (DateTime.UtcNow - sessionStartTime).TotalSeconds;

#if ENABLE_FIREBASE
        Merge(SessionRef, new Dictionary<string, object>
        {
            { "end_time",         Timestamp.FromDateTime(DateTime.UtcNow) },
            { "duration_seconds", SaneSeconds(durationSeconds) },
            { "levels_played",    levelsPlayedThisSession },
            { "ended_by_pause",   fromPause }
        }, "session_end");
#endif
    }

    private void AddPlayTime(double seconds)
    {
        if (!isReady) return;

        long safe = SaneSeconds(seconds);
        if (safe <= 0) return;

#if ENABLE_FIREBASE
        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_play_minutes", FieldValue.Increment(safe / 60.0) }
        }, "play_time");
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // LEVEL OLAYLARI
    // ─────────────────────────────────────────────────────────────

    public void LogLevelStart(int levelIndex)
    {
        if (!isReady) return;

        currentLevelIndex   = levelIndex;
        levelStartTime      = DateTime.UtcNow;
        levelActive         = true;
        levelsPlayedThisSession++;

#if ENABLE_FIREBASE
        var levelData = new Dictionary<string, object>
        {
            { "level_index", levelIndex },
            { "attempts",    FieldValue.Increment(1) },
            { "last_played", Timestamp.FromDateTime(DateTime.UtcNow) }
        };

        // first_attempt yalnızca bu cihazda level ilk kez açıldığında yazılır.
        string seenKey = PREF_LEVEL_SEEN + levelIndex;
        if (PlayerPrefs.GetInt(seenKey, 0) == 0)
        {
            levelData["first_attempt"] = Timestamp.FromDateTime(DateTime.UtcNow);
            PlayerPrefs.SetInt(seenKey, 1);
            PlayerPrefs.Save();
        }

        Merge(GetLevelRef(levelIndex), levelData, "level_start");

        // En uzak level — Firestore okumadan, yerel maksimumla karşılaştırılır.
        if (levelIndex > PlayerPrefs.GetInt(PREF_FARTHEST, -1))
        {
            PlayerPrefs.SetInt(PREF_FARTHEST, levelIndex);
            PlayerPrefs.Save();

            MergeProfileAndRoot(new Dictionary<string, object>
            {
                { "farthest_level", levelIndex }
            }, "farthest_level");
        }
#endif
    }

    public void LogLevelComplete(int levelIndex, float durationSeconds)
    {
        if (!isReady) return;
        levelActive = false;

        long safeDuration = SaneSeconds(durationSeconds);

#if ENABLE_FIREBASE
        var updates = new Dictionary<string, object>
        {
            { "level_index",      levelIndex },
            { "completions",      FieldValue.Increment(1) },
            { "total_time_spent", FieldValue.Increment(safeDuration) },
            { "last_played",      Timestamp.FromDateTime(DateTime.UtcNow) }
        };

        // En iyi süre de yerel olarak takip edilir — okuma gerekmez.
        string bestKey = PREF_BEST_TIME + levelIndex;
        int previousBest = PlayerPrefs.GetInt(bestKey, 0);
        if (safeDuration > 0 && (previousBest == 0 || safeDuration < previousBest))
        {
            PlayerPrefs.SetInt(bestKey, (int)safeDuration);
            PlayerPrefs.Save();
            updates["best_time"] = safeDuration;
        }

        Merge(GetLevelRef(levelIndex), updates, "level_complete");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_completions", FieldValue.Increment(1) }
        }, "completion_count");
#endif

        AddPlayTime(safeDuration);
    }

    public void LogLevelFail(int levelIndex, float durationSeconds)
    {
        if (!isReady) return;
        levelActive = false;

        long safeDuration = SaneSeconds(durationSeconds);

#if ENABLE_FIREBASE
        Merge(GetLevelRef(levelIndex), new Dictionary<string, object>
        {
            { "level_index",      levelIndex },
            { "fails",            FieldValue.Increment(1) },
            { "total_time_spent", FieldValue.Increment(safeDuration) },
            { "last_played",      Timestamp.FromDateTime(DateTime.UtcNow) }
        }, "level_fail");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_fails", FieldValue.Increment(1) }
        }, "fail_count");
#endif

        AddPlayTime(safeDuration);
    }

    public void LogLevelRetry(int levelIndex)
    {
        if (!isReady) return;

        double elapsedSeconds = levelActive ? (DateTime.UtcNow - levelStartTime).TotalSeconds : 0;
        long safeElapsed = SaneSeconds(elapsedSeconds);

#if ENABLE_FIREBASE
        var updates = new Dictionary<string, object>
        {
            { "level_index", levelIndex },
            { "retries",     FieldValue.Increment(1) },
            { "last_played", Timestamp.FromDateTime(DateTime.UtcNow) }
        };
        if (safeElapsed > 0)
            updates["total_time_spent"] = FieldValue.Increment(safeElapsed);

        Merge(GetLevelRef(levelIndex), updates, "level_retry");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_retries", FieldValue.Increment(1) }
        }, "retry_count");
#endif

        AddPlayTime(safeElapsed);

        // Yeni deneme başlıyor
        levelStartTime = DateTime.UtcNow;
        levelActive    = true;
    }

    public void LogLevelReset(int levelIndex)
    {
        if (!isReady) return;

        double elapsedSeconds = levelActive ? (DateTime.UtcNow - levelStartTime).TotalSeconds : 0;
        long safeElapsed = SaneSeconds(elapsedSeconds);

#if ENABLE_FIREBASE
        var updates = new Dictionary<string, object>
        {
            { "level_index", levelIndex },
            { "resets",      FieldValue.Increment(1) },
            { "last_played", Timestamp.FromDateTime(DateTime.UtcNow) }
        };
        if (safeElapsed > 0)
            updates["total_time_spent"] = FieldValue.Increment(safeElapsed);

        Merge(GetLevelRef(levelIndex), updates, "level_reset");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "total_resets", FieldValue.Increment(1) }
        }, "reset_count");
#endif

        AddPlayTime(safeElapsed);

        // Reset = tekrar başlıyor
        levelStartTime = DateTime.UtcNow;
        levelActive    = true;
    }

    public void LogLevelQuit(int levelIndex, float durationSeconds)
    {
        if (!isReady || levelIndex < 0) return;
        levelActive = false;

        long safeDuration = SaneSeconds(durationSeconds);

#if ENABLE_FIREBASE
        Merge(GetLevelRef(levelIndex), new Dictionary<string, object>
        {
            { "level_index",      levelIndex },
            { "quits",            FieldValue.Increment(1) },
            { "total_time_spent", FieldValue.Increment(safeDuration) },
            { "last_played",      Timestamp.FromDateTime(DateTime.UtcNow) }
        }, "level_quit");

        MergeProfileAndRoot(new Dictionary<string, object>
        {
            { "last_level_quit", levelIndex }
        }, "last_quit");
#endif

        AddPlayTime(safeDuration);
    }

    // ─────────────────────────────────────────────────────────────
    // UYGULAMA ARKA PLAN / KAPATMA
    // ─────────────────────────────────────────────────────────────

    void OnApplicationPause(bool pauseStatus)
    {
        if (!isReady) return;

        if (pauseStatus)
        {
            pausedAt = DateTime.UtcNow;
            levelActiveBeforePause = levelActive;

            if (levelActive)
            {
                float timeOnLevel = (float)(DateTime.UtcNow - levelStartTime).TotalSeconds;
                LogLevelQuit(currentLevelIndex, timeOnLevel);
            }
            EndSession(fromPause: true);
        }
        else
        {
            double awaySeconds = (DateTime.UtcNow - pausedAt).TotalSeconds;

            // Arka planda geçen süre level süresine yazılmasın
            if (levelActiveBeforePause)
            {
                levelActive    = true;
                levelStartTime = DateTime.UtcNow;
            }

            if (awaySeconds >= NEW_SESSION_AFTER_SECONDS)
            {
                sessionId               = NewSessionId();
                sessionStartTime        = DateTime.UtcNow;
                levelsPlayedThisSession = 0;
                StartSession();
            }
            else
            {
                // Kısa kesinti — aynı oturum devam ediyor, arka plan süresi düşülür.
                sessionStartTime = sessionStartTime.AddSeconds(awaySeconds);
            }
        }
    }

    void OnApplicationQuit()
    {
        EndSession(fromPause: false);
    }
}
