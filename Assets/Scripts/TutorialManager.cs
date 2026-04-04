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
    private bool specialTutorialClosedForThisLevel = false;

    [Header("Seviye Bazlı Eğitimler")]
    public List<LevelTutorial> levelTutorials = new List<LevelTutorial>();

    public float durationPerSegment = 0.8f;

    private Camera cam;
    private Sequence currentSeq;
    private LevelTutorial activeTutorial;
    private Vector2 lastTrackedOffset;
    private int lastTrackedLevelIndex = -1;

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
            specialTutorialClosedForThisLevel = false;

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

        // --- ÖZEL PANEL KONTROLÜ ---
        if (activeTutorial.showSpecialPanel && !specialTutorialClosedForThisLevel)
        {
            if (specialTutorialPanel != null)
            {
                specialTutorialPanel.SetActive(true);
                if (specialTutorialText != null && !string.IsNullOrEmpty(activeTutorial.specialText))
                {
                    specialTutorialText.text = activeTutorial.specialText;
                }
                
                // Normal tutorial elini gizle
                if (handImage != null) handImage.gameObject.SetActive(false);
                if (currentSeq != null) currentSeq.Kill();
                return; // Normal tutorial'a devam etme (OK butonuna basınca gelicek)
            }
        }
        else
        {
            if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
        }

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

        if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
    }

    public void OnSpecialTutorialOKPressed()
    {
        specialTutorialClosedForThisLevel = true;
        if (specialTutorialPanel != null) specialTutorialPanel.SetActive(false);
        
        // Şimdi normal el eğitimini başlat
        StartTutorial();
    }
}
