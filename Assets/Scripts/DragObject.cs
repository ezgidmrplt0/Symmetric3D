using UnityEngine;

public class DragObject : MonoBehaviour
{
    private Camera cam;
    private bool dragging = false;

    private Vector3 offset;
    private float zDepth;

    private Vector3 startPosition;

    [Header("Ayarlar")]
    public float snapDistance = 1.5f;
    
    [Header("Çarpışma (Collision)")]
    [Tooltip("Diğer objelerin içinden geçmesini engelleyen çap (Gerekirse Inspector'dan küçültüp büyütebilirsiniz)")]
    public float collisionDistance = 1.0f;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
                TryPick(touch.position);

            if (touch.phase == TouchPhase.Moved && dragging)
                Drag(touch.position);

            if (touch.phase == TouchPhase.Ended)
                Drop();
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            TryPick(Input.mousePosition);

        if (Input.GetMouseButton(0) && dragging)
            Drag(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            Drop();
#endif
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

                zDepth = cam.WorldToScreenPoint(transform.position).z;

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

        // Objeye hızlıca fareyi çeksek bile objenin içinden (tunneling) geçmemesi için adım adım hareket simülasyonu
        int steps = Mathf.CeilToInt(expectedDistance / 0.1f);
        if (steps > 0)
        {
            Vector3 stepVec = moveDir / steps;

            for (int s = 0; s < steps; s++)
            {
                currentPos += stepVec;

                // Her adımda diğer objelerle çarpışmayı (overlap) çöz
                for (int i = 0; i < 2; i++) 
                {
                    foreach (DragObject obj in allObjects)
                    {
                        if (obj == this) continue;

                        Vector2 myPos2D = new Vector2(currentPos.x, currentPos.y);
                        Vector2 otherPos2D = new Vector2(obj.transform.position.x, obj.transform.position.y);

                        float dist = Vector2.Distance(myPos2D, otherPos2D);

                        if (dist < collisionDistance)
                        {
                            Vector2 pushDir = (myPos2D - otherPos2D).normalized;
                            if (pushDir == Vector2.zero) pushDir = Vector2.up;

                            // İtme miktarını uygulayarak etrafından kaymasını sağla
                            Vector2 resolvedPos2D = otherPos2D + pushDir * collisionDistance;
                            currentPos.x = resolvedPos2D.x;
                            currentPos.y = resolvedPos2D.y;
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

        GameObject[] grids = GameObject.FindGameObjectsWithTag("Grid");

        float closestDistance = Mathf.Infinity;
        Transform closestGrid = null;

        foreach (GameObject grid in grids)
        {
            float dist = Vector3.Distance(transform.position, grid.transform.position);

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestGrid = grid.transform;
            }
        }

        if (closestGrid != null)
        {
            // O grid'de başka bir top var mı diye kontrol edelim
            DragObject[] allObjects = FindObjectsOfType<DragObject>();
            bool isOccupied = false;

            foreach (DragObject obj in allObjects)
            {
                if (obj == this) continue;
                
                // Başka bir objenin X, Y kordinatları o grid ile aynıysa o grid doludur
                float distToGrid = Vector2.Distance(
                    new Vector2(obj.transform.position.x, obj.transform.position.y), 
                    new Vector2(closestGrid.position.x, closestGrid.position.y)
                );

                if (distToGrid < 0.1f)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (closestDistance < snapDistance && !isOccupied)
            {
                // Boş ve yakınsa oraya oturt
                transform.position = new Vector3(
                    closestGrid.position.x,
                    closestGrid.position.y,
                    transform.position.z
                );

                // Bırakıldıktan sonra simetriği olup olmadığını kontrol et
                LiquidTransfer transfer = GetComponentInChildren<LiquidTransfer>();
                if (transfer != null) transfer.CheckSymmetry();
            }
            else
            {
                // Dolu bir yere veya uzağa bırakıldıysa geri dön
                transform.position = startPosition;
            }
        }
        else
        {
            // grid bulunamadıysa geri dön
            transform.position = startPosition;
        }
    }
}