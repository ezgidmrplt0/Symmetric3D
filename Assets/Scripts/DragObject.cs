using UnityEngine;
using DG.Tweening;

public class DragObject : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;

    private Vector2 screenGrabOffset; 
    private float zDepth;
    private Vector3 startPosition;
    private Vector2 startScreenPos;
    private float startTime;

    [Header("Ayarlar")]
    [Header("Görsel (Drag)")]
    [Tooltip("Sürüklerken objenin kameraya ne kadar yaklaşacağını belirler.")]
    public float dragZOffset = -0.05f; // Screen space'de kameraya yaklaşma payı
    
    [Header("Çarpışma (Collision)")]
    [Tooltip("Görsel izdüşüm üzerinden diğer objelere ne kadar yaklaşabileceğini belirler.")]
    public float collisionDistance = 0.5f;

    [Header("Yüzey Geçişi (Wrap-around)")]
    public float wrapThreshold = 1.2f; // Kaza ile dönmeyi önlemek için biraz daha genişlettik (1.0 -> 1.2)
    private bool wrapInProgress = false;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        // 1. INPUT YÖNETİMİ
        Vector3 inputPos = Vector3.zero;
        bool inputDown = false;
        bool inputUp = false;
        bool inputHeld = false;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            inputPos = t.position;
            inputDown = (t.phase == TouchPhase.Began);
            inputUp = (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled);
            inputHeld = (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary);
        }
        else
        {
            inputPos = Input.mousePosition;
            inputDown = Input.GetMouseButtonDown(0);
            inputUp = Input.GetMouseButtonUp(0);
            inputHeld = Input.GetMouseButton(0);
        }

        if (inputDown) TryPick(inputPos);
        else if (inputUp && dragging) Drop(inputPos);
        else if (inputHeld && dragging) Drag(inputPos);
    }

    void TryPick(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                // --- 3D YÜZEY KİLİDİ (FACING CAMERA CHECK) ---
                if (transform.parent != null && transform.parent.name.StartsWith("Face_"))
                {
                    float dot = Vector3.Dot(transform.parent.forward, cam.transform.forward);
                    
                    // Mutlak değer (Abs) kullanıyoruz. 
                    // Yüzey bize tam bakıyorsa bu değer 1.0 veya -1.0 olur.
                    // Eğer yüzey yan duruyorsa bu değer 0'a yakın olur.
                    // 0.4'ten küçükse parça kilitli kalır.
                    if (Mathf.Abs(dot) < 0.4f) 
                    {
                        Debug.Log($"<color=red>[DragObject]</color> <b>LOCKED:</b> '{gameObject.name}' yan yüzeyde (Hassasiyet: {Mathf.Abs(dot):F2}).");
                        return;
                    }
                }

                dragging = true;
                startPosition = transform.position;

                Vector3 objectScreenPos = cam.WorldToScreenPoint(transform.position);
                zDepth = objectScreenPos.z + dragZOffset; // Screen space Z'si (kameraya göre mesafe)

                screenGrabOffset = (Vector2)objectScreenPos - (Vector2)screenPos;

                startScreenPos = screenPos;
                startTime = Time.time;

                if (TutorialManager.Instance != null) TutorialManager.Instance.HideTutorial();
                Debug.Log($"<color=yellow>[DragObject]</color> <b>PICK:</b> '{gameObject.name}' | StartPos: {startPosition} | ScreenGrabOffset: {screenGrabOffset}");
            }
        }
    }

    void Drag(Vector3 screenPos)
    {
        Vector2 targetScreenPos = (Vector2)screenPos + screenGrabOffset;
        Vector3 rayPoint = new Vector3(targetScreenPos.x, targetScreenPos.y, zDepth);
        Vector3 desiredPos = cam.ScreenToWorldPoint(rayPoint);

        DragObject[] allObjects = FindObjectsOfType<DragObject>();

        // currentPos kameranın düzlemine hizalanacak
        Vector3 currentPos = transform.position;
        // Kamera önüne ne kadar çıkacağına bak
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.04f)); 

        // Sahnedeki collision mesafesini koddan kısıtlayalım (Inspector'da 1.0 kalmış olabilir)
        float activeCollisionDist = Mathf.Min(collisionDistance, 0.75f);

        Vector3 stepVec = moveDir / steps;
        for (int s = 0; s < steps; s++)
        {
            currentPos += stepVec;

            // --- GÖRSEL ÇARPIŞMA (PERSPEKTİF) ---
            Vector3 screenP = cam.WorldToScreenPoint(currentPos);
            Ray projRay = cam.ScreenPointToRay(screenP);

            foreach (DragObject obj in allObjects)
            {
                if (obj == this || !obj.gameObject.activeInHierarchy) continue;
                
                Vector3 otherScreenP = cam.WorldToScreenPoint(obj.transform.position);

                // Ekranda (X-Y) aynı düzlemde çakışıyorlar mı (piksel/ekran birimi cinsinden)
                // Basitlik için: Eğer aynı grid yüzeyinde değillerse çarpışmasınlar bile.
                // Küpte farklı yüzlerdeki objeler birbirini itmeye çalışmasın. Aynı Parent'a sahiplerse:
                if (obj.transform.parent != transform.parent && transform.parent != null) continue;

                Vector2 other2D = new Vector2(obj.transform.position.x, obj.transform.position.y);
                Vector2 myVirtual2D = new Vector2(currentPos.x, currentPos.y);
                
                // 3D uzaklık kontrolü yapabiliriz
                float d = Vector3.Distance(currentPos, obj.transform.position);

                if (d < activeCollisionDist)
                {
                    Vector3 pushDir = (currentPos - obj.transform.position).normalized;
                    if (pushDir == Vector3.zero) pushDir = Random.onUnitSphere;
                    
                    // İtmeyi Z eksenine yansıtma (Drag sırasında zDepth'de kalmak istiyoruz)
                    pushDir.z = 0;
                    pushDir.Normalize();

                    float pushPower = (activeCollisionDist - d) * 0.6f; 
                    Vector3 push = pushDir * pushPower;
                    
                    currentPos += push;
                    
                    if (s == steps - 1) 
                        Debug.Log($"<color=cyan>[DragObject]</color> <b>COLLISION 3D:</b> {obj.name} | Mesafe: {d:F2} | Push: {push:F3}");
                }
            }
        }
        
        // Son pozisyonu atama (Z ekseninin serbest kalması 3D'de önemlidir)
        transform.position = currentPos;

        // --- YÜZEY GEÇİŞİ (WRAP-AROUND) ---
        CheckWrapAround();
    }

    void CheckWrapAround()
    {
        if (wrapInProgress) return;
        
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || !spawner.levels[spawner.currentLevelIndex].is3DCube) return;

        CubeRotator rotator = spawner.GetComponentInChildren<CubeRotator>();
        if (rotator == null || rotator.IsRotating) return;

        // --- SADECE KENDİ GRİDİMİZDEYSE DÖNMEYİ DURDUR ---
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Input.touchCount > 0) ray = cam.ScreenPointToRay(Input.GetTouch(0).position);
        
        RaycastHit[] hits = Physics.RaycastAll(ray, 30f);
        foreach (var h in hits)
        {
            // Sadece AKTİF yüzeyimizdeki bir GRİD hücresine çarparsak dönmeyi engelle.
            if (h.transform.CompareTag("Grid") && h.transform.parent == transform.parent)
            {
                return; // Kendi alanımızdayız, rotasyon yok.
            }
        }

        // --- DOMİNANT EKSEN KONTROLÜ VE YÖN DÜZELTME ---
        Vector3 localPos = transform.localPosition;
        float absX = Mathf.Abs(localPos.x);
        float absY = Mathf.Abs(localPos.y);
        float threshold = 0.8f; 
        Vector3 rotAxis = Vector3.zero;

        if (absX > absY) // Yatay hareket (Sağ-Sol) daha baskın
        {
            if (absX > threshold)
            {
                // Sağa çekince Sağ yüz gelsin (-Y rotasyon), Sola çekince Sol yüz gelsin (+Y rotasyon)
                rotAxis = (localPos.x > 0) ? Vector3.down : Vector3.up;
            }
        }
        else // Dikey hareket (Yukarı-Aşağı) daha baskın
        {
            if (absY > threshold)
            {
                // Yukarı çekince Üst yüz gelsin (+X rotasyon), Aşağı çekince Alt yüz gelsin (-X rotasyon)
                rotAxis = (localPos.y > 0) ? Vector3.right : Vector3.left;
            }
        }

        if (rotAxis != Vector3.zero)
        {
            StartCoroutine(WrapAroundCoroutine(rotator, rotAxis));
        }
    }

    System.Collections.IEnumerator WrapAroundCoroutine(CubeRotator rotator, Vector3 axis)
    {
        wrapInProgress = true;
        
        // Rotasyon sırasında parçayı biraz havaya kaldıralım (Görsel geri bildirim)
        transform.DOLocalMoveZ(-0.4f, 0.2f);
        
        rotator.Rotate90(axis);
        
        // Obje henüz arkaya gitmeden yeni yüzeyi yakalayalım
        yield return new WaitForSeconds(rotator.rotationDuration * 0.3f);

        FindAndAttachToNewFace();

        yield return new WaitForSeconds(rotator.rotationDuration * 0.7f);
        wrapInProgress = false;
    }

    void FindAndAttachToNewFace()
    {
        // Kameradan objeye doğru bir ray atarak yeni yüzeydeki gridi bulalım
        Ray ray = new Ray(cam.transform.position, (transform.position - cam.transform.position).normalized);
        RaycastHit[] hits = Physics.RaycastAll(ray, 30f);
        
        Transform bestGrid = null;
        float minAngle = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.transform.CompareTag("Grid"))
            {
                // Kameraya en dik (en çok bize bakan) gridi seçelim
                float angle = Vector3.Angle(-cam.transform.forward, hit.transform.forward);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestGrid = hit.transform;
                }
            }
        }

        if (bestGrid != null && bestGrid.parent != transform.parent)
        {
            float preservedZ = transform.localEulerAngles.z;
            transform.SetParent(bestGrid.parent, true);
            
            GridSpawner spawner = FindObjectOfType<GridSpawner>();
            float oz = (spawner != null) ? -spawner.objectOffset * 0.2f : -0.1f;
            
            // --- SNAPPING (MIKNATIS ETKİSİ) ---
            // Parçayı yeni yüzeydeki gridin tam üstüne yumuşakça taşıyoruz
            Vector3 targetLocal = new Vector3(bestGrid.localPosition.x, bestGrid.localPosition.y, oz);
            transform.DOLocalMove(targetLocal, 0.2f).SetEase(Ease.OutCubic);
            
            transform.localRotation = Quaternion.Euler(0, 0, preservedZ);
            
            // Drag derinliğini (zDepth) güncelle (Kritik: Yüzey mesafesi değişmiş olabilir)
            Vector3 objectScreenPos = cam.WorldToScreenPoint(transform.position);
            zDepth = objectScreenPos.z + dragZOffset;
            
            // Sürükleme ofsetini (parmak altındaki konum) sıfırla ki obje zıplamasın
            screenGrabOffset = Vector2.zero; 

            Debug.Log($"<color=cyan>[DragObject]</color> <b>SNAPPED</b> to {bestGrid.parent.name}");
        }
    }

    void Drop(Vector3 finalScreenPos)
    {
        dragging = false;
        GridSpawner spawner = FindObjectOfType<GridSpawner>();

        float screenDist = Vector2.Distance(finalScreenPos, startScreenPos);
        float duration = Time.time - startTime;

        Debug.Log($"<color=white>[DragObject]</color> <b>DROP:</b> '{gameObject.name}' | ScreenMove: {screenDist:F1} | Duration: {duration:F2}s");

        // Klik Algılama (Rotation)
        if (screenDist < 40f && duration < 0.4f)
        {
            Debug.Log("<color=white>[DragObject]</color> -> Rotation Tetiklendi (Click detected).");
            transform.position = startPosition;
            if (spawner != null && spawner.CurrentLevelType == LevelData.LevelType.Rotation)
            {
                ObjectRotator rotator = GetComponent<ObjectRotator>() ?? GetComponentInChildren<ObjectRotator>();
                if (rotator != null) rotator.RotateObject();
            }
            return;
        }

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

        if (targetGrid != null)
        {
            // --- AKTİF YÜZEY KONTROLÜ (DROP) ---
            // Sadece kameraya bakan yüzeye yerleştirmeye izin ver
            if (targetGrid.parent != null && targetGrid.parent.name.StartsWith("Face_"))
            {
                float dot = Vector3.Dot(targetGrid.parent.forward, cam.transform.forward);
                if (Mathf.Abs(dot) < 0.6f) // Yan yüzdeyse (eğikse) drop iptal
                {
                    Debug.LogWarning("<color=red>[DragObject]</color> -> DROP FAIL: Sadece aktif yüze yerleştirilebilir!");
                    transform.position = startPosition;
                    return;
                }
            }

            Debug.Log($"<color=green>[DragObject]</color> -> Hedef Grid Bulundu: {targetGrid.name} (Dist: {minGridDist:F2})");
            
            // Doluluk Kontrolü
            DragObject[] all = FindObjectsOfType<DragObject>();
            bool isFull = false;
            foreach (var o in all)
            {
                if (o == this) continue;
                
                float d = Vector3.Distance(o.transform.position, targetGrid.position);

                if (d < 0.25f)
                {
                    isFull = true;
                    Debug.LogWarning($"<color=red>[DragObject]</color> -> DROP FAIL: '{targetGrid.name}' dolu! Engelleyen: {o.name}");
                    break;
                }
            }

            if (!isFull)
            {
                float oz = (spawner != null) ? -spawner.objectOffset : -0.3f;
                
                // --- ROTASYON KORUMA ---
                // Yeni parent'a (yüzeye) geçerken mevcut yerel Z dönüşünü (bakış yönünü) korumalıyız.
                float preservedZ = transform.localEulerAngles.z;
                
                transform.SetParent(targetGrid.parent, true);
                
                // Pozisyonu grid'in merkezine oturt
                transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, oz);
                
                // Rotasyonu: Gridin (yüzeyin) düzleminde kalacak şekilde sadece kendi Z'sini koru
                transform.localRotation = Quaternion.Euler(0, 0, preservedZ);

                Debug.Log("<color=green>[DragObject]</color> -> Yerleştirme Başarılı (Bakış Yönü Korundu).");

                LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
                if (lt != null) lt.CheckSymmetry();
            }
            else 
            {
                Debug.LogWarning("<color=red>[DragObject]</color> -> DROP FAIL: Hedef dolu!");
                transform.DOMove(startPosition, 0.4f).SetEase(Ease.OutBack);
            }
        }
        else
        {
            Debug.LogWarning("<color=red>[DragObject]</color> -> DROP FAIL: Alakasız yer!");
            // Alakasız bir yere bırakıldığında yumuşakça eski yerine dönsün
            transform.DOMove(startPosition, 0.4f).SetEase(Ease.OutBack);
        }
    }
}