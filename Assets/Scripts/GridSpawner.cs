using UnityEngine;
using System.Collections.Generic;

public class GridSpawner : MonoBehaviour
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

    private List<GameObject> activeSpawnedObjects = new List<GameObject>();

    // Kolaylık property'si ─ sequence listesine kısayol
    private List<LevelData> levels => sequence != null ? sequence.levels : null;

    void Start()
    {
        SpawnCurrentLevel();
    }

    public void SpawnCurrentLevel()
    {
        ClearCurrentLevel();

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("GridSpawner: LevelSequenceData atanmamış veya boş!");
            return;
        }

        if (currentLevelIndex < levels.Count && levels[currentLevelIndex] != null)
        {
            SpawnLevel(levels[currentLevelIndex]);
        }
        else
        {
            Debug.LogWarning("GridSpawner: Geçersiz level index!");
        }
    }

    public void NextLevel()
    {
        GameManager.Instance?.ResetLevelState();

        if (levels == null || levels.Count == 0) return;

        int progress = GameManager.Instance != null ? GameManager.Instance.totalProgress : 0;

        // newMechanicUnlocked true ise progress sıfırlanmış olsa bile tüm tipler açık sayılır
        bool mechanicUnlocked = GameManager.Instance != null && GameManager.Instance.newMechanicUnlocked;
        int effectiveProgress = mechanicUnlocked ? 100 : progress;

        // Kilitli olmayan bir sonraki level'ı bul
        int startIndex = currentLevelIndex;
        int next = currentLevelIndex;

        for (int i = 1; i <= levels.Count; i++)
        {
            int candidate = (currentLevelIndex + i) % levels.Count;
            LevelData candidateLevel = levels[candidate];

            // Tüm listeyi dolaştıysak başa dön (hepsi kilitliyse bile çalışmaya devam et)
            if (candidate == startIndex)
            {
                next = candidate;
                break;
            }

            if (candidateLevel == null) continue;

            // Unlock kontrolü — effectiveProgress ile
            if (sequence != null && !sequence.IsLevelUnlocked(candidateLevel, effectiveProgress))
            {
                Debug.Log($"[GridSpawner] '{candidateLevel.levelDisplayName}' kilitli, atlanıyor.");
                continue;
            }

            next = candidate;
            break;
        }

        if (next == startIndex && next == currentLevelIndex)
            Debug.Log("Oyun Bitti! Tüm leveller tamamlandı.");

        currentLevelIndex = next;
        SpawnCurrentLevel();
    }

    void ClearCurrentLevel()
    {
        foreach (GameObject obj in activeSpawnedObjects)
            if (obj != null) Destroy(obj);
        activeSpawnedObjects.Clear();
    }

    void SpawnLevel(LevelData level)
    {
        int gX = level.gridX;
        int gY = level.gridY;

        float gridSize = gridPrefab.transform.localScale.x;
        float offsetX = (gX - 1) * (gridSize + spacing) / 2f;
        float offsetY = (gY - 1) * (gridSize + spacing) / 2f;

        // Grid Zeminlerini Çiz
        for (int x = 0; x < gX; x++)
        {
            for (int y = 0; y < gY; y++)
            {
                Vector3 pos = new Vector3(
                    x * (gridSize + spacing) - offsetX,
                    y * (gridSize + spacing) - offsetY,
                    0
                );
                GameObject gridObj = Instantiate(gridPrefab, transform.position + pos, Quaternion.identity, transform);
                activeSpawnedObjects.Add(gridObj);
            }
        }

        // Oyuncu Objelerini Çiz
        foreach (var piece in level.pieces)
        {
            Vector3 piecePos = new Vector3(
                piece.gridPosition.x * (gridSize + spacing) - offsetX,
                piece.gridPosition.y * (gridSize + spacing) - offsetY,
                -objectOffset
            );

            GameObject newObj = Instantiate(objectPrefab, transform.position + piecePos,
                Quaternion.Euler(0, 0, piece.rotationZ), transform);
            activeSpawnedObjects.Add(newObj);

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.liquidColor   = piece.liquidColor;
                lt.currentSlices = piece.currentSlices;
            }
        }
    }

    public Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        if (levels == null || levels.Count == 0 || currentLevelIndex >= levels.Count)
            return transform.position;

        LevelData level = levels[currentLevelIndex];
        float gridSize  = gridPrefab.transform.localScale.x;
        float offsetX   = (level.gridX - 1) * (gridSize + spacing) / 2f;
        float offsetY   = (level.gridY - 1) * (gridSize + spacing) / 2f;

        return transform.position + new Vector3(
            gridPos.x * (gridSize + spacing) - offsetX,
            gridPos.y * (gridSize + spacing) - offsetY,
            -objectOffset
        );
    }
}