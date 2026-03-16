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
    [Tooltip("3D şekillerin köşelerine yerleştirilecek prefab. Boş bırakılırsa frameSegmentPrefab kullanılır.")]
    public GameObject shapeCornerPrefab;
    public Camera mainCamera;
    public float frameThickness = 0.15f;
    public float framePadding = 0.15f;    // Grid ile çerçeve arasındaki boşluk
    public float cameraPadding = 1.2f;    // Ekran kenarlarından daha fazla pay
    public float cameraVerticalOffset = 0.5f; // Grid'i dikeyde kaydırmak için

    [Header("UI Referansları")]
    public TextMeshProUGUI levelText;

    private List<GameObject> activeSpawnedObjects = new List<GameObject>();
    private List<GameObject> activeFrameSegments = new List<GameObject>();
    private Dictionary<int, Transform> spawnedFaceRoots = new Dictionary<int, Transform>();

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

        if (level.boardMode == LevelData.BoardMode.Shape3D)
        {
            SpawnShapeLevel(level, gridSize);
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
        foreach(var f in level.shapeFaces) { // LevelData structure changed, but keeping this for compatibility or updating to shapeFaces
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
            if (i >= level.shapeFaces.Count) break;
            var faceData = level.shapeFaces[i];
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
            if (fIdx < 0 || fIdx >= level.shapeFaces.Count || !level.shapeFaces[fIdx].isActive) continue;
            
            Transform facePivot = cubeRoot.transform.Find($"Face_{fIdx}");
            if (facePivot == null) continue;

            float pMinX=0, pMaxX=level.shapeFaces[fIdx].gridX-1;
            float pMinY=0, pMaxY=level.shapeFaces[fIdx].gridY-1;
            bool isFaceCustom = level.shapeFaces[fIdx].customGridPositions != null && level.shapeFaces[fIdx].customGridPositions.Count > 0;
            if (isFaceCustom) {
                pMinX=pMinY=float.MaxValue; pMaxX=pMaxY=float.MinValue;
                foreach (var pos in level.shapeFaces[fIdx].customGridPositions) {
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

    void SpawnShapeLevel(LevelData level, float gridSize)
    {
        if (level.shapePrefab == null)
        {
            Debug.LogWarning("Shape3D level ama shapePrefab atanmamış.");
            return;
        }

        // 1. Prefab'ı spawn et
        GameObject shapeRoot = Instantiate(level.shapePrefab, transform);
        shapeRoot.name = "SpawnedShapeRoot";
        shapeRoot.transform.localPosition = Vector3.zero;
        shapeRoot.transform.localRotation = Quaternion.identity;
        activeSpawnedObjects.Add(shapeRoot);

        ShapeDefinition def = shapeRoot.GetComponent<ShapeDefinition>();
        if (def == null)
        {
            Debug.LogWarning("Spawn edilen shape prefabında ShapeDefinition yok.");
            return;
        }
        def.RefreshFaces();
        spawnedFaceRoots.Clear();

        // 2. Mesh'in gerçek geometrik merkezini (vertex centroid) bul.
        //    AABB bounds.center ≠ centroid: üçgen prizma gibi asimetrik şekillerde pivot kayar.
        //    Vertex ortalaması → dönme sırasında görsel merkez sabit kalır.
        Vector3 meshCenter = shapeRoot.transform.position; // fallback
        MeshFilter mf = shapeRoot.GetComponentInChildren<MeshFilter>();
        if (mf != null)
        {
            Vector3[] verts = mf.sharedMesh.vertices;
            if (verts.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (var v in verts) sum += v;
                meshCenter = mf.transform.TransformPoint(sum / verts.Length);
            }
            else
            {
                meshCenter = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
            }
        }

        // 3. Pivot'u mesh merkezine koy; shapeRoot'u altına parent'la
        //    Böylece CubeRotator pivot etrafında dönerken obje kendi merkezinde döner.
        GameObject shapePivot = new GameObject("ShapePivotRoot");
        shapePivot.transform.SetParent(transform);
        shapePivot.transform.position = meshCenter;
        shapePivot.transform.localRotation = Quaternion.identity;
        activeSpawnedObjects.Add(shapePivot);

        shapeRoot.transform.SetParent(shapePivot.transform, true); // worldPositionStays=true

        // 4. CubeRotator pivot'a ekle (shapeRoot'a değil) — döner pivot, shapeRoot onunla döner
        CubeRotator rotator = shapePivot.AddComponent<CubeRotator>();

        // Prizma mı? — dönme eksenlerini buna göre ayarla
        bool hasTri = false;
        for (int fi = 0; fi < def.FaceCount; fi++)
            if (def.GetFace(fi)?.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle) { hasTri = true; break; }
        rotator.isPrism = hasTri;

        // 5. Grid ve parçaları spawn et
        float step = gridSize + spacing;

        for (int i = 0; i < def.FaceCount; i++)
        {
            ShapeFaceMarker marker = def.GetFace(i);
            if (marker == null) continue;
            if (i >= level.shapeFaces.Count) continue;

            var faceData = level.shapeFaces[i];
            if (!faceData.isActive) continue;

            spawnedFaceRoots[i] = marker.transform;
            SpawnFaceGrid(level, marker, faceData, gridSize, step);
        }

        SpawnShapePieces(level, def, gridSize, step);
        SpawnShapeCorners(shapeRoot, hasTri);
        AdjustShapeCamera(shapePivot.transform, def, gridSize);
    }

    void SpawnFaceGrid(LevelData level, ShapeFaceMarker marker, LevelData.FaceLayoutData faceData, float gridSize, float step)
    {
        int gx = faceData.gridX;
        // FIX 3: Üçgen yüzlerde gridY her zaman gridX'e eşit olmalı (cellsInThisRow = gx - y formülü bunu gerektirir)
        int gy = (marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle) ? gx : faceData.gridY;

        // Marker'ın yerel uzayı -0.5 ile 0.5 arasındadır (Quad mesh yapısı).
        // Üçgen yüzlerde areaScale ile grid'i biraz küçültüp merkeze çekiyoruz —
        // böylece hücreler diagonal kenara dayanmak yerine içeride kalır.
        bool isTriangle = marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
        float areaScale = isTriangle ? 0.82f : 1.0f;
        float stepX = areaScale / gx;
        float stepY = areaScale / gy;
        float startX = -0.5f;
        float startY = -0.5f;

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        for (int x = 0; x < gx; x++)
        {
            for (int y = 0; y < gy; y++)
            {
                // --- PRO PIRAMIT MERKEZLEME (Row-Based Centering) ---
                float xOffset = 0;
                if (isTriangle)
                {
                    // FIX 3: Clamp ile negatife düşmeyi engelle
                    int cellsInThisRow = Mathf.Max(0, gx - y);

                    if (cellsInThisRow == 0 || x >= cellsInThisRow) continue;

                    // Satırı tam merkeze almak için gereken kaydırma:
                    // (ToplamGenişlik - SatırGenişliği) / 2
                    float rowWidth = cellsInThisRow * stepX;
                    xOffset = (1.0f - rowWidth) * 0.5f;
                }

                // Yerel pozisyon
                Vector3 localPos = new Vector3(
                    startX + (x + 0.5f) * stepX + xOffset,
                    startY + (y + 0.5f) * stepY,
                    -marker.surfaceOffset
                );

                GameObject gridObj = Instantiate(gridPrefab, marker.transform);
                gridObj.transform.localPosition = localPos;
                gridObj.transform.localRotation = Quaternion.identity;

                // Üçgen yüzlerde diagonal kenara taşmayı önlemek için scale küçültüldü.
                // Matematiksel sınır: hücre yarı-genişliği < stepX/4 (= 0.083 for gx=3).
                // 0.7 * stepX/2 = 0.117 → taşar; 0.45 * stepX/2 = 0.075 → güvenli.
                float gridVisualScale = (marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle) ? 0.8f : 0.7f;
                gridObj.transform.localScale = new Vector3(stepX * gridVisualScale, stepY * gridVisualScale, 0.05f);

                activeSpawnedObjects.Add(gridObj);
                occupied.Add(new Vector2Int(x, y));
            }
        }
    }

    void SpawnShapePieces(LevelData level, ShapeDefinition def, float gridSize, float step)
    {
        Dictionary<int, LinkedObjectGroup> groups = new Dictionary<int, LinkedObjectGroup>();

        foreach (var piece in level.pieces)
        {
            if (piece.faceIndex < 0 || piece.faceIndex >= def.FaceCount) continue;

            ShapeFaceMarker marker = def.GetFace(piece.faceIndex);
            if (marker == null) continue;
            if (piece.faceIndex >= level.shapeFaces.Count) continue;

            var faceData = level.shapeFaces[piece.faceIndex];
            if (!faceData.isActive) continue;

            // Marker'ın yerel uzayı
            // FIX 3: Üçgen yüzlerde gridY = gridX olarak zorla
            bool isTriFace = marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
            int effectiveGridY = isTriFace ? faceData.gridX : faceData.gridY;
            // SpawnFaceGrid ile aynı areaScale kullan; piece pozisyonları grid hücreleriyle hizalı kalır
            float triAreaScale = isTriFace ? 0.82f : 1.0f;
            float stepX = triAreaScale / faceData.gridX;
            float stepY = triAreaScale / effectiveGridY;
            float startX = -0.5f;
            float startY = -0.5f;

            // --- PRO PIRAMIT MERKEZLEME (Pieces) ---
            float xOffset = 0;
            if (isTriFace)
            {
                // FIX 3: Clamp ile negatife düşmeyi engelle
                int cellsInThisRow = Mathf.Max(0, faceData.gridX - piece.gridPosition.y);
                float rowWidth = cellsInThisRow * stepX;
                xOffset = (1.0f - rowWidth) * 0.5f;
            }

            // Z burada placeholder; gerçek offset scale hesabından sonra aşağıda set edilir
            Vector3 localPos = new Vector3(
                startX + (piece.gridPosition.x + 0.5f) * stepX + xOffset,
                startY + (piece.gridPosition.y + 0.5f) * stepY,
                0f
            );

            GameObject newObj = Instantiate(objectPrefab, marker.transform);
            newObj.transform.localPosition = localPos;
            newObj.transform.localRotation = Quaternion.Euler(0, 0, piece.rotationZ);

            // Piece scale: hücrenin %72'sini doldur (2D grid mantığıyla tutarlı).
            // Eğik yüzlerde marker asimetrik scale'e sahip (b.size.z × sideWidth);
            // her eksen için local scale ayrı hesaplanır, piece dünyada kare görünür.
            {
                Vector3 ws = marker.transform.lossyScale;
                // triAreaScale kullanarak gerçek hücre boyutunu baz al
                float cellWorldW = (triAreaScale / faceData.gridX) * Mathf.Abs(ws.x);
                float cellWorldH = (triAreaScale / effectiveGridY) * Mathf.Abs(ws.y);
                float worldSize  = Mathf.Min(cellWorldW, cellWorldH) * 0.72f;
                newObj.transform.localScale = new Vector3(
                    worldSize / Mathf.Abs(ws.x),
                    worldSize / Mathf.Abs(ws.y),
                    worldSize
                );

                // Z offset: parça boyutuna orantılı — parçalar yüzeye yakın durur
                float zOff = worldSize * 0.5f + marker.surfaceOffset;
                newObj.transform.localPosition = new Vector3(localPos.x, localPos.y, -zOff);
            }

            activeSpawnedObjects.Add(newObj);

            if (piece.linkId > 0)
            {
                if (!groups.ContainsKey(piece.linkId))
                {
                    GameObject groupObj = new GameObject("LinkedGroup_" + piece.linkId);
                    groupObj.transform.SetParent(transform);
                    groupObj.transform.position = transform.position;
                    LinkedObjectGroup log = groupObj.AddComponent<LinkedObjectGroup>();
                    groups[piece.linkId] = log;
                    activeSpawnedObjects.Add(groupObj);
                }

                newObj.transform.SetParent(groups[piece.linkId].transform, true);
            }

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.liquidColor = piece.liquidColor;
                lt.currentSlices = piece.currentSlices;
                lt.isShadowTrigger = piece.isShadowTrigger;
            }
        }

        foreach (var kvp in groups)
            kvp.Value.InitGroup();
    }

    void AdjustShapeCamera(Transform shapeRoot, ShapeDefinition def, float gridSize)
    {
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return;

        Renderer[] rends = shapeRoot.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        b.Expand(cameraPadding);

        if (cam.orthographic)
        {
            float h = b.size.y;
            float w = b.size.x;
            float sizeByHeight = h * 0.5f;
            float sizeByWidth = (w * 0.5f) / cam.aspect;
            float targetSize = Mathf.Max(sizeByHeight, sizeByWidth);

            cam.DOOrthoSize(targetSize, 0.6f).SetEase(Ease.OutCubic);

            Vector3 target = b.center;
            target.y += cameraVerticalOffset;
            target.z = cam.transform.position.z;

            cam.transform.DOMove(target, 0.6f).SetEase(Ease.OutCubic);
        }
        else
        {
            Vector3 target = b.center;
            target.y += cameraVerticalOffset;

            float maxSize = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float distance = Mathf.Max(4f, maxSize * 1.6f);

            Vector3 camPos = target - cam.transform.forward * distance;
            cam.transform.DOMove(camPos, 0.6f).SetEase(Ease.OutCubic);
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

    void SpawnShapeCorners(GameObject shapeRoot, bool isPrism)
    {
        MeshFilter mf = shapeRoot.GetComponentInChildren<MeshFilter>();
        if (mf == null) return;

        // Mesh bounds'u shapeRoot-local uzayına çevir
        Bounds meshB = mf.sharedMesh.bounds;
        Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
        Vector3[] bc = {
            new Vector3(meshB.min.x, meshB.min.y, meshB.min.z), new Vector3(meshB.max.x, meshB.min.y, meshB.min.z),
            new Vector3(meshB.min.x, meshB.max.y, meshB.min.z), new Vector3(meshB.max.x, meshB.max.y, meshB.min.z),
            new Vector3(meshB.min.x, meshB.min.y, meshB.max.z), new Vector3(meshB.max.x, meshB.min.y, meshB.max.z),
            new Vector3(meshB.min.x, meshB.max.y, meshB.max.z), new Vector3(meshB.max.x, meshB.max.y, meshB.max.z),
        };
        foreach (var c in bc)
        {
            Vector3 rl = shapeRoot.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
            mn = Vector3.Min(mn, rl); mx = Vector3.Max(mx, rl);
        }

        float cx = (mn.x + mx.x) * 0.5f;
        float t = frameThickness * 0.4f;

        // Kenar çiftleri: (başlangıç, bitiş) — local uzayda
        List<(Vector3, Vector3)> edges = new List<(Vector3, Vector3)>();

        if (isPrism)
        {
            // Üçgen prizma: 6 köşe, 9 kenar
            Vector3 fBL = new Vector3(mn.x, mn.y, mn.z); // ön sol-alt
            Vector3 fBR = new Vector3(mx.x, mn.y, mn.z); // ön sağ-alt
            Vector3 fT  = new Vector3(cx,   mx.y, mn.z); // ön üst
            Vector3 bBL = new Vector3(mn.x, mn.y, mx.z); // arka sol-alt
            Vector3 bBR = new Vector3(mx.x, mn.y, mx.z); // arka sağ-alt
            Vector3 bT  = new Vector3(cx,   mx.y, mx.z); // arka üst

            // Ön üçgen
            edges.Add((fBL, fBR)); edges.Add((fBL, fT)); edges.Add((fBR, fT));
            // Arka üçgen
            edges.Add((bBL, bBR)); edges.Add((bBL, bT)); edges.Add((bBR, bT));
            // Bağlantı kenarları
            edges.Add((fBL, bBL)); edges.Add((fBR, bBR)); edges.Add((fT, bT));
        }
        else
        {
            // Küp: 8 köşe, 12 kenar
            Vector3[] v = {
                new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z),
                new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z),
                new Vector3(mn.x, mn.y, mx.z), new Vector3(mx.x, mn.y, mx.z),
                new Vector3(mn.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mx.z),
            };
            // Ön yüz
            edges.Add((v[0], v[1])); edges.Add((v[2], v[3]));
            edges.Add((v[0], v[2])); edges.Add((v[1], v[3]));
            // Arka yüz
            edges.Add((v[4], v[5])); edges.Add((v[6], v[7]));
            edges.Add((v[4], v[6])); edges.Add((v[5], v[7]));
            // Bağlantı kenarları
            edges.Add((v[0], v[4])); edges.Add((v[1], v[5]));
            edges.Add((v[2], v[6])); edges.Add((v[3], v[7]));
        }

        GameObject prefabToUse = shapeCornerPrefab != null ? shapeCornerPrefab : frameSegmentPrefab;
        foreach (var (a, b) in edges)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            float len = dir.magnitude;

            GameObject seg;
            if (prefabToUse != null)
                seg = Instantiate(prefabToUse, shapeRoot.transform);
            else
            {
                seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.transform.SetParent(shapeRoot.transform, false);
                Destroy(seg.GetComponent<BoxCollider>());
            }

            seg.transform.localPosition = mid;
            // Segmentin uzun eksenini (local Y) kenar yönüne hizala
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            seg.transform.localScale = new Vector3(t, len, t);
            activeSpawnedObjects.Add(seg);
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
                            spawner.levels[spawner.currentLevelIndex].boardMode == LevelData.BoardMode.Shape3D;

                if (!is3D) return false;
            }
        }

        return true;
    }
}