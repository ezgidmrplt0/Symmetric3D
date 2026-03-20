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
    public float cameraPadding = 0.8f;
    public float cameraZoomFactor = 0.75f;
    public float cameraVerticalOffset = 0.5f;
    [Tooltip("3D şekil spawn Z offseti — negatif değer şekli kameraya yaklaştırır.")]
    public float shapeZOffset = -1f;

    [Header("UI Referansları")]
    public TextMeshProUGUI levelText;

    // ──────────────────────────────────────────────────────────────
    // ÖZEL DURUM (partial dosyalar da erişir)
    // ──────────────────────────────────────────────────────────────

    private List<GameObject> activeSpawnedObjects = new List<GameObject>();
    private List<GameObject> activeFrameSegments = new List<GameObject>();
    private Dictionary<int, Transform> spawnedFaceRoots = new Dictionary<int, Transform>();
    private List<LevelData.PieceData> pendingPieces = new List<LevelData.PieceData>();

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

    public void SpawnCurrentLevel()
    {
        ClearCurrentLevel();

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("GridSpawner: LevelSequenceData atanmamış veya boş!");
            return;
        }

        if (currentLevelIndex < levels.Count && levels[currentLevelIndex] != null)
            SpawnLevel(levels[currentLevelIndex]);
        else
            Debug.LogWarning("GridSpawner: Geçersiz level index!");
    }

    public void NextLevel()
    {
        GameManager.Instance?.ResetLevelState();

        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("[GridSpawner] Levels listesi boş! NextLevel çalışamaz.");
            return;
        }

        int effectiveProgress = GameManager.Instance != null
            ? GameManager.Instance.lifetimeProgress
            : 0;

        Debug.Log($"[GridSpawner] NextLevel() | Mevcut Index: {currentLevelIndex} | Lifetime Progress: {effectiveProgress}");

        int startIndex = currentLevelIndex;
        int next = currentLevelIndex;

        for (int i = 1; i <= levels.Count; i++)
        {
            int candidate = (currentLevelIndex + i) % levels.Count;
            LevelData candidateLevel = levels[candidate];

            if (candidate == startIndex)
            {
                Debug.Log("[GridSpawner] Tüm liste tarandı, başka açık level bulunamadı. Mevcut seviye tekrarlanacak.");
                next = candidate;
                break;
            }

            if (candidateLevel == null)
            {
                Debug.LogWarning($"[GridSpawner] Index {candidate} boş (null)! Geçiliyor...");
                continue;
            }

            if (sequence != null && !sequence.IsLevelUnlocked(candidateLevel, effectiveProgress))
            {
                Debug.Log($"[GridSpawner] '{candidateLevel.levelDisplayName}' (Index: {candidate}) kilitli! Gerekli: {sequence.GetUnlockProgress(candidateLevel.levelType)}");
                continue;
            }

            Debug.Log($"[GridSpawner] Yeni level bulundu: {candidateLevel.levelDisplayName} (Index: {candidate})");
            next = candidate;
            break;
        }

        if (next == startIndex && next == currentLevelIndex)
            Debug.Log("<color=orange>[GridSpawner] Oyun Bitti veya Tüm leveller kilitli!</color>");

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
    }

    /// <summary>Board moduna göre 2D veya 3D spawn'ı başlatır.</summary>
    void SpawnLevel(LevelData level)
    {
        GameManager.Instance?.ResetLevelState();

        if (levelText != null)
            levelText.text = "LEVEL " + (currentLevelIndex + 1);

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
            Debug.LogWarning("Shadow için boş yer bulunamadı!");
            return;
        }

        Transform targetGrid = emptyGrids[Random.Range(0, emptyGrids.Count)];

        float triggerRot = trigger.transform.localRotation.eulerAngles.z;
        float shadowRot = (triggerRot + 180f) % 360f;

        GameObject shadowObj = Instantiate(objectPrefab, targetGrid.parent);
        shadowObj.transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, -objectOffset);
        shadowObj.transform.localRotation = Quaternion.Euler(0, 0, shadowRot);
        activeSpawnedObjects.Add(shadowObj);

        Vector3 targetScale = objectPrefab.transform.localScale;
        shadowObj.transform.localScale = Vector3.zero;
        shadowObj.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack);

        LiquidTransfer lt = shadowObj.GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            lt.liquidColor = trigger.liquidColor;
            lt.currentSlices = trigger.currentSlices;
            lt.isShadowChild = true;
            lt.UpdateVisuals();
        }
    }

    public void TrySpawnPending(int clearedLinkId)
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

        foreach(var piece in toSpawn)
        {
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
                    float worldS = Mathf.Min(sx*Mathf.Abs(ws.x), sy*Mathf.Abs(ws.y))*0.55f;
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
                if(dobj != null) dobj.linkId = piece.linkId;

                LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
                if (lt != null)
                {
                    lt.isShadowTrigger = piece.isShadowTrigger;
                    lt.spawnShadowAfterLinkID = piece.spawnShadowAfterLinkID;
                    lt.shadowSpawned = true;

                    if (clearedLinkId == 0)
                    {
                        // Sahadaki son parçanın tam simetrisini al
                        LiquidTransfer mirror = FindMirrorTarget();
                        if (mirror != null)
                        {
                            lt.liquidColor = mirror.liquidColor;
                            lt.currentSlices = mirror.currentSlices;
                            float mirrorRot = (mirror.transform.eulerAngles.z + 180f) % 360f;
                            newObj.transform.eulerAngles = new Vector3(0, 0, mirrorRot);
                        }
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
            DOVirtual.DelayedCall(0.6f, CheckForFail); // Spawn animasyonu biter bitmez tekrar kontrol et
            return;
        }

        Debug.Log("[GridSpawner] Oynanabilir hamle kalmadı, FAIL tetikleniyor.");
        GameManager.Instance?.LevelFail();
    }

    private bool PossibleMovesExist()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        List<LiquidTransfer> activePieces = new List<LiquidTransfer>();

        Debug.Log($"<color=cyan>[GridSpawner]</color> Fail Kontrolü başladı. Sahnede toplam {all.Length} LiquidTransfer bulundu.");

        foreach (var lt in all)
        {
            if (lt.transferring)
            {
                Debug.Log($"<color=yellow>[GridSpawner]</color> '{lt.name}' şu an transfer halinde. Kontrol erteleniyor.");
                return true;
            }
            if (lt != null && lt.gameObject.activeInHierarchy)
                activePieces.Add(lt);
        }

        if (activePieces.Count == 0)
        {
            Debug.Log("<color=green>[GridSpawner]</color> Aktif parça kalmadı, temiz kazanıldı.");
            return true;
        }

        string piecesLog = "[GridSpawner] Aktif Parçalar Listesi:\n";
        foreach (var ap in activePieces) piecesLog += $"- {ap.name} | Renk: {ap.liquidColor} | Dilim: {ap.currentSlices}/{ap.maxSlices} | Shadow: {ap.isShadowTrigger}\n";
        Debug.Log(piecesLog);

        if (activePieces.Count == 1)
        {
            Debug.Log($"<color=red>[GridSpawner]</color> Tek parça kaldı ve eşleşme imkansız!");
            return false;
        }

        for (int i = 0; i < activePieces.Count; i++)
        {
            for (int j = i + 1; j < activePieces.Count; j++)
            {
                if (CanInteractionsExist(activePieces[i], activePieces[j]))
                {
                    Debug.Log($"<color=green>[GridSpawner]</color> Potansiyel Eşleşme Bulundu: {activePieces[i].name} <-> {activePieces[j].name}");
                    return true;
                }
            }
        }

        Debug.Log($"<color=red>[GridSpawner]</color> {activePieces.Count} parça arasında hiçbir geçerli etkileşim bulunamadı. FAIL KOŞULU!");
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
