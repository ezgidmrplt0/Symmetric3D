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
    public bool IsDragging => dragging;
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
    public float targetRotZ; // tween hedef rotasyonu (art arda tıklamada doğru hesaplama için)
    private GameObject rotateIcon;
    private Quaternion cachedWorldRotation;
    private Vector3 cachedLocalScale;
    private static DragObject[] cachedDragObjects;
    private static Transform[] cachedGridCells;
    private static Vector3[] cachedGridCellPositions;
    private GameObject dragGlowInstance;

    [Header("Görsel (Drag)")]
    [Tooltip("Sürüklerken objenin kameraya ne kadar yaklaşacağını belirler.")]
    public float dragZOffset = -0.05f;
    [Tooltip("Sürükleme başlayınca objenin dünya uzayında kameraya ne kadar çıkacağı (world units).")]
    public float dragLift = 0.5f;

    [Header("Çarpışma (Collision)")]
    [Tooltip("Görsel izdüşüm üzerinden diğer objelere ne kadar yaklaşabileceğini belirler.")]
    public float collisionDistance = 1.0f;

    [Header("Rotation")]
    [Tooltip("Rotation levellerinde bu parça döndürülebilir mi?")]
    public bool canRotate = true;
    [Tooltip("Döndürülebilir parçalarda gösterilecek ikon sprite'ı")]
    public Sprite rotateIconSprite;

    [Header("Yüzey Geçişi (Wrap-around) — Sadece 3D")]
    public float wrapThreshold = 1.2f;
    private float wrapCooldown = 0f;

    private int activeTouchIndex = -1;
    public int linkId = 0;
    private bool hasPlayedPickupSound = false;

    [Header("Donuk (Frozen) Durumu — Devre Dışı")]
    [HideInInspector]
    public bool isFrozen = false;

    public void SetFrozen(bool frozen)
    {
        isFrozen = false;
    }

    // ──────────────────────────────────────────────────────────────
    // BAŞLANGIÇ
    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        cam = Camera.main;
        activeSpawner = FindObjectOfType<GridSpawner>();
        targetRotZ = transform.localEulerAngles.z;
        canRotate = false; // Magic Sort modunda rotasyon kapalı
    }

    public void OnUnlinked()
    {
        enabled = true;
        linkId = 0;
        canRotate = false;
    }

    void CreateRotateIcon()
    {
        rotateIcon = new GameObject("RotateIcon");
        rotateIcon.transform.SetParent(transform);
        rotateIcon.transform.localScale = Vector3.one * 0.1f;

        SpriteRenderer sr = rotateIcon.AddComponent<SpriteRenderer>();
        sr.sprite = rotateIconSprite;
        sr.sortingOrder = 10;
    }

    void LateUpdate()
    {
        if (rotateIcon != null)
        {
            // İkonu merkeze al, parçanın biraz önünde durması için z değerini koru
            rotateIcon.transform.position = transform.position + new Vector3(0, 0, -0.3f);
            rotateIcon.transform.rotation = transform.rotation;
        }
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

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                // Transfer animasyonu devam ediyorsa hiçbir şey yapma (tween'i öldürme!)
                LiquidTransfer transfer = GetComponentInChildren<LiquidTransfer>();
                if (transfer != null && transfer.transferring) return;

                // ── MAGIC SORT ETKİLEŞİMİ (DOKUN-SEÇ & DÖK) ──────────
                if (LiquidTransfer.SelectedBottle != null)
                {
                    // 1. Aynı şişeye tıklandıysa: Seçimi kaldır
                    if (LiquidTransfer.SelectedBottle == transfer)
                    {
                        transfer.Deselect();
                        return;
                    }

                    // 2. Başka bir şişeye tıklandıysa: Dökme kontrolü
                    if (LiquidTransfer.SelectedBottle.CanPourInto(transfer))
                    {
                        LiquidTransfer source = LiquidTransfer.SelectedBottle;
                        source.PourInto(transfer);
                        GameManager.Instance?.RegisterMove();
                        TutorialManager.Instance?.HideTutorial();
                        return;
                    }
                    else
                    {
                        // Dökülemiyorsa ama tıklanan şişede sıvı varsa seçimi bu yeni şişeye geçir
                        if (transfer != null && transfer.currentSlices > 0)
                        {
                            transfer.Select();
                            return;
                        }
                        else
                        {
                            // Geçersiz hedef (dolu veya uyumsuz)
                            EffectsManager.Instance?.ShakeTransform(transform);
                            VibrationManager.TryVibrate();
                            return;
                        }
                    }
                }
                else
                {
                    // Henüz hiçbir şişe seçili değilken tıklandıysa
                    if (transfer != null && transfer.currentSlices > 0)
                    {
                        transfer.Select();
                    }
                    else
                    {
                        // Boş şişe kaynak olarak seçilemez
                        EffectsManager.Instance?.ShakeTransform(transform);
                        return;
                    }
                }

                activeTouchIndex = touchIndex;
                DOTween.Kill(transform);

                // 3D yüzey kilidi: sadece kameraya bakan yüzey aktif
                ShapeFaceMarker parentMarker = transform.parent != null
                    ? transform.parent.GetComponent<ShapeFaceMarker>() : null;
                if (parentMarker != null)
                {
                    float dot = Vector3.Dot(transform.parent.forward, cam.transform.forward);
                    if (Mathf.Abs(dot) < 0.45f)
                    {
                        return;
                    }
                }

                dragging = true;
                hasPlayedPickupSound = false;
                startPosition = transform.position;
                startLocalPos = transform.localPosition;
                startParent = transform.parent;
                startScreenPos = screenPos;
                startTime = Time.time;
                wrapCooldown = 0.2f;

                cachedWorldSize     = transform.lossyScale.x;
                cachedLocalRotZ     = targetRotZ;
                cachedWorldRotation = transform.rotation;
                cachedLocalScale    = transform.localScale;

                dragPlane = new Plane(Vector3.forward, transform.position);
                Ray grabRay = cam.ScreenPointToRay(screenPos);
                if (dragPlane.Raycast(grabRay, out float grabEnter))
                    worldGrabOffset = transform.position - grabRay.GetPoint(grabEnter);
                else
                    worldGrabOffset = Vector3.zero;

                if (TutorialManager.Instance != null) TutorialManager.Instance.HideTutorial();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // SÜRÜKLEME — moda göre yönlendir
    // ──────────────────────────────────────────────────────────────

    void Drag(Vector3 screenPos)
    {
        DOTween.Kill(transform);

        if (!hasPlayedPickupSound && Vector2.Distance(screenPos, startScreenPos) > 15f)
        {
            hasPlayedPickupSound = true;
            AudioManager.PlayPickup();
        }

        Ray dragRay = cam.ScreenPointToRay(screenPos);
        Vector3 desiredPos = transform.position;
        if (dragPlane.Raycast(dragRay, out float dragEnter))
        {
            desiredPos = dragRay.GetPoint(dragEnter) + worldGrabOffset;
            desiredPos.z = transform.position.z;
        }

        // Sürüklenen obje parmağı takip eder
        transform.position = desiredPos;
    }

    // ──────────────────────────────────────────────────────────────
    // BIRAKMA — Magic Sort Hedef Kontrolü
    // ──────────────────────────────────────────────────────────────

    void Drop(Vector3 finalScreenPos)
    {
        dragging = false;
        activeTouchIndex = -1;

        if (EffectsManager.Instance != null)
            EffectsManager.Instance.DestroyDragGlow(dragGlowInstance);
        dragGlowInstance = null;

        float screenDist = Vector2.Distance(finalScreenPos, startScreenPos);

        // Kısa dokunuş (TAP): Zaten TryPick içinde seçildi / döküldü
        if (screenDist < 35f)
        {
            return;
        }

        // SÜRÜKLEME (DRAG) BİTTİ: Bırakılan yerin altındaki en yakın şişeyi bul
        LiquidTransfer myTransfer = GetComponentInChildren<LiquidTransfer>();
        DragObject[] allObjects = FindObjectsOfType<DragObject>();
        DragObject closestTarget = null;
        float minDist = float.MaxValue;

        foreach (var obj in allObjects)
        {
            if (obj == null || obj == this) continue;
            float d = Vector3.Distance(transform.position, obj.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closestTarget = obj;
            }
        }

        // Eğer yakın bir şişenin üstüne bırakıldıysa ve dökülebiliyorsa
        if (closestTarget != null && minDist < 1.5f)
        {
            LiquidTransfer targetTransfer = closestTarget.GetComponentInChildren<LiquidTransfer>();
            if (myTransfer != null && myTransfer.CanPourInto(targetTransfer))
            {
                ReturnToStart();
                myTransfer.PourInto(targetTransfer);
                GameManager.Instance?.RegisterMove();
                TutorialManager.Instance?.HideTutorial();
                return;
            }
        }

        // Geçersiz bir yere bırakıldıysa başlangıç yerine dön
        ReturnToStart();
        if (myTransfer != null && myTransfer.IsSelected)
        {
            myTransfer.Deselect();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ORTAK: GERİ DÖN
    // ──────────────────────────────────────────────────────────────

    void ReturnToStart()
    {
        LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
        Vector3 targetPos = (lt != null && lt.OriginalLocalPos != Vector3.zero) ? lt.OriginalLocalPos : startLocalPos;
        Quaternion targetRot = (lt != null) ? lt.OriginalLocalRot : transform.localRotation;

        if (startParent != null)
        {
            transform.SetParent(startParent, true);
        }
        transform.DOKill();
        transform.DOLocalMove(targetPos, 0.25f).SetEase(Ease.OutQuad);
        transform.DOLocalRotateQuaternion(targetRot, 0.25f).SetEase(Ease.OutQuad);
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
    /// Optimized version that uses a pre-filtered list of objects.
    /// </summary>
    private bool IsDiagonallyBlockedCached(Vector3 from, Vector3 to, System.Collections.Generic.List<DragObject> objectsOnFace)
    {
        if (objectsOnFace == null) return false;

        float gridStep = GetGridStep();
        float diagDist = gridStep * Mathf.Sqrt(2f);
        float tolerance = gridStep * 0.35f;

        Vector2 p1 = new Vector2(from.x, from.y);
        Vector2 p2 = new Vector2(to.x, to.y);

        for (int i = 0; i < objectsOnFace.Count; i++)
        {
            DragObject a = objectsOnFace[i];
            if (a == null) continue;

            for (int j = i + 1; j < objectsOnFace.Count; j++)
            {
                DragObject b = objectsOnFace[j];
                if (b == null) continue;

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

    private bool IsDiagonallyBlocked(Vector3 from, Vector3 to, DragObject[] allObjects, bool sameParentOnly)
    {
        if (allObjects == null) return false;

        float gridStep = GetGridStep();
        float diagDist = gridStep * Mathf.Sqrt(2f);
        float tolerance = gridStep * 0.35f;

        Vector2 p1 = new Vector2(from.x, from.y);
        Vector2 p2 = new Vector2(to.x, to.y);

        for (int i = 0; i < allObjects.Length; i++)
        {
            DragObject a = allObjects[i];
            if (a == null || a == this) continue;

            for (int j = i + 1; j < allObjects.Length; j++)
            {
                DragObject b = allObjects[j];
                if (b == null || b == this) continue;

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
