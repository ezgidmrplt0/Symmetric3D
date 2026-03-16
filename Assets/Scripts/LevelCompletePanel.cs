using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class LevelCompletePanel : MonoBehaviour
{
    [Header("UI Referansları")]
    public GameObject panelRoot;
    public Image progressBarImage;
    public TextMeshProUGUI progressText;
    public Button nextLevelButton;
    public GameObject newMechanicUnlockBanner;
    public NewLevelUnlockPanel newLevelUnlockPopup;

    [Header("Animasyon Ayarları")]
    public float barAnimDuration = 1.2f;
    public float panelFadeInDuration = 0.3f;

    private GridSpawner gridSpawner;
    private CanvasGroup canvasGroup;
    private Material barMat;         
    private float currentFill = 0f;

    void Awake()
    {
        if (panelRoot == null) return;
        canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();

        panelRoot.SetActive(false);

        if (progressBarImage != null && progressBarImage.material != null)
        {
            barMat = new Material(progressBarImage.material);
            progressBarImage.material = barMat;
        }

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            
        GameManager.OnLevelCompleted.AddListener(ShowPanel);
    }

    void Start()
    {
        gridSpawner = FindObjectOfType<GridSpawner>();

        // --- KRİTİK: EVENT SYSTEM VE RAYCASTER KONTROLÜ ---
        if (EventSystem.current == null)
        {
            Debug.Log("<color=red>[LevelCompletePanel]</color> Sahnede EventSystem bulunamadı! Oluşturuluyor...");
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            Debug.Log("<color=orange>[LevelCompletePanel]</color> Canvas üzerinde GraphicRaycaster eksik! Ekleniyor...");
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    void Update()
    {
        // FAILSAFE: Buton bileşeni çalışmasa bile tıklamayı manuel yakala
        if (panelRoot != null && panelRoot.activeInHierarchy && Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current == null) return;

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var hit in results)
            {
                // Eğer tıklanan obje butonsa veya butonun bir parçasıysa işlemi başlat
                if (hit.gameObject == nextLevelButton.gameObject || hit.gameObject.transform.IsChildOf(nextLevelButton.transform))
                {
                    Debug.Log("<color=green>[LevelCompletePanel]</color> Failsafe: Tıklama manuel yakalandı!");
                    OnNextLevelClicked();
                    break;
                }
                
                // Debug logu her zaman aktif kalsın ki neyin engellediğini görelim
                Debug.Log($"<color=cyan>[UI Debug]</color> Tıklanan: <b>{hit.gameObject.name}</b>");
            }
        }
    }

    void OnDestroy()
    {
        GameManager.OnLevelCompleted.RemoveListener(ShowPanel);
    }

    void ShowPanel()
    {
        if (GameManager.Instance == null || panelRoot == null) return;

        // --- KATMAN ÖNCELİĞİ FİX ---
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999; 
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        panelRoot.transform.SetAsLastSibling();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.HideTutorial();

        panelRoot.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.DOFade(1f, panelFadeInDuration).SetUpdate(true);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(true);
            nextLevelButton.interactable = true;
            var img = nextLevelButton.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        int previousProgress = GameManager.Instance.previousTotalProgress;
        int currentProgress  = GameManager.Instance.totalProgress;

        SetFill(previousProgress / 100f);

        if (newMechanicUnlockBanner != null)
            newMechanicUnlockBanner.SetActive(false);

        // Bar animasyonu
        DOVirtual.DelayedCall(0.5f, () =>
        {
            DOTween.To(
                () => currentFill,
                x => SetFill(x),
                currentProgress / 100f,
                barAnimDuration
            ).SetEase(Ease.OutCubic).SetUpdate(true)
            .OnComplete(() =>
            {
                if (GameManager.Instance.newMechanicUnlocked && newMechanicUnlockBanner != null)
                {
                    newMechanicUnlockBanner.SetActive(true);
                    newMechanicUnlockBanner.transform.localScale = Vector3.zero;
                    newMechanicUnlockBanner.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
                }

                if (GameManager.Instance.hitProgressHundred)
                {
                    if (newLevelUnlockPopup != null)
                    {
                        GameManager.Instance.GetTypeForProgress(GameManager.Instance.lifetimeProgress, out LevelData.LevelType displayType);
                        newLevelUnlockPopup.Show(displayType);
                        
                        // Önemli: Popup butonu kapatmasın diye en arkaya itelim veya en öne çekelim
                        newLevelUnlockPopup.transform.SetAsFirstSibling(); 
                    }
                }
            });
        }).SetUpdate(true);
    }

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
        // Çift tıklamayı kod seviyesinde de engelle
        if (canvasGroup != null && !canvasGroup.interactable) return;

        Debug.Log("<color=green>[LevelCompletePanel]</color> Next Level Butonuna Basıldı!");

        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        if (canvasGroup != null)
            canvasGroup.interactable = false;

        canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
        {
            panelRoot.SetActive(false);
            if (gridSpawner != null)
            {
                gridSpawner.NextLevel();
            }
        });
    }
}
