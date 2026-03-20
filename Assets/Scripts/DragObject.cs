using UnityEngine;
using DG.Tweening;

/// <summary>
/// Ana DragObject bileşeni — ortak durum, giriş döngüsü ve paylaşılan yardımcılar.
/// 2D mantığı: DragObject.Flat2D.cs
/// 3D mantığı: DragObject.Shape3D.cs
/// </summary>
public partial class DragObject : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;
    private GridSpawner activeSpawner;

    private Plane dragPlane;
    private Vector3 worldGrabOffset;
    private Vector3 startPosition;
    private Vector3 startLocalPos;
    private Transform startParent;
    private Vector2 startScreenPos;
    private float startTime;
    private float cachedWorldSize;
    private float cachedLocalRotZ;
    private Quaternion cachedWorldRotation;
    private Vector3 cachedLocalScale;

    [Header("Görsel (Drag)")]
    [Tooltip("Sürüklerken objenin kameraya ne kadar yaklaşacağını belirler.")]
    public float dragZOffset = -0.05f;
    [Tooltip("Sürükleme başlayınca objenin dünya uzayında kameraya ne kadar çıkacağı (world units).")]
    public float dragLift = 0.5f;

    [Header("Çarpışma (Collision)")]
    [Tooltip("Görsel izdüşüm üzerinden diğer objelere ne kadar yaklaşabileceğini belirler.")]
    public float collisionDistance = 0.25f;

    [Header("Yüzey Geçişi (Wrap-around) — Sadece 3D")]
    public float wrapThreshold = 1.2f;
    private float wrapCooldown = 0f;

    private int activeTouchIndex = -1;
    public int linkId = 0;

    // ──────────────────────────────────────────────────────────────
    // BAŞLANGIÇ
    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        cam = Camera.main;
        activeSpawner = FindObjectOfType<GridSpawner>();
    }

    // ──────────────────────────────────────────────────────────────
    // GİRİŞ DÖNGÜSÜ
    // ──────────────────────────────────────────────────────────────

    void Update()
    {
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

    // ──────────────────────────────────────────────────────────────
    // ORTAK: ALMA (PICK)
    // ──────────────────────────────────────────────────────────────

    void TryPick(Vector3 screenPos, int touchIndex)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Debug.Log($"[PICK] Raycast hit: {hit.transform.name} | tag: {hit.transform.tag} | parent: {hit.transform.parent?.name}");

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                activeTouchIndex = touchIndex;
                DOTween.Kill(transform);

                // 3D yüzey kilidi: sadece kameraya bakan yüzey aktif
                ShapeFaceMarker parentMarker = transform.parent != null
                    ? transform.parent.GetComponent<ShapeFaceMarker>() : null;
                if (parentMarker != null)
                {
                    float dot = Vector3.Dot(transform.parent.forward, cam.transform.forward);
                    Debug.Log($"[PICK] Face dot check: face.forward={transform.parent.forward:F2} cam.transform.forward={cam.transform.forward:F2} |dot|={Mathf.Abs(dot):F2}");
                    if (Mathf.Abs(dot) < 0.45f)
                    {
                        Debug.Log($"<color=gray>[PICK] DENIED: Yan yüzey (|dot|={Mathf.Abs(dot):F2})</color>");
                        return;
                    }
                }

                dragging = true;
                startPosition = transform.position;
                startLocalPos = transform.localPosition;
                startParent = transform.parent;
                startScreenPos = screenPos;
                startTime = Time.time;
                wrapCooldown = 0.2f;

                cachedWorldSize     = transform.lossyScale.x;
                cachedLocalRotZ     = transform.localEulerAngles.z;
                cachedWorldRotation = transform.rotation;
                cachedLocalScale    = transform.localScale;
                transform.SetParent(null, true);

                dragPlane = new Plane(Vector3.forward, transform.position);
                Ray grabRay = cam.ScreenPointToRay(screenPos);
                if (dragPlane.Raycast(grabRay, out float grabEnter))
                    worldGrabOffset = transform.position - grabRay.GetPoint(grabEnter);
                else
                    worldGrabOffset = Vector3.zero;

                if (TutorialManager.Instance != null) TutorialManager.Instance.HideTutorial();
                Debug.Log($"<color=yellow>[PICK] OK: {gameObject.name} | worldPos={startPosition:F2} | parent={startParent?.name} | worldSize={cachedWorldSize:F3}</color>");
            }
            else
            {
                Debug.Log($"[PICK] Miss: hit {hit.transform.name} ama bu obje değil ({gameObject.name})");
            }
        }
        else
        {
            Debug.Log($"[PICK] Raycast boş isabet yok — {gameObject.name} deneniyordu");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // SÜRÜKLEME — moda göre yönlendir
    // ──────────────────────────────────────────────────────────────

    void Drag(Vector3 screenPos)
    {
        DOTween.Kill(transform);

        Ray dragRay = cam.ScreenPointToRay(screenPos);
        Vector3 desiredPos = transform.position;
        if (dragPlane.Raycast(dragRay, out float dragEnter))
        {
            desiredPos = dragRay.GetPoint(dragEnter) + worldGrabOffset;
            desiredPos.z = transform.position.z;
        }

        DragObject[] allObjects = FindObjectsOfType<DragObject>();

        if (IsShape3DMode())
            DragShape3D(screenPos, desiredPos, allObjects);
        else
            DragFlat2D(screenPos, desiredPos, allObjects);
    }

    // ──────────────────────────────────────────────────────────────
    // BIRAKMA — moda göre yönlendir
    // ──────────────────────────────────────────────────────────────

    void Drop(Vector3 finalScreenPos)
    {
        dragging = false;
        activeTouchIndex = -1;
        GridSpawner spawner = FindObjectOfType<GridSpawner>();

        // TAP KONTROLÜ — Rotation modunda kısa dokunuş = 90° döndür
        float screenDist = Vector2.Distance(finalScreenPos, startScreenPos);
        float tapDuration = Time.time - startTime;
        if (screenDist < 50f && tapDuration < 0.5f && !IsShape3DMode() &&
            spawner != null && spawner.CurrentLevelType.HasFlag(LevelData.LevelType.Rotation))
        {
            if (startParent != null) transform.SetParent(startParent, true);
            transform.localPosition = startLocalPos;
            transform.DOLocalRotate(new Vector3(0, 0, cachedLocalRotZ + 90f), 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
                    if (lt != null) lt.CheckSymmetry();
                });
            return;
        }

        // En yakın grid hücresini bul — objenin gerçek pozisyonuna göre (mouse değil)
        Transform targetGrid = null;
        float minGridDist = float.MaxValue;

        Debug.Log($"[DROP] {gameObject.name} bırakıldı. Obje pozisyonu: {transform.position:F2}");
        GameObject[] gridCells = GameObject.FindGameObjectsWithTag("Grid");
        foreach (GameObject cellObj in gridCells)
        {
            if (!cellObj.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, cellObj.transform.position);
            Debug.Log($"[DROP]   grid: {cellObj.name} | pos: {cellObj.transform.position:F2} | dist: {d:F3}");
            if (d < minGridDist)
            {
                minGridDist = d;
                targetGrid = cellObj.transform;
            }
        }

        if (targetGrid == null)
        {
            Debug.LogWarning($"[DROP] FAIL: Hiç Grid tag'li obje bulunamadı. Geri dönüyor.");
            ReturnToStart();
            return;
        }

        Debug.Log($"[DROP] Hedef grid: {targetGrid.name} | parent: {targetGrid.parent?.name} | pos: {targetGrid.position:F2}");

        // Engelli hücre kontrolü
        if (targetGrid.name.Contains("Blocked"))
        {
            Debug.LogWarning("[DROP] FAIL: Engelli hücre.");
            ReturnToStart();
            return;
        }

        // Doluluk kontrolü
        // Shape3D'de hücre aralığı ~0.46 dünya birimi — sabit 0.6f eşiği komşu hücreleri
        // "dolu" sayıyordu. Parça dünya boyutuna göre dinamik eşik kullan.
        float fullThreshold = cachedWorldSize > 0.001f ? cachedWorldSize * 0.9f : 0.4f;
        DragObject[] all = FindObjectsOfType<DragObject>();
        foreach (var o in all)
        {
            if (o == this) continue;
            float d = Vector3.Distance(o.transform.position, targetGrid.position);
            if (d < fullThreshold)
            {
                Debug.LogWarning($"[DROP] FAIL: Dolu hücre — {o.name} dist={d:F3} < eşik={fullThreshold:F3}");
                ReturnToStart();
                return;
            }
        }

        Debug.Log($"[DROP] OK — {(IsShape3DMode() ? "Shape3D" : "Flat2D")} modunda yerleştiriliyor.");
        if (IsShape3DMode())
            DropShape3D(targetGrid, spawner);
        else
            DropFlat2D(targetGrid, spawner);
    }

    // ──────────────────────────────────────────────────────────────
    // ORTAK: GERİ DÖN
    // ──────────────────────────────────────────────────────────────

    void ReturnToStart()
    {
        if (startParent != null)
        {
            transform.SetParent(startParent, true);
            transform.DOLocalMove(startLocalPos, 0.4f).SetEase(Ease.OutBack);
        }
        else
        {
            transform.DOMove(startPosition, 0.4f).SetEase(Ease.OutBack);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ORTAK YARDIMCILAR
    // ──────────────────────────────────────────────────────────────

    /// <summary>Mevcut seviyenin Shape3D modunda olup olmadığını döner.</summary>
    private bool IsShape3DMode()
    {
        return activeSpawner != null &&
               activeSpawner.levels != null &&
               activeSpawner.currentLevelIndex < activeSpawner.levels.Count &&
               activeSpawner.levels[activeSpawner.currentLevelIndex].boardMode == LevelData.BoardMode.Shape3D;
    }

    private float GetGridStep()
    {
        if (activeSpawner != null && activeSpawner.gridPrefab != null)
            return activeSpawner.gridPrefab.transform.localScale.x + activeSpawner.spacing;
        return 1.4f;
    }

    /// <summary>
    /// İki obje çapraz komşuysa aralarındaki segmentten geçişi engeller.
    /// sameParentOnly=true → sadece aynı parent'taki objeler kontrol edilir (3D yüzey modu).
    /// </summary>
    private bool IsDiagonallyBlocked(Vector3 from, Vector3 to, DragObject[] allObjects, bool sameParentOnly)
    {
        float gridStep = GetGridStep();
        float diagDist = gridStep * Mathf.Sqrt(2f);
        float tolerance = gridStep * 0.35f;

        Vector2 p1 = new Vector2(from.x, from.y);
        Vector2 p2 = new Vector2(to.x, to.y);

        for (int i = 0; i < allObjects.Length; i++)
        {
            for (int j = i + 1; j < allObjects.Length; j++)
            {
                DragObject a = allObjects[i];
                DragObject b = allObjects[j];
                if (a == this || b == this) continue;
                if (!a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy) continue;
                if (sameParentOnly && startParent != null)
                {
                    if (a.transform.parent != startParent || b.transform.parent != startParent) continue;
                }

                Vector2 pa = new Vector2(a.transform.position.x, a.transform.position.y);
                Vector2 pb = new Vector2(b.transform.position.x, b.transform.position.y);

                float distAB = Vector2.Distance(pa, pb);
                if (Mathf.Abs(distAB - diagDist) > tolerance) continue;

                if (SegmentsIntersect2D(p1, p2, pa, pb))
                    return true;
            }
        }
        return false;
    }

    private bool SegmentsIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1x = p2.x - p1.x, d1y = p2.y - p1.y;
        float d2x = p4.x - p3.x, d2y = p4.y - p3.y;
        float denom = d1x * d2y - d1y * d2x;
        if (Mathf.Abs(denom) < 0.0001f) return false;

        float dx = p3.x - p1.x, dy = p3.y - p1.y;
        float t = (dx * d2y - dy * d2x) / denom;
        float u = (dx * d1y - dy * d1x) / denom;

        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }
}
