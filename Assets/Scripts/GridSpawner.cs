using UnityEngine;
using System.Collections.Generic;

public class GridSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject gridPrefab;
    public GameObject objectPrefab;

    [Header("Level Data Kaynağı")]
    public List<LevelData> levels = new List<LevelData>();
    public int currentLevelIndex = 0;

    [Header("Görsel Ayarlar")]
    public float spacing = 0.4f;
    public float objectOffset = 0.3f;

    private List<GameObject> activeSpawnedObjects = new List<GameObject>(); // Sahnede o an var olan objeleri temizlemek için listeliyoruz

    void Start()
    {
        SpawnCurrentLevel();
    }

    public void SpawnCurrentLevel()
    {
        ClearCurrentLevel();

        if (levels.Count > 0 && currentLevelIndex < levels.Count)
        {
            if (levels[currentLevelIndex] != null)
            {
                SpawnLevel(levels[currentLevelIndex]);
            }
        }
        else
        {
            Debug.LogWarning("GridSpawner üzerinde yüklü Level kalmadı veya indeks hatalı!");
        }
    }

    public void NextLevel()
    {
        if (currentLevelIndex < levels.Count - 1)
        {
            currentLevelIndex++;
            SpawnCurrentLevel();
        }
        else
        {
            Debug.Log("Oyun Bitti! Tüm leveller tamamlandı.");
            // Burada başa dönebilir veya kazandın ekranı çıkartabilirsiniz
            currentLevelIndex = 0; 
            SpawnCurrentLevel();
        }
    }

    void ClearCurrentLevel()
    {
        foreach (GameObject obj in activeSpawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
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

                Vector3 worldPos = transform.position + pos;
                GameObject gridObj = Instantiate(gridPrefab, worldPos, Quaternion.identity, transform);
                activeSpawnedObjects.Add(gridObj);
            }
        }

        // Oyuncu Objelerini (Küreleri / Pastaları) Çiz
        foreach (var piece in level.pieces)
        {
            Vector3 piecePos = new Vector3(
                piece.gridPosition.x * (gridSize + spacing) - offsetX,
                piece.gridPosition.y * (gridSize + spacing) - offsetY,
                -objectOffset
            );

            Vector3 worldPiecePos = transform.position + piecePos;

            // Objeyi belirtilen açıyla rotasyonlu olarak oluştur
            GameObject newObj = Instantiate(objectPrefab, worldPiecePos, Quaternion.Euler(0, 0, piece.rotationZ), transform);
            activeSpawnedObjects.Add(newObj);
            
            // Renk ve dilim atamasını yap
            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.liquidColor = piece.liquidColor;
                lt.currentSlices = piece.currentSlices;
            }
        }
    }

    public Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        if (levels.Count == 0 || currentLevelIndex >= levels.Count) return transform.position;
        LevelData level = levels[currentLevelIndex];
        
        float gridSize = gridPrefab.transform.localScale.x;

        float offsetX = (level.gridX - 1) * (gridSize + spacing) / 2f;
        float offsetY = (level.gridY - 1) * (gridSize + spacing) / 2f;

        Vector3 piecePos = new Vector3(
            gridPos.x * (gridSize + spacing) - offsetX,
            gridPos.y * (gridSize + spacing) - offsetY,
            -objectOffset
        );

        return transform.position + piecePos;
    }
}