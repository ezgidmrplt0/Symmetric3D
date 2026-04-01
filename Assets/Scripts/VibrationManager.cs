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
        _iOS_VibrateLight();
#elif UNITY_ANDROID
        // 20ms: Minimum hissedilebilir, hafif tık
        _Android_VibrateShort(20);
#endif
    }

    public static void VibrateSuccess()
    {
        if (!IsEnabled) return;
#if UNITY_EDITOR
        Debug.Log("Vibration Success (Editor)");
#elif UNITY_IOS
        _iOS_VibrateLight();
#elif UNITY_ANDROID
        // 40ms: Başarı için kısa tık
        _Android_VibrateShort(40);
#endif
    }

    public static void VibrateFail()
    {
        if (!IsEnabled) return;
#if UNITY_EDITOR
        Debug.Log("Vibration Fail (Editor)");
#elif UNITY_IOS
        _iOS_VibrateMedium();
#elif UNITY_ANDROID
        // 60ms: Hata için biraz daha belirgin ama yine de hafif
        _Android_VibrateShort(60);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _iOS_VibrateLight(); 
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _iOS_VibrateMedium(); 
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _iOS_VibrateHeavy(); 
    // Not: Bu kısım Assets/Plugins/iOS/VibrationPlugin.mm dosyasını kullanır.
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
