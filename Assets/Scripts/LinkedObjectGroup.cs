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

    void Start()
    {
        cam = Camera.main;
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
        
        // Z ekseninde objelerin (-0.33) arkasına, grid (-0) önüne alalım
        backPanel.transform.localPosition = new Vector3(0, 0, -0.15f);
        
        LineRenderer lr = backPanel.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        
        // Cisimleri sıraya dizerek sürekli bir çizgi (pill/oval) yapalım
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
                if (d < minDist)
                {
                    minDist = d;
                    closest = u;
                }
            }
            pts.Add(closest.transform.localPosition);
            current = closest;
            unvisited.Remove(closest);
        }

        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            p.z = 0; // backPanel'in 0'ına (yani parent'ın -0.15'ine) göre
            lr.SetPosition(i, p);
        }

        // Genişliği grid hücrelerine ve objelere daha uyumlu hale getirelim.
        float width = 0.62f;
        lr.startWidth = width;
        lr.endWidth = width;
        
        // Oval / Yuvarlatılmış köşeler
        lr.numCapVertices = 15;
        lr.numCornerVertices = 15;

        // Temaya uygun Premium Şeffaf Materyal (Buzlu Cam / Jelibon Taban)
        Material mat = new Material(Shader.Find("Standard"));

        // Arkayı %100 kapatmaması ve gridlerle harmanlanması için saydam mod (Transparent)
        mat.SetFloat("_Mode", 3); 
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // Temadaki pastel mavi ve saf beyaz grid ortamında "Dokulu, Koyu Buzlu Cam" etkisi yaratır
        mat.color = new Color(0.2f, 0.25f, 0.3f, 0.55f); 
        
        // Parlamasını şık durması için korudum
        mat.SetFloat("_Glossiness", 0.7f); 
        mat.SetFloat("_Metallic", 0.1f);
        
        lr.material = mat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    void Update()
    {
        // Temizle (Destroy olan objeler null olur)
        childDrags.RemoveAll(item => item == null);
        
        // Sadece 1 obje kalırsa ya da obje kalmazsa tabakayı yok et
        if (childDrags.Count <= 1 && backPanel != null)
        {
            Destroy(backPanel);
        }

        if (childDrags.Count == 0) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began) TryPick(touch.position);
            if (touch.phase == TouchPhase.Moved && dragging) Drag(touch.position);
            if (touch.phase == TouchPhase.Ended) Drop();
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) TryPick(Input.mousePosition);
        if (Input.GetMouseButton(0) && dragging) Drag(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) Drop();
