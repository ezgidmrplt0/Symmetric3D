using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class NewLevelUnlockPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public CanvasGroup canvasGroup;
    public RectTransform popupWindow;
    
    [Space(10)]
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI levelNameText;
    public Image rewardImage;
    public Button okButton;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (okButton != null) okButton.onClick.AddListener(Hide);
    }

    public void Show(LevelData.LevelType type)
    {
        // UI Metinlerini ve Görüntülerini Ayarla
        if (headerText != null) headerText.text = "Level Unlocked!";
        if (levelNameText != null) levelNameText.text = type.ToString();
        
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
