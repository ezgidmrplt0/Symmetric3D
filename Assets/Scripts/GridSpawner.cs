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
    [Tooltip("Ekranın üst kısmında UI (buton, level yazısı vb.) tarafından kullanılan yükseklik oranı (0–1). " +
             "Örn: 0.12 → ekranın %12'si UI'a ayrılmış. Kamera hesaplamaları bu alanı dışarıda tutar.")]
    [Range(0f, 0.4f)]
    public float uiTopMarginNormalized = 0.12f;
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
    private Dictionary<int, LinkedObjectGroup> groups = new Dictionary<int, LinkedObjectGroup>();
    public Dictionary<int, LinkedObjectGroup> GroupsDictionary => groups;

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

        // İlk açılışta mevcut level başlangıcını logla
        GameManager.Instance?.ResetLevelState();

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
        if (levels == null || levels.Count == 0)
        {
            return;
        }

        // Bir sonraki seviyeye sırayla ve atlamadan geç (Level 22 -> Level 23 -> Level 24)
        currentLevelIndex = (currentLevelIndex + 1) % levels.Count;

        PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
        PlayerPrefs.Save();

        // Yeni level başlangıcını sıfırla ve spawn et
        GameManager.Instance?.ResetLevelState();

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
            if (best == null || lt.currentSlices > best.currentSlices)
                best = lt;
        }
        return best;
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

        // Aynı linked grubundaki objeler hiçbir zaman birbirini tamamlayamaz
        DragObject dobjA = a.GetComponentInParent<DragObject>();
        DragObject dobjB = b.GetComponentInParent<DragObject>();
        if (dobjA != null && dobjB != null && dobjA.linkId > 0 && dobjA.linkId == dobjB.linkId)
            return false;

        bool colorMatch = ColorMixData.ColorsMatch(a.liquidColor, b.liquidColor);
        bool sliceMatch = a.currentSlices == b.currentSlices;
        bool notFull    = a.currentSlices < a.maxSlices;
        bool capable    = colorMatch && sliceMatch && notFull;

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
