using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [System.Serializable]
    public struct LevelTutorial
    {
        public string levelDisplayName; 
        public LevelData levelAsset;
        public int levelIndex;
        public Vector2Int[] path;
        [Tooltip("Elin merkezden ne kadar sapacağını belirler (Pixel cinsinden)")]
        public Vector2 handOffset; 

        [Header("Özel Panel Ayarları")]
        public bool showSpecialPanel;
        [TextArea(3, 5)]
        public string specialText;
    }

    [Header("UI Tanımlamaları")]
    public RectTransform handImage; 

    [Header("Özel Seviye 6 Tutorial")]
    public GameObject specialTutorialPanel;
    public TextMeshProUGUI specialTutorialText;

    [Header("Seviye Bazlı Eğitimler")]
    public List<LevelTutorial> levelTutorials = new List<LevelTutorial>();

    public float durationPerSegment = 0.8f;

    private Camera cam;
    private Sequence currentSeq;
    private LevelTutorial activeTutorial;
    private Vector2 lastTrackedOffset;
    private int lastTrackedLevelIndex = -1;
    private int _rotationTutorialStep = 0;

    // ── Level 1 Magic Sort (Önce Tıkla, Tıklanınca Sağa Kaydır) ──
    private enum Level1Step { TapLeft, SlideRight }
    private Level1Step _level1Step = Level1Step.TapLeft;
    private bool _isLevel1TutorialActive = false;
    private DragObject _cachedLeftBottle;
    private DragObject _cachedRightBottle;
    private LiquidTransfer _lastSelectedBottle;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void Start()
    {
        Invoke(nameof(StartTutorial), 0.5f);
    }

    /// <summary>Bu levelde tutorial eli gösteriliyor mu (analitik için).</summary>
    public bool TutorialActiveForCurrentLevel { get; private set; }

    private void Update()
    {
        if (!Application.isPlaying || handImage == null) return;

        // ── Level 1 Seçim Durumu Canlı Takibi ──
        if (_isLevel1TutorialActive)
        {
            if (LiquidTransfer.SelectedBottle != _lastSelectedBottle)
            {
                _lastSelectedBottle = LiquidTransfer.SelectedBottle;
                if (_lastSelectedBottle != null)
                {
                    OnBottleSelected(_lastSelectedBottle);
                }
                else
                {
                    OnBottleDeselected();
                }
            }
            return;
        }

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.levels == null) return;

        LevelData currentLevel = (spawner.currentLevelIndex < spawner.levels.Count) ? spawner.levels[spawner.currentLevelIndex] : null;

        bool levelChanged = (spawner.currentLevelIndex != lastTrackedLevelIndex);
        
        LevelTutorial currentTut = default;
        bool hasTut = false;
        foreach (var tut in levelTutorials)
        {
            if (tut.levelAsset != null && tut.levelAsset == currentLevel) { currentTut = tut; hasTut = true; break; }
            if (tut.levelIndex == spawner.currentLevelIndex) { currentTut = tut; hasTut = true; break; }
        }

        if (levelChanged || (hasTut && currentTut.handOffset != lastTrackedOffset))
        {
            lastTrackedLevelIndex = spawner.currentLevelIndex;
            _rotationTutorialStep = 0;

            if (hasTut) lastTrackedOffset = currentTut.handOffset;

            CancelInvoke(nameof(StartTutorial));
            Invoke(nameof(StartTutorial), 0.5f);
        }
    }

    [ContextMenu("Force Start Tutorial")]
    public void StartTutorial()
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.levels == null) return;

        LevelData currentLevel = (spawner.currentLevelIndex < spawner.levels.Count) ? spawner.levels[spawner.currentLevelIndex] : null;

        activeTutorial = default;
        bool found = false;
        foreach (var tut in levelTutorials)
        {
            if (tut.levelAsset != null && tut.levelAsset == currentLevel)
            {
                activeTutorial = tut;
                found = true;
                break;
            }
            if (tut.levelIndex == spawner.currentLevelIndex)
            {
                activeTutorial = tut;
                found = true;
                break;
            }
        }

        // --- HARDCODED TUTORIAL KONTROLÜ ---
        if (!found && currentLevel != null)
        {
            if (spawner.currentLevelIndex == 0 || currentLevel.name == "Level_01")
            {
                found = true;
            }
            else if (spawner.currentLevelIndex == 5 || currentLevel.name.Contains("Rotation"))
            {
                found = true;
            }
            else if (spawner.currentLevelIndex == 10 || currentLevel.name.Contains("Linked"))
            {
                found = true;
            }
            else if (currentLevel.name == "Ice")
            {
                found = true;
            }
        }

        TutorialActiveForCurrentLevel = found;

        if (!found)
        {
            if (handImage != null) handImage.gameObject.SetActive(false);
            if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
            return;
        }

        // ── LEVEL 1 MAGIC SORT: Önce Tıkla, Tıklanınca Sağa Kaydır ──
        if (spawner.currentLevelIndex == 0 || (currentLevel != null && currentLevel.name == "Level_01"))
        {
            StartLevel1MagicSortTutorial();
            return;
        }

        // --- LEVEL 6 ROTATION TUTORIAL ADIMLARI ---
        if (spawner.currentLevelIndex == 5 || (currentLevel != null && currentLevel.name.Contains("Rotation")))
        {
            if (_rotationTutorialStep == 0)
            {
                activeTutorial.path = new Vector2Int[] { new Vector2Int(0, 0) };
            }
            else
            {
                activeTutorial.path = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            }
        }

        // --- LEVEL 11 LINKED TUTORIAL ADIMLARI ---
        if (spawner.currentLevelIndex == 10 || (currentLevel != null && currentLevel.name.Contains("Linked")))
        {
            activeTutorial.path = new Vector2Int[] { new Vector2Int(1, 1), new Vector2Int(2, 1) };
        }

        // --- ICE (FROZEN) TUTORIAL ---
        if (currentLevel != null && currentLevel.name == "Ice")
        {
            activeTutorial.path = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        }

        if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
        lastTrackedOffset = activeTutorial.handOffset;

        RunGenericPathTutorial(spawner);
    }

    // ──────────────────────────────────────────────────────────────
    // LEVEL 1: ÖNCE SOLDAN TIKLAT, TIKLANINCA SAĞA KAYDIR
    // ──────────────────────────────────────────────────────────────

    private void StartLevel1MagicSortTutorial()
    {
        _isLevel1TutorialActive = true;
        _lastSelectedBottle = LiquidTransfer.SelectedBottle;

        FindLevel1Bottles(out _cachedLeftBottle, out _cachedRightBottle);

        if (_cachedLeftBottle == null)
        {
            CancelInvoke(nameof(StartLevel1MagicSortTutorial));
            Invoke(nameof(StartLevel1MagicSortTutorial), 0.2f);
            return;
        }

        if (LiquidTransfer.SelectedBottle != null)
        {
            PlayLevel1SlideRightAnimation();
        }
        else
        {
            PlayLevel1TapLeftBottleAnimation();
        }
    }

    private void FindLevel1Bottles(out DragObject leftBottle, out DragObject rightBottle)
    {
        leftBottle = null;
        rightBottle = null;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner != null)
        {
            leftBottle = spawner.GetPieceAt(new Vector2Int(0, 0));
            rightBottle = spawner.GetPieceAt(new Vector2Int(1, 0));
        }

        if (leftBottle == null || rightBottle == null)
        {
            DragObject[] allBottles = FindObjectsOfType<DragObject>();
            if (allBottles != null && allBottles.Length >= 2)
            {
                System.Array.Sort(allBottles, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                leftBottle = allBottles[0];
                rightBottle = allBottles[allBottles.Length - 1];
            }
            else if (allBottles != null && allBottles.Length == 1)
            {
                leftBottle = allBottles[0];
            }
        }
    }

    private Vector3 GetBottleScreenPos(DragObject bottle)
    {
        if (bottle == null) return Vector3.zero;
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 worldPos = bottle.transform.position;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Parmak ucu şişenin alt yarısına (sıvıya) denk gelecek şekilde ergonomik offset:
        Vector3 offset = new Vector3(25f, -45f, 0f);
        if (activeTutorial.handOffset != Vector2.zero)
        {
            offset += (Vector3)activeTutorial.handOffset;
        }

        return screenPos + offset;
    }

    /// <summary>
    /// 1. AŞAMA: Soldaki şişeye tıklamayı gösteren animasyon (dokunup bırakma).
    /// </summary>
    private void PlayLevel1TapLeftBottleAnimation()
    {
        _level1Step = Level1Step.TapLeft;
        if (handImage == null) return;
        if (_cachedLeftBottle == null) FindLevel1Bottles(out _cachedLeftBottle, out _cachedRightBottle);
        if (_cachedLeftBottle == null) return;

        handImage.gameObject.SetActive(true);
        CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        if (currentSeq != null) currentSeq.Kill();
        currentSeq = DOTween.Sequence();

        Vector3 baseScale = Vector3.one * 1.6f;

        currentSeq.AppendCallback(() =>
        {
            if (_cachedLeftBottle != null)
                handImage.position = GetBottleScreenPos(_cachedLeftBottle);
            handImage.localScale = baseScale;
        });

        currentSeq.Append(cg.DOFade(1f, 0.2f));

        // 1. Tıklama Hareketi (parmak basar ve geri kalkar)
        currentSeq.Append(handImage.DOScale(baseScale * 0.76f, 0.26f).SetEase(Ease.OutQuad));
        currentSeq.Append(handImage.DOScale(baseScale, 0.22f).SetEase(Ease.OutBack));
        currentSeq.AppendInterval(0.35f);

        // 2. Tıklama Hareketi
        currentSeq.Append(handImage.DOScale(baseScale * 0.76f, 0.26f).SetEase(Ease.OutQuad));
        currentSeq.Append(handImage.DOScale(baseScale, 0.22f).SetEase(Ease.OutBack));
        currentSeq.AppendInterval(0.55f);

        currentSeq.SetLoops(-1);
    }

    /// <summary>
    /// 2. AŞAMA: Soldaki şişe seçilince/tıklanınca sağdaki şişeye doğru kaydırmayı gösteren animasyon.
    /// </summary>
    private void PlayLevel1SlideRightAnimation()
    {
        _level1Step = Level1Step.SlideRight;
        if (handImage == null) return;
        if (_cachedLeftBottle == null || _cachedRightBottle == null) FindLevel1Bottles(out _cachedLeftBottle, out _cachedRightBottle);
        if (_cachedLeftBottle == null || _cachedRightBottle == null) return;

        handImage.gameObject.SetActive(true);
        CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        if (currentSeq != null) currentSeq.Kill();
        currentSeq = DOTween.Sequence();

        Vector3 baseScale = Vector3.one * 1.6f;

        currentSeq.AppendCallback(() =>
        {
            if (_cachedLeftBottle != null)
                handImage.position = GetBottleScreenPos(_cachedLeftBottle);
            handImage.localScale = baseScale;
        });

        currentSeq.Append(cg.DOFade(1f, 0.2f));

        // Şişeyi tutma / dokunma
        currentSeq.Append(handImage.DOScale(baseScale * 0.82f, 0.22f).SetEase(Ease.OutQuad));

        // Soldan sağdaki şişeye doğru akıcı kaydırma (Slide right!)
        currentSeq.Append(handImage.DOMove(GetBottleScreenPos(_cachedRightBottle), 0.9f).SetEase(Ease.InOutQuad));

        // Sağda bırakma
        currentSeq.Append(handImage.DOScale(baseScale, 0.2f).SetEase(Ease.OutQuad));
        currentSeq.Append(cg.DOFade(0f, 0.22f));
        currentSeq.AppendInterval(0.35f);

        currentSeq.SetLoops(-1);
    }

    public void OnBottleSelected(LiquidTransfer bottle)
    {
        if (_isLevel1TutorialActive)
        {
            PlayLevel1SlideRightAnimation();
        }
    }

    public void OnBottleDeselected()
    {
        if (_isLevel1TutorialActive)
        {
            PlayLevel1TapLeftBottleAnimation();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // DİĞER SEVİYELER İÇİN STANDART PATİKA TUTORIAL'I
    // ──────────────────────────────────────────────────────────────

    private void RunGenericPathTutorial(GridSpawner spawner)
    {
        if (handImage != null && activeTutorial.path != null && activeTutorial.path.Length > 0)
        {
            handImage.gameObject.SetActive(true);
            CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
            if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
            
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0f;

            if (currentSeq != null) currentSeq.Kill();
            currentSeq = DOTween.Sequence();
            
            System.Func<int, Vector3> getPathScreenPos = (idx) => {
                Vector2Int gp = activeTutorial.path[Mathf.Clamp(idx, 0, activeTutorial.path.Length - 1)];
                DragObject piece = spawner.GetPieceAt(gp);
                Vector3 worldPos = (piece != null) ? piece.transform.position : spawner.GetWorldPosition(gp);
                return cam.WorldToScreenPoint(worldPos) + (Vector3)activeTutorial.handOffset;
            };

            Vector3 baseScale = Vector3.one * 1.5f;

            currentSeq.AppendInterval(0.2f);
            currentSeq.AppendCallback(() => {
                handImage.position = getPathScreenPos(0);
                handImage.localScale = baseScale; 
            });
            
            currentSeq.Append(cg.DOFade(1f, 0.3f));

            if (activeTutorial.path.Length == 1)
            {
                currentSeq.Append(handImage.DOScale(baseScale * 0.8f, 0.4f).SetEase(Ease.InOutSine));
                currentSeq.Append(handImage.DOScale(baseScale, 0.4f).SetEase(Ease.InOutSine));
                currentSeq.AppendInterval(0.3f);
            }
            else
            {
                currentSeq.Append(handImage.DOScale(baseScale * 0.9f, 0.3f).SetEase(Ease.OutBack));
                for (int i = 1; i < activeTutorial.path.Length; i++)
                {
                    int nextIndex = i;
                    currentSeq.Append(handImage.DOMove(getPathScreenPos(nextIndex), durationPerSegment)
                        .SetEase(Ease.InOutSine));
                }
                currentSeq.Append(handImage.DOScale(baseScale, 0.3f));
            }
            
            currentSeq.Append(cg.DOFade(0f, 0.3f));
            currentSeq.SetLoops(-1);
        }
    }

    public void HideTutorial()
    {
        _isLevel1TutorialActive = false;
        if (currentSeq != null) currentSeq.Kill();
        if (handImage != null)
        {
            handImage.DOKill();
            handImage.gameObject.SetActive(false);
        }

        if (specialTutorialPanel != null)
        {
            specialTutorialPanel.transform.DOKill();
            specialTutorialPanel.SetActive(false);
        }
    }

    public void OnSpecialTutorialOKPressed()
    {
        if (specialTutorialPanel != null)
        {
            specialTutorialPanel.transform.DOKill();
            specialTutorialPanel.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
                .OnComplete(() => {
                    if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
                    StartTutorial();
                });
        }
        else
        {
            StartTutorial();
        }
    }

    public void OnPieceRotated(DragObject piece)
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return;

        LevelData currentLevel = (spawner.currentLevelIndex < spawner.levels.Count) ? spawner.levels[spawner.currentLevelIndex] : null;
        if (spawner.currentLevelIndex == 5 || (currentLevel != null && currentLevel.name.Contains("Rotation")))
        {
            if (_rotationTutorialStep == 0)
            {
                _rotationTutorialStep = 1;
                StartTutorial();
            }
        }
    }
}
