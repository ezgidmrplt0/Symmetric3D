using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

/// <summary>
/// Level yöneticisi ve koordinatör.
/// 2D spawn mantığı: GridSpawner.Flat2D.cs
/// 3D spawn mantığı: GridSpawner.Shape3D.cs
/// </summary>
public partial class GridSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject gridPrefab;
    public GameObject objectPrefab;

    [Header("Level Kaynağı")]
    [Tooltip("Symmetric3D > Level Akış Yöneticisi'nden oluşturulan asset buraya sürüklenir")]
    public LevelSequenceData sequence;
    public int currentLevelIndex = 0;

    [Header("Görsel Ayarlar")]
    public float spacing = 0.4f;
    public float objectOffset = 0.3f;

    [Header("Kamera ve Çerçeve (Modüler)")]
    public GameObject frameSegmentPrefab;
    [Tooltip("3D şekillerin köşelerine yerleştirilecek prefab. Boş bırakılırsa frameSegmentPrefab kullanılır.")]
    public GameObject shapeCornerPrefab;
    [Tooltip("2D levellerde grid arkasına koyulacak beyaz zemin prefabı.")]
    public GameObject backgroundPlatePrefab;
    public Camera mainCamera;
    public float frameThickness = 0.15f;
    public float framePadding = 0.15f;
    public float cameraPadding = 0.2f;
    public float cameraZoomFactor = 0.65f;
    public float cameraVerticalOffset = 0.1f;
    [Tooltip("3D şekil spawn Z offseti — negatif değer şekli kameraya yaklaştırır.")]
    public float shapeZOffset = -1f;

    [Header("UI Referansları")]
    public TextMeshProUGUI levelText;
    public TMP_FontAsset globalFont;
    public TextMeshProUGUI timerText; // Geri sayım sayacı metni

    // ──────────────────────────────────────────────────────────────
    // ÖZEL DURUM (partial dosyalar da erişir)
    // ──────────────────────────────────────────────────────────────

    private List<GameObject> activeSpawnedObjects = new List<GameObject>();
    private List<GameObject> activeFrameSegments = new List<GameObject>();
    private Dictionary<int, Transform> spawnedFaceRoots = new Dictionary<int, Transform>();
    private List<LevelData.PieceData> pendingPieces = new List<LevelData.PieceData>();
    private bool lastRemainingTriggered = false;

    // ──────────────────────────────────────────────────────────────
    // KOLAYLIK ÖZELLİKLERİ
    // ──────────────────────────────────────────────────────────────

    public List<LevelData> levels => sequence != null ? sequence.levels : null;

    public LevelData.LevelType CurrentLevelType
    {
        get
        {
            if (levels == null || currentLevelIndex >= levels.Count || levels[currentLevelIndex] == null)
                return LevelData.LevelType.Classic;
            return levels[currentLevelIndex].levelType;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // BAŞLANGIÇ
    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        currentLevelIndex = PlayerPrefs.GetInt("CurrentLevelIndex", 0);

        if (levels != null && currentLevelIndex >= levels.Count)
            currentLevelIndex = 0;

        if (levelText == null)
        {
            GameObject levelObj = GameObject.Find("LEVEL");
            if (levelObj != null) levelText = levelObj.GetComponent<TextMeshProUGUI>();
            else
            {
                GameObject canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    Transform t = canvas.transform.Find("LEVEL");
                    if (t != null) levelText = t.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        SpawnCurrentLevel();
    }

    // ──────────────────────────────────────────────────────────────
    // LEVEL AKIŞ YÖNETİMİ
    // ──────────────────────────────────────────────────────────────

    public void RestartLevel()
    {
        SpawnCurrentLevel();
    }

    public void SpawnCurrentLevel()
    {
        ClearCurrentLevel();

        if (levels == null || levels.Count == 0)
        {
            return;
        }

        if (currentLevelIndex < levels.Count && levels[currentLevelIndex] != null)
            SpawnLevel(levels[currentLevelIndex]);
    }

    public void NextLevel()
    {
        GameManager.Instance?.ResetLevelState();

        if (levels == null || levels.Count == 0)
        {
            return;
        }

        int effectiveProgress = GameManager.Instance != null
            ? GameManager.Instance.lifetimeProgress
            : 0;


        int startIndex = currentLevelIndex;
        int next = currentLevelIndex;

        for (int i = 1; i <= levels.Count; i++)
        {
            int candidate = (currentLevelIndex + i) % levels.Count;
            LevelData candidateLevel = levels[candidate];

            if (candidate == startIndex)
            {
                next = candidate;
                break;
            }

            if (candidateLevel == null)
            {
                continue;
            }

            if (sequence != null && !sequence.IsLevelUnlocked(candidateLevel, effectiveProgress))
            {
                continue;
            }

            next = candidate;
            break;
        }

        currentLevelIndex = next;
        PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
        PlayerPrefs.Save();

        SpawnCurrentLevel();
    }

    void ClearCurrentLevel()
    {
        foreach (GameObject obj in activeSpawnedObjects)
            if (obj != null) Destroy(obj);
        activeSpawnedObjects.Clear();

        foreach (GameObject seg in activeFrameSegments)
            if (seg != null) Destroy(seg);
        activeFrameSegments.Clear();
        pendingPieces.Clear();
        lastRemainingTriggered = false;
    }

    /// <summary>Board moduna göre 2D veya 3D spawn'ı başlatır.</summary>
    void SpawnLevel(LevelData level)
    {

        if (levelText != null)
        {
            levelText.text = "LEVEL " + (currentLevelIndex + 1);
            if (globalFont != null) levelText.font = globalFont;
        }

        // --- TIMER BAŞLATMA ---
        LevelTimer timer = LevelTimer.Instance;
        if (timer == null) timer = FindObjectOfType<LevelTimer>();
        if (timer != null)
        {
            if (timerText != null) timer.timerText = timerText;
            timer.ResetTimer(level.timeLimit);
        }

        float gridSize = gridPrefab.transform.localScale.x;

        if (level.boardMode == LevelData.BoardMode.Shape3D)
            SpawnShapeLevel(level, gridSize);
        else
            SpawnFlat2DLevel(level, gridSize);
    }

    // ──────────────────────────────────────────────────────────────
    // GÖLGE / YARDIMCI
    // ──────────────────────────────────────────────────────────────

    private Vector3 FindEmptyGridPosition(Vector3 targetPos, float gridSize, float offsetX, float offsetY, LevelData level)
    {
        float step = gridSize + spacing;
        DragObject[] existing = FindObjectsOfType<DragObject>();

        // Tüm grid hücrelerini listele
        List<Vector3> allCells = new List<Vector3>();
        if (level.customGridPositions != null && level.customGridPositions.Count > 0)
        {
            foreach (var gp in level.customGridPositions)
                allCells.Add(transform.position + new Vector3(gp.x * step - offsetX, gp.y * step - offsetY, -objectOffset));
        }
        else
        {
            for (int x = 0; x < level.gridX; x++)
                for (int y = 0; y < level.gridY; y++)
                    allCells.Add(transform.position + new Vector3(x * step - offsetX, y * step - offsetY, -objectOffset));
        }

        // Dolu hücreleri çıkar
        foreach (var obj in existing)
        {
            Vector3 objPos = new Vector3(obj.transform.position.x, obj.transform.position.y, -objectOffset);
            Vector3 closest = allCells[0];
            float minDist = float.MaxValue;
            foreach (var cell in allCells)
            {
                float d = Vector3.Distance(objPos, cell);
                if (d < minDist) { minDist = d; closest = cell; }
            }
            if (minDist < step * 0.5f) allCells.Remove(closest);
        }

        if (allCells.Count == 0) return targetPos; // Fallback: üst üste gel

        // Hedef pozisyona en yakın boş hücreyi döndür
        Vector3 best = allCells[0];
        float bestDist = Vector3.Distance(targetPos, best);
        foreach (var cell in allCells)
        {
            float d = Vector3.Distance(targetPos, cell);
            if (d < bestDist) { bestDist = d; best = cell; }
        }
        return best;
    }

    private LiquidTransfer FindMirrorTarget()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        LiquidTransfer best = null;
        foreach (var lt in all)
        {
            if (lt.transferring) continue;
            if (lt.isShadowTrigger && !lt.shadowSpawned) continue;
            if (best == null || lt.currentSlices > best.currentSlices)
                best = lt;
        }
        return best;
    }

    public HashSet<int> GetPendingSpawnIds()
    {
        HashSet<int> ids = new HashSet<int>();
        foreach (var p in pendingPieces)
            if (p.spawnShadowAfterLinkID > 0)
                ids.Add(p.spawnShadowAfterLinkID);
        return ids;
    }

    public bool HasPendingShadows()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        foreach (var lt in all)
            if (lt.isShadowTrigger && !lt.transferring) return true;
        return false;
    }

    public void SpawnShadowFor(LiquidTransfer trigger)
    {
        SpawnDynamicShadow(trigger.liquidColor, trigger.currentSlices, trigger.transform.eulerAngles.z);
    }

    /// <summary>
    /// Dinamik shadow spawn: renk/dilim/rotasyon veriden üretilir.
    /// Rotasyon otomatik olarak +180° (tam zıt yön) uygulanır.
    /// Spawn sonrası sahneyi CheckSymmetry ile tarar.
    /// </summary>
    public void SpawnDynamicShadow(Color color, int slices, float sourceRotationZ)
    {
        if (levels == null || levels.Count == 0 || currentLevelIndex >= levels.Count) return;

        List<Transform> validGrids = new List<Transform>();
        foreach (Transform t in transform.GetComponentsInChildren<Transform>())
        {
            if (t.name.Contains("Grid") || t.CompareTag("Grid"))
                if (t.gameObject != this.gameObject && !t.name.Contains("Face_"))
                    validGrids.Add(t);
        }

        DragObject[] existing = FindObjectsOfType<DragObject>();
        List<Transform> emptyGrids = new List<Transform>(validGrids);

        foreach (var obj in existing)
        {
            Transform nearest = null;
            float minDist = 0.4f;
            foreach (var g in validGrids)
            {
                float dist = Vector3.Distance(obj.transform.position, g.position);
                if (dist < minDist) { minDist = dist; nearest = g; }
            }
            if (nearest != null) emptyGrids.Remove(nearest);
        }

        if (emptyGrids.Count == 0)
        {
            return;
        }

        Transform targetGrid = emptyGrids[Random.Range(0, emptyGrids.Count)];
        float shadowRot = (sourceRotationZ + 180f) % 360f;

        Transform parentTransform = targetGrid.parent != null ? targetGrid.parent : transform;
        GameObject shadowObj = Instantiate(objectPrefab, parentTransform);
        shadowObj.transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, -objectOffset);
        shadowObj.transform.localRotation = Quaternion.Euler(0, 0, shadowRot);
        activeSpawnedObjects.Add(shadowObj);

        DragObject shadowDrag = shadowObj.GetComponent<DragObject>();
        if (shadowDrag != null) shadowDrag.canRotate = false;

        // Shape3D modunda yüzey marker'ı varsa lossyScale ile scale hesapla
        ShapeFaceMarker sfm = parentTransform.GetComponent<ShapeFaceMarker>();
        Vector3 targetScale;
        if (sfm != null && levels != null && currentLevelIndex < levels.Count)
        {
            LevelData level = levels[currentLevelIndex];
            int faceIdx = -1;
            foreach (var kvp in spawnedFaceRoots)
                if (kvp.Value == parentTransform) { faceIdx = kvp.Key; break; }

            if (faceIdx >= 0 && faceIdx < level.shapeFaces.Count)
            {
                LevelData.FaceLayoutData fd = level.shapeFaces[faceIdx];
                bool isTri = sfm.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
                int gy = isTri ? fd.gridX : fd.gridY;
                float area = isTri ? 0.82f : 1f;
                float sx = area / fd.gridX;
                float sy = area / gy;
                Vector3 ws = parentTransform.lossyScale;
                float worldS = Mathf.Min(sx * Mathf.Abs(ws.x), sy * Mathf.Abs(ws.y)) * 0.68f;
                targetScale = new Vector3(worldS / Mathf.Abs(ws.x), worldS / Mathf.Abs(ws.y), worldS);
                shadowObj.transform.localPosition = new Vector3(
                    targetGrid.localPosition.x,
                    targetGrid.localPosition.y,
                    -(worldS * 0.5f + sfm.surfaceOffset)
                );
            }
            else
            {
                targetScale = objectPrefab.transform.localScale;
            }
        }
        else
        {
            targetScale = objectPrefab.transform.localScale;
        }

        shadowObj.transform.localScale = Vector3.zero;
        shadowObj.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack);

        LiquidTransfer lt = shadowObj.GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            lt.liquidColor = color;
            lt.currentSlices = slices;
            lt.isShadowChild = true;
            lt.UpdateVisuals();
        }
    }

    private bool HasLastRemainingRule()
    {
        if (levels == null || currentLevelIndex >= levels.Count) return false;
        return levels[currentLevelIndex].lastRemainingShadow;
    }

    public void TrySpawnPending(int clearedLinkId, LiquidTransfer mirrorSource = null, LiquidTransfer mirrorOther = null)
    {
        if (levels == null || currentLevelIndex >= levels.Count) return;
        LevelData level = levels[currentLevelIndex];
        float gridSize = gridPrefab.transform.localScale.x;

        // 2D Offset Hesapla
        bool isCustom = level.customGridPositions != null && level.customGridPositions.Count > 0;
        float minX = 0, maxX = level.gridX - 1;
        float minY = 0, maxY = level.gridY - 1;
        if (isCustom)
        {
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            foreach (var pos in level.customGridPositions)
            {
                if (pos.x < minX) minX = pos.x; if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y; if (pos.y > maxY) maxY = pos.y;
            }
        }
        float offsetX = (minX + maxX) * (gridSize + spacing) / 2f;
        float offsetY = (minY + maxY) * (gridSize + spacing) / 2f;

        List<LevelData.PieceData> toSpawn = new List<LevelData.PieceData>();
        foreach(var p in pendingPieces) if(p.spawnShadowAfterLinkID == clearedLinkId) toSpawn.Add(p);

        // --- SIRALI EŞLEŞME (ORDERED MATCHING) ---
        toSpawn.Sort((a, b) => a.gridPosition.x != b.gridPosition.x ? a.gridPosition.x.CompareTo(b.gridPosition.x) : a.gridPosition.y.CompareTo(b.gridPosition.y));
        
        List<LiquidTransfer> mirrors = new List<LiquidTransfer>();
        if (mirrorSource != null) mirrors.Add(mirrorSource);
        if (mirrorOther != null) mirrors.Add(mirrorOther);
        mirrors.Sort((a, b) => a.initialGridPos.x != b.initialGridPos.x ? a.initialGridPos.x.CompareTo(b.initialGridPos.x) : a.initialGridPos.y.CompareTo(b.initialGridPos.y));

        for (int pIdx = 0; pIdx < toSpawn.Count; pIdx++)
        {
            var piece = toSpawn[pIdx];
            pendingPieces.Remove(piece);
            GameObject newObj = null;

            if (level.boardMode == LevelData.BoardMode.Shape3D)
            {
                // 3D Yüzey Spawn
                if (spawnedFaceRoots.TryGetValue(piece.faceIndex, out Transform marker))
                {
                    ShapeFaceMarker sfm = marker.GetComponent<ShapeFaceMarker>();
                    LevelData.FaceLayoutData fd = level.shapeFaces[piece.faceIndex];
                    
                    bool isTri = sfm.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
                    int gy = isTri ? fd.gridX : fd.gridY;
                    float area = isTri ? 0.82f : 1f;
                    float sx = area / fd.gridX; float sy = area / gy;
                    float xOff = 0;
                    if (isTri) { int cells = Mathf.Max(0, fd.gridX - piece.gridPosition.y); xOff = (1f - cells*sx)*0.5f; }

                    Vector3 lPos = new Vector3(-0.5f+(piece.gridPosition.x+0.5f)*sx+xOff, -0.5f+(piece.gridPosition.y+0.5f)*sy, 0);
                    newObj = Instantiate(objectPrefab, marker);
                    newObj.transform.localPosition = lPos;
                    newObj.transform.localRotation = Quaternion.Euler(0,0,piece.rotationZ);

                    Vector3 ws = marker.lossyScale;
                    float worldS = Mathf.Min(sx*Mathf.Abs(ws.x), sy*Mathf.Abs(ws.y))*0.68f;
                    newObj.transform.localScale = new Vector3(worldS/Mathf.Abs(ws.x), worldS/Mathf.Abs(ws.y), worldS);
                    newObj.transform.localPosition = new Vector3(lPos.x, lPos.y, -(worldS*0.5f + sfm.surfaceOffset));
                }
            }
            else
            {
                // 2D Spawn
                Vector3 targetPos = transform.position + new Vector3(piece.gridPosition.x*(gridSize+spacing)-offsetX, piece.gridPosition.y*(gridSize+spacing)-offsetY, -objectOffset);
                Vector3 spawnPos = FindEmptyGridPosition(targetPos, gridSize, offsetX, offsetY, level);
                newObj = Instantiate(objectPrefab, spawnPos, Quaternion.Euler(0,0,piece.rotationZ), transform);
            }

            if (newObj != null)
            {
                activeSpawnedObjects.Add(newObj);
                DragObject dobj = newObj.GetComponent<DragObject>();
                if(dobj != null) { dobj.linkId = piece.linkId; dobj.canRotate = false; }

                LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
                if (lt != null)
                {
                    lt.isShadowTrigger = piece.isShadowTrigger;
                    lt.spawnShadowAfterLinkID = piece.spawnShadowAfterLinkID;
                    lt.shadowSpawned = true;
                    lt.initialGridPos = piece.gridPosition;
                    lt.initialFaceIndex = piece.faceIndex;

                    // Dinamik Simetri (Mirror Logic - Ordered Matching)
                    LiquidTransfer mirror = null;
                    if (mirrors.Count > 0) mirror = mirrors[pIdx % mirrors.Count];

                    if (mirror != null)
                    {
                        lt.liquidColor = mirror.liquidColor;
                        // Akıllı Miktar
                        lt.currentSlices = Mathf.Clamp(mirror.currentSlices, 1, mirror.maxSlices - 1);
                        // SIVI AKTARIMI İÇİN TAM ZITTI (+180)
                        float mRotZ = mirror.transform.eulerAngles.z;
                        newObj.transform.localRotation = Quaternion.Euler(0, 0, (mRotZ + 180f) % 360f);
                    }
                    else
                    {
                        lt.liquidColor = piece.liquidColor;
                        lt.currentSlices = piece.currentSlices;
                    }

                    lt.UpdateVisuals();
                }

                Vector3 targetS = newObj.transform.localScale;
                newObj.transform.localScale = Vector3.zero;
                newObj.transform.DOScale(targetS, 0.5f).SetEase(Ease.OutBack);
            }
        }
    }

    public Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        if (levels == null || levels.Count == 0 || currentLevelIndex >= levels.Count)
            return transform.position;

        LevelData level = levels[currentLevelIndex];
        float gridSize = gridPrefab.transform.localScale.x;

        float minX = 0, maxX = level.gridX - 1;
        float minY = 0, maxY = level.gridY - 1;

        if (level.customGridPositions != null && level.customGridPositions.Count > 0)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            foreach (var pos in level.customGridPositions)
            {
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
        }

        float offsetX = (minX + maxX) * (gridSize + spacing) / 2f;
        float offsetY = (minY + maxY) * (gridSize + spacing) / 2f;

        return transform.position + new Vector3(
            gridPos.x * (gridSize + spacing) - offsetX,
            gridPos.y * (gridSize + spacing) - offsetY,
            -objectOffset
        );
    }

    public DragObject GetPieceAt(Vector2Int gridPos, int faceIndex = 0)
    {
        DragObject[] all = FindObjectsOfType<DragObject>();
        foreach (var obj in all)
        {
            if (obj == null) continue;
            LiquidTransfer lt = obj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null && lt.initialGridPos == gridPos && lt.initialFaceIndex == faceIndex)
            {
                // Mevcut konumunu kontrol et (Sürüklenmiş olabilir ama başlangıç verisine bakıyoruz)
                return obj;
            }
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────
    // FAIL KONTROLÜ
    // ──────────────────────────────────────────────────────────────

    public void CheckForFail()
    {
        if (PossibleMovesExist()) return;

        // Hamle kalmadı ama spawn bekleyen shadow var mı?
        if (pendingPieces.Exists(p => p.spawnShadowAfterLinkID == 0))
        {
            TrySpawnPending(0);
            DOVirtual.DelayedCall(0.6f, CheckForFail);
            return;
        }

        // LastRemaining shadow kuralı: tek obje kaldıysa tam zıttını spawn et
        if (!lastRemainingTriggered && HasLastRemainingRule())
        {
            LiquidTransfer[] allLt = FindObjectsOfType<LiquidTransfer>();
            List<LiquidTransfer> active = new List<LiquidTransfer>();
            foreach (var lt in allLt)
                if (lt != null && lt.gameObject.activeInHierarchy && !lt.transferring) active.Add(lt);

            if (active.Count == 1)
            {
                lastRemainingTriggered = true;
                LiquidTransfer remaining = active[0];
                Color c = remaining.liquidColor;
                int s = remaining.currentSlices;
                float r = remaining.transform.eulerAngles.z;

                DOVirtual.DelayedCall(0.3f, () => SpawnDynamicShadow(c, s, r));
                return;
            }
        }

        GameManager.Instance?.LevelFail();
    }

    private bool PossibleMovesExist()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        List<LiquidTransfer> activePieces = new List<LiquidTransfer>();


        foreach (var lt in all)
        {
            if (lt.transferring)
            {
                return true;
            }
            if (lt != null && lt.gameObject.activeInHierarchy)
                activePieces.Add(lt);
        }

        if (activePieces.Count == 0)
        {
            return true;
        }

        if (activePieces.Count == 1)
        {
            return false;
        }

        for (int i = 0; i < activePieces.Count; i++)
        {
            for (int j = i + 1; j < activePieces.Count; j++)
            {
                if (CanInteractionsExist(activePieces[i], activePieces[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanInteractionsExist(LiquidTransfer a, LiquidTransfer b)
    {
        if (a == null || b == null) return false;

        bool capable = false;
        if (CurrentLevelType.HasFlag(LevelData.LevelType.ColorMix))
        {
            if (!ColorMixData.ColorsMatch(a.liquidColor, b.liquidColor))
                if (ColorMixData.TryGetMix(a.liquidColor, b.liquidColor, out _)) capable = true;
        }

        if (!capable && CurrentLevelType.HasFlag(LevelData.LevelType.Classic))
        {
            bool colorMatch = ColorMixData.ColorsMatch(a.liquidColor, b.liquidColor);
            bool sliceMatch = a.currentSlices == b.currentSlices;
            bool notFull = a.currentSlices < a.maxSlices;
            if (colorMatch && sliceMatch && notFull) capable = true;
        }

        if (!capable) return false;

        if (!CurrentLevelType.HasFlag(LevelData.LevelType.Rotation))
        {
            Vector3 myFace    = a.transform.up;
            Vector3 otherFace = b.transform.up;

            if (Vector3.Dot(myFace, -otherFace) < 0.9f)
            {
                // Shape3D modunda parçalar yüzey değiştirebilir → yön uyuşmasa da fail sayma
                bool is3D = levels != null &&
                            currentLevelIndex < levels.Count &&
                            levels[currentLevelIndex].boardMode == LevelData.BoardMode.Shape3D;
                if (!is3D) return false;
            }
        }

        return true;
    }
}
