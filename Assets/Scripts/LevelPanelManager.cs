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

    [Header("Reward Preview")]
    public Sprite giftSprite;
    public Image nextMechanicPreviewImage;
    public Sprite shufflePreviewSprite;
    public TMP_Text nextMechanicLabel;

    // ── PRIVATE ──────────────────────────────────────────────────
    private GridSpawner gridSpawner;
    private Material barMat;
    private float currentFill = 0f;
    private bool nextLevelClickedOnce = false;
    private Image previewBgImage;

    void SetupPreviewFillSupport()
    {
        if (nextMechanicPreviewImage == null) return;

        // Görsel boyutunu zengin, dolgun ve tam ortalı bir boyuta büyüt (250x250)
        Vector2 idealIconSize = new Vector2(250f, 250f);

        RectTransform srcRect = nextMechanicPreviewImage.rectTransform;
        srcRect.anchorMin = new Vector2(0.5f, 0.5f);
        srcRect.anchorMax = new Vector2(0.5f, 0.5f);
        srcRect.pivot = new Vector2(0.5f, 0.5f);
        srcRect.sizeDelta = idealIconSize;
        srcRect.anchoredPosition = new Vector2(0f, 15f);

        if (previewBgImage == null)
        {
            GameObject bgObj = new GameObject("NextMechanic_SilhouetteBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(nextMechanicPreviewImage.transform.parent, false);
            bgObj.transform.SetSiblingIndex(nextMechanicPreviewImage.transform.GetSiblingIndex());

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = srcRect.anchorMin;
            bgRect.anchorMax = srcRect.anchorMax;
            bgRect.anchoredPosition = srcRect.anchoredPosition;
            bgRect.sizeDelta = srcRect.sizeDelta;
            bgRect.pivot = srcRect.pivot;
            bgRect.localScale = srcRect.localScale;

            previewBgImage = bgObj.GetComponent<Image>();
            previewBgImage.raycastTarget = false;
            previewBgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        }
        else
        {
            RectTransform bgRect = previewBgImage.rectTransform;
            bgRect.anchorMin = srcRect.anchorMin;
            bgRect.anchorMax = srcRect.anchorMax;
            bgRect.anchoredPosition = srcRect.anchoredPosition;
            bgRect.sizeDelta = srcRect.sizeDelta;
            bgRect.pivot = srcRect.pivot;
        }

        // Yüzdelik textini ikonun tam ortasına temiz ve bozulmasız şekilde yerleştir
        if (progressText != null)
        {
            progressText.transform.SetParent(nextMechanicPreviewImage.transform, false);
            RectTransform ptRect = progressText.rectTransform;
            ptRect.anchorMin = new Vector2(0.5f, 0.5f);
            ptRect.anchorMax = new Vector2(0.5f, 0.5f);
            ptRect.pivot = new Vector2(0.5f, 0.5f);
            ptRect.sizeDelta = new Vector2(250f, 80f);
            ptRect.anchoredPosition = Vector2.zero;

            progressText.alignment = TextAlignmentOptions.Center;
            progressText.verticalAlignment = VerticalAlignmentOptions.Middle;
            progressText.fontStyle = FontStyles.Bold;
            progressText.color = Color.white;
            progressText.transform.SetAsLastSibling();
        }
    }

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

        // Elementleri aktif et
        if (progressBarImage != null) progressBarImage.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(true);

        // Yeni hediye/ödül kilit açılması var mı (%100 ulaşıldı mı?)
        LevelData.LevelType unlockedType = LevelData.LevelType.Classic;
        bool hasNewUnlock = GameManager.Instance.hitProgressHundred &&
                           GameManager.Instance.GetTypeForProgress(GameManager.Instance.lifetimeProgress, out unlockedType);

        if (hasNewUnlock)
        {
            // %100 ULAŞILDI: Tek panelde Ödül Açıldı bilgisini göster
            if (newMechanicUnlockBanner != null)
            {
                newMechanicUnlockBanner.SetActive(true);
                newMechanicUnlockBanner.transform.localScale = Vector3.zero;
                newMechanicUnlockBanner.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (nextMechanicLabel != null)
            {
                nextMechanicLabel.gameObject.SetActive(true);
                nextMechanicLabel.text = "REWARD UNLOCKED!";
            }

            Sprite unlockSprite = GetIconForType(unlockedType);
            if (nextMechanicPreviewImage != null)
            {
                if (unlockSprite != null)
                {
                    SetupPreviewFillSupport();

                    if (previewBgImage != null)
                    {
                        previewBgImage.sprite = unlockSprite;
                        previewBgImage.gameObject.SetActive(true);
                    }

                    nextMechanicPreviewImage.sprite   = unlockSprite;
                    nextMechanicPreviewImage.material = null;
                    nextMechanicPreviewImage.gameObject.SetActive(true);
                }
                else
                {
                    nextMechanicPreviewImage.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // ARA SEVİYELER (%20, %40, %60, %80):
            if (newMechanicUnlockBanner != null) newMechanicUnlockBanner.SetActive(false);
            UpdateNextMechanicPreview();
        }

        // Progress bar ve Görsel Silüet Dolum animasyonu (%20, %40, %60, %80, %100)
        float startFill = GameManager.Instance.previousTotalProgress / 100f;
        float endFill = GameManager.Instance.hitProgressHundred ? 1f : (GameManager.Instance.totalProgress / 100f);

        SetFill(startFill);
        DOTween.To(() => currentFill, x => SetFill(x), endFill, barAnimDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    Sprite GetIconForType(LevelData.LevelType type)
    {
        int levelNum = PlayerPrefs.GetInt("CurrentLevelIndex", 0) + 1;

        // Level 11+ (Seviye 11 ve sonrası) -> GIFT (Hediye Paketi)
        if (levelNum >= 11)
        {
            if (giftSprite != null) return giftSprite;
#if UNITY_EDITOR
            Sprite loadedGift = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Gift.png");
            if (loadedGift != null) { giftSprite = loadedGift; return loadedGift; }
#endif
        }
        // Level 6-10 -> LINKED
        else if (levelNum >= 6)
        {
            foreach (var item in mechanicIcons)
            {
                if (item.icon != null && item.levelType == LevelData.LevelType.Linked)
                    return item.icon;
            }
#if UNITY_EDITOR
            Sprite linkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Linked(1).png");
            if (linkSprite != null) return linkSprite;
#endif
        }
        // Level 1-5 -> ROTATION
        else
        {
            foreach (var item in mechanicIcons)
            {
                if (item.icon != null && item.levelType == LevelData.LevelType.Rotation)
                    return item.icon;
            }
#if UNITY_EDITOR
            Sprite rotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Rotation.png");
            if (rotSprite != null) return rotSprite;
#endif
            if (shufflePreviewSprite != null) return shufflePreviewSprite;
        }

        if (giftSprite != null) return giftSprite;
#if UNITY_EDITOR
        Sprite fallbackGift = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Gift.png");
        if (fallbackGift != null) return fallbackGift;
#endif

        if (nextMechanicPreviewImage != null && nextMechanicPreviewImage.sprite != null)
            return nextMechanicPreviewImage.sprite;

        return null;
    }

    void SetFill(float value)
    {
        currentFill = value;
        if (barMat != null) barMat.SetFloat("_FillAmount", value);
        if (progressBarImage != null && progressBarImage.type == Image.Type.Filled) progressBarImage.fillAmount = value;
        if (progressText != null) progressText.text = "%" + Mathf.RoundToInt(value * 100);

        // Görseli yüzdelik oranında aşağıdan yukarıya doğru renkle doldur (Arka tarafta karanlık silüet görünür kalır)
        if (nextMechanicPreviewImage != null)
        {
            if (!nextMechanicPreviewImage.gameObject.activeSelf)
                nextMechanicPreviewImage.gameObject.SetActive(true);
            if (previewBgImage != null && !previewBgImage.gameObject.activeSelf)
                previewBgImage.gameObject.SetActive(true);

            nextMechanicPreviewImage.type = Image.Type.Filled;
            nextMechanicPreviewImage.fillMethod = Image.FillMethod.Vertical;
            nextMechanicPreviewImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            nextMechanicPreviewImage.fillAmount = value;
            nextMechanicPreviewImage.color = Color.white;
        }
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
        int levelIndex = PlayerPrefs.GetInt("CurrentLevelIndex", 0);
        FirebaseManager.Instance?.LogLevelRetry(levelIndex);

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

        int levelIndex = PlayerPrefs.GetInt("CurrentLevelIndex", 0);
        FirebaseManager.Instance?.LogLevelReset(levelIndex);

        GameManager.Instance?.ResetLevelState();
        if (gridSpawner == null) gridSpawner = FindObjectOfType<GridSpawner>();
        gridSpawner?.SpawnCurrentLevel();
    }

    // ── UNLOCK POPUP ─────────────────────────────────────────────

    void ShowUnlockPopup(LevelData.LevelType type)
    {
        if (unlockPanelRoot == null) return;

        if (unlockHeaderText != null) unlockHeaderText.text = "New Reward!";
        if (unlockLevelNameText != null) unlockLevelNameText.text = "Reward Unlocked";

        if (unlockRewardImage != null)
        {
            Sprite found = GetIconForType(type);
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
            UpdateNextMechanicPreview();
        });
    }

    // ── NEXT MEKANİK / HEDİYE PREVİEW ─────────────────────────────

    void UpdateNextMechanicPreview()
    {
        if (nextMechanicPreviewImage == null) return;

        LevelData.LevelType nextType = LevelData.LevelType.Rotation;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GetNextMechanicToUnlock(out nextType);
        }

        Sprite previewSprite = GetIconForType(nextType);

        if (previewSprite != null)
        {
            SetupPreviewFillSupport();

            if (nextMechanicLabel != null)
            {
                int levelNum = PlayerPrefs.GetInt("CurrentLevelIndex", 0) + 1;
                if (levelNum >= 11)
                {
                    nextMechanicLabel.text = "NEXT REWARD";
                }
                else
                {
                    nextMechanicLabel.text = "NEXT MECHANIC";
                }
                nextMechanicLabel.gameObject.SetActive(true);
            }

            if (previewBgImage != null)
            {
                previewBgImage.sprite = previewSprite;
                previewBgImage.type = Image.Type.Simple;
                previewBgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
                previewBgImage.gameObject.SetActive(true);
            }

            nextMechanicPreviewImage.sprite   = previewSprite;
            nextMechanicPreviewImage.material = null;
            nextMechanicPreviewImage.type     = Image.Type.Filled;
            nextMechanicPreviewImage.fillMethod = Image.FillMethod.Vertical;
            nextMechanicPreviewImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            nextMechanicPreviewImage.color    = Color.white;
            nextMechanicPreviewImage.gameObject.SetActive(true);
        }
    }

    void HideMechanicPreview()
    {
        if (nextMechanicLabel != null) nextMechanicLabel.gameObject.SetActive(false);
        if (nextMechanicPreviewImage != null) nextMechanicPreviewImage.gameObject.SetActive(false);
        if (previewBgImage != null) previewBgImage.gameObject.SetActive(false);
    }
}
