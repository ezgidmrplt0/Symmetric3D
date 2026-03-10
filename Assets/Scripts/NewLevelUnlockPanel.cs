using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class NewLevelUnlockPanel : MonoBehaviour
{
    // Mekaniklere özel ikonu ayarlamak için oluşturduğumuz struct
    [System.Serializable]
    public struct MechanicIconData
    {
        public LevelData.LevelType levelType;
        public Sprite icon;
    }

    [Header("UI References")]
    public GameObject panelRoot;
    public CanvasGroup canvasGroup;
    public RectTransform popupWindow;
    
    [Space(10)]
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI levelNameText;
    public Image rewardImage;
    public Button okButton;

    [Header("Mekanik İkonları")]
    [Tooltip("Hangi mekanik türü için hangi görselin çıkacağını buradan ayarlayabilirsiniz.")]
    public System.Collections.Generic.List<MechanicIconData> mechanicIcons = new System.Collections.Generic.List<MechanicIconData>();

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (okButton != null) okButton.onClick.AddListener(Hide);
    }

    public void Show(LevelData.LevelType type)
    {
        // UI Metinlerini ve Görüntülerini Ayarla
        if (headerText != null) headerText.text = "New Mechanic!";
        if (levelNameText != null) levelNameText.text = type.ToString();
        
        // Doğru ikonu bul ve uygula
        if (rewardImage != null)
        {
            Sprite foundSprite = null;
            foreach (var item in mechanicIcons)
            {
                if (item.levelType == type)
                {
                    foundSprite = item.icon;
                    break;
                }
            }

            if (foundSprite != null)
            {
                rewardImage.sprite = foundSprite;
                rewardImage.gameObject.SetActive(true);
            }
            else
            {
                // İkon atanmamışsa image'i gizle veya sabit bırak
                rewardImage.gameObject.SetActive(false);
            }
        }
        
        panelRoot.SetActive(true);
        
        // Animasyonlar
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.4f);
        }
        
        if (popupWindow != null)
        {
            popupWindow.localScale = Vector3.zero;
            popupWindow.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        }
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
            {
                panelRoot.SetActive(false);
            });
        }
        else
        {
            panelRoot.SetActive(false);
        }
    }
}
