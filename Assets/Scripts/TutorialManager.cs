using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [System.Serializable]
    public struct LevelTutorial
    {
        public string levelDisplayName; 
        public int levelIndex;
        public Vector2Int[] path;
        [Tooltip("Elin merkezden ne kadar sapacağını belirler (Pixel cinsinden)")]
        public Vector2 handOffset; 
    }

    [Header("UI Tanımlamaları")]
    public RectTransform handImage; 

    [Header("Seviye Bazlı Eğitimler")]
    public List<LevelTutorial> levelTutorials = new List<LevelTutorial>();

    public float durationPerSegment = 0.8f;

    private Camera cam;
    private Sequence currentSeq;
    private LevelTutorial activeTutorial;
    private Vector2 lastTrackedOffset;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;

        // --- 11. Level Tutorial Otomatik Ekleme ---
        bool level11Found = false;
        foreach (var tut in levelTutorials)
        {
            if (tut.levelIndex == 10) { level11Found = true; break; }
        }

        if (!level11Found)
        {
            levelTutorials.Add(new LevelTutorial
            {
                levelDisplayName = "Level 11",
                levelIndex = 10,
                path = new Vector2Int[] { new Vector2Int(0, 2), new Vector2Int(0, 1) },
                handOffset = new Vector2(0, -40f) // Elin parçanın biraz altında durması için
            });
        }
    }

    private void Start()
    {
        Invoke("StartTutorial", 0.5f);
    }

    private void Update()
    {
        // Inspector'dan yapılan değişikliği canlı yakalamak için
        if (Application.isPlaying && handImage != null && handImage.gameObject.activeInHierarchy)
        {
            // O anki levelin offsetini kontrol et
            foreach (var tut in levelTutorials)
            {
                if (tut.levelIndex == FindObjectOfType<GridSpawner>()?.currentLevelIndex)
                {
                    if (tut.handOffset != lastTrackedOffset)
                    {
                        Debug.Log($"<color=cyan>[Tutorial]</color> Offset Değişikliği Algılandı: {tut.handOffset}. Resetleniyor...");
                        lastTrackedOffset = tut.handOffset;
                        StartTutorial();
                    }
                    break;
                }
            }
        }
    }

    public void StartTutorial()
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null) return;

        bool found = false;
        foreach (var tut in levelTutorials)
        {
            if (tut.levelIndex == spawner.currentLevelIndex)
            {
                activeTutorial = tut;
                lastTrackedOffset = tut.handOffset;
                found = true;
                break;
            }
        }

        if (!found)
        {
            if (handImage != null) handImage.gameObject.SetActive(false);
            return;
        }

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
            
            currentSeq.AppendInterval(0.2f);
            currentSeq.AppendCallback(() => {
                if (spawner == null) return;
                Vector3 basePos = cam.WorldToScreenPoint(spawner.GetWorldPosition(activeTutorial.path[0]));
                handImage.position = basePos + (Vector3)activeTutorial.handOffset;
            });
            
            currentSeq.Append(cg.DOFade(1f, 0.3f));
            currentSeq.Join(handImage.DOScale(0.9f, 0.3f).SetEase(Ease.OutBack));
            
            for (int i = 1; i < activeTutorial.path.Length; i++)
            {
                int nextIndex = i;
                currentSeq.Append(handImage.DOMove(cam.WorldToScreenPoint(spawner.GetWorldPosition(activeTutorial.path[nextIndex])) + (Vector3)activeTutorial.handOffset, durationPerSegment)
                    .SetEase(Ease.InOutSine));
            }
            
            currentSeq.Append(handImage.DOScale(1f, 0.3f));
            currentSeq.Join(cg.DOFade(0f, 0.3f));
            
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
    }
}
