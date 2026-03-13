using UnityEngine;

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
                // Piksel mesafesini dünya mesafesine tahmini çevirmek veya doğrudan dünya koordinatlarını kullanmak:
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
            Debug.Log($"<color=green>[DragObject]</color> -> Hedef Grid Bulundu: {targetGrid.name} (Dist: {minGridDist:F2})");
            
            // Doluluk Kontrolü
            DragObject[] all = FindObjectsOfType<DragObject>();
            bool isFull = false;
            foreach (var o in all)
            {
                if (o == this) continue;
                
                float d = Vector2.Distance(new Vector2(o.transform.position.x, o.transform.position.y), 
                                         new Vector2(targetGrid.position.x, targetGrid.position.y));

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
                transform.SetParent(targetGrid.parent, true);
                
                // Rotasyonu gridin local rotation'ına ve kendi z'sine uydur
                transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, oz);
                // Grid'in yukarı baktığı yön, nesnenin baktığı yönle uyuşmalıdır.
                transform.localRotation = Quaternion.Euler(0, 0, transform.localEulerAngles.z);

                Debug.Log("<color=green>[DragObject]</color> -> Yerleştirme Başarılı.");

                LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
                if (lt != null) lt.CheckSymmetry();
            }
            else transform.position = startPosition;
        }
        else
        {
            Debug.LogWarning("<color=red>[DragObject]</color> -> DROP FAIL: Altında Grid yok!");
            transform.position = startPosition;
        }
    }
}