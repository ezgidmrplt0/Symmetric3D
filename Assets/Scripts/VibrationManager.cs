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
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        Handheld.Vibrate();
#endif
    }
}
