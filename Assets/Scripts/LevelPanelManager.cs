using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class LevelPanelManager : MonoBehaviour
{
    // ── COMPLETE PANEL ──────────────────────────────────────────
    [Header("Complete Panel")]
    public GameObject completePanelRoot;
    public Image progressBarImage;
    public TextMeshProUGUI progressText;
    public Button nextLevelButton;
    public GameObject newMechanicUnlockBanner;
    public float barAnimDuration = 1.2f;

    // ── FAIL PANEL ───────────────────────────────────────────────
    [Header("Fail Panel")]
    public GameObject failPanelRoot;
    public Button retryButton;

    // ── RESET BUTTON ─────────────────────────────────────────────
    [Header("Reset Level Button")]
    public Button resetLevelButton;

    // ── UNLOCK POPUP ─────────────────────────────────────────────
    [Header("Unlock Popup")]
    public GameObject unlockPanelRoot;
    public CanvasGroup unlockCanvasGroup;
    public RectTransform unlockPopupWindow;
    public TextMeshProUGUI unlockHeaderText;
    public TextMeshProUGUI unlockLevelNameText;
    public Image unlockRewardImage;
    public Button unlockOkButton;

    [System.Serializable]
    public struct MechanicIconData
    {
        public LevelData.LevelType levelType;
        public Sprite icon;
    }
    public List<MechanicIconData> mechanicIcons = new List<MechanicIconData>();

    [Header("Next Mechanic Preview")]
    public Image nextMechanicPreviewImage;
    public Sprite shufflePreviewSprite;
    public TMP_Text nextMechanicLabel;

    // ── PRIVATE ──────────────────────────────────────────────────
    private GridSpawner gridSpawner;
    private Material barMat;
    private float currentFill = 0f;
    private bool nextLevelClickedOnce = false;

    void Awake()
    {
        if (completePanelRoot != null)
        {
            completePanelRoot.SetActive(false);
            if (progressBarImage != null && progressBarImage.material != null)
            {
                barMat = new Material(progressBarImage.material);
                progressBarImage.material = barMat;
            }
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        if (failPanelRoot != null)
        {
            failPanelRoot.SetActive(false);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (unlockPanelRoot != null) unlockPanelRoot.SetActive(false);
        if (unlockOkButton != null) unlockOkButton.onClick.AddListener(HideUnlockPopup);

        if (resetLevelButton != null) resetLevelButton.onClick.AddListener(OnResetLevelClicked);

        if (nextMechanicPreviewImage != null)
            nextMechanicPreviewImage.gameObject.SetActive(false);

        GameManager.OnLevelCompleted.AddListener(ShowCompletePanel);
        GameManager.OnLevelFailed.AddListener(ShowFailPanel);
    }

    void Start()
    {
        gridSpawner = FindObjectOfType<GridSpawner>();

        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Uygulama kapanıp açılma: level tamamlandı ama Next'e basılmamıştı → win paneli tekrar göster
        if (GameManager.Instance != null && GameManager.Instance.IsLevelCompleting)
        {
            GameManager.Instance.previousTotalProgress =
                (GameManager.Instance.totalProgress - GameManager.Instance.progressPerLevel + 100) % 100;
            ShowCompletePanel();
        }
    }

    void Update()
    {
        // Unlock popup açıkken failsafe çalışmasın
        if (unlockPanelRoot != null && unlockPanelRoot.activeInHierarchy) return;

        // Failsafe: next level butonunu manuel yakala
        if (completePanelRoot != null && completePanelRoot.activeInHierarchy && Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current == null || nextLevelButton == null) return;
            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var hit in results)
            {
                if (hit.gameObject == nextLevelButton.gameObject || hit.gameObject.transform.IsChildOf(nextLevelButton.transform))
                {
                    OnNextLevelClicked();
                    break;
                }
            }
        }
    }

    void OnDestroy()
    {
        GameManager.OnLevelCompleted.RemoveListener(ShowCompletePanel);
        GameManager.OnLevelFailed.RemoveListener(ShowFailPanel);
    }

    // ── COMPLETE ─────────────────────────────────────────────────

    void ShowCompletePanel()
    {
        if (GameManager.Instance == null || completePanelRoot == null) return;

        nextLevelClickedOnce = false;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.HideTutorial();
        
        // Timer'ı durdur
        LevelTimer.Instance?.StopTimer();

        completePanelRoot.SetActive(true);
        completePanelRoot.transform.localScale = Vector3.zero;
        completePanelRoot.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        
        VibrationManager.VibrateSuccess();

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(true);
            nextLevelButton.interactable = true;
        }

        bool showBar = GameManager.Instance.HadMechanicsToUnlock();
        if (progressBarImage != null) progressBarImage.gameObject.SetActive(showBar);
        if (progressText != null) progressText.gameObject.SetActive(showBar);
        if (newMechanicUnlockBanner != null) newMechanicUnlockBanner.SetActive(false);

        UpdateNextMechanicPreview();

        if (!showBar) return;

        SetFill(GameManager.Instance.previousTotalProgress / 100f);

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DOTween.To(() => currentFill, x => SetFill(x), GameManager.Instance.totalProgress / 100f, barAnimDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true)
                .OnComplete(() =>
                {
                    LevelData.LevelType displayType = LevelData.LevelType.Classic;
                    bool hasNewUnlock = GameManager.Instance.hitProgressHundred &&
                                       GameManager.Instance.GetTypeForProgress(GameManager.Instance.lifetimeProgress, out displayType);

                    if (hasNewUnlock && newMechanicUnlockBanner != null)
                    {
                        newMechanicUnlockBanner.SetActive(true);
                        newMechanicUnlockBanner.transform.localScale = Vector3.zero;
                        newMechanicUnlockBanner.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
                    }

                    if (hasNewUnlock)
                        ShowUnlockPopup(displayType);
                });
        }).SetUpdate(true);
    }

    void SetFill(float value)
    {
        currentFill = value;
        if (barMat != null) barMat.SetFloat("_FillAmount", value);
        if (progressText != null) progressText.text = "%" + Mathf.RoundToInt(value * 100);
    }

    void OnNextLevelClicked()
    {
        if (nextLevelClickedOnce) return;
        nextLevelClickedOnce = true;

        if (gridSpawner == null) gridSpawner = FindObjectOfType<GridSpawner>();
        completePanelRoot.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            completePanelRoot.SetActive(false);
            gridSpawner?.NextLevel();
        });
    }

    // ── FAIL ─────────────────────────────────────────────────────

    void ShowFailPanel()
    {
        if (failPanelRoot == null) return;
        failPanelRoot.SetActive(true);
        failPanelRoot.transform.localScale = Vector3.zero;
        failPanelRoot.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        
        VibrationManager.VibrateFail();

        // Timer'ı durdur
        LevelTimer.Instance?.StopTimer();
    }

    void OnRetryClicked()
    {
        failPanelRoot.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            failPanelRoot.SetActive(false);
            GameManager.Instance?.ResetLevelState();
            FindObjectOfType<GridSpawner>()?.SpawnCurrentLevel();
        });
    }

    void OnResetLevelClicked()
    {
        // Win paneli açıkken reset engelle
        if (completePanelRoot != null && completePanelRoot.activeInHierarchy)
            return;

        // Fail paneli açıksa kapat
        if (failPanelRoot != null && failPanelRoot.activeInHierarchy)
            failPanelRoot.SetActive(false);

        GameManager.Instance?.ResetLevelState();
        if (gridSpawner == null) gridSpawner = FindObjectOfType<GridSpawner>();
        gridSpawner?.SpawnCurrentLevel();
    }

    // ── UNLOCK POPUP ─────────────────────────────────────────────

    void ShowUnlockPopup(LevelData.LevelType type)
    {
        if (unlockPanelRoot == null) return;

        if (unlockHeaderText != null) unlockHeaderText.text = "New Mechanic!";
        if (unlockLevelNameText != null) unlockLevelNameText.text = type.ToString();

        if (unlockRewardImage != null)
        {
            Sprite found = null;
            foreach (var item in mechanicIcons)
            {
                if (type.HasFlag(item.levelType))
                {
                    found = item.icon;
                    if (found != null) break;
                }
            }
            unlockRewardImage.sprite = found;
            unlockRewardImage.gameObject.SetActive(found != null);
        }

        // Next level butonunu kilitle, unlock popup kapanana kadar basılmasın
        if (nextLevelButton != null) nextLevelButton.interactable = false;

        unlockPanelRoot.SetActive(true);
        unlockPanelRoot.transform.SetAsLastSibling();
        unlockPanelRoot.transform.localScale = Vector3.zero;
        unlockPanelRoot.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    void HideUnlockPopup()
    {
        unlockPanelRoot.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            unlockPanelRoot.SetActive(false);
            if (nextLevelButton != null) nextLevelButton.interactable = true;
            // Popup kapandıktan sonra preview'ı güncelle (artık bir sonraki mekanik değişmiş olabilir)
            UpdateNextMechanicPreview();
        });
    }

    // ── NEXT MEKANİK PREVİEW ─────────────────────────────────────

    void UpdateNextMechanicPreview()
    {
        if (nextMechanicPreviewImage == null) return;

        if (GameManager.Instance.GetNextMechanicToUnlock(out LevelData.LevelType nextType))
        {
            // Henüz açılmamış mekanik var → gri silüet göster
            Sprite previewSprite = null;
            foreach (var item in mechanicIcons)
                if (nextType.HasFlag(item.levelType)) { previewSprite = item.icon; break; }

            if (previewSprite != null)
            {
                if (nextMechanicLabel != null) nextMechanicLabel.text = "NEXT MECHANIC";
                nextMechanicPreviewImage.sprite   = previewSprite;
                nextMechanicPreviewImage.material = null;
                nextMechanicPreviewImage.color    = Color.white;
                nextMechanicPreviewImage.gameObject.SetActive(true);
            }
            else
            {
                ShowShufflePreview();
            }
        }
        else
        {
            // Tüm mekanikler açıldı → shuffle resmi
            ShowShufflePreview();
        }
    }

    void ShowShufflePreview()
    {
        if (nextMechanicLabel != null) nextMechanicLabel.text = "SHUFFLE";
        if (nextMechanicPreviewImage == null) return;
        if (shufflePreviewSprite != null)
        {
            nextMechanicPreviewImage.sprite   = shufflePreviewSprite;
            nextMechanicPreviewImage.material = null;   // silüet yok, normal göster
            nextMechanicPreviewImage.color    = Color.white;
            nextMechanicPreviewImage.gameObject.SetActive(true);
        }
        else
        {
            nextMechanicPreviewImage.gameObject.SetActive(false);
        }
    }
}
