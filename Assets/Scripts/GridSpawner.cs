using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

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

    // Kolaylık property'leri
    private List<LevelData> levels => sequence != null ? sequence.levels : null;

    public LevelData.LevelType CurrentLevelType
    {
        get
        {
            if (levels == null || currentLevelIndex >= levels.Count || levels[currentLevelIndex] == null)
                return LevelData.LevelType.Classic;
            return levels[currentLevelIndex].levelType;
        }
    }

    void Start()
    {
        // PlayerPrefs'ten kaldığı yeri oku
        currentLevelIndex = PlayerPrefs.GetInt("CurrentLevelIndex", 0);
        
        // Eğer index listeden büyükse sıfırla (data değişmiş olabilir)
        if (levels != null && currentLevelIndex >= levels.Count)
            currentLevelIndex = 0;

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

        // Unlock kontrolü için birikimli progress kullan (hiç sıfırlanmaz)
        int effectiveProgress = GameManager.Instance != null
            ? GameManager.Instance.lifetimeProgress
            : 0;

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
                Debug.Log($"[GridSpawner] '{candidateLevel.levelDisplayName}' kilitli! (Tür: {candidateLevel.levelType}, Gerekli: {sequence.GetUnlockProgress(candidateLevel.levelType)}, Mevcut: {effectiveProgress})");
                continue;
            }

            next = candidate;
            break;
        }

        if (next == startIndex && next == currentLevelIndex)
            Debug.Log("Oyun Bitti! Tüm leveller tamamlandı.");

        currentLevelIndex = next;
        
        // Kaldığı yeri kaydet
        PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
        PlayerPrefs.Save();

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
        float gridSize = gridPrefab.transform.localScale.x;
        bool isCustom = level.customGridPositions != null && level.customGridPositions.Count > 0;

        float minX = 0, maxX = level.gridX - 1;
        float minY = 0, maxY = level.gridY - 1;

        if (isCustom)
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

        // Grid Zeminlerini Çiz
        if (isCustom)
        {
            foreach (var pos in level.customGridPositions)
            {
                Vector3 worldPos = new Vector3(
                    pos.x * (gridSize + spacing) - offsetX,
                    pos.y * (gridSize + spacing) - offsetY,
                    0
                );
                GameObject gridObj = Instantiate(gridPrefab, transform.position + worldPos, Quaternion.identity, transform);
                activeSpawnedObjects.Add(gridObj);
            }
        }
        else
        {
            for (int x = 0; x < level.gridX; x++)
            {
                for (int y = 0; y < level.gridY; y++)
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
                lt.isShadowTrigger = piece.isShadowTrigger;
            }
        }
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

        LevelData level = levels[currentLevelIndex];
        float gridSize = gridPrefab.transform.localScale.x;

        // 1. Tüm geçerli grid pozisyonlarını listele
        List<Vector2Int> allPositions = new List<Vector2Int>();
        if (level.customGridPositions != null && level.customGridPositions.Count > 0)
        {
            allPositions.AddRange(level.customGridPositions);
        }
        else
        {
            for (int x = 0; x < level.gridX; x++)
                for (int y = 0; y < level.gridY; y++)
                    allPositions.Add(new Vector2Int(x, y));
        }

        // 2. Dolu pozisyonları filtrele
        DragObject[] existing = FindObjectsOfType<DragObject>();
        List<Vector2Int> occupied = new List<Vector2Int>();
        // Bu biraz kaba bir hesaplama ama grid sistemine göre yuvarlayabiliriz
        // Alternatif: Her DragObject'in bir gridPos tutması daha sağlıklı olurdu.
        // Ama şimdilik dünya pozisyonundan geri dönüş yapalım.
        
        // offsetX/offsetY hesaplamayı tekrar yapalım (GetWorldPosition içindeki gibi)
        float minX = 0, maxX = level.gridX - 1;
        float minY = 0, maxY = level.gridY - 1;
        bool isCustom = level.customGridPositions != null && level.customGridPositions.Count > 0;
        if (isCustom)
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

        foreach (var obj in existing)
        {
            Vector3 localPos = obj.transform.position - transform.position;
            int x = Mathf.RoundToInt((localPos.x + offsetX) / (gridSize + spacing));
            int y = Mathf.RoundToInt((localPos.y + offsetY) / (gridSize + spacing));
            occupied.Add(new Vector2Int(x, y));
        }

        List<Vector2Int> emptyPositions = new List<Vector2Int>();
        foreach (var p in allPositions)
            if (!occupied.Contains(p)) emptyPositions.Add(p);

        if (emptyPositions.Count == 0)
        {
            Debug.LogWarning("Shadow için boş yer bulunamadı!");
            return;
        }

        // 3. Rastgele bir yer seç
        Vector2Int spawnGridPos = emptyPositions[Random.Range(0, emptyPositions.Count)];
        Vector3 spawnWorldPos = GetWorldPosition(spawnGridPos);

        // 4. Spawn et
        // Shadow trigger yukarı bakıyorsa (0), shadow aşağı (180) baksın? 
        // Ya da trigger nereye bakıyorsa onun tam tersine baksın ki eşleşebilsinler.
        float triggerRot = trigger.transform.rotation.eulerAngles.z;
        float shadowRot = (triggerRot + 180f) % 360f;

        GameObject shadowObj = Instantiate(objectPrefab, spawnWorldPos, Quaternion.Euler(0, 0, shadowRot), transform);
        activeSpawnedObjects.Add(shadowObj);

        // Görsel efekt: Ölçeklenerek gelsin
        Vector3 targetScale = objectPrefab.transform.localScale;
        shadowObj.transform.localScale = Vector3.zero;
        shadowObj.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack);

        LiquidTransfer lt = shadowObj.GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            lt.liquidColor = trigger.liquidColor;
            lt.currentSlices = trigger.currentSlices;
            lt.isShadowChild = true; // Gölge olarak işaretle
            lt.UpdateVisuals();      // Görselleri güncelle (renk ve doluluk)
        }
    }

    public Vector3 GetWorldPosition(Vector2Int gridPos)
    {
        if (levels == null || levels.Count == 0 || currentLevelIndex >= levels.Count)
            return transform.position;

        LevelData level = levels[currentLevelIndex];
        float gridSize  = gridPrefab.transform.localScale.x;
        
        bool isCustom = level.customGridPositions != null && level.customGridPositions.Count > 0;
        float minX = 0, maxX = level.gridX - 1;
        float minY = 0, maxY = level.gridY - 1;

        if (isCustom)
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
}