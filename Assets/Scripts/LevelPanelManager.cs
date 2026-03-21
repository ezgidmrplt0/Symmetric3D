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

        completePanelRoot.SetActive(true);
        completePanelRoot.transform.localScale = Vector3.zero;
        completePanelRoot.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(true);
            nextLevelButton.interactable = true;
        }

        SetFill(GameManager.Instance.previousTotalProgress / 100f);
        if (newMechanicUnlockBanner != null) newMechanicUnlockBanner.SetActive(false);

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DOTween.To(() => currentFill, x => SetFill(x), GameManager.Instance.totalProgress / 100f, barAnimDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true)
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
                        GameManager.Instance.GetTypeForProgress(GameManager.Instance.lifetimeProgress, out LevelData.LevelType displayType);
                        ShowUnlockPopup(displayType);
                    }
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
        // Açık paneller varsa kapat
        if (completePanelRoot != null && completePanelRoot.activeInHierarchy)
            completePanelRoot.SetActive(false);
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
            // Şimdi next level butonunu aç
            if (nextLevelButton != null) nextLevelButton.interactable = true;
        });
    }
}
