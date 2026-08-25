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

    [Header("Transfer Yönü Tutorial")]
    [Tooltip("Hangi level index'inde aktif olsun (0 tabanlı, Level 6 = 5)")]
    public int transferTutorialLevelIndex = 5;
    [Tooltip("Tutorial sadece giver bu kadar slice'a sahipken tetiklensin (2 = yarım)")]
    public int transferTutorialTriggerSlices = 2;
    [Tooltip("Bırakıldıktan kaç saniye sonra oyun donsun (sıvı animasyonunun yerleşmesi için)")]
    public float transferFreezeDelay = 0.35f;
    public RectTransform forbiddenDropXIcon;    // Yanlış hamle X ikonu (UI Image)
    public RectTransform transferArrowUI;       // Yön oku (UI Image)
    public CanvasGroup transferTutorialOverlay; // Yarı saydam karartma paneli
    public TextMeshProUGUI tapToContinueText;   // "Devam için dokun" yazısı

    private LiquidTransfer _pendingReceiver;
    private LiquidTransfer _pendingGiver;
    private bool _waitingForTransferTap = false;
    private float _transferTutorialStartRealtime;
    private LiquidTransfer _lastAdjacentQuarter; // Redirect sırasında tespit edilen komşu çeyrek
    private Vector3 _receiverOriginalScale;

    [Header("Seviye Bazlı Eğitimler")]
    public List<LevelTutorial> levelTutorials = new List<LevelTutorial>();

    public float durationPerSegment = 0.8f;

    private Camera cam;
    private Sequence currentSeq;
    private LevelTutorial activeTutorial;
    private Vector2 lastTrackedOffset;
    private int lastTrackedLevelIndex = -1;
    private int _rotationTutorialStep = 0;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void Start()
    {
        Invoke("StartTutorial", 0.5f);
    }

    private void Update()
    {
        // Transfer tutorial tap detection (timeScale = 0 olduğu için realtimeSinceStartup kullan)
        if (_waitingForTransferTap && Time.realtimeSinceStartup - _transferTutorialStartRealtime > 0.4f)
        {
            bool tapped = Input.GetMouseButtonDown(0);
            if (!tapped && Input.touchCount > 0)
                tapped = Input.GetTouch(0).phase == TouchPhase.Began;

            if (tapped)
                OnTransferTutorialTapped();
        }

        if (!Application.isPlaying || handImage == null) return;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.levels == null) return;

        // Mevcut aktif level datasını al
        LevelData currentLevel = (spawner.currentLevelIndex < spawner.levels.Count) ? spawner.levels[spawner.currentLevelIndex] : null;

        // Level veya Offset değişikliğini canlı yakalamak için
        bool levelChanged = (spawner.currentLevelIndex != lastTrackedLevelIndex);
        
        // Mevcut levelin tutorial verisini bul (Asset üzerinden veya Index üzerinden eşle)
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

            CancelInvoke("StartTutorial");
            Invoke("StartTutorial", 0.5f);
        }
    }

    [ContextMenu("Force Start Tutorial")]
    public void StartTutorial()
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.levels == null) return;

        // --- MEVCUT LEVELİN TUTORIAL VERİSİNİ BUL ---
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

        if (!found)
        {
            if (handImage != null) handImage.gameObject.SetActive(false);
            if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
            return;
        }

        // --- LEVEL 6 ROTATION TUTORIAL ADIMLARI ---
        if (spawner.currentLevelIndex == 5 || (currentLevel != null && currentLevel.name.Contains("Rotation")))
        {
            if (_rotationTutorialStep == 0)
            {
                // Adım 1: Objenin üzerine tek dokunuş (Tap / Rotate)
                activeTutorial.path = new Vector2Int[] { new Vector2Int(0, 0) };
            }
            else
            {
                // Adım 2: Objeyi sağdaki boş hücreye sürükleme (Drag / Transfer)
                activeTutorial.path = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            }
        }

        // --- LEVEL 11 LINKED TUTORIAL ADIMLARI ---
        if (spawner.currentLevelIndex == 10 || (currentLevel != null && currentLevel.name.Contains("Linked")))
        {
            // Mor parçadan (1, 1) sağdaki boş hücreye (2, 1) 1 grid sağa sürükle
            activeTutorial.path = new Vector2Int[] { new Vector2Int(1, 1), new Vector2Int(2, 1) };
        }

        // --- ÖZEL PANEL KONTROLÜ (KAPALI) ---
        if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);

        lastTrackedOffset = activeTutorial.handOffset;


        if (handImage != null && activeTutorial.path.Length > 0)
        {
            handImage.gameObject.SetActive(true);
            CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
            if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
            
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0f;

            if (currentSeq != null) currentSeq.Kill();
            currentSeq = DOTween.Sequence();
            
            // --- HEDEF POZİSYON HESAPLAMA (Nesne Odaklı) ---
            System.Func<int, Vector3> getPathScreenPos = (idx) => {
                Vector2Int gp = activeTutorial.path[Mathf.Clamp(idx, 0, activeTutorial.path.Length - 1)];
                DragObject piece = spawner.GetPieceAt(gp);
                Vector3 worldPos = (piece != null) ? piece.transform.position : spawner.GetWorldPosition(gp);
                return cam.WorldToScreenPoint(worldPos) + (Vector3)activeTutorial.handOffset;
            };

            currentSeq.AppendInterval(0.2f);
            currentSeq.AppendCallback(() => {
                handImage.position = getPathScreenPos(0);
                handImage.localScale = Vector3.one; 
            });
            
            currentSeq.Append(cg.DOFade(1f, 0.3f));

            if (activeTutorial.path.Length == 1)
            {
                // --- TIKLAMA (TAP) ANİMASYONU ---
                // El sadece orada durur ve üzerine tıklıyormuş gibi küçülüp büyür.
                currentSeq.Append(handImage.DOScale(0.8f, 0.4f).SetEase(Ease.InOutSine));
                currentSeq.Append(handImage.DOScale(1.0f, 0.4f).SetEase(Ease.InOutSine));
                currentSeq.AppendInterval(0.3f);
            }
            else
            {
                // --- SÜRÜKLEME (DRAG) ANİMASYONU ---
                currentSeq.Append(handImage.DOScale(0.9f, 0.3f).SetEase(Ease.OutBack));
                for (int i = 1; i < activeTutorial.path.Length; i++)
                {
                    int nextIndex = i;
                    currentSeq.Append(handImage.DOMove(getPathScreenPos(nextIndex), durationPerSegment)
                        .SetEase(Ease.InOutSine));
                }
                currentSeq.Append(handImage.DOScale(1f, 0.3f));
            }
            
            currentSeq.Append(cg.DOFade(0f, 0.3f));
            currentSeq.SetLoops(-1);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // TRANSFER YÖN TUTORIAL
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// LiquidTransfer tarafından çağrılır. Eğer tutorial aktifse transfer bekletilir ve true döner.
    /// </summary>
    public bool TryInterceptTransfer(LiquidTransfer receiver, LiquidTransfer giver)
    {
        // Zaten bir transfer işleniyorsa yeni intercept alma
        if (_waitingForTransferTap) return false;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.currentLevelIndex != transferTutorialLevelIndex) return false;

        // Sadece belirtilen slice sayısındaki giver'lar için tetikle (ör. 1 = çeyrek)
        if (giver.currentSlices != transferTutorialTriggerSlices) return false;

        _pendingReceiver = receiver;
        _pendingGiver    = giver;

        ShowTransferTutorial(receiver, giver);
        return true;
    }

    void ShowTransferTutorial(LiquidTransfer receiver, LiquidTransfer giver)
    {
        // Hemen intercept'i kilitle (yeni trigger gelmesin)
        _waitingForTransferTap = true;

        // El animasyonunu gizle
        if (currentSeq != null) currentSeq.Kill();
        if (handImage != null) handImage.gameObject.SetActive(false);

        // Sıvı yerleşsin, sonra dondur ve UI'ı göster
        DOVirtual.DelayedCall(transferFreezeDelay, () =>
        {
            if (receiver == null || giver == null) { _waitingForTransferTap = false; return; }
            Time.timeScale = 0f;
            _transferTutorialStartRealtime = Time.realtimeSinceStartup;
            ShowTransferTutorialUI(receiver, giver);
        });
    }

    void ShowTransferTutorialUI(LiquidTransfer receiver, LiquidTransfer giver)
    {
        // Karartma overlay
        if (transferTutorialOverlay != null)
        {
            transferTutorialOverlay.gameObject.SetActive(true);
            transferTutorialOverlay.alpha = 0f;
            transferTutorialOverlay.DOFade(0.55f, 0.3f).SetUpdate(true);
        }

        // Ok animasyonu: giver → receiver (screen space)
        if (transferArrowUI != null && cam != null)
        {
            Vector3 giverScreen   = cam.WorldToScreenPoint(giver.transform.position);
            Vector3 receiverScreen = cam.WorldToScreenPoint(receiver.transform.position);

            // Oku giver pozisyonuna koy, receiver'a döndür
            transferArrowUI.gameObject.SetActive(true);
            transferArrowUI.position = giverScreen;

            Vector2 dir = ((Vector2)receiverScreen - (Vector2)giverScreen).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transferArrowUI.rotation = Quaternion.Euler(0f, 0f, angle);

            CanvasGroup arrowCG = transferArrowUI.GetComponent<CanvasGroup>();
            if (arrowCG == null) arrowCG = transferArrowUI.gameObject.AddComponent<CanvasGroup>();
            arrowCG.alpha = 0f;
            arrowCG.interactable = false;
            arrowCG.blocksRaycasts = false;

            // Fade in → yavaş smooth hareket → fade out → başa dön (loop)
            Sequence arrowSeq = DOTween.Sequence().SetUpdate(true).SetId("TransferArrow");
            arrowSeq.Append(arrowCG.DOFade(1f, 0.45f).SetEase(Ease.OutQuad));
            arrowSeq.Append(transferArrowUI.DOMove(receiverScreen, 1.3f).SetEase(Ease.InOutSine));
            arrowSeq.Append(arrowCG.DOFade(0f, 0.45f).SetEase(Ease.InQuad));
            arrowSeq.AppendCallback(() => transferArrowUI.position = giverScreen);
            arrowSeq.AppendInterval(0.25f);
            arrowSeq.SetLoops(-1);
        }

        // Receiver parlama (pulse)
        if (receiver.transform.parent != null)
        {
            _receiverOriginalScale = receiver.transform.parent.localScale;
            receiver.transform.parent.DOKill();
            receiver.transform.parent
                .DOScale(_receiverOriginalScale * 1.18f, 0.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        // "Devam için dokun" yazısı
        if (tapToContinueText != null)
        {
            tapToContinueText.gameObject.SetActive(true);
            tapToContinueText.DOFade(0f, 0f).SetUpdate(true);
            tapToContinueText.DOFade(1f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
    }

    void OnTransferTutorialTapped()
    {
        _waitingForTransferTap = false;

        // Ok döngüsünü durdur
        DOTween.Kill("TransferArrow");

        // Tüm elemanları aynı anda smooth kapat (timeScale hâlâ 0, SetUpdate(true))
        Sequence closeSeq = DOTween.Sequence().SetUpdate(true);

        if (transferTutorialOverlay != null)
            closeSeq.Join(transferTutorialOverlay.DOFade(0f, 0.5f).SetEase(Ease.InQuad));

        if (transferArrowUI != null)
        {
            CanvasGroup arrowCG = transferArrowUI.GetComponent<CanvasGroup>();
            if (arrowCG != null)
                closeSeq.Join(arrowCG.DOFade(0f, 0.4f).SetEase(Ease.InQuad));
        }

        if (tapToContinueText != null)
        {
            tapToContinueText.DOKill();
            closeSeq.Join(tapToContinueText.DOFade(0f, 0.4f).SetEase(Ease.InQuad));
        }

        if (_pendingReceiver != null && _pendingReceiver.transform.parent != null)
        {
            _pendingReceiver.transform.parent.DOKill();
            closeSeq.Join(_pendingReceiver.transform.parent
                .DOScale(_receiverOriginalScale, 0.35f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true));
        }

        LiquidTransfer capturedReceiver = _pendingReceiver;
        LiquidTransfer capturedGiver    = _pendingGiver;
        _pendingReceiver = null;
        _pendingGiver    = null;

        closeSeq.OnComplete(() =>
        {
            if (transferTutorialOverlay != null) transferTutorialOverlay.gameObject.SetActive(false);
            if (transferArrowUI != null)         transferArrowUI.gameObject.SetActive(false);
            if (tapToContinueText != null)       tapToContinueText.gameObject.SetActive(false);

            Time.timeScale = 1f;

            if (capturedReceiver != null && capturedGiver != null)
                capturedReceiver.StartTransfer(capturedGiver);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // DROP YÖNLENDİRME
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Yanlış hamle tespit edilirse hedef hücrenin karşı tarafındaki boş hücreyi döner.
    /// null dönerse yönlendirme yok, normal drop devam eder.
    /// </summary>
    public Transform GetDropRedirect(DragObject piece, Transform targetGrid, GridSpawner spawner)
    {
        if (spawner.currentLevelIndex != transferTutorialLevelIndex) return null;

        LiquidTransfer pieceLT = piece.GetComponentInChildren<LiquidTransfer>();
        if (pieceLT == null || pieceLT.currentSlices != transferTutorialTriggerSlices) return null;

        // Hedef hücrenin yanında aynı renkte başka bir çeyrek var mı?
        LiquidTransfer adjacentQuarter = FindAdjacentQuarter(targetGrid.position, piece, spawner, pieceLT.liquidColor);
        if (adjacentQuarter == null) { _lastAdjacentQuarter = null; return null; }
        _lastAdjacentQuarter = adjacentQuarter;

        float step = spawner.gridPrefab.transform.localScale.x + spawner.spacing;

        // Parça komşu çeyreğin SAĞINA mı bırakılıyor? (yanlış yön → yönlendir)
        // Soluna bırakılıyorsa doğru hamle → geçir
        if (targetGrid.position.x > adjacentQuarter.transform.position.x - step * 0.5f)
            return null;

        // Çeyreğin hedef hücreye göre yönü
        Vector3 dirToQuarter = (adjacentQuarter.transform.position - targetGrid.position).normalized;

        GameObject[] gridObjs = GameObject.FindGameObjectsWithTag("Grid");
        float occupied = step * 0.9f;

        // 1. Önce karşı taraftaki bitişik boş hücreyi dene
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (GameObject g in gridObjs)
        {
            if (!g.activeInHierarchy || g.name.Contains("Blocked")) continue;
            Vector3 pos = g.transform.position;

            float d = Vector3.Distance(pos, targetGrid.position);
            if (d < 0.01f || d >= step * 1.4f) continue;

            Vector3 dirToCell = (pos - targetGrid.position).normalized;
            if (Vector3.Dot(dirToCell, dirToQuarter) > -0.3f) continue;

            bool empty = true;
            foreach (DragObject obj in FindObjectsOfType<DragObject>())
            {
                if (obj == piece) continue;
                if (Vector3.Distance(obj.transform.position, pos) < occupied) { empty = false; break; }
            }
            if (!empty) continue;

            if (d < bestDist) { bestDist = d; best = g.transform; }
        }

        if (best != null) return best;

        // 2. Karşı taraf doluysa boarddaki en yakın boş hücreye kay
        bestDist = float.MaxValue;
        foreach (GameObject g in gridObjs)
        {
            if (!g.activeInHierarchy || g.name.Contains("Blocked")) continue;
            Vector3 pos = g.transform.position;

            float d = Vector3.Distance(pos, targetGrid.position);
            if (d < 0.01f || d > bestDist) continue;

            bool empty = true;
            foreach (DragObject obj in FindObjectsOfType<DragObject>())
            {
                if (obj == piece) continue;
                if (Vector3.Distance(obj.transform.position, pos) < occupied) { empty = false; break; }
            }
            if (!empty) continue;

            best = g.transform;
            bestDist = d;
        }

        return best; // Tüm board doluysa null (edge case)
    }

    /// <summary>
    /// Redirect sonrası çağrılır. Komşu çeyrekten yönlendirilen hücreye el animasyonu başlatır.
    /// </summary>
    public void OnDropRedirected(Transform redirectCell, Transform originalTarget)
    {
        if (_lastAdjacentQuarter == null || cam == null) return;

        LiquidTransfer quarter = _lastAdjacentQuarter;
        _lastAdjacentQuarter = null;

        // Orijinal hedefe X ikonu göster
        ShowForbiddenX(originalTarget.position);

        // DropFlat2D animasyonu bitsin (0.25s), sonra eli göster
        DOVirtual.DelayedCall(0.3f, () =>
        {
            if (quarter == null) return;
            Vector3 fromScreen = cam.WorldToScreenPoint(quarter.transform.position)
                                 + (Vector3)activeTutorial.handOffset;
            Vector3 toScreen   = cam.WorldToScreenPoint(redirectCell.position)
                                 + (Vector3)activeTutorial.handOffset;
            PlayHandAnimationFromTo(fromScreen, toScreen);
        });
    }

    void ShowForbiddenX(Vector3 worldPos)
    {
        if (forbiddenDropXIcon == null || cam == null) return;

        forbiddenDropXIcon.position   = cam.WorldToScreenPoint(worldPos);
        forbiddenDropXIcon.localScale = Vector3.zero;
        forbiddenDropXIcon.gameObject.SetActive(true);

        forbiddenDropXIcon.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(forbiddenDropXIcon.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.45f);
        seq.Append(forbiddenDropXIcon.DOScale(0f, 0.2f).SetEase(Ease.InBack));
        seq.OnComplete(() => forbiddenDropXIcon.gameObject.SetActive(false));
    }

    void PlayHandAnimationFromTo(Vector3 fromScreen, Vector3 toScreen)
    {
        if (handImage == null) return;
        if (currentSeq != null) currentSeq.Kill();

        handImage.gameObject.SetActive(true);
        CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        currentSeq = DOTween.Sequence();
        currentSeq.AppendInterval(0.15f);
        currentSeq.AppendCallback(() => {
            handImage.position   = fromScreen;
            handImage.localScale = Vector3.one;
        });
        currentSeq.Append(cg.DOFade(1f, 0.3f));
        currentSeq.Append(handImage.DOScale(0.9f, 0.25f).SetEase(Ease.OutBack));
        currentSeq.Append(handImage.DOMove(toScreen, durationPerSegment * 2f).SetEase(Ease.InOutSine));
        currentSeq.Append(handImage.DOScale(1f, 0.2f));
        currentSeq.Append(cg.DOFade(0f, 0.3f));
        currentSeq.SetLoops(-1);
    }

    LiquidTransfer FindAdjacentQuarter(Vector3 targetPos, DragObject excludePiece, GridSpawner spawner, Color matchColor)
    {
        float step      = spawner.gridPrefab.transform.localScale.x + spawner.spacing;
        float threshold = step * 1.3f;

        foreach (LiquidTransfer lt in FindObjectsOfType<LiquidTransfer>())
        {
            if (lt.GetComponentInParent<DragObject>() == excludePiece) continue;
            if (lt.currentSlices != transferTutorialTriggerSlices) continue;
            if (!ColorMixData.ColorsMatch(lt.liquidColor, matchColor)) continue;
            if (Vector3.Distance(lt.transform.position, targetPos) < threshold)
                return lt;
        }
        return null;
    }

    public void HideTutorial()
    {
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
