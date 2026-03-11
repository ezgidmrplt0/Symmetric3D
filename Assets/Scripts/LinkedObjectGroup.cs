using UnityEngine;
using System.Collections.Generic;

public class LinkedObjectGroup : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;

    private Vector3 offset;
    private float zDepth;

    private Vector3 startPosition;
    private Vector2 startScreenPos;
    private float startTime;

    public float snapDistance = 1.5f;
    public float collisionDistance = 1.0f;

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

        // Genişliğini orta bir değere aldım (Küreleri taşmadan tatlıca kaplasın)
        lr.startWidth = 0.85f;
        lr.endWidth = 0.85f;
        
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
        // Daha az saydam (0.75) ve daha koyu/tok bir gri/lacivert tonu.
        mat.color = new Color(0.25f, 0.3f, 0.35f, 0.75f); 
        
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
            bool hitChild = false;
            foreach (var d in childDrags)
            {
                if (d != null && hit.transform.IsChildOf(d.transform))
                {
                    hitChild = true;
                    break;
                }
            }

            if (hitChild)
            {
                dragging = true;
                startPosition = transform.position;
                zDepth = cam.WorldToScreenPoint(transform.position).z;
                startScreenPos = screenPos;
                startTime = Time.time;

                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDepth));
                offset = transform.position - world;
            }
        }
    }

    void Drag(Vector3 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDepth));
        Vector3 desiredPos = world + offset;

        DragObject[] allObjects = FindObjectsOfType<DragObject>();

        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float expectedDistance = moveDir.magnitude;

        int steps = Mathf.CeilToInt(expectedDistance / 0.1f);
        if (steps > 0)
        {
            Vector3 stepVec = moveDir / steps;
            for (int s = 0; s < steps; s++)
            {
                currentPos += stepVec;

                for (int i = 0; i < 2; i++)
                {
                    foreach (DragObject obj in allObjects)
                    {
                        if (obj == null || obj.transform.IsChildOf(this.transform)) continue;

                        for (int j = 0; j < childDrags.Count; j++)
                        {
                            Vector3 worldOffset = childDrags[j].transform.position - transform.position;
                            Vector3 myChildPos = currentPos + worldOffset;
                            Vector2 myPos2D = new Vector2(myChildPos.x, myChildPos.y);
                            Vector2 otherPos2D = new Vector2(obj.transform.position.x, obj.transform.position.y);

                            float dist = Vector2.Distance(myPos2D, otherPos2D);
                            if (dist < collisionDistance)
                            {
                                Vector2 pushDir = (myPos2D - otherPos2D).normalized;
                                if (pushDir == Vector2.zero) pushDir = Vector2.up;

                                Vector2 resolvedPos2D = otherPos2D + pushDir * collisionDistance;
                                currentPos.x += (resolvedPos2D.x - myPos2D.x);
                                currentPos.y += (resolvedPos2D.y - myPos2D.y);
                            }
                        }
                    }
                }
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
                // Grubu kendi merkezi etrafında 90 derece döndür
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

        // Grubun ilk elemanı (anchor) için tüm gridleri dene
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
