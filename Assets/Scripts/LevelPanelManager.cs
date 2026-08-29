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
        public float scaleMultiplier;
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

    void SetupPreviewFillSupport(float scaleMultiplier = 1.0f)
    {
        if (nextMechanicPreviewImage == null) return;

        if (scaleMultiplier <= 0f) scaleMultiplier = 1.0f;
        float iconSize = 480f * scaleMultiplier;

        // 1. LayoutGroup kısıtlamalarını kaldırmak için LayoutElement ekle/ayarla
        UnityEngine.UI.LayoutElement srcLayout = nextMechanicPreviewImage.GetComponent<UnityEngine.UI.LayoutElement>();
        if (srcLayout == null) srcLayout = nextMechanicPreviewImage.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        srcLayout.ignoreLayout = true;
        srcLayout.preferredWidth = iconSize;
        srcLayout.preferredHeight = iconSize;
        srcLayout.minWidth = iconSize;
        srcLayout.minHeight = iconSize;

        Vector2 idealIconSize = new Vector2(iconSize, iconSize);

        RectTransform srcRect = nextMechanicPreviewImage.rectTransform;
        srcRect.localScale = Vector3.one;
        srcRect.anchorMin = new Vector2(0.5f, 0.5f);
        srcRect.anchorMax = new Vector2(0.5f, 0.5f);
        srcRect.pivot = new Vector2(0.5f, 0.5f);
        srcRect.sizeDelta = idealIconSize;
        srcRect.anchoredPosition = new Vector2(0f, 10f);

        if (previewBgImage == null)
        {
            GameObject bgObj = new GameObject("NextMechanic_SilhouetteBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.LayoutElement));
            bgObj.transform.SetParent(nextMechanicPreviewImage.transform.parent, false);
            bgObj.transform.SetSiblingIndex(nextMechanicPreviewImage.transform.GetSiblingIndex());

            UnityEngine.UI.LayoutElement bgLayout = bgObj.GetComponent<UnityEngine.UI.LayoutElement>();
            bgLayout.ignoreLayout = true;

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.localScale = Vector3.one;
            bgRect.anchorMin = srcRect.anchorMin;
            bgRect.anchorMax = srcRect.anchorMax;
            bgRect.anchoredPosition = srcRect.anchoredPosition;
            bgRect.sizeDelta = srcRect.sizeDelta;
            bgRect.pivot = srcRect.pivot;

            previewBgImage = bgObj.GetComponent<Image>();
            previewBgImage.raycastTarget = false;
            previewBgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        }
        else
        {
            UnityEngine.UI.LayoutElement bgLayout = previewBgImage.GetComponent<UnityEngine.UI.LayoutElement>();
            if (bgLayout == null) bgLayout = previewBgImage.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            bgLayout.ignoreLayout = true;

            RectTransform bgRect = previewBgImage.rectTransform;
            bgRect.localScale = Vector3.one;
            bgRect.anchorMin = srcRect.anchorMin;
            bgRect.anchorMax = srcRect.anchorMax;
            bgRect.anchoredPosition = srcRect.anchoredPosition;
            bgRect.sizeDelta = srcRect.sizeDelta;
            bgRect.pivot = srcRect.pivot;
        }

        // Yüzdelik textini ikonun tam ortasına devasa ve net yerleştir
        if (progressText != null)
        {
            progressText.transform.SetParent(nextMechanicPreviewImage.transform, false);

            UnityEngine.UI.LayoutElement ptLayout = progressText.GetComponent<UnityEngine.UI.LayoutElement>();
            if (ptLayout == null) ptLayout = progressText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            ptLayout.ignoreLayout = true;

            RectTransform ptRect = progressText.rectTransform;
            ptRect.localScale = Vector3.one;
            ptRect.anchorMin = new Vector2(0.5f, 0.5f);
            ptRect.anchorMax = new Vector2(0.5f, 0.5f);
            ptRect.pivot = new Vector2(0.5f, 0.5f);
            ptRect.sizeDelta = new Vector2(iconSize, 120f);
            ptRect.anchoredPosition = Vector2.zero;

            progressText.alignment = TextAlignmentOptions.Center;
            progressText.verticalAlignment = VerticalAlignmentOptions.Middle;
            progressText.fontStyle = FontStyles.Bold;
            progressText.fontSize = 68f;
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
            if (nextLevelButton != null) { nextLevelButton.onClick.AddListener(OnNextLevelClicked); nextLevelButton.onClick.AddListener(AudioManager.PlayButtonClick); }
        }

        if (failPanelRoot != null)
        {
            failPanelRoot.SetActive(false);
            if (retryButton != null) { retryButton.onClick.AddListener(OnRetryClicked); retryButton.onClick.AddListener(AudioManager.PlayButtonClick); }
        }

        if (unlockPanelRoot != null) unlockPanelRoot.SetActive(false);
        if (unlockOkButton != null) { unlockOkButton.onClick.AddListener(HideUnlockPopup); unlockOkButton.onClick.AddListener(AudioManager.PlayButtonClick); }

        if (resetLevelButton != null) { resetLevelButton.onClick.AddListener(OnResetLevelClicked); resetLevelButton.onClick.AddListener(AudioManager.PlayButtonClick); }

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
            int levelNum = PlayerPrefs.GetInt("CurrentLevelIndex", 0) + 1;
            int cycleLen = Mathf.Max(1, 100 / GameManager.Instance.progressPerLevel);
            int pos = ((levelNum - 1) % cycleLen) + 1;
            GameManager.Instance.previousTotalProgress = (pos - 1) * GameManager.Instance.progressPerLevel;
            GameManager.Instance.totalProgress = pos * GameManager.Instance.progressPerLevel;
            GameManager.Instance.hitProgressHundred = GameManager.Instance.totalProgress >= 100;
            if (GameManager.Instance.hitProgressHundred) GameManager.Instance.totalProgress = 0;
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
        AudioManager.PlayWin();

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
                           GameManager.Instance.GetTypeForProgress(out unlockedType);

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

            Sprite unlockSprite = GetIconForType(unlockedType, out float unlockScale);
            if (nextMechanicPreviewImage != null)
            {
                if (unlockSprite != null)
                {
                    SetupPreviewFillSupport(unlockScale);

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
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (GameManager.Instance != null && GameManager.Instance.hitProgressHundred)
                {
                    Trigger100PercentConfetti();
                }
            });
    }

    Sprite GetIconForType(LevelData.LevelType type, out float scaleMultiplier)
    {
        scaleMultiplier = 1.0f;
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
                {
                    scaleMultiplier = item.scaleMultiplier > 0 ? item.scaleMultiplier : 1.15f;
                    return item.icon;
                }
            }
#if UNITY_EDITOR
            Sprite linkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Linked(1).png");
            if (linkSprite != null) { scaleMultiplier = 1.15f; return linkSprite; }
#endif
        }
        // Level 1-5 -> ROTATION
        else
        {
            foreach (var item in mechanicIcons)
            {
                if (item.icon != null && item.levelType == LevelData.LevelType.Rotation)
                {
                    scaleMultiplier = item.scaleMultiplier > 0 ? item.scaleMultiplier : 1.15f;
                    return item.icon;
                }
            }
#if UNITY_EDITOR
            Sprite rotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Rotation.png");
            if (rotSprite != null) { scaleMultiplier = 1.15f; return rotSprite; }
#endif
            if (shufflePreviewSprite != null) { scaleMultiplier = 1.15f; return shufflePreviewSprite; }
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

    Sprite GetIconForType(LevelData.LevelType type)
    {
        return GetIconForType(type, out _);
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

        AudioManager.PlayButtonClick();

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
        AudioManager.PlayButtonClick();
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
        AudioManager.PlayButtonClick();
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
            Sprite found = GetIconForType(type, out float popupScale);
            unlockRewardImage.sprite = found;
            unlockRewardImage.rectTransform.sizeDelta = new Vector2(400f * popupScale, 400f * popupScale);
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
        AudioManager.PlayButtonClick();
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

        Sprite previewSprite = GetIconForType(nextType, out float previewScale);

        if (previewSprite != null)
        {
            SetupPreviewFillSupport(previewScale);

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

    // ── 🎊 KONFETİ VE KUTLAMA EFEKTİ ──────────────────────────────

    [ContextMenu("Test Confetti Efekti")]
    public void Trigger100PercentConfetti()
    {
        if (completePanelRoot == null) return;

        // İkon ve yüzdelik metnine zıplama (Punch Scale) animasyonu ver
        if (nextMechanicPreviewImage != null)
        {
            nextMechanicPreviewImage.transform.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), 0.65f, 8, 0.8f).SetUpdate(true);
        }
        if (progressText != null)
        {
            progressText.transform.DOPunchScale(new Vector3(0.4f, 0.4f, 0f), 0.65f, 8, 0.8f).SetUpdate(true);
        }

        VibrationManager.VibrateSuccess();

        // UI Konfeti Konteyneri oluştur
        GameObject confettiContainer = new GameObject("UI_Confetti_Container", typeof(RectTransform));
        confettiContainer.transform.SetParent(completePanelRoot.transform, false);
        confettiContainer.transform.SetAsLastSibling();

        RectTransform containerRect = confettiContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        Color[] confettiColors = new Color[]
        {
            new Color(1f, 0.22f, 0.4f),   // Koyu Pembe / Kırmızı
            new Color(1f, 0.82f, 0.1f),   // Altın Sarısı
            new Color(0.2f, 0.9f, 0.35f),  // Parlak Yeşil
            new Color(0.15f, 0.75f, 1f),  // Canlı Mavi
            new Color(0.85f, 0.35f, 1f),  // Neon Mor
            new Color(1f, 0.5f, 0.15f)    // Sıcak Turuncu
        };

        Vector3 originPos = (nextMechanicPreviewImage != null)
            ? nextMechanicPreviewImage.rectTransform.anchoredPosition
            : Vector2.zero;

        int particleCount = 55;
        for (int i = 0; i < particleCount; i++)
        {
            GameObject pObj = new GameObject($"Confetti_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pObj.transform.SetParent(confettiContainer.transform, false);

            RectTransform pRect = pObj.GetComponent<RectTransform>();
            pRect.anchoredPosition = originPos + new Vector3(Random.Range(-25f, 25f), Random.Range(-25f, 25f), 0f);

            float width = Random.Range(12f, 24f);
            float height = Random.Range(8f, 16f);
            pRect.sizeDelta = new Vector2(width, height);
            pRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Image img = pObj.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = confettiColors[Random.Range(0, confettiColors.Length)];

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float force = Random.Range(200f, 480f);
            Vector2 initialVel = new Vector2(Mathf.Cos(angle) * force, Mathf.Sin(angle) * force + Random.Range(120f, 280f));
            float rotSpeed = Random.Range(-450f, 450f);
            float gravity = Random.Range(450f, 750f);

            StartCoroutine(AnimateConfettiPiece(pRect, img, initialVel, rotSpeed, gravity));
        }

        Destroy(confettiContainer, 2.5f);
    }

    System.Collections.IEnumerator AnimateConfettiPiece(RectTransform pRect, Image img, Vector2 velocity, float rotSpeed, float gravity)
    {
        float duration = Random.Range(1.4f, 2.1f);
        float elapsed = 0f;
        Vector2 pos = pRect.anchoredPosition;
        Color startColor = img.color;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            velocity.y -= gravity * dt;
            velocity.x *= Mathf.Pow(0.94f, dt * 60f);
            pos += velocity * dt;
            pRect.anchoredPosition = pos;

            pRect.Rotate(0f, 0f, rotSpeed * dt);

            float alpha = Mathf.Clamp01(1f - (elapsed / duration));
            img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }
    }
}