#endif
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
            }
        }
    }

    void Drag(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!dragPlane.Raycast(ray, out float enter)) return;

        Vector3 worldPoint = ray.GetPoint(enter);
        Vector3 desiredPos = worldPoint + worldGrabOffset;

        DragObject[] allObjects = FindObjectsOfType<DragObject>(true);

        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;

        List<Vector3> childOffsets = new List<Vector3>();
        foreach (var d in childDrags)
        {
            if (d != null) childOffsets.Add(d.transform.position - transform.position);
            else childOffsets.Add(Vector3.zero);
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.05f));
        Vector3 stepVec = moveDir / steps;

        GridSpawner spawner = FindObjectOfType<GridSpawner>();

        for (int s = 0; s < steps; s++)
        {
            Vector3 nextPos = currentPos + stepVec;
            bool stepBlocked = false;

            // 1. DİĞER OBJELERLE ÇARPIŞMA KONTROLÜ
            for (int iter = 0; iter < 2; iter++)
            {
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

                        if (d < collisionDistance)
                        {
                            Vector2 push = (p2 - op2).normalized;
                            if (push == Vector2.zero) push = Vector2.up;

                            Vector2 resolved = op2 + push * collisionDistance;
                            nextPos.x += (resolved.x - p2.x);
                            nextPos.y += (resolved.y - p2.y);
                            
                            p2 = new Vector2(nextPos.x + childOffsets[i].x, nextPos.y + childOffsets[i].y);
                        }
                    }
                }
            }

            // 2. GRID VE BOŞLUK (HOLE) KONTROLÜ
            // Her bir parçanın altında geçerli bir grid hücresi var mı?
            if (spawner != null)
            {
                float gridSize = spawner.gridPrefab.transform.localScale.x;
                float cellSize = gridSize + spawner.spacing;
                float halfCell = cellSize * 0.5f;

                for (int i = 0; i < childDrags.Count; i++)
                {
                    if (childDrags[i] == null) continue;
                    Vector3 worldChildPos = nextPos + childOffsets[i];
                    Vector3 localChildPos = spawner.transform.InverseTransformPoint(worldChildPos);
                    
                    bool overValidGrid = false;
                    foreach (Transform cell in spawner.transform)
                    {
                        if (!cell.CompareTag("Grid") || !cell.gameObject.activeInHierarchy || cell.name.Contains("Blocked")) 
                            continue;

                        Vector3 cellLp = spawner.transform.InverseTransformPoint(cell.position);
                        if (Mathf.Abs(localChildPos.x - cellLp.x) <= halfCell && 
                            Mathf.Abs(localChildPos.y - cellLp.y) <= halfCell)
                        {
                            overValidGrid = true;
                            break;
                        }
                    }

                    if (!overValidGrid)
                    {
                        stepBlocked = true;
                        break;
                    }
                }
            }

            if (!stepBlocked)
            {
                currentPos = nextPos;
            }
            else
            {
                break; // Geçersiz bir alana gelindiği için hareketi burada kes
            }
        }

        transform.position = new Vector3(currentPos.x, currentPos.y, desiredPos.z);
    }

    void Drop()
    {
        dragging = false;

        float screenDist = Vector2.Distance(Input.mousePosition, startScreenPos);
        float duration = Time.time - startTime;

        if (screenDist < 50f && duration < 0.5f)
        {
            transform.position = startPosition;

            GridSpawner spawner = FindObjectOfType<GridSpawner>();
            if (spawner != null && spawner.CurrentLevelType == LevelData.LevelType.Rotation)
            {
                transform.Rotate(0, 0, 90f);
                foreach (var c in childDrags)
                {
                    if (c == null) continue;
                    LiquidTransfer transfer = c.GetComponentInChildren<LiquidTransfer>();
                    if (transfer != null) transfer.CheckSymmetry();
                }
            }
            return;
        }

        GameObject[] grids = GameObject.FindGameObjectsWithTag("Grid");
        bool allFit = false;
        Vector3 bestParentPosition = startPosition;
        float bestGroupDistance = Mathf.Infinity;

        foreach (GameObject grid in grids)
        {
            float dist = Vector3.Distance(childDrags[0].transform.position, grid.transform.position);

            if (dist < snapDistance)
            {
                Vector3 worldOffset0 = childDrags[0].transform.position - transform.position;
                Vector3 proposedParentPos = grid.transform.position - worldOffset0;
                proposedParentPos.z = transform.position.z;

                bool attemptValid = true;

                for (int i = 0; i < childDrags.Count; i++)
                {
                    Vector3 worldOffsetI = childDrags[i].transform.position - transform.position;
                    Vector3 testChildPos = proposedParentPos + worldOffsetI;
                    bool foundGrid = false;
                    bool occupied = false;

                    foreach (GameObject g in grids)
                    {
                        if (Vector2.Distance(testChildPos, g.transform.position) < 0.1f)
                        {
                            foundGrid = true;
                            DragObject[] allObs = FindObjectsOfType<DragObject>();
                            foreach (var obs in allObs)
                            {
                                if (obs == null || obs.transform.IsChildOf(this.transform)) continue;
                                if (Vector2.Distance(obs.transform.position, testChildPos) < 0.1f)
                                {
                                    occupied = true; break;
                                }
                            }
                            break;
                        }
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
