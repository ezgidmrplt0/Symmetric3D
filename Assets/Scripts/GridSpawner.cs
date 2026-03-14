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

        if (level.is3DCube && level.cubeFaces != null && level.cubeFaces.Length == 6)
        {
            Spawn3DCubeLevel(level, gridSize);
            return;
        }

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

    void Spawn3DCubeLevel(LevelData level, float gridSize)
    {
        // Yamukluk (Y ekseni) olmadan, sadece 3D derinliği verecek simetrik hafif yukarı bakış açısı
        GameObject pivotRoot = new GameObject("CubePivotRoot");
        pivotRoot.transform.SetParent(transform);
        pivotRoot.transform.localPosition = Vector3.zero;
        pivotRoot.transform.localRotation = Quaternion.Euler(8f, 0f, 0f); // Daha az eğik (15 -> 8), sıvıları görmek daha kolay
        activeSpawnedObjects.Add(pivotRoot);

        GameObject cubeRoot = new GameObject("CubeRoot");
        cubeRoot.transform.SetParent(pivotRoot.transform);
        cubeRoot.transform.localPosition = Vector3.zero;
        cubeRoot.transform.localRotation = Quaternion.identity;

        cubeRoot.AddComponent<CubeRotator>(); // Animasyon rotasyonu sağlar

        // Küpün en geniş yüzeyinden yarıçapı hesapla
        float maxDim = 0;
        foreach(var f in level.cubeFaces) {
            if(!f.isActive) continue;
            if(f.gridX > maxDim) maxDim = f.gridX;
            if(f.gridY > maxDim) maxDim = f.gridY;
        }
        
        float cellTotalSize = gridSize + spacing;
        
        // Toplam görsel boyutu doğru hesapla ki köşeler kusursuz birleşsin
        float visualWidth = (maxDim - 1) * cellTotalSize + gridSize;
        float gridDepth = gridPrefab.transform.localScale.z;
        
        // Yüzeylerin birbiri içine girmemesi için radius'u et kalınlığı (gridDepth) kadar dışarı itiyoruz
        float cubeRadius = (visualWidth / 2f) + (gridDepth / 2f);

        // İçine siyah boşlukları kapatan bir Core (İskelet) ekleyelim
        GameObject coreCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        coreCube.transform.SetParent(cubeRoot.transform, false);
        
        // Siyah çekirdek, iç kısımdaki boşluğu tam olarak doldurmalı (Z-Fighting olmasın diye sadece %1 ufaltıyoruz)
        float coreSize = visualWidth - 0.05f;
        if (coreSize < 0.1f) coreSize = visualWidth * 0.9f;

        coreCube.transform.localScale = new Vector3(coreSize, coreSize, coreSize);
        Renderer coreRend = coreCube.GetComponent<Renderer>();
        if (coreRend != null) {
            coreRend.material = new Material(Shader.Find("Standard"));
            coreRend.material.color = new Color(0.1f, 0.1f, 0.11f); // Daha koyu, tok bir metalik siyah
            coreRend.material.SetFloat("_Glossiness", 0.5f);
        }

        Vector3[] faceRots = {
            new Vector3(0, 0, 0),       // Front
            new Vector3(0, 180, 0),     // Back
            new Vector3(0, -90, 0),     // Right
            new Vector3(0, 90, 0),      // Left
            new Vector3(90, 0, 0),      // Top
            new Vector3(-90, 0, 0)      // Bottom
        };
        Vector3[] faceDirs = { Vector3.back, Vector3.forward, Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        Dictionary<int, LinkedObjectGroup> groups = new Dictionary<int, LinkedObjectGroup>();

        for (int i = 0; i < 6; i++)
        {
            var faceData = level.cubeFaces[i];
            if (!faceData.isActive) continue;

            GameObject facePivot = new GameObject($"Face_{i}");
            facePivot.transform.SetParent(cubeRoot.transform);
            facePivot.transform.localPosition = faceDirs[i] * cubeRadius;
            facePivot.transform.localRotation = Quaternion.Euler(faceRots[i]);

            float fMinX = 0, fMaxX = faceData.gridX - 1;
            float fMinY = 0, fMaxY = faceData.gridY - 1;

            bool isFaceCustom = faceData.customGridPositions != null && faceData.customGridPositions.Count > 0;
            if (isFaceCustom) {
                fMinX = fMinY = float.MaxValue; fMaxX = fMaxY = float.MinValue;
                foreach (var pos in faceData.customGridPositions) {
                    if (pos.x < fMinX) fMinX = pos.x; if (pos.x > fMaxX) fMaxX = pos.x;
                    if (pos.y < fMinY) fMinY = pos.y; if (pos.y > fMaxY) fMaxY = pos.y;
                }
            }

            float offsetFX = (fMinX + fMaxX) * (gridSize + spacing) / 2f;
            float offsetFY = (fMinY + fMaxY) * (gridSize + spacing) / 2f;

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            
            if (isFaceCustom) {
                foreach (var pos in faceData.customGridPositions) {
                    Vector3 worldPos = new Vector3(pos.x * (gridSize + spacing) - offsetFX, pos.y * (gridSize + spacing) - offsetFY, 0);
                    GameObject gridObj = Instantiate(gridPrefab, facePivot.transform);
                    gridObj.transform.localPosition = worldPos;
                    activeSpawnedObjects.Add(gridObj);
                    occupied.Add(pos);
                }
            } else {
                for (int x = 0; x < faceData.gridX; x++) {
                    for (int y = 0; y < faceData.gridY; y++) {
                        Vector3 wPos = new Vector3(x * (gridSize + spacing) - offsetFX, y * (gridSize + spacing) - offsetFY, 0);
                        GameObject gridObj = Instantiate(gridPrefab, facePivot.transform);
                        gridObj.transform.localPosition = wPos;
                        activeSpawnedObjects.Add(gridObj);
                        occupied.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 3D Küp modunda kenarların havada kalmaması için çerçeveleri her yüzeye ekliyoruz.
            // is3DCube flag'ini (gizli parametre gibi) kullanarak köşelerden taşmamasını sağlayacağız.
            SpawnCleanFrameSegmentsLocal(occupied, cellTotalSize, gridSize, offsetFX, offsetFY, facePivot.transform, true);
        }

        // Parçalar
        foreach (var piece in level.pieces)
        {
            int fIdx = piece.faceIndex;
            if (fIdx < 0 || fIdx > 5 || !level.cubeFaces[fIdx].isActive) continue;
            
            Transform facePivot = cubeRoot.transform.Find($"Face_{fIdx}");
            if (facePivot == null) continue;

            float pMinX=0, pMaxX=level.cubeFaces[fIdx].gridX-1;
            float pMinY=0, pMaxY=level.cubeFaces[fIdx].gridY-1;
            bool isFaceCustom = level.cubeFaces[fIdx].customGridPositions != null && level.cubeFaces[fIdx].customGridPositions.Count > 0;
            if (isFaceCustom) {
                pMinX=pMinY=float.MaxValue; pMaxX=pMaxY=float.MinValue;
                foreach (var pos in level.cubeFaces[fIdx].customGridPositions) {
                    if (pos.x<pMinX) pMinX=pos.x; if (pos.x>pMaxX) pMaxX=pos.x;
                    if (pos.y<pMinY) pMinY=pos.y; if (pos.y>pMaxY) pMaxY=pos.y;
                }
            }
            float offsetPX = (pMinX + pMaxX) * (gridSize + spacing) / 2f;
            float offsetPY = (pMinY + pMaxY) * (gridSize + spacing) / 2f;

            // 3D modunda parçaları yüzeyin üzerine çıkarıyoruz (Gömülü kalmaması için).
            // DragObject.cs içindeki yeni -baseOffset * 1.3f mantığıyla eşleşmeli.
            float localOffset = objectOffset * 1.3f; 
            Vector3 localPos = new Vector3(
                piece.gridPosition.x * (gridSize + spacing) - offsetPX,
                piece.gridPosition.y * (gridSize + spacing) - offsetPY,
                -localOffset
            );

            GameObject newObj = Instantiate(objectPrefab, facePivot);
            newObj.transform.localPosition = localPos;
            newObj.transform.localRotation = Quaternion.Euler(0, 0, piece.rotationZ);
            activeSpawnedObjects.Add(newObj);

            if (piece.linkId > 0)
            {
                if (!groups.ContainsKey(piece.linkId))
                {
                    GameObject groupObj = new GameObject("LinkedGroup_" + piece.linkId);
                    groupObj.transform.parent = cubeRoot.transform;
                    groupObj.transform.localPosition = Vector3.zero;
                    groupObj.transform.localRotation = Quaternion.identity;
                    LinkedObjectGroup log = groupObj.AddComponent<LinkedObjectGroup>();
                    groups[piece.linkId] = log;
                    activeSpawnedObjects.Add(groupObj);
                }
                newObj.transform.SetParent(groups[piece.linkId].transform, true);
            }

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null) { lt.liquidColor = piece.liquidColor; lt.currentSlices = piece.currentSlices; lt.isShadowTrigger = piece.isShadowTrigger; }
        }

        foreach (var kvp in groups) kvp.Value.InitGroup();

        // 3D için sabit uzak bir kamera pozisyonu
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null)
        {
            float screenRatio = (float)Screen.width / (float)Screen.height;
            float targetRatio = 0.56f; // Standart telefon portrait oranı
            float aspectMultiplier = targetRatio / screenRatio;
            if (aspectMultiplier < 1f) aspectMultiplier = 1f; // Tablette daralmamasını engelle

            float targetDistance = Mathf.Max(8.5f, cubeRadius * 4.5f) * aspectMultiplier;
            
            // Küpü zeminden (arka plandan) uzaklaştıralım (Negatif Z)
            // Z ekseninde öne çekmek, arkadaki zeminle çakışmayı önler
            float zShift = -cubeRadius * 1.5f; 
            pivotRoot.transform.localPosition = new Vector3(0, 0, zShift);

            // Eğer ortografik ise boyutu ayarla
            if (cam.orthographic) {
                cam.DOOrthoSize(targetDistance * 0.85f, 0.6f).SetEase(Ease.OutCubic);
            }
            
            // Kamera hedefi küpün yeni merkezi olsun
            Vector3 cubeCenter = pivotRoot.transform.position; 
            Vector3 camTarget = cubeCenter - cam.transform.forward * targetDistance;
            camTarget.y += cameraVerticalOffset; // Mevcut vertikal offseti koru
            cam.transform.DOMove(camTarget, 0.6f).SetEase(Ease.OutCubic);
        }
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

    void SpawnCleanFrameSegmentsLocal(HashSet<Vector2Int> occupied, float step, float gridSize, float offsetX, float offsetY, Transform parentTarget, bool is3DCube = false)
    {
        float t = frameThickness;
        float edge = gridSize / 2f + framePadding;

        foreach (var pos in occupied)
        {
            Vector3 center = new Vector3(pos.x * step - offsetX, pos.y * step - offsetY, 0);

            float length = step;

            bool left = occupied.Contains(pos + Vector2Int.left);
            bool right = occupied.Contains(pos + Vector2Int.right);
            bool up = occupied.Contains(pos + Vector2Int.up);
            bool down = occupied.Contains(pos + Vector2Int.down);

            if (!up)
            {
                float len = step;
                if (!is3DCube) {
                    if (!left) len += t; if (!right) len += t; 
                }
                float xOffset = 0;
                if (!is3DCube) {
                    if (!left && right) xOffset = -t / 2f; if (!right && left) xOffset = t / 2f;
                }
                SpawnCustomLocalSegment(center + new Vector3(xOffset, edge + t / 2f, 0), new Vector3(len, t, t), parentTarget);
            }
            if (!down)
            {
                float len = step;
                if (!is3DCube) {
                    if (!left) len += t; if (!right) len += t; 
                }
                float xOffset = 0;
                if (!is3DCube) {
                    if (!left && right) xOffset = -t / 2f; if (!right && left) xOffset = t / 2f;
                }
                SpawnCustomLocalSegment(center + new Vector3(xOffset, -edge - t / 2f, 0), new Vector3(len, t, t), parentTarget);
            }
            if (!left)
            {
                float len = step;
                if (!is3DCube) {
                    if (!up) len += t; if (!down) len += t; 
                }
                float yOffset = 0;
                if (!is3DCube) {
                    if (!down && up) yOffset = -t / 2f; if (!up && down) yOffset = t / 2f;
                }
                SpawnCustomLocalSegment(center + new Vector3(-edge - t / 2f, yOffset, 0), new Vector3(t, len, t), parentTarget);
            }
            if (!right)
            {
                float len = step;
                if (!is3DCube) {
                    if (!up) len += t; if (!down) len += t; 
                }
                float yOffset = 0;
                if (!is3DCube) {
                    if (!down && up) yOffset = -t / 2f; if (!up && down) yOffset = t / 2f;
                }
                SpawnCustomLocalSegment(center + new Vector3(edge + t / 2f, yOffset, 0), new Vector3(t, len, t), parentTarget);
            }
        }
    }

    void SpawnCustomLocalSegment(Vector3 localPos, Vector3 scale, Transform parentTarget)
    {
        GameObject seg = null;
        if (frameSegmentPrefab != null) seg = Instantiate(frameSegmentPrefab, parentTarget);
        else {
            seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.transform.SetParent(parentTarget, false);
            Destroy(seg.GetComponent<BoxCollider>());
        }

        seg.transform.localPosition = localPos;
        seg.transform.localRotation = Quaternion.identity;
        seg.transform.localScale = scale;
        activeFrameSegments.Add(seg);
        activeSpawnedObjects.Add(seg);
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

        // 1. Sahnedeki tüm grid'leri topla
        List<Transform> validGrids = new List<Transform>();
        foreach (Transform t in transform.GetComponentsInChildren<Transform>())
        {
            if (t.name.Contains("Grid") || t.CompareTag("Grid")) // Prefab ismine veya tag'e göre
            {
                // Grid parent'ının BoxCollider'ını vs. almamak için
                // Genelde grid prefab'ı direkt ekliyoruz.
                if (t.gameObject != this.gameObject && !t.name.Contains("Face_"))
                    validGrids.Add(t);
            }
        }

        // 2. Dolu pozisyonları filtrele
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

        // 3. Rastgele bir yer seç
        Transform targetGrid = emptyGrids[Random.Range(0, emptyGrids.Count)];

        // 4. Spawn et (Trigger nereye bakıyorsa gölge zıttına baksın)
        float triggerRot = trigger.transform.localRotation.eulerAngles.z;
        float shadowRot = (triggerRot + 180f) % 360f;

        GameObject shadowObj = Instantiate(objectPrefab, targetGrid.parent);
        shadowObj.transform.localPosition = new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, -objectOffset);
        shadowObj.transform.localRotation = Quaternion.Euler(0, 0, shadowRot);
        
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
                // 3D Küp modunda parçalar yer değiştirebileceği için yön şimdilik tutmasa da fail demeyelim
                GridSpawner spawner = FindObjectOfType<GridSpawner>();
                bool is3D = spawner != null && spawner.levels != null && 
                            spawner.currentLevelIndex < spawner.levels.Count && 
                            spawner.levels[spawner.currentLevelIndex].is3DCube;

                if (!is3D) return false;
            }
        }

        return true;
    }
}