using UnityEngine;

/// <summary>
/// Mobil performansını artırmak için FPS sabitleyici ve dinamik çözünürlük ölçeklendirici.
/// </summary>
public class FPSOptimizer : MonoBehaviour
{
    [Header("Target FPS")]
    public int targetFPS = 60;

    [Header("Dynamic Resolution")]
    public float minScale = 0.7f;
    public float maxScale = 1f;

    private float deltaTime = 0.0f;

    void Start()
    {
        // FPS sabitle
        Application.targetFrameRate = targetFPS;

        // VSync kapat (mobilde önemli)
        QualitySettings.vSyncCount = 0;
    }

    void Update()
    {
        // FPS hesapla
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;

        // FPS düşerse çözünürlüğü düşür
        if (fps < 30)
        {
            ScalableBufferManager.ResizeBuffers(minScale, minScale);
        }
        else if (fps > 50)
        {
            ScalableBufferManager.ResizeBuffers(maxScale, maxScale);
        }
    }
}
