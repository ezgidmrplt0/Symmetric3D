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
    public float dragZOffset = -0.8f;
    
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
            if (hit.transform == transform)
            {
                dragging = true;
                startPosition = transform.position;
                zDepth = dragZOffset;

                Vector3 objectScreenPos = cam.WorldToScreenPoint(transform.position);
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
        Ray ray = cam.ScreenPointToRay(targetScreenPos);
        Plane dragPlane = new Plane(Vector3.forward, new Vector3(0, 0, zDepth));
        
        float enter;
        if (!dragPlane.Raycast(ray, out enter)) return;

        Vector3 desiredPos = ray.GetPoint(enter);
        DragObject[] allObjects = FindObjectsOfType<DragObject>();

        Vector3 currentPos = transform.position;
        currentPos.z = zDepth;
        
        // Mesafe farkına göre adım sayısını belirle
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
                
                Plane gridPlane = new Plane(Vector3.forward, new Vector3(0, 0, obj.transform.position.z));
                if (gridPlane.Raycast(projRay, out float pEnter))
                {
                    Vector3 virtualGroundPos = projRay.GetPoint(pEnter);
                    Vector2 other2D = new Vector2(obj.transform.position.x, obj.transform.position.y);
                    Vector2 myVirtual2D = new Vector2(virtualGroundPos.x, virtualGroundPos.y);
                    
                    float d = Vector2.Distance(myVirtual2D, other2D);
                    if (d < activeCollisionDist)
                    {
                        Vector2 pushDir = (myVirtual2D - other2D).normalized;
                        if (pushDir == Vector2.zero) pushDir = Random.insideUnitCircle.normalized;
                        
                        // İtmeyi biraz yumuşatalım (Dampening)
                        float pushPower = (activeCollisionDist - d) * 0.6f; 
                        Vector2 push = pushDir * pushPower;
                        
                        currentPos.x += push.x;
                        currentPos.y += push.y;
                        
                        if (s == steps - 1) // Sadece son adımda detaylı log yazalım (kalabalığı önlemek için)
                            Debug.Log($"<color=cyan>[DragObject]</color> <b>COLLISION:</b> {obj.name}({obj.GetInstanceID()}) | Mesafe: {d:F2} | Push: {push:F3}");
                    }
                }
            }
        }
        
        transform.position = new Vector3(currentPos.x, currentPos.y, zDepth);
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
                transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, oz);
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