using UnityEngine;
using DG.Tweening;

public class ObjectRotator : MonoBehaviour
{
    [Header("Dönüş Ayarları")]
    [Tooltip("Obje tıklandığında hangi eksende ve kaç derece dönecek? (Örn: Z ekseninde 90 derece için 0, 0, 90)")]
    public Vector3 rotationAmount = new Vector3(0, 0, 90);
    
    [Tooltip("Dönüşün ne kadar saniye süreceği")]
    public float rotateDuration = 0.3f;


    private bool isRotating = false;

    public void RotateObject()
    {
        if (isRotating) return;

        isRotating = true;

        // Objeyi kendi animasyon motorunuzla (DOTween) pürüzsüzce 90 derece döndür
        // RotateMode.WorldAxisAdd yerine LocalAxisAdd daha kararlı rotasyon sağlar
        transform.DORotate(rotationAmount, rotateDuration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutBack) // Animasyonun sonunda tatlı bir sekme efekti
            .OnComplete(() => 
            {
                isRotating = false;
                
                // Rotasyon bitince sıvı aktarımı (dilim birleştirme) için kontrol et
                LiquidTransfer liquidTransfer = GetComponentInChildren<LiquidTransfer>();
                if(liquidTransfer != null)
                {
                    liquidTransfer.CheckSymmetry();
                }
            });
    }
}
