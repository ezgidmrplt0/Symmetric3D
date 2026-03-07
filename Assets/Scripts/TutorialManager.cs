using UnityEngine;
using DG.Tweening;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("UI Tanımlamaları")]
    [Tooltip("El objesi (Image) buraya sürüklenip bırakılacak")]
    public RectTransform handImage; 

    [Header("Animasyon Yolu (Grid Koordinatları)")]
    [Tooltip("Elin sırayla gideceği grid noktaları. (Haritada sol alt köşe X:0, Y:0'dır.)")]
    public Vector2Int[] gridPath = new Vector2Int[] { new Vector2Int(1, 0), new Vector2Int(0, 0) }; 
    public float durationPerSegment = 0.8f;

    private Camera cam;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void Start()
    {
        // Objerin var olması için yarım saniye gecikmeli başlatıyoruz
        Invoke("StartTutorial", 0.5f);
    }

    private void StartTutorial()
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();

        // Sadece level 1 (0. index) için öğretici gösterilsin
        if (spawner != null && spawner.currentLevelIndex != 0)
        {
            gameObject.SetActive(false);
            return;
        }

        if (handImage != null && gridPath.Length > 0 && spawner != null)
        {
            CanvasGroup cg = handImage.GetComponent<CanvasGroup>();
            if (cg == null) cg = handImage.gameObject.AddComponent<CanvasGroup>();
            
            cg.alpha = 0f;

            Sequence seq = DOTween.Sequence();
            
            // Her döngü başında pozisyonu (Ekran çözünürlüğü değişse bile) doğru hesaplar
            seq.AppendInterval(0.3f);
            seq.AppendCallback(() => {
                handImage.position = cam.WorldToScreenPoint(spawner.GetWorldPosition(gridPath[0]));
            });
            
            // Görün ve küçül
            seq.Append(cg.DOFade(1f, 0.3f));
            seq.Join(handImage.DOScale(0.9f, 0.3f).SetEase(Ease.OutBack));
            
            // Noktaları dolaş (DOMove komutu RectTransform'da Screen Space pozisyonlarına sorunsuz gider)
            for (int i = 1; i < gridPath.Length; i++)
            {
                int index = i;
                seq.Append(handImage.DOMove(cam.WorldToScreenPoint(spawner.GetWorldPosition(gridPath[index])), durationPerSegment)
                    .SetEase(Ease.InOutSine));
            }
            
            // İş bitince büyü ve kaybol
            seq.Append(handImage.DOScale(1f, 0.3f));
            seq.Join(cg.DOFade(0f, 0.3f));
            
            seq.SetLoops(-1);
        }
    }

    public void HideTutorial()
    {
        if (gameObject.activeSelf)
        {
            if (handImage != null) 
            {
                handImage.DOKill(); 
            }
            gameObject.SetActive(false); 
        }
    }
}
