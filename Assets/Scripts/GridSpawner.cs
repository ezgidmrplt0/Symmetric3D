using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

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

    [Header("Kamera ve Çerçeve (Modüler)")]
    public GameObject frameSegmentPrefab;
    public Camera mainCamera;
    public float frameThickness = 0.15f;
    public float framePadding = 0.15f;    // Grid ile çerçeve arasındaki boşluk
    public float cameraPadding = 1.2f;    // Ekran kenarlarından daha fazla pay
    public float cameraVerticalOffset = 0.5f; // Grid'i dikeyde kaydırmak için

    [Header("UI Referansları")]
    public TextMeshProUGUI levelText;

    private List<GameObject> activeSpawnedObjects = new List<GameObject>();
    private List<GameObject> activeFrameSegments = new List<GameObject>();

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

        // Eğer Inspector'da atanmadıysa otomatik bulmaya çalış
        if (levelText == null)
        {
            GameObject levelObj = GameObject.Find("LEVEL");
            if (levelObj != null) levelText = levelObj.GetComponent<TextMeshProUGUI>();
            else
            {
                // Alternatif olarak Canvas altında ara
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
        // GameManager durumunu temizle (özellikle levelCompleting flag'ı)
        GameManager.Instance?.ResetLevelState();

        // Level Text Güncelle (İnsan bazlı: 0. index -> Level 1)
        if (levelText != null)
        {
            levelText.text = "LEVEL " + (currentLevelIndex + 1);
        }

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
        Dictionary<int, LinkedObjectGroup> groups = new Dictionary<int, LinkedObjectGroup>();

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

            // Bağlı grup kontrolü
            if (piece.linkId > 0)
            {
                if (!groups.ContainsKey(piece.linkId))
                {
                    GameObject groupObj = new GameObject("LinkedGroup_" + piece.linkId);
                    groupObj.transform.parent = transform;
                    groupObj.transform.position = transform.position;
                    LinkedObjectGroup log = groupObj.AddComponent<LinkedObjectGroup>();
                    groups[piece.linkId] = log;
                    activeSpawnedObjects.Add(groupObj); // Temizlerken silinsin
                }
                newObj.transform.parent = groups[piece.linkId].transform;
            }

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.liquidColor   = piece.liquidColor;
                lt.currentSlices = piece.currentSlices;
                lt.isShadowTrigger = piece.isShadowTrigger;
            }
        }

        // Grubu başlat (Drag logic'lerini kapat, vs.)
        foreach (var kvp in groups)
        {
            kvp.Value.InitGroup();
        }

        // Çerçeve ve Kamera Ayarla (Artık otomatik coroutine ile)
        StartCoroutine(AdjustViewportCoroutine(level, minX, maxX, minY, maxY, gridSize));
    }

    IEnumerator AdjustViewportCoroutine(LevelData level, float minX, float maxX, float minY, float maxY, float gridSize)
    {
        // 1. Ekran oranının (aspect ratio) güncellenmesi için bir kare bekle
        yield return new WaitForEndOfFrame();

        // 2. Grid Pozisyonlarını Topla
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        if (level.customGridPositions != null && level.customGridPositions.Count > 0)
        {
            foreach (var p in level.customGridPositions) occupied.Add(p);
        }
        else
        {
            for (int x = 0; x < level.gridX; x++)
                for (int y = 0; y < level.gridY; y++)
                    occupied.Add(new Vector2Int(x, y));
        }

        // 3. Eski Çerçeveyi Temizle
        foreach (var seg in activeFrameSegments) if (seg != null) Destroy(seg);
        activeFrameSegments.Clear();

        float step = gridSize + spacing;
        float offsetX = (minX + maxX) * step / 2f;
        float offsetY = (minY + maxY) * step / 2f;

        // Bounds initialization: Sadece grid merkezini baz alma, tüm alanları kapsa
        bool boundsInit = false;
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);

        // 4. Kenarları Tespit Et ve Çiz
        foreach (var pos in occupied)
        {
            Vector3 tileWorldPos = transform.position + new Vector3(
                pos.x * step - offsetX,
                pos.y * step - offsetY,
                0
            );

            if (!boundsInit)
            {
                combinedBounds = new Bounds(tileWorldPos, Vector3.one * gridSize);
                boundsInit = true;
            }
            else
            {
                combinedBounds.Encapsulate(new Bounds(tileWorldPos, Vector3.one * gridSize));
            }
        }

        SpawnCleanFrameSegments(occupied, step, gridSize, offsetX, offsetY);

        // 5. Kamerayı Ayarla (Otomatik ve Merkezi)
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null)
        {
            // Bounding box'a frame kalınlığını ve padding'i de ekle
            float frameFullEdge = framePadding + frameThickness;
            combinedBounds.Expand(frameFullEdge * 2f);

            float h = combinedBounds.size.y + cameraPadding * 2f;
            float w = combinedBounds.size.x + cameraPadding * 2f;

            if (cam.orthographic)
            {
                float sizeByHeight = h / 2f;
                float sizeByWidth = (w / 2f) / cam.aspect;
                float targetSize = Mathf.Max(sizeByHeight, sizeByWidth);
                
                cam.DOOrthoSize(targetSize, 0.6f).SetEase(Ease.OutCubic);
                
                Vector3 camTarget = combinedBounds.center;
                camTarget.y += cameraVerticalOffset; 
                camTarget.z = cam.transform.position.z;
                cam.transform.DOMove(camTarget, 0.6f).SetEase(Ease.OutCubic);
            }
            else
            {
                // Perspective Zoom: Mesafeyi hesapla
                float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distByHeight = (h / 2f) / Mathf.Tan(halfFovRad);
                float distByWidth = (w / 2f) / (Mathf.Tan(halfFovRad) * cam.aspect);
                
                float targetDistance = Mathf.Max(distByHeight, distByWidth);
                
                // Kameranın bakış doğrultusunu (forward) bozmadan mesafeyi ayarla
                Vector3 baseTarget = combinedBounds.center;
                baseTarget.y += cameraVerticalOffset;
                
                Vector3 camTarget = baseTarget - cam.transform.forward * targetDistance;
                cam.transform.DOMove(camTarget, 0.6f).SetEase(Ease.OutCubic);
            }
        }
    }

    void SpawnCleanFrameSegments(HashSet<Vector2Int> occupied, float step, float gridSize, float offsetX, float offsetY)
{
    float t = frameThickness;
    float edge = gridSize / 2f + framePadding;

    foreach (var pos in occupied)
    {
        Vector3 center = transform.position + new Vector3(
            pos.x * step - offsetX,
            pos.y * step - offsetY,
            0
        );

        float length = step; // gridSize yerine hücre boyutu olan step'i kullanmak grid iç çakışmalarını çözer.
        
        // Komşulukları (çevredeki hücreleri) kontrol et
        bool left = occupied.Contains(pos + Vector2Int.left);
        bool right = occupied.Contains(pos + Vector2Int.right);
        bool up = occupied.Contains(pos + Vector2Int.up);
        bool down = occupied.Contains(pos + Vector2Int.down);

        // TOP
        if (!up)
        {
            float len = step;
            if (!left) len += t; // Sol boşsa kaplamak için sola uzat
            if (!right) len += t; // Sağ boşsa kaplamak için sağa uzat

            float xOffset = 0;
            if (!left && right) xOffset = -t / 2f; 
            if (!right && left) xOffset = t / 2f;  

            Vector3 worldPos = center + new Vector3(xOffset, edge + t / 2f, 0);
            SpawnCustomSegment(worldPos, new Vector3(len, t, t));
        }

        // BOTTOM
        if (!down)
        {
            float len = step;
            if (!left) len += t; 
            if (!right) len += t; 

            float xOffset = 0;
            if (!left && right) xOffset = -t / 2f;
            if (!right && left) xOffset = t / 2f;

            Vector3 worldPos = center + new Vector3(xOffset, -edge - t / 2f, 0);
            SpawnCustomSegment(worldPos, new Vector3(len, t, t));
        }

        // LEFT
        if (!left)
        {
            float len = step;
            if (!up) len += t; 
            if (!down) len += t; 

            float yOffset = 0;
            if (!down && up) yOffset = -t / 2f;
            if (!up && down) yOffset = t / 2f;

            Vector3 worldPos = center + new Vector3(-edge - t / 2f, yOffset, 0);
            SpawnCustomSegment(worldPos, new Vector3(t, len, t));
        }

        // RIGHT
        if (!right)
        {
            float len = step;
            if (!up) len += t; 
            if (!down) len += t; 

            float yOffset = 0;
            if (!down && up) yOffset = -t / 2f;
            if (!up && down) yOffset = t / 2f;

            Vector3 worldPos = center + new Vector3(edge + t / 2f, yOffset, 0);
            SpawnCustomSegment(worldPos, new Vector3(t, len, t));
        }
    }
}
    void SpawnCustomSegment(Vector3 worldPos, Vector3 scale)
    {
        GameObject seg = null;
        if (frameSegmentPrefab != null)
            seg = Instantiate(frameSegmentPrefab, worldPos, Quaternion.identity, transform);
        else
        {
            seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.transform.position = worldPos;
            seg.transform.parent = transform;
            Destroy(seg.GetComponent<BoxCollider>());
        }

        seg.transform.localScale = scale;
        activeFrameSegments.Add(seg);
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

    public void CheckForFail()
    {
        if (PossibleMovesExist()) return;
        
        Debug.Log("[GridSpawner] Oynanabilir hamle kalmadı, FAIL tetikleniyor.");
        GameManager.Instance?.LevelFail();
    }

    private bool PossibleMovesExist()
    {
        LiquidTransfer[] all = FindObjectsOfType<LiquidTransfer>();
        List<LiquidTransfer> activePieces = new List<LiquidTransfer>();

        Debug.Log($"<color=cyan>[GridSpawner]</color> Fail Kontrolü başladı. Sahnede toplam {all.Length} LiquidTransfer bulundu.");

        // Eğer silinen nesneler varsa (transferring), bekle
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

        // Parça detaylarını dök
        string piecesLog = "[GridSpawner] Aktif Parçalar Listesi:\n";
        foreach(var ap in activePieces) piecesLog += $"- {ap.name} | Renk: {ap.liquidColor} | Dilim: {ap.currentSlices}/{ap.maxSlices} | Shadow: {ap.isShadowTrigger}\n";
        Debug.Log(piecesLog);

        // Shadow Trigger kontrolü (Tek başına ise ama gölge bekliyorsa fail değildir)
        if (activePieces.Count == 1)
        {
            var lt = activePieces[0];
            if (lt.isShadowTrigger && !lt.shadowSpawned)
            {
                 Debug.Log("<color=white>[GridSpawner]</color> Tek parça kaldı ama ShadowTrigger! Gölge doğurması bekleniyor.");
                 return true; 
            }
            Debug.Log($"<color=red>[GridSpawner]</color> Tek parça kaldı ve eşleşme imkansız! (ShadowTrigger değil veya gölge doğurmuş)");
            return false;
        }

        // Tüm çiftleri kontrol et
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

        // Hiç eşleşme bulunamadıysa ama Shadow bekleyen bir trigger varsa yine fail değildir
        foreach(var lt in activePieces) 
        {
            if (lt.isShadowTrigger && !lt.shadowSpawned)
            {
                Debug.Log($"<color=white>[GridSpawner]</color> Eşleşme yok ama '{lt.name}' bir ShadowTrigger. Bekleniyor.");
                return true;
            }
        }

        Debug.Log($"<color=red>[GridSpawner]</color> {activePieces.Count} parça arasında hiçbir geçerli etkileşim bulunamadı. FAIL KOŞULU!");
        return false;
    }

    private bool CanInteractionsExist(LiquidTransfer a, LiquidTransfer b)
    {
        if (a == null || b == null) return false;
        
        // Shadow durumunda her zaman hamle var sayalım (gölge henüz gelmemişse bile)
        if (a.isShadowChild || b.isShadowChild) return true; 

        bool capable = false;
        if (CurrentLevelType == LevelData.LevelType.ColorMix)
        {
            // Renkler farklı ve karışabiliyorsa hamle var demektir
            if (!ColorMixData.ColorsMatch(a.liquidColor, b.liquidColor))
            {
                if (ColorMixData.TryGetMix(a.liquidColor, b.liquidColor, out _)) capable = true;
            }
        }
        else
        {
            // Classic: Aynı renk + Aynı dilim sayısı + Dolu değilse
            bool colorMatch = ColorMixData.ColorsMatch(a.liquidColor, b.liquidColor);
            bool sliceMatch = a.currentSlices == b.currentSlices;
            bool notFull = a.currentSlices < a.maxSlices;

            if (colorMatch && sliceMatch && notFull) capable = true;
        }

        if (!capable) return false;

        // ROTASYON KONTROLÜ
        // Eğer Rotation modu değilse, parçaların birbirine bakabilecek (zıt) olması gerekir.
        if (CurrentLevelType != LevelData.LevelType.Rotation)
        {
            Vector3 myFace    = a.transform.up;
            Vector3 otherFace = b.transform.up;
            
            // Eğer dot product -0.9'dan küçükse (birbirlerinin tam zıttı yöne bakıyorlar demektir)
            // Not: IsAdjacentFaceToFace mantığında transform.up'lar birbirine bakmalı.
            // Bu da up vektörlerinin birbirine zıt olması demektir (Biri Up diğeri Down gibi).
            if (Vector3.Dot(myFace, -otherFace) < 0.9f)
            {
                // Debug.Log($"[GridSpawner] {a.name} ve {b.name} uyumlu ama yönleri zıt değil! FAIL adayı.");
                return false;
            }
        }

        return true;
    }
}