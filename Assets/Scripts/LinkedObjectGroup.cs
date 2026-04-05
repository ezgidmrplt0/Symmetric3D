using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class LinkedObjectGroup : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;

    private Plane dragPlane;
    private Vector3 worldGrabOffset;

    private Vector3 startPosition;
    private Vector2 startScreenPos;
    private float startTime;

    public float snapDistance = 1.5f;
    public float collisionDistance = 0.35f;

    public List<DragObject> childDrags = new List<DragObject>();
    private GameObject backPanel;
    private bool backPanelFading = false;

    // Drag başında hesaplanan child world offset'leri (drag boyunca sabit kalır)
    private List<Vector3> dragChildOffsets = new List<Vector3>();
    private GridSpawner spawnerCache;
    private DragObject[] cachedAllObjects; // NEW: Cache for objects during drag

    void Start()
    {
        cam = Camera.main;
        spawnerCache = FindObjectOfType<GridSpawner>();
    }

    public void InitGroup()
    {
        // Add all children and disable their individual DragObject script
        foreach (DragObject d in GetComponentsInChildren<DragObject>())
        {
            if (!childDrags.Contains(d))
            {
                childDrags.Add(d);
                d.enabled = false;
            }
        }

        CreateBackPanel();
    }

    void CreateBackPanel()
    {
        if (childDrags.Count <= 1) return;

        backPanel = new GameObject("LinkedTabaka");
        backPanel.transform.parent = this.transform;
        backPanel.transform.localPosition = new Vector3(0, 0, -0.15f);
        backPanel.transform.localRotation = Quaternion.identity;
        backPanel.transform.localScale = Vector3.one;

        // Objeleri en yakın komşu sırasına diz (aynı mantık)
        List<Vector3> pts = new List<Vector3>();
        List<DragObject> unvisited = new List<DragObject>(childDrags);

        DragObject current = unvisited[0];
        pts.Add(current.transform.localPosition);
        unvisited.Remove(current);

        while (unvisited.Count > 0)
        {
            DragObject closest = null;
            float minDist = float.MaxValue;
            foreach (var u in unvisited)
            {
                float d = Vector3.Distance(current.transform.localPosition, u.transform.localPosition);
                if (d < minDist) { minDist = d; closest = u; }
            }
            pts.Add(closest.transform.localPosition);
            current = closest;
            unvisited.Remove(closest);
        }

        // LineRenderer yerine sabit flat mesh kullan (kameraya dönmez)
        MeshFilter mf = backPanel.AddComponent<MeshFilter>();
        mf.mesh = BuildCapsuleChainMesh(pts, 0.31f);

        MeshRenderer mr = backPanel.AddComponent<MeshRenderer>();
        // Sprites/Default: unlit, ışıkla etkileşimi yok, objeleri karartmaz
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.2f, 0.25f, 0.3f, 0.55f);
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        // Objelerin altında render et (objeler genellikle sortingOrder=0)
        mr.sortingOrder = -1;
    }

    Mesh BuildCapsuleChainMesh(List<Vector3> pts, float radius)
    {
        int capSegs = 12; // her yarım daire kaç segmentten oluşsun
        int N = pts.Count;

        var p = new Vector2[N];
        for (int i = 0; i < N; i++) p[i] = new Vector2(pts[i].x, pts[i].y);

        var outline = new List<Vector2>();

        if (N == 1)
        {
            // Tek nokta: tam daire
            for (int i = 0; i < capSegs * 2; i++)
            {
                float a = (float)i / (capSegs * 2) * Mathf.PI * 2f;
                outline.Add(p[0] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
        else
        {
            // Segment yönleri
            var dirs = new Vector2[N - 1];
            for (int i = 0; i < N - 1; i++)
                dirs[i] = (p[i + 1] - p[i]).normalized;

            // Başlangıç kapağı: p[0]'ın sağ perpine göre geriye doğru yarım daire
            {
                Vector2 rp = new Vector2(dirs[0].y, -dirs[0].x);
                float a0 = Mathf.Atan2(rp.y, rp.x);
                for (int i = 0; i <= capSegs; i++)
                {
                    float a = a0 - (float)i / capSegs * Mathf.PI;
                    outline.Add(p[0] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }

            // Sol kenar: p[1] → p[N-1], iç köşelerde miter birleşimi
            for (int vi = 1; vi < N; vi++)
            {
                Vector2 lp;
                if (vi < N - 1)
                {
                    Vector2 l1 = new Vector2(-dirs[vi - 1].y, dirs[vi - 1].x);
                    Vector2 l2 = new Vector2(-dirs[vi].y, dirs[vi].x);
                    lp = (l1 + l2).normalized;
                    float dot = Vector2.Dot(l1, lp);
                    if (dot > 0.1f) lp /= dot;
                }
                else
                {
                    lp = new Vector2(-dirs[vi - 1].y, dirs[vi - 1].x);
                }
                outline.Add(p[vi] + lp * radius);
            }

            // Bitiş kapağı: p[N-1]'in sol perpinden ileriye doğru yarım daire
            {
                Vector2 lp = new Vector2(-dirs[N - 2].y, dirs[N - 2].x);
                float a0 = Mathf.Atan2(lp.y, lp.x);
                for (int i = 1; i <= capSegs; i++) // i=0 zaten sol kenarda eklendi
                {
                    float a = a0 - (float)i / capSegs * Mathf.PI;
                    outline.Add(p[N - 1] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }

            // Sağ kenar: p[N-2] → p[1] geriye doğru, iç köşelerde miter
            for (int vi = N - 2; vi >= 1; vi--)
            {
                Vector2 r1 = new Vector2(dirs[vi - 1].y, -dirs[vi - 1].x);
                Vector2 r2 = new Vector2(dirs[vi].y, -dirs[vi].x);
                Vector2 rp = (r1 + r2).normalized;
                float dot = Vector2.Dot(r1, rp);
                if (dot > 0.1f) rp /= dot;
                outline.Add(p[vi] + rp * radius);
            }
            // p[0] sağ perpi başlangıç kapağının ilk noktasıyla örtüşür → polygon kapanır
        }

        // Merkez noktasından fan triangulation (konveks şekiller için geçerli)
        Vector2 centroid = Vector2.zero;
        foreach (var v in outline) centroid += v;
        centroid /= outline.Count;

        var verts = new List<Vector3> { new Vector3(centroid.x, centroid.y, 0) };
        foreach (var v in outline) verts.Add(new Vector3(v.x, v.y, 0));

        int n = outline.Count;
        var tris = new List<int>();
        for (int i = 0; i < n; i++)
        {
            tris.Add(0);
            tris.Add(1 + i);
            tris.Add(1 + (i + 1) % n);
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Update()
    {
        // Temizle (Destroy olan objeler null olur)
        childDrags.RemoveAll(item => item == null);
        
        // Sadece 1 obje kalırsa linked grubunu çöz, objeyi serbest bırak
        if (childDrags.Count == 1 && childDrags[0] != null)
        {
            childDrags[0].OnUnlinked();
            childDrags.Clear();
        }

        // Obje kalmazsa tabakayı yok et
        if (childDrags.Count <= 1 && backPanel != null && !backPanelFading)
        {
            backPanelFading = true;
            GameObject panelToDestroy = backPanel;
            backPanel = null;
            panelToDestroy.transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => { if (panelToDestroy != null) Destroy(panelToDestroy); });
        }

        if (childDrags.Count == 0) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began) TryPick(touch.position);
            if (touch.phase == TouchPhase.Moved && dragging) Drag(touch.position);
            if (touch.phase == TouchPhase.Ended && dragging) Drop();
        }
        else
        {
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0)) TryPick(Input.mousePosition);
            if (Input.GetMouseButton(0) && dragging) Drag(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) && dragging) Drop();
#endif
        }
    }

    void TryPick(Vector3 screenPos)
    {
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.HideTutorial();

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            DragObject hitTarget = null;
            foreach (var d in childDrags)
            {
                if (d != null && hit.transform.IsChildOf(d.transform))
                {
                    hitTarget = d;
                    break;
                }
            }

            if (hitTarget != null)
            {
                dragging = true;
                startPosition = transform.position;
                startScreenPos = screenPos;
                startTime = Time.time;

                // Child'ların parent'a göre world-space offset'lerini kaydet
                dragChildOffsets.Clear();
                foreach (var d in childDrags)
                    dragChildOffsets.Add(d != null ? d.transform.position - transform.position : Vector3.zero);

                dragPlane = new Plane(Vector3.forward, transform.position);
                Ray dragRay = cam.ScreenPointToRay(screenPos);
                if (dragPlane.Raycast(dragRay, out float enter))
                {
                    worldGrabOffset = transform.position - dragRay.GetPoint(enter);
                }
                else
                {
                    worldGrabOffset = Vector3.zero;
                }

                // OPTIMIZATION: Cache all objects once at the start of group drag
                cachedAllObjects = FindObjectsOfType<DragObject>(true);
            }
        }
    }

    void Drag(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!dragPlane.Raycast(ray, out float enter)) return;

        Vector3 worldPoint = ray.GetPoint(enter);
        Vector3 desiredPos = worldPoint + worldGrabOffset;

        // Use cached objects instead of FindObjectsOfType every frame
        if (cachedAllObjects == null) cachedAllObjects = FindObjectsOfType<DragObject>(true);
        DragObject[] allObjects = cachedAllObjects;

        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;

        List<Vector3> childOffsets = new List<Vector3>();
        foreach (var d in childDrags)
        {
            if (d != null) childOffsets.Add(d.transform.position - transform.position);
            else childOffsets.Add(Vector3.zero);
        }

        // Çoklu obje grubunda efektif çarpışma mesafesini grup genişliğine göre ölçekle.
        // Diyagonal geçişi engellemek için minChildSep / sqrt(2) üstünde olmalı → * 0.75f
        // Bu sayede: 1-grid diyagonal gap → geçemez, 2-grid diyagonal gap → geçebilir.
        float effectiveCollDist = collisionDistance;
        if (childDrags.Count >= 2 && childOffsets.Count >= 2)
        {
            float minChildSep = float.MaxValue;
            for (int i = 0; i < childOffsets.Count; i++)
                for (int j = i + 1; j < childOffsets.Count; j++)
                {
                    float sep = new Vector2(childOffsets[i].x - childOffsets[j].x,
                                           childOffsets[i].y - childOffsets[j].y).magnitude;
                    if (sep < minChildSep) minChildSep = sep;
                }
            effectiveCollDist = Mathf.Max(collisionDistance, minChildSep * 0.75f);
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.05f));
        Vector3 stepVec = moveDir / steps;

        if (spawnerCache == null) spawnerCache = FindObjectOfType<GridSpawner>();

        for (int s = 0; s < steps; s++)
        {
            Vector3 nextPos = currentPos + stepVec;
            bool stepBlocked = false;

            // Diğer objelerle çarpışma çözümü (3 iterasyon)
            for (int iter = 0; iter < 3; iter++)
            {
                // a) Her child nokta → dış obje
                for (int i = 0; i < childDrags.Count; i++)
                {
                    if (childDrags[i] == null) continue;

                    Vector3 proposedChildPos = nextPos + childOffsets[i];
                    Vector2 p2 = new Vector2(proposedChildPos.x, proposedChildPos.y);

                    foreach (var other in allObjects)
                    {
                        if (other == null || !other.gameObject.activeInHierarchy || other.transform.IsChildOf(this.transform))
                            continue;

                        Vector2 op2 = new Vector2(other.transform.position.x, other.transform.position.y);
                        float d = Vector2.Distance(p2, op2);

                        if (d < effectiveCollDist)
                        {
                            Vector2 push = (p2 - op2).normalized;
                            if (push == Vector2.zero) push = Vector2.up;
                            Vector2 resolved = op2 + push * effectiveCollDist;
                            nextPos.x += (resolved.x - p2.x);
                            nextPos.y += (resolved.y - p2.y);
                            p2 = new Vector2(nextPos.x + childOffsets[i].x, nextPos.y + childOffsets[i].y);
                        }
                    }
                }

                // b) Child'lar arası segment → dış obje (ikiler arasından obje geçişini engeller)
                for (int i = 0; i < childDrags.Count; i++)
                {
                    for (int j = i + 1; j < childDrags.Count; j++)
                    {
                        if (childDrags[i] == null || childDrags[j] == null) continue;

                        Vector2 segA = new Vector2((nextPos + childOffsets[i]).x, (nextPos + childOffsets[i]).y);
                        Vector2 segB = new Vector2((nextPos + childOffsets[j]).x, (nextPos + childOffsets[j]).y);

                        foreach (var other in allObjects)
                        {
                            if (other == null || !other.gameObject.activeInHierarchy || other.transform.IsChildOf(this.transform))
                                continue;

                            Vector2 op2 = new Vector2(other.transform.position.x, other.transform.position.y);
                            Vector2 ab = segB - segA;
                            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(op2 - segA, ab) / ab.sqrMagnitude) : 0f;
                            Vector2 closest = segA + t * ab;
                            float d = Vector2.Distance(op2, closest);

                            if (d < effectiveCollDist)
                            {
                                Vector2 pushDir = (closest - op2).normalized;
                                if (pushDir == Vector2.zero) pushDir = Vector2.up;
                                float penetration = effectiveCollDist - d;
                                nextPos.x += pushDir.x * penetration;
                                nextPos.y += pushDir.y * penetration;
                                segA = new Vector2((nextPos + childOffsets[i]).x, (nextPos + childOffsets[i]).y);
                                segB = new Vector2((nextPos + childOffsets[j]).x, (nextPos + childOffsets[j]).y);
                            }
                        }
                    }
                }
            }

            // Nihai doğrulama: zıt pushlar iptal ettiyse adımı tamamen engelle
            for (int i = 0; i < childDrags.Count && !stepBlocked; i++)
            {
                if (childDrags[i] == null) continue;
                Vector2 p2 = new Vector2((nextPos + childOffsets[i]).x, (nextPos + childOffsets[i]).y);
                foreach (var other in allObjects)
                {
                    if (other == null || !other.gameObject.activeInHierarchy || other.transform.IsChildOf(this.transform))
                        continue;
                    if (Vector2.Distance(p2, new Vector2(other.transform.position.x, other.transform.position.y)) < effectiveCollDist)
                    {
                        stepBlocked = true;
                        break;
                    }
                }
            }

            // Grid / boşluk kontrolü
            if (!stepBlocked && spawnerCache != null)
            {
                float gridSize = spawnerCache.gridPrefab.transform.localScale.x;
                float halfCell = (gridSize + spawnerCache.spacing) * 0.5f;

                for (int i = 0; i < childDrags.Count; i++)
                {
                    if (childDrags[i] == null) continue;
                    Vector3 localChildPos = spawnerCache.transform.InverseTransformPoint(nextPos + childOffsets[i]);

                    bool overValidGrid = false;
                    foreach (Transform cell in spawnerCache.transform)
                    {
                        if (!cell.CompareTag("Grid") || !cell.gameObject.activeInHierarchy || cell.name.Contains("Blocked"))
                            continue;
                        Vector3 cellLp = spawnerCache.transform.InverseTransformPoint(cell.position);
                        if (Mathf.Abs(localChildPos.x - cellLp.x) <= halfCell &&
                            Mathf.Abs(localChildPos.y - cellLp.y) <= halfCell)
                        {
                            overValidGrid = true;
                            break;
                        }
                    }
                    if (!overValidGrid) { stepBlocked = true; break; }
                }
            }

            if (!stepBlocked)
                currentPos = nextPos;
            else
                break;
        }

        transform.position = new Vector3(currentPos.x, currentPos.y, desiredPos.z);
    }

    void Drop()
    {
        dragging = false;
        cachedAllObjects = null; // Clear cache

        float screenDist = Vector2.Distance(Input.mousePosition, startScreenPos);
        float duration = Time.time - startTime;

        if (screenDist < 50f && duration < 0.5f)
        {
            transform.position = startPosition;

            GridSpawner spawner = FindObjectOfType<GridSpawner>();
            bool anyCanRotate = childDrags.Exists(c => c != null && c.canRotate);
            if (spawner != null && spawner.CurrentLevelType.HasFlag(LevelData.LevelType.Rotation) && anyCanRotate)
            {
                foreach (var c in childDrags)
                {
                    if (c == null || !c.canRotate) continue;
                    c.targetRotZ += 90f;
                    c.transform.DOKill();
                    DragObject captured = c;
                    c.transform.DOLocalRotate(new Vector3(0, 0, c.targetRotZ), 0.3f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() => {
                            LiquidTransfer lt = captured.GetComponentInChildren<LiquidTransfer>();
                            if (lt != null) lt.CheckSymmetry();
                        });
                }
            }
            return;
        }

        GameObject[] grids = GameObject.FindGameObjectsWithTag("Grid");
        bool allFit = false;
        Vector3 bestParentPosition = startPosition;
        float bestGroupDistance = Mathf.Infinity;

        DragObject[] allObsDrop = FindObjectsOfType<DragObject>();

        foreach (GameObject grid in grids)
        {
            float dist = Vector3.Distance(childDrags[0].transform.position, grid.transform.position);
            if (dist >= snapDistance) continue;

            Vector3 proposedParentPos = grid.transform.position - dragChildOffsets[0];
            proposedParentPos.z = transform.position.z;

            bool attemptValid = true;

            for (int i = 0; i < childDrags.Count; i++)
            {
                Vector3 testChildPos = proposedParentPos + dragChildOffsets[i];
                bool foundGrid = false;
                bool occupied = false;

                foreach (GameObject g in grids)
                {
                    if (Vector2.Distance(testChildPos, g.transform.position) > 0.1f) continue;
                    foundGrid = true;
                    foreach (var obs in allObsDrop)
                    {
                        if (obs == null || obs.transform.IsChildOf(this.transform)) continue;
                        if (Vector2.Distance(obs.transform.position, testChildPos) < 0.1f)
                        {
                            occupied = true;
                            break;
                        }
                    }
                    break;
                }

                if (!foundGrid || occupied)
                {
                    attemptValid = false;
                    break;
                }
            }

            if (attemptValid && dist < bestGroupDistance)
            {
                bestGroupDistance = dist;
                bestParentPosition = proposedParentPos;
                allFit = true;
            }
        }

        if (allFit)
        {
            transform.position = bestParentPosition;
            foreach (var c in childDrags)
            {
                if (c == null) continue;
                LiquidTransfer transfer = c.GetComponentInChildren<LiquidTransfer>();
                if (transfer != null) transfer.CheckSymmetry();
            }
        }
        else
        {
            transform.position = startPosition;
        }
    }
}
