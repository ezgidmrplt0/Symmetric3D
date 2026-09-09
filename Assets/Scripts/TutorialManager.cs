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
    private bool isTutorialCompleted = false;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void Start()
    {
        Invoke("StartTutorial", 0.5f);
    }

    /// <summary>Bu levelde tutorial eli gösteriliyor mu (analitik için).</summary>
    public bool TutorialActiveForCurrentLevel { get; private set; }

    /// <summary>
    /// Tutorial kısıtlaması aktif mi: Sadece bu levelde tutorial varsa ve tutorial adımları henüz bitmediyse true döner.
    /// </summary>
    public bool IsRestrictingInput => TutorialActiveForCurrentLevel && !isTutorialCompleted && activeTutorial.path != null && activeTutorial.path.Length > 0;

    public void OnLevelSpawned(int levelIndex)
    {
        lastTrackedLevelIndex = levelIndex;
        _rotationTutorialStep = 0;
        isTutorialCompleted = false;
        HideTutorial();
        CancelInvoke("StartTutorial");
        Invoke("StartTutorial", 0.5f);
    }

    private void Update()
    {
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
            isTutorialCompleted = false;

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

        // --- HARDCODED TUTORIAL: Inspector'da entry olmasa bile çalışır ---
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

        // --- LEVEL 1 CLASSIC TUTORIAL ---
        // Oyunun temel kuralını (ağız ağıza getirme) hiçbir yerde öğretmiyorduk.
        // El, sağ alttaki parçayı sol alta sürükleyerek eşleşmeyi gösterir.
        if (spawner.currentLevelIndex == 0 || (currentLevel != null && currentLevel.name == "Level_01"))
        {
            activeTutorial.path = new Vector2Int[] { new Vector2Int(1, 0), new Vector2Int(0, 0) };
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

    // ──────────────────────────────────────────────────────────────
    // TUTORIAL KISITLAMA VE DENETİM METODLARI
    // ──────────────────────────────────────────────────────────────

    /// <summary>Bu tutorial adımı sadece dokunma (rotate/tap) adımı mı?</summary>
    public bool IsTapTutorialStep()
    {
        return IsRestrictingInput && activeTutorial.path != null && activeTutorial.path.Length == 1;
    }

    /// <summary>Bu tutorial adımı sürükleme (drag) adımı mı?</summary>
    public bool IsDragTutorialStep()
    {
        return IsRestrictingInput && activeTutorial.path != null && activeTutorial.path.Length > 1;
    }

    /// <summary>
    /// Bu parçanın hareket ettirilmesine / seçilmesine izin var mı?
    /// Sadece tutorialın başlangıç hücresindeki parçaya izin verilir.
    /// </summary>
    public bool IsAllowedPiece(DragObject piece)
    {
        if (!IsRestrictingInput || piece == null) return true;
        if (activeTutorial.path == null || activeTutorial.path.Length == 0) return true;

        Vector2Int requiredStart = activeTutorial.path[0];
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return true;

        // 1. Spawner üzerinden başlangıç parçası eşleşmesi
        DragObject expected = spawner.GetPieceAt(requiredStart);
        if (expected != null && expected == piece) return true;

        // 2. LiquidTransfer initialGridPos kontrolü
        LiquidTransfer lt = piece.GetComponentInChildren<LiquidTransfer>();
        if (lt != null && lt.initialGridPos == requiredStart) return true;

        // 3. Dünya koordinatı mesafe kontrolü (hücre merkezine yakınlık)
        Vector3 startWorld = spawner.GetWorldPosition(requiredStart);
        if (Vector2.Distance(piece.transform.position, startWorld) < 0.45f) return true;

        return false;
    }

    /// <summary>
    /// Bağlı grubun (LinkedObjectGroup) hareket ettirilmesine izin var mı?
    /// Sadece tutorial başlangıç parçasını içeren grup seçilebilir.
    /// </summary>
    public bool IsAllowedGroup(LinkedObjectGroup group)
    {
        if (!IsRestrictingInput || group == null) return true;
        if (activeTutorial.path == null || activeTutorial.path.Length == 0) return true;

        Vector2Int requiredStart = activeTutorial.path[0];
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return true;

        DragObject expected = spawner.GetPieceAt(requiredStart);
        if (expected != null && group.childDrags != null && group.childDrags.Contains(expected)) return true;

        if (group.childDrags != null)
        {
            foreach (var child in group.childDrags)
            {
                if (child == null) continue;
                LiquidTransfer lt = child.GetComponentInChildren<LiquidTransfer>();
                if (lt != null && lt.initialGridPos == requiredStart) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sürüklenen pozisyonu başlangıç ve hedef arasındaki doğruya (rota çizgisine) kısıtlar.
    /// Parça başka bir yöne veya hücreye kayamaz.
    /// </summary>
    public Vector3 ConstrainDragPosition(Vector3 desiredPos, Vector3 startPos)
    {
        if (!IsDragTutorialStep()) return desiredPos;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return desiredPos;

        Vector3 startWorld = spawner.GetWorldPosition(activeTutorial.path[0]);
        Vector3 targetWorld = spawner.GetWorldPosition(activeTutorial.path[activeTutorial.path.Length - 1]);
        Vector3 track = targetWorld - startWorld;
        float trackLen = track.magnitude;

        if (trackLen < 0.001f) return desiredPos;

        Vector3 trackDir = track / trackLen;
        Vector3 toDesired = desiredPos - startPos;
        float proj = Vector3.Dot(toDesired, trackDir);
        proj = Mathf.Clamp(proj, 0f, trackLen);

        Vector3 constrained = startPos + trackDir * proj;
        constrained.z = desiredPos.z;
        return constrained;
    }

    /// <summary>
    /// Bağlı grup için sürüklenen pozisyonu rota çizgisine kısıtlar.
    /// </summary>
    public Vector3 ConstrainGroupDragPosition(Vector3 desiredPos, Vector3 groupStartPos)
    {
        if (!IsDragTutorialStep()) return desiredPos;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return desiredPos;

        Vector3 startWorld = spawner.GetWorldPosition(activeTutorial.path[0]);
        Vector3 targetWorld = spawner.GetWorldPosition(activeTutorial.path[activeTutorial.path.Length - 1]);
        Vector3 track = targetWorld - startWorld;
        float trackLen = track.magnitude;

        if (trackLen < 0.001f) return desiredPos;

        Vector3 trackDir = track / trackLen;
        Vector3 toDesired = desiredPos - groupStartPos;
        float proj = Vector3.Dot(toDesired, trackDir);
        proj = Mathf.Clamp(proj, 0f, trackLen);

        Vector3 constrained = groupStartPos + trackDir * proj;
        constrained.z = desiredPos.z;
        return constrained;
    }

    /// <summary>
    /// Bırakılan grid hücresinin tutorial hedef hücresi olup olmadığını kontrol eder.
    /// </summary>
    public bool IsValidDropTarget(Transform targetGrid)
    {
        if (!IsDragTutorialStep()) return true;
        if (targetGrid == null) return false;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return true;

        Vector2Int targetPos = activeTutorial.path[activeTutorial.path.Length - 1];
        Vector3 targetWorld = spawner.GetWorldPosition(targetPos);
        return Vector2.Distance(targetGrid.position, targetWorld) < 0.45f;
    }

    /// <summary>
    /// Bağlı grubun bırakıldığı pozisyonun tutorial hedefiyle eşleşip eşleşmediğini kontrol eder.
    /// </summary>
    public bool IsValidGroupDropPosition(Vector3 proposedGroupPos, Vector3 groupStartPos)
    {
        if (!IsDragTutorialStep()) return true;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return true;

        Vector3 startWorld = spawner.GetWorldPosition(activeTutorial.path[0]);
        Vector3 targetWorld = spawner.GetWorldPosition(activeTutorial.path[activeTutorial.path.Length - 1]);
        Vector3 expectedGroupPos = groupStartPos + (targetWorld - startWorld);

        return Vector2.Distance(proposedGroupPos, expectedGroupPos) < 0.45f;
    }

    /// <summary>Sürükleme başladığında el görselini gizler.</summary>
    public void OnDragStarted()
    {
        if (handImage != null)
        {
            handImage.DOKill();
            handImage.gameObject.SetActive(false);
        }
        if (currentSeq != null) currentSeq.Pause();
    }

    /// <summary>Yanlış hamle yapıldığında veya yarıda bırakıldığında tutorial elini yeniden gösterir.</summary>
    public void ResumeTutorialHand()
    {
        if (IsRestrictingInput)
        {
            CancelInvoke("StartTutorial");
            Invoke("StartTutorial", 0.2f);
        }
    }

    /// <summary>Tutorial adımı başarıyla gerçekleştirildiğinde çağrılır.</summary>
    public void OnTutorialActionCompleted()
    {
        isTutorialCompleted = true;
        HideTutorial();
    }
}
