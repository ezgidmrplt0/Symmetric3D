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
    public float rotationDuration = 0.4f;

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
                RotateCube(diff);
            }
        }
    }

    void RotateCube(Vector2 swipeDelta)
    {
        // Yön bul
        bool horizontal = Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y);

        Vector3 rotAxis = Vector3.zero;

        if (horizontal)
        {
            if (swipeDelta.x > 0) rotAxis = Vector3.down; // Sağa swipe, sola döner (arka görünür)
            else rotAxis = Vector3.up; 
        }
        else
        {
            if (swipeDelta.y > 0) rotAxis = Vector3.right; // Yukarı swipe, aşağı döner (alt görünür)
            else rotAxis = Vector3.left;
        }

        isAnimating = true;

        transform.DOLocalRotate(transform.localEulerAngles + (rotAxis * 90f), rotationDuration, RotateMode.Fast)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                isAnimating = false;
                
                // Rotasyonu tam 90 derecelerde kalacak şekilde sınırla
                Vector3 finalRot = transform.localEulerAngles;
                finalRot.x = Mathf.Round(finalRot.x / 90f) * 90f;
                finalRot.y = Mathf.Round(finalRot.y / 90f) * 90f;
                finalRot.z = Mathf.Round(finalRot.z / 90f) * 90f;
                transform.localEulerAngles = finalRot;
            });
    }
}
