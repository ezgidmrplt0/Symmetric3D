using UnityEngine;
using UnityEngine.UI;

public class VibrationToggleButton : MonoBehaviour
{
    [Header("Icon")]
    public Sprite vibrationOnIcon;

    [Header("References")]
    public Image buttonIcon;
    // Sol üst → sağ alt kırmızı çizgi. Hierarchy'de buton child'ı olarak
    // oluştur: Image, beyaz sprite, kırmızı renk, rotation Z = -45, genişliği
    // ikon boyutunun ~1.4x, yüksekliği ince (örn. 6-8 px).
    public Image slashImage;

    void Start()
    {
        UpdateVisual();
    }

    public void OnButtonClicked()
    {
        VibrationManager.Toggle();
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (buttonIcon != null)
            buttonIcon.sprite = vibrationOnIcon;

        if (slashImage != null)
            slashImage.gameObject.SetActive(!VibrationManager.IsEnabled);
    }
}
