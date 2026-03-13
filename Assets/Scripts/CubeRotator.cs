using UnityEngine;
using DG.Tweening;

public class CubeRotator : MonoBehaviour
{
    private Vector2 startTouchPos;
    private Quaternion startRotation;
    private bool isDragging = false;
    
    // Animating
    private bool isAnimating = false;

    [Header("Ayarlar")]
    public float swipeThreshold = 50f;
    public float rotationDuration = 0.5f;

    void Update()
    {
        if (isAnimating) return;

        // Ekrana basıldığında DragObject ile çakışmaması için basit bir kontrol:
        // Raycast atarak "CubeRotator" boşluğuna mı tıklandığını anlayabiliriz, 
        // ancak daha basit (hypercasual tarzı): sürükleme çok başlarsa cube döner, yavaşsa parça kayar.
        // Biz swipe detection (kaydırma) yapalım.

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // Küp üzerinde bir DragObject (sıvı veya parça) yoksa döndür
            // Hypercasual'da genellikle ekranın dış kısımlarından (veya boşluktan) sürüklenince döner.
            bool hitDragObject = false;
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform.GetComponentInParent<DragObject>() != null)
                {
                    hitDragObject = true;
                }
            }

            if (!hitDragObject)
            {
                startTouchPos = Input.mousePosition;
                startRotation = transform.rotation;
                isDragging = true;
            }
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            
            Vector2 endTouchPos = Input.mousePosition;
            Vector2 diff = endTouchPos - startTouchPos;

            if (diff.magnitude > swipeThreshold)
            {
                RotateCube(diff, startTouchPos);
            }
        }
    }

    void RotateCube(Vector2 swipeDelta, Vector2 startPoint)
    {
        // Yön bul
        bool horizontal = Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y);
        float screenHeightFactor = startPoint.y / Screen.height;

        Vector3 rotAxis = Vector3.zero;

        if (horizontal)
        {
            // Ekranın alt yarısında mı? (0.0 - 1.0 arası, 0 alt kısımdır)
            bool isLowerScreen = screenHeightFactor < 0.4f;

            if (isLowerScreen)
            {
                // Z-Ekseni rotasyonu (Direksiyon gibi dönme)
                // Sağa swipe -> Saat yönü dönsün istiyoruz
                if (swipeDelta.x > 0) rotAxis = Vector3.forward;
                else rotAxis = Vector3.back;
                
                Debug.Log($"<color=orange>[CubeRotator]</color> Z-Axis Rotation (Lower Screen: {screenHeightFactor:F2})");
            }
            else
            {
                // Y-Ekseni rotasyonu (Normal sağ-sol çevirme)
                if (swipeDelta.x > 0) rotAxis = Vector3.down; 
                else rotAxis = Vector3.up;
                
                Debug.Log($"<color=cyan>[CubeRotator]</color> Y-Axis Rotation (Upper Screen: {screenHeightFactor:F2})");
            }
        }
        else
        {
            // X-Ekseni rotasyonu (Yukarı-aşağı devirme)
            if (swipeDelta.y > 0) rotAxis = Vector3.right; 
            else rotAxis = Vector3.left;
        }

        isAnimating = true;

        // --- DÜNYA/KAMERA ALANINDA ROTASYON ---
        // Hedef rotasyonu mevcut rotasyonun SOLUNDAN çarparak ekliyoruz.
        // Bu sayede rotasyon "Dünya/Parent" koordinatlarında (Kameraya göre) uygulanır.
        // Yerel koordinat kilitlenmesi (gimbal lock veya yön karmaşası) önlenmiş olur.
        Quaternion targetRot = Quaternion.Euler(rotAxis * 90f) * transform.localRotation;

        transform.DOLocalRotateQuaternion(targetRot, rotationDuration)
            .SetEase(Ease.InOutCubic)
            .OnComplete(() => {
                isAnimating = false;
                
                // Rotasyonu tam 90 derecelerde kalacak şekilde temizle
                Vector3 finalEuler = transform.localEulerAngles;
                finalEuler.x = Mathf.Round(finalEuler.x / 90f) * 90f;
                finalEuler.y = Mathf.Round(finalEuler.y / 90f) * 90f;
                finalEuler.z = Mathf.Round(finalEuler.z / 90f) * 90f;
                transform.localEulerAngles = finalEuler;
            });
    }
}
