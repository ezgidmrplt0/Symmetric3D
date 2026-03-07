using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelCompletePanel : MonoBehaviour
{
    [Header("UI Referansları")]
    [Tooltip("Tüm panelin root GameObject'i (başlangıçta kapalı olmalı)")]
    public GameObject panelRoot;

    [Tooltip("ProgressBar shader'lı Image — herhangi bir Image, sadece Material'ini ProgressBar.mat yap")]
    public Image progressBarImage;

    [Tooltip("Yüzde sayısını gösteren Text (örn: %40)")]
    public TextMeshProUGUI progressText;

    [Tooltip("Next Level butonu")]
    public Button nextLevelButton;

    [Tooltip("Yeni Mechanic açıldığında gösterilecek obje (isteğe bağlı)")]
    public GameObject newMechanicUnlockBanner;

    [Header("Animasyon Ayarları")]
    public float barAnimDuration = 1.2f;
    public float panelFadeInDuration = 0.4f;

    private GridSpawner gridSpawner;
    private CanvasGroup canvasGroup;
    private Material barMat;         // runtime instance (sahneyi kirletmez)
    private float currentFill = 0f;

    void Awake()
    {
        canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();

        panelRoot.SetActive(false);

        // Material'in runtime kopyasını al (diğer nesneleri etkilemez)
        if (progressBarImage != null && progressBarImage.material != null)
        {
            barMat = new Material(progressBarImage.material);
            progressBarImage.material = barMat;
        }

        nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        GameManager.OnLevelCompleted.AddListener(ShowPanel);
    }

    void Start()
    {
        gridSpawner = FindObjectOfType<GridSpawner>();
    }

    void OnDestroy()
    {
        GameManager.OnLevelCompleted.RemoveListener(ShowPanel);
    }

    void ShowPanel()
    {
        if (GameManager.Instance == null) return;

        // Artıştan önceki değeri GameManager'dan oku (her zaman doğru başlangıç noktası)
        int previousProgress = GameManager.Instance.previousTotalProgress;
        int currentProgress  = GameManager.Instance.totalProgress;

        // Başlangıç değerlerini ayarla
        SetFill(previousProgress / 100f);

        if (newMechanicUnlockBanner != null)
            newMechanicUnlockBanner.SetActive(false);

        // Paneli göster
        panelRoot.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, panelFadeInDuration);

        // Bar animasyonu
        DOVirtual.DelayedCall(0.5f, () =>
        {
            DOTween.To(
                () => currentFill,
                x => SetFill(x),
                currentProgress / 100f,
                barAnimDuration
            ).SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                if (GameManager.Instance.newMechanicUnlocked && newMechanicUnlockBanner != null)
                {
                    newMechanicUnlockBanner.SetActive(true);
                    newMechanicUnlockBanner.transform.localScale = Vector3.zero;
                    newMechanicUnlockBanner.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
                }
            });
        });
    }

    // Shader _FillAmount'u ve text'i birlikte günceller
    void SetFill(float value)
    {
        currentFill = value;
        if (barMat != null)
            barMat.SetFloat("_FillAmount", value);
        if (progressText != null)
            progressText.text = "%" + Mathf.RoundToInt(value * 100);
    }

    void OnNextLevelClicked()
    {
        canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
        {
            panelRoot.SetActive(false);
            gridSpawner?.NextLevel();
        });
    }
}
