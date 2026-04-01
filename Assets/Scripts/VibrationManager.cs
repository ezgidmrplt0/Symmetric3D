using UnityEngine;

public class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance;

    private const string VIBRATION_KEY = "VibrationEnabled";

    public static bool IsEnabled { get; private set; }

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

        IsEnabled = PlayerPrefs.GetInt(VIBRATION_KEY, 1) == 1;
    }

    public static void SetEnabled(bool value)
    {
        IsEnabled = value;
        PlayerPrefs.SetInt(VIBRATION_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void Toggle()
    {
        SetEnabled(!IsEnabled);
    }

    public static void TryVibrate()
    {
        if (!IsEnabled) return;

#if UNITY_EDITOR
        Debug.Log("Vibration Triggered (Editor)");
#elif UNITY_IOS
        // iOS için daha hafif (Light) haptic feedback
        _iOS_VibrateLight();
#elif UNITY_ANDROID
        // Android için çok kısa (15ms) bir darbe
        _Android_VibrateShort(15);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _iOS_VibrateLight(); 
    // Not: Bu kısım .mm plugin'i gerektirir.
    // Mobil dışı platformlarda Handheld.Vibrate() hata verdiği için kaldırıldı.
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void _Android_VibrateShort(long milliseconds)
    {
        try {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (vibrator.Call<bool>("hasVibrator")) {
                vibrator.Call("vibrate", milliseconds);
            }
        } catch (System.Exception e) {
            Debug.LogWarning("Android vibration failed: " + e.Message);
        }
    }
#endif
}
