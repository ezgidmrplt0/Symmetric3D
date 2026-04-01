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
        // iOS için 'Light' haptic feedback (VibrationPlugin.mm kullanılır)
        _iOS_VibrateLight();
#elif UNITY_ANDROID
        // Android için minimum 'tick' hissi için 30ms idealdir
        _Android_VibrateShort(30);
#endif
    }

    public static void VibrateSuccess()
    {
        if (!IsEnabled) return;
#if UNITY_EDITOR
        Debug.Log("Vibration Success (Editor)");
#elif UNITY_IOS
        _iOS_VibrateMedium();
#elif UNITY_ANDROID
        _Android_VibrateShort(80);
#endif
    }

    public static void VibrateFail()
    {
        if (!IsEnabled) return;
#if UNITY_EDITOR
        Debug.Log("Vibration Fail (Editor)");
#elif UNITY_IOS
        _iOS_VibrateHeavy();
#elif UNITY_ANDROID
        _Android_VibrateShort(150);
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
