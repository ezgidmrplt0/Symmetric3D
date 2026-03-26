using UnityEngine;
using TMPro;
using System;

/// <summary>
/// Level bazlı geri sayım sayacı.
/// Süre bitince GameManager üzerinden LevelFail tetikler.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance;

    [Header("Ayarlar")]
    public TextMeshProUGUI timerText;
    public float startingTime = 150f; // 2:30 (150 saniye) default
    
    private float currentTime;
    private bool isRunning = false;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // İlk tıklama yapıldığında ve henüz çalışmıyorsa başlat
        if (!isRunning && Input.GetMouseButtonDown(0))
        {
            StartTimer();
        }

        if (!isRunning) return;

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            UpdateDisplay();
        }
        else
        {
            currentTime = 0;
            isRunning = false;
            UpdateDisplay();
            TimeExpired();
        }
    }

    public void StartTimer()
    {
        if (currentTime > 0)
        {
            isRunning = true;
        }
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer(float customTime = -1f)
    {
        // Eğer customTime 0 gelirse (mevcut assetlerdeki default değer 0 olduğu için), 
        // startingTime (150) kullanacak şekilde fail-safe ekliyoruz.
        currentTime = (customTime > 0.01f) ? customTime : startingTime;
        isRunning = false;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (timerText == null) return;

        TimeSpan time = TimeSpan.FromSeconds(currentTime);
        // "02.30" formatında (noktalı) gösterim
        timerText.text = string.Format("{0:D2}.{1:D2}", time.Minutes, time.Seconds);

        // Eğer süre azaldıysa (örn 10 sn) rengini kırmızı yapabiliriz
        if (currentTime <= 10f)
            timerText.color = Color.red;
        else
            timerText.color = Color.white;
    }

    private void TimeExpired()
    {
        GameManager.Instance?.LevelFail();
    }
}
