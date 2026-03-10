using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LevelFailPanel : MonoBehaviour
{
    [Header("UI Referansları")]
    [Tooltip("Fail panelinin kök objesi (başlangıçta kapalı olmalı)")]
    public GameObject panelRoot;

    [Tooltip("Tekrar Dene butonu")]
    public Button retryButton;

    [Header("Animasyon Ayarları")]
    public float panelFadeInDuration = 0.4f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[LevelFailPanel] panelRoot atanmamış!");
            return;
        }

        canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();

        panelRoot.SetActive(false);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        GameManager.OnLevelFailed.AddListener(ShowPanel);
    }

    void OnDestroy()
    {
        GameManager.OnLevelFailed.RemoveListener(ShowPanel);
    }

    void ShowPanel()
    {
        panelRoot.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, panelFadeInDuration).SetUpdate(true); // Oyun durmuş olsa bile çalışsın
    }

    void OnRetryClicked()
    {
        canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
        {
            panelRoot.SetActive(false);
            
            // GameManager durumunu sıfırla
            GameManager.Instance?.ResetLevelState();
            
            // Mevcut seviyeyi yeniden başlat
            GridSpawner spawner = FindObjectOfType<GridSpawner>();
            if (spawner != null)
            {
                spawner.SpawnCurrentLevel();
            }
        });
    }
}
