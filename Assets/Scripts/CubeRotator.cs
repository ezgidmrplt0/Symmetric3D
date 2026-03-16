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

    [Header("Shape")]
    [Tooltip("Prizma için true yapın: Z-ekseni roll'u devre dışı kalır, yatay swipe her zaman Y döndürür.")]
    public bool isPrism = false;

    private int activeFingerId = -1;
    private Quaternion discreteRotation = Quaternion.identity;
    // Prizma: Y ekseninde her 90° dönüşte bu toggle olur.
    // false = üçgen yüz kameraya bakıyor (dikey = 90°)
    // true  = dikdörtgen yüz kameraya bakıyor (dikey = 120°)
    private bool prismViewingRect = false;

    void Awake()
    {
        discreteRotation = transform.localRotation;
        prismViewingRect = false;
    }

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
                // Marker ismine değil ShapeFaceMarker bileşenine göre kontrol et.
                // (Eski: "Face_" prefix, Yeni: "Marker_FrontTriangle" vb.)
                Transform face = dragComp.transform.parent;
                ShapeFaceMarker faceMarker = face != null ? face.GetComponent<ShapeFaceMarker>() : null;

                if (faceMarker != null)
                {
                    // Yalnızca kameraya dönük yüzlerdeki parçalar sürüklenebilir/bloklayabilir
                    float dot = Vector3.Dot(face.forward, Camera.main.transform.forward);
                    if (Mathf.Abs(dot) >= 0.4f) hitDragObject = true;
                }
                else
                {
                    // Flat2D veya marker dışı parent — direkt blokla
                    hitDragObject = true;
                }
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
        bool horizontal = Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y);
        float screenHeightFactor = startPoint.y / Screen.height;
        bool isLowerScreen = screenHeightFactor < 0.4f;

        Vector3 rotAxis;

        if (horizontal)
        {
            if (!isPrism && isLowerScreen)
                rotAxis = swipeDelta.x > 0 ? Vector3.forward : Vector3.back;
            else
                rotAxis = swipeDelta.x > 0 ? Vector3.down : Vector3.up;
        }
        else
        {
            rotAxis = swipeDelta.y > 0 ? Vector3.right : Vector3.left;
        }

        // Açı mantığı RotateByAngle içinde; dışarıdan 90f pasla yeter
        RotateByAngle(rotAxis, 90f);
    }

    public void Rotate90(Vector3 axis) => RotateByAngle(axis, 90f);

    public void RotateByAngle(Vector3 axis, float requestedAngle)
    {
        if (isAnimating) return;

        float angle = requestedAngle;

        if (isPrism)
        {
            bool isZ = axis == Vector3.forward || axis == Vector3.back;
            bool isY = axis == Vector3.up      || axis == Vector3.down;
            bool isX = axis == Vector3.right   || axis == Vector3.left;

            if (isZ) return; // Prizmada Z-roll yok

            if (isY)
            {
                angle = 90f;
                prismViewingRect = !prismViewingRect; // her Y dönüşünde yüz tipi değişir
            }
            else if (isX)
            {
                // Dikdörtgen yüze bakılıyorken 120° → kenar altta; üçgen yüzdeyken 90°
                angle = prismViewingRect ? 120f : 90f;
            }
        }

        isAnimating = true;
        discreteRotation = Quaternion.Euler(axis * angle) * discreteRotation;
        Debug.Log($"[ROTATE] axis={axis} angle={angle} prismRect={prismViewingRect} → {discreteRotation.eulerAngles:F0}");

        transform.DOLocalRotateQuaternion(discreteRotation, rotationDuration)
            .SetEase(Ease.InOutCubic)
            .OnComplete(() => {
                isAnimating = false;
                transform.localRotation = discreteRotation;
            });
    }
}
