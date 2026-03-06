using UnityEngine;
using DG.Tweening;

public class ObjectRotator : MonoBehaviour
{
    [Header("Dönüş Ayarları")]
    [Tooltip("Obje tıklandığında hangi eksende ve kaç derece dönecek? (Örn: Z ekseninde 90 derece için 0, 0, 90)")]
    public Vector3 rotationAmount = new Vector3(0, 0, 90);
    
    [Tooltip("Dönüşün ne kadar saniye süreceği")]
    public float rotateDuration = 0.3f;


    // Objenin üzerinde bir Collider (Örn: SphereCollider veya BoxCollider) olduğu sürece fare ve dokunmatik tıklamaları algılar
    /*
    void OnMouseDown()
    {
        // Eğer obje zaten dönüyorsa, bitene kadar yeni tıklamayı engelle (bug olmaması için)
        if (isRotating) return;

        isRotating = true;

        // Objeyi kendi animasyon motorunuzla (DOTween) pürüzsüzce 90 derece döndür
        transform.DORotate(rotationAmount, rotateDuration, RotateMode.WorldAxisAdd)
            .SetEase(Ease.OutBack) // Animasyonun sonunda tatlı bir sekme (zıplama) efekti verir
            .OnComplete(() => 
            {
                isRotating = false;
                
                // Rotasyon bitince sıvı aktarımı için kontrol et
                LiquidTransfer liquidTransfer = GetComponentInChildren<LiquidTransfer>();
                if(liquidTransfer != null)
                {
                    liquidTransfer.CheckSymmetry();
                }
            });
    }
    */
}
