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
    public float rotationDuration = 1.2f;
    public bool IsRotating => isAnimating;

    private int activeFingerId = -1;

    void Update()
    {
        if (isAnimating) return;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && !isDragging)
                {
                    if (TryStartRotation(t.position)) activeFingerId = t.fingerId;
                }
                else if (isDragging && activeFingerId == t.fingerId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        EndRotation(t.position);
                        activeFingerId = -1;
                    }
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) TryStartRotation(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && isDragging) EndRotation(Input.mousePosition);
        }
    }

    bool TryStartRotation(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        bool hitDragObject = false;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            DragObject dragComp = hit.transform.GetComponentInParent<DragObject>();
            if (dragComp != null)
            {
                Transform face = dragComp.transform.parent;
                if (face != null && face.name.StartsWith("Face_"))
                {
                    float dot = Vector3.Dot(face.forward, Camera.main.transform.forward);
                    if (Mathf.Abs(dot) >= 0.4f) hitDragObject = true;
                }
                else hitDragObject = true;
            }
        }

        if (!hitDragObject)
        {
            startTouchPos = screenPos;
            startRotation = transform.rotation;
            isDragging = true;
            return true;
        }
        return false;
    }

    void EndRotation(Vector2 screenPos)
    {
        isDragging = false;
        Vector2 diff = screenPos - startTouchPos;
        if (diff.magnitude > swipeThreshold) RotateCube(diff, startTouchPos);
    }

    // ... (RotateCube and Rotate90 remain same)

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
    
    public void Rotate90(Vector3 axis)
    {
        if (isAnimating) return;
        
        isAnimating = true;
        Quaternion targetRot = Quaternion.Euler(axis * 90f) * transform.localRotation;
        Debug.Log($"[ROTATE] axis={axis} | euler={transform.localEulerAngles:F0} → {targetRot.eulerAngles:F0}");
        
        transform.DOLocalRotateQuaternion(targetRot, rotationDuration)
            .SetEase(Ease.InOutCubic)
            .OnComplete(() => {
                isAnimating = false;
                Vector3 finalEuler = transform.localEulerAngles;
                finalEuler.x = Mathf.Round(finalEuler.x / 90f) * 90f;
                finalEuler.y = Mathf.Round(finalEuler.y / 90f) * 90f;
                finalEuler.z = Mathf.Round(finalEuler.z / 90f) * 90f;
                transform.localEulerAngles = finalEuler;
            });
    }
}
