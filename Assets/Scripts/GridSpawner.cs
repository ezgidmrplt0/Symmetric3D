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
    [Tooltip("2D ve 3D sahnelerde kameranın şişelere bakış eğiklik açısı (ör: 20 derece).")]
    public float cameraPitchAngle = 20f;
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

        if (levels != null && (currentLevelIndex >= levels.Count || currentLevelIndex < 0))
        {
            currentLevelIndex = 0;
            PlayerPrefs.SetInt("CurrentLevelIndex", 0);
        }

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
        GameManager.Instance?.ResetLevelState();
        SpawnCurrentLevel();
    }

    public void SpawnCurrentLevel()
    {
        ClearCurrentLevel();

        if (levels == null || levels.Count == 0)
        {
            return;
        }

        // PlayerPrefs'i her zaman güncel level ile senkron tut
        if (PlayerPrefs.GetInt("CurrentLevelIndex", 0) != currentLevelIndex)
        {
            PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
            PlayerPrefs.Save();
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

        currentLevelIndex = Mathf.Min(currentLevelIndex + 1, levels.Count - 1);

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
    // MAGIC SORT ŞİŞE DİZİLİMİ (ÇAKIŞMASIZ KAVİSLİ & ŞAŞIRTMANI DİZİLİM)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Klasik Düz 2D Dizilim (Satır Başına Maksimum 4 Şişe):
    /// Şişe sayısına göre dengeli satırlara böler (ör. 4=2+2, 5=3+2, 6=3+3, 7=4+3, 8=4+4).
    /// </summary>
    public static Vector3 GetBottlePosition(int index, int totalBottles, float spacingX = 1.65f, float spacingY = 2.4f)
    {
        if (totalBottles <= 0) return Vector3.zero;
        if (totalBottles == 1) return Vector3.zero;

        int rowCount;
        if (totalBottles <= 3) rowCount = 1;
        else if (totalBottles <= 8) rowCount = 2;
        else if (totalBottles <= 12) rowCount = 3;
        else rowCount = Mathf.CeilToInt(totalBottles / 4.0f);

        int baseCount = totalBottles / rowCount;
        int remainder = totalBottles % rowCount;

        int[] rowCapacities = new int[rowCount];
        for (int r = 0; r < rowCount; r++)
        {
            rowCapacities[r] = baseCount + (r < remainder ? 1 : 0);
        }

        int targetRow = 0;
        int indexInRow = index;
        for (int r = 0; r < rowCount; r++)
        {
            if (indexInRow < rowCapacities[r])
            {
                targetRow = r;
                break;
            }
            indexInRow -= rowCapacities[r];
        }

        int countInRow = rowCapacities[targetRow];
        float posX = (indexInRow - (countInRow - 1) * 0.5f) * spacingX;

        float middleRowIdx = (rowCount - 1) * 0.5f;
        float posY = (middleRowIdx - targetRow) * spacingY;

        return new Vector3(posX, posY, 0f);
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

    /// <summary>
    /// Tahtada hâlâ eşleşme bekleyen parça sayısı. Boşalmış (0 dilim) ve dolmuş
    /// (max dilim) parçalar sayılmaz — onlar yok olma animasyonundadır.
    /// </summary>
    public int CountActivePieces()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        int count = 0;
        foreach (var lt in all)
        {
            if (lt == null || lt.gameObject == null || !lt.gameObject.activeInHierarchy) continue;
            if (lt.currentSlices <= 0 || lt.currentSlices >= lt.maxSlices) continue;
            count++;
        }
        return count;
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
        // Eğer level tamamlandıysa veya zaten fail olduysa tekrar tetikleme
        if (GameManager.Instance != null && GameManager.Instance.IsLevelCompleting) return;

        if (PossibleMovesExist()) return;

        GameManager.Instance?.LevelFail();
    }

    private bool PossibleMovesExist()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        List<LiquidTransfer> activePieces = new List<LiquidTransfer>();

        foreach (var lt in all)
        {
            if (lt == null || lt.gameObject == null || !lt.gameObject.activeInHierarchy)
                continue;

            // Sıvı aktarımı sürüyorsa hamle devam ediyor demektir
            if (lt.transferring)
            {
                return true;
            }

            activePieces.Add(lt);
        }

        if (activePieces.Count <= 0) return true;

        // Bütün dolu şişeler tamamlanmış mı?
        bool allComplete = true;
        int withLiquid = 0;
        foreach (var p in activePieces)
        {
            if (p.currentSlices > 0)
            {
                withLiquid++;
                if (p.currentSlices < p.maxSlices) allComplete = false;
            }
        }
        if (allComplete && withLiquid > 0) return true;

        // Herhangi bir şişe A'dan başka bir şişe B'ye aktarım yapılabilir mi?
        for (int i = 0; i < activePieces.Count; i++)
        {
            LiquidTransfer a = activePieces[i];
            if (a.currentSlices <= 0) continue;

            for (int j = 0; j < activePieces.Count; j++)
            {
                if (i == j) continue;
                LiquidTransfer b = activePieces[j];
                if (a.CanPourInto(b))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanInteractionsExist(LiquidTransfer a, LiquidTransfer b, int unfrozenCount = 2)
    {
        if (a == null || b == null) return false;
        return a.CanPourInto(b) || b.CanPourInto(a);
    }
}
