using UnityEngine;
using DG.Tweening;

public class DragObject : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;

    private Vector2 screenGrabOffset; 
    private float zDepth;
    private Vector3 startPosition; // World position for reference
    private Vector3 startLocalPos;
    private Transform startParent;
    private Vector2 startScreenPos;
    private float startTime;
    private float cachedWorldSize;    // lossyScale.x before unparenting
    private float cachedLocalRotZ;    // localEulerAngles.z before unparenting
    private Vector3 cachedLocalScale; // localScale before unparenting

    [Header("Ayarlar")]
    [Header("Görsel (Drag)")]
    [Tooltip("Sürüklerken objenin kameraya ne kadar yaklaşacağını belirler.")]
    public float dragZOffset = -0.05f; // Screen space'de kameraya yaklaşma payı
    [Tooltip("Sürükleme başlayınca objenin dünya uzayında kameraya ne kadar çıkacağı (world units).")]
    public float dragLift = 0.5f;
    [Header("Çarpışma (Collision)")]
    [Tooltip("Görsel izdüşüm üzerinden diğer objelere ne kadar yaklaşabileceğini belirler.")]
    public float collisionDistance = 0.5f;

    [Header("Yüzey Geçişi (Wrap-around)")]
    public float wrapThreshold = 1.2f; 
    private float wrapCooldown = 0f; 

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        // 1. INPUT YÖNETİMİ (MULTI-TOUCH UYUMLU)
        if (wrapCooldown > 0f) wrapCooldown -= Time.deltaTime;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector3 tPos = t.position;

                if (t.phase == TouchPhase.Began && !dragging) TryPick(tPos, i);
                else if (dragging && activeTouchIndex == i)
                {
                    if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) Drag(tPos);
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) Drop(tPos);
                }
            }
        }
        else
        {
            Vector3 mPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) TryPick(mPos, -1);
            else if (dragging)
            {
                if (Input.GetMouseButton(0)) Drag(mPos);
                else if (Input.GetMouseButtonUp(0)) Drop(mPos);
            }
        }
    }

    private int activeTouchIndex = -1;

    void TryPick(Vector3 screenPos, int touchIndex)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                activeTouchIndex = touchIndex;
                DOTween.Kill(transform);
                
                // --- 3D YÜZEY KİLİDİ (SADECE ÖN YÜZ AKTİF) ---
                // ShapeFaceMarker bileşenine bakarak 3D yüzey kontrolü yap (isim bağımsız)
                ShapeFaceMarker parentMarker = transform.parent != null
                    ? transform.parent.GetComponent<ShapeFaceMarker>() : null;
                if (parentMarker != null)
                {
                    float dot = Vector3.Dot(transform.parent.forward, cam.transform.forward);
                    if (Mathf.Abs(dot) < 0.45f)
                    {
                        Debug.Log($"<color=gray>[DragObject]</color> Pick Denied: Yan yüzey (|dot|={Mathf.Abs(dot):F2})");
                        return;
                    }
                }

                dragging = true;
                startPosition = transform.position;
                startLocalPos = transform.localPosition;
                startParent = transform.parent;
                
                startScreenPos = screenPos;
                startTime = Time.time;
                wrapCooldown = 0.2f; // Picking anlık dönmeyi engellemek için kısa bir başlangıç cooldown'ı

                // Küpten kopar → Dünya uzayında bağımsız kalsın
                cachedWorldSize  = transform.lossyScale.x;
                cachedLocalRotZ  = transform.localEulerAngles.z;
                cachedLocalScale = transform.localScale;
                transform.SetParent(null, true);

                Vector3 objectScreenPos = cam.WorldToScreenPoint(transform.position);
                
                // --- CLIP ÖNLEME (KAMERAYA DAHA ÇOK YAKLAŞTIR) ---
                // Sürüklerken küpün içinden geçmemesi için kameraya doğru belirgince çekiyoruz.
                zDepth = objectScreenPos.z - 1.8f; 
                screenGrabOffset = (Vector2)objectScreenPos - (Vector2)screenPos;

                // Hafifçe kaldır (Görsel)
                transform.DOMove(cam.ScreenToWorldPoint(new Vector3(objectScreenPos.x, objectScreenPos.y, zDepth)), 0.15f).SetEase(Ease.OutCubic);
                
                if (TutorialManager.Instance != null) TutorialManager.Instance.HideTutorial();
                Debug.Log($"<color=yellow>[DragObject]</color> PICKED: {gameObject.name}");
            }
        }
    }

    void Drag(Vector3 screenPos)
    {
        DOTween.Kill(transform); 

        Vector2 targetScreenPos = (Vector2)screenPos + screenGrabOffset;
        Vector3 rayPoint = new Vector3(targetScreenPos.x, targetScreenPos.y, zDepth);
        Vector3 desiredPos = cam.ScreenToWorldPoint(rayPoint);

        DragObject[] allObjects = FindObjectsOfType<DragObject>();
        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.04f)); 

        float activeCollisionDist = Mathf.Min(collisionDistance, 0.75f);

        Vector3 stepVec = moveDir / steps;
        for (int s = 0; s < steps; s++)
        {
            currentPos += stepVec;

            foreach (DragObject obj in allObjects)
            {
                if (obj == this || !obj.gameObject.activeInHierarchy) continue;
                if (obj.transform.parent != transform.parent && transform.parent != null) continue;

                float d = Vector3.Distance(currentPos, obj.transform.position);

                if (d < activeCollisionDist)
                {
                    Vector3 pushDir = (currentPos - obj.transform.position).normalized;
                    if (pushDir == Vector3.zero) pushDir = Random.onUnitSphere;
                    pushDir.z = 0;
                    pushDir.Normalize();

                    float pushPower = (activeCollisionDist - d) * 0.6f; 
                    currentPos += pushDir * pushPower;
                }
            }
        }
        
        transform.position = currentPos;

        // --- POSITION-BASED CONTINUOUS ROTATION (CENTER-STOP) ---
        if (wrapCooldown <= 0f)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 offset = (Vector2)screenPos - screenCenter;

            // Ekranın %28/%22'si bir merkez boşluk bırakır
            float hThreshold = Screen.width * 0.28f; 
            float vThreshold = Screen.height * 0.22f;

            Vector3 rotAxis = Vector3.zero;
            float screenHeightFactor = screenPos.y / Screen.height;
            
            GridSpawner spawner = FindObjectOfType<GridSpawner>();
            CubeRotator rotator = spawner?.GetComponentInChildren<CubeRotator>();
            bool isPrism = rotator != null && rotator.isPrism;

            // Yatay kontrol — CubeRotator ile aynı mantık
            if (Mathf.Abs(offset.x) > hThreshold)
            {
                if (!isPrism && screenHeightFactor < 0.4f)
                    rotAxis = offset.x > 0 ? Vector3.forward : Vector3.back;
                else
                    rotAxis = offset.x > 0 ? Vector3.down : Vector3.up;
            }
            else if (Mathf.Abs(offset.y) > vThreshold)
            {
                rotAxis = offset.y > 0 ? Vector3.right : Vector3.left;
            }

            if (rotAxis != Vector3.zero && rotator != null && !rotator.IsRotating)
            {
                // Açı mantığı CubeRotator.RotateByAngle içinde — prizma için otomatik ayarlanır
                rotator.RotateByAngle(rotAxis, 90f);
                wrapCooldown = rotator.rotationDuration * 0.8f;
                Debug.Log($"<color=orange>[DragObject]</color> Continuous Rotation Axis: {rotAxis} (HeightFactor: {screenHeightFactor:F2})");
            }
        }
    }

    void Drop(Vector3 finalScreenPos)
    {
        dragging = false;
        activeTouchIndex = -1;
        GridSpawner spawner = FindObjectOfType<GridSpawner>();

        // En yakın gridi bul
        Ray ray = cam.ScreenPointToRay(finalScreenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        Transform targetGrid = null;
        float minGridDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.transform.CompareTag("Grid"))
            {
                if (hit.distance < minGridDist)
                {
                    minGridDist = hit.distance;
                    targetGrid = hit.transform;
                }
            }
        }

        // --- ENGELLİ HÜCRE KONTROLÜ ---
        if (targetGrid != null && targetGrid.name.Contains("Blocked"))
        {
            Debug.LogWarning("<color=red>[DragObject]</color> -> DROP FAIL: Engelli hücre.");
            targetGrid = null;
        }

        if (targetGrid != null)
        {
            // Hedef grid'in parent'ı bir ShapeFaceMarker ise yüzey açısını kontrol et
            ShapeFaceMarker dropMarker = targetGrid.parent != null
                ? targetGrid.parent.GetComponent<ShapeFaceMarker>() : null;
            if (dropMarker != null)
            {
                float dot = Vector3.Dot(targetGrid.parent.forward, cam.transform.forward);
                if (Mathf.Abs(dot) < 0.45f)
                {
                    Debug.Log($"<color=gray>[DragObject]</color> Drop Denied: Yan yüzey (|dot|={Mathf.Abs(dot):F2})");
                    targetGrid = null;
                }
            }
        }

        if (targetGrid != null)
        {
            // Doluluk kontrolü
            DragObject[] all = FindObjectsOfType<DragObject>();
            bool isFull = false;
            foreach (var o in all)
            {
                if (o == this) continue;
                if (Vector3.Distance(o.transform.position, targetGrid.position) < 0.25f)
                {
                    isFull = true; break;
                }
            }

            if (!isFull)
            {
                // --- DERİNLİK FIX (GRID ÜSTÜNDE DURMA) ---
                bool is3D = (spawner != null && spawner.levels != null && spawner.levels[spawner.currentLevelIndex].boardMode == LevelData.BoardMode.Shape3D);

                float oz;
                ShapeFaceMarker dropFaceMarker = targetGrid.parent?.GetComponent<ShapeFaceMarker>();
                float surfaceOff = dropFaceMarker?.surfaceOffset ?? 0.01f;
                if (is3D)
                {
                    // cachedWorldSize = lossyScale.x iken parça bağımsızdı → doğru dünya boyutu
                    oz = -(cachedWorldSize * 0.5f + surfaceOff);
                }
                else
                {
                    oz = -((spawner != null) ? spawner.objectOffset : 0.3f);
                }

                // --- YÖN VE HİZALAMA FIX ---
                // Parçayı yeni yüzeye bağla (Dünya rotasyonunu koru)
                transform.SetParent(targetGrid.parent, true);

                // lossyScale döndürülmüş + non-uniform parent'ta güvenilmez.
                // Hierarchy'deki localScale'leri çarparak gerçek etkin scale'i hesapla.
                if (is3D)
                {
                    if (targetGrid.parent == startParent)
                    {
                        // Aynı yüzeye bırakıldı: pick anındaki orijinal scale doğrudan kullanılabilir.
                        transform.localScale = cachedLocalScale;
                    }
                    else
                    {
                        // Farklı yüzeye bırakıldı: hierarchy walk ile rotation-independent scale hesapla.
                        float accX = 1f, accY = 1f;
                        Transform cur = targetGrid.parent;
                        while (cur != null) { accX *= cur.localScale.x; accY *= cur.localScale.y; cur = cur.parent; }
                        float sx = Mathf.Abs(accX) > 0.001f ? cachedWorldSize / Mathf.Abs(accX) : cachedLocalScale.x;
                        float sy = Mathf.Abs(accY) > 0.001f ? cachedWorldSize / Mathf.Abs(accY) : cachedLocalScale.y;
                        transform.localScale = new Vector3(sx, sy, cachedLocalScale.z);
                    }
                }

                // Parçanın orijinal Z rotasyonunu (pick anındaki) en yakın 90°'e yuvarla
                float snappedZ = Mathf.Round(cachedLocalRotZ / 90f) * 90f;

                transform.DOLocalMove(new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, oz), 0.25f).SetEase(Ease.OutCubic);
                transform.DOLocalRotate(new Vector3(0, 0, snappedZ), 0.15f).SetEase(Ease.OutBack).OnComplete(() => {
                    LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
                    if (lt != null) lt.CheckSymmetry();
                });

                return;
            }
        }

        // FAIL: Eski yerine (Face'ine) geri dönmeli
        Debug.LogWarning("<color=red>[DragObject]</color> DROP FAIL -> Reverting...");
        ReturnToStart();
    }

    void ReturnToStart()
    {
        // Eğer küp döndüyse, eski world position yanlış kalır. 
        // Bu yüzden eski parent'ına local olarak geri dönmeli.
        if (startParent != null)
        {
            transform.SetParent(startParent, true);
            transform.DOLocalMove(startLocalPos, 0.4f).SetEase(Ease.OutBack);
        }
        else
        {
            // Fallback: World pos
            transform.DOMove(startPosition, 0.4f).SetEase(Ease.OutBack);
        }
    }
}