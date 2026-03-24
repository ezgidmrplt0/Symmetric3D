using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// GridSpawner — Shape3D (küp/prizma) modu için spawn ve kamera mantığı.
/// Bu dosya SADECE 3D'ye özgü kodu içerir; 2D koduna dokunmaz.
/// Ana sınıf: GridSpawner.cs
/// </summary>
public partial class GridSpawner
{
    // ──────────────────────────────────────────────────────────────
    // 3D ŞEKİL SPAWN
    // ──────────────────────────────────────────────────────────────

    private void SpawnShapeLevel(LevelData level, float gridSize)
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

        // 2. Mesh'in geometrik merkezini bul (pivot kaymasından bağımsız)
        Vector3 meshCenter = shapeRoot.transform.position;
        MeshFilter mf = shapeRoot.GetComponentInChildren<MeshFilter>();
        if (mf != null)
            meshCenter = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);

        // 3. Pivot'u mesh merkezine taşı; CubeRotator kendi merkezinde dönsün
        GameObject shapePivot = new GameObject("ShapePivotRoot");
        shapePivot.transform.SetParent(transform);
        shapePivot.transform.position = meshCenter;
        shapePivot.transform.localRotation = Quaternion.identity;
        activeSpawnedObjects.Add(shapePivot);

        shapeRoot.transform.SetParent(shapePivot.transform, true);

        // Şekli zeminden kaldır (pivot ilişkisi kurulduktan sonra kaydır)
        shapePivot.transform.position -= new Vector3(0f, 0f, shapeZOffset);

        // 4. CubeRotator pivot'a ekle
        CubeRotator rotator = shapePivot.AddComponent<CubeRotator>();

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
            SpawnFaceGrid(marker, faceData, gridSize);
        }

        SpawnShapePieces(level, def, gridSize);
        SpawnShapeCorners(shapeRoot, hasTri);
        AdjustShapeCamera(shapePivot.transform, def);
    }

    // ──────────────────────────────────────────────────────────────
    // YÜZEY GRİDİ
    // ──────────────────────────────────────────────────────────────

    private void SpawnFaceGrid(ShapeFaceMarker marker, LevelData.FaceLayoutData faceData, float gridSize)
    {
        int gx = faceData.gridX;
        // Üçgen yüzlerde gridY her zaman gridX'e eşit olmalı (row-based centering için)
        int gy = (marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle) ? gx : faceData.gridY;

        bool isTriangle = marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
        float areaScale = isTriangle ? 0.82f : 1.0f;
        float stepX = areaScale / gx;
        float stepY = areaScale / gy;
        float startX = -0.5f;
        float startY = -0.5f;

        for (int x = 0; x < gx; x++)
        {
            for (int y = 0; y < gy; y++)
            {
                float xOffset = 0;
                if (isTriangle)
                {
                    int cellsInThisRow = Mathf.Max(0, gx - y);
                    if (cellsInThisRow == 0 || x >= cellsInThisRow) continue;

                    float rowWidth = cellsInThisRow * stepX;
                    xOffset = (1.0f - rowWidth) * 0.5f;
                }

                Vector3 localPos = new Vector3(
                    startX + (x + 0.5f) * stepX + xOffset,
                    startY + (y + 0.5f) * stepY,
                    -marker.surfaceOffset
                );

                GameObject gridObj = Instantiate(gridPrefab, marker.transform);
                gridObj.transform.localPosition = localPos;
                gridObj.transform.localRotation = Quaternion.identity;

                // Grid scale: marker'ın lossyScale'ine göre hücre boyutunu hesapla
                {
                    Vector3 ws = marker.transform.lossyScale;
                    float cellWorldW = stepX * Mathf.Abs(ws.x);
                    float cellWorldH = stepY * Mathf.Abs(ws.y);
                    float worldSize = Mathf.Min(cellWorldW, cellWorldH) * 0.70f;
                    float zS = gridPrefab != null ? gridPrefab.transform.localScale.z : 0.05f;
                    gridObj.transform.localScale = new Vector3(
                        worldSize / Mathf.Abs(ws.x),
                        worldSize / Mathf.Abs(ws.y),
                        zS
                    );
                }

                activeSpawnedObjects.Add(gridObj);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // YÜZEY PARÇAları
    // ──────────────────────────────────────────────────────────────

    private void SpawnShapePieces(LevelData level, ShapeDefinition def, float gridSize)
    {
        Dictionary<int, LinkedObjectGroup> groups = new Dictionary<int, LinkedObjectGroup>();

        foreach (var piece in level.pieces)
        {
            if (piece.isShadowTrigger)
            {
                pendingPieces.Add(piece);
                continue;
            }

            if (piece.faceIndex < 0 || piece.faceIndex >= def.FaceCount) continue;

            ShapeFaceMarker marker = def.GetFace(piece.faceIndex);
            if (marker == null) continue;
            if (piece.faceIndex >= level.shapeFaces.Count) continue;

            var faceData = level.shapeFaces[piece.faceIndex];
            if (!faceData.isActive) continue;

            bool isTriFace = marker.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle;
            int effectiveGridY = isTriFace ? faceData.gridX : faceData.gridY;
            float triAreaScale = isTriFace ? 0.82f : 1.0f;
            float stepX = triAreaScale / faceData.gridX;
            float stepY = triAreaScale / effectiveGridY;
            float startX = -0.5f;
            float startY = -0.5f;

            float xOffset = 0;
            if (isTriFace)
            {
                int cellsInThisRow = Mathf.Max(0, faceData.gridX - piece.gridPosition.y);
                float rowWidth = cellsInThisRow * stepX;
                xOffset = (1.0f - rowWidth) * 0.5f;
            }

            Vector3 localPos = new Vector3(
                startX + (piece.gridPosition.x + 0.5f) * stepX + xOffset,
                startY + (piece.gridPosition.y + 0.5f) * stepY,
                0f
            );

            GameObject newObj = Instantiate(objectPrefab, marker.transform);
            newObj.transform.localPosition = localPos;
            newObj.transform.localRotation = Quaternion.Euler(0, 0, piece.rotationZ);

            // Parça scale: marker'ın dünya boyutuna göre her eksen ayrı hesaplanır → yüzeyde kare görünür
            {
                Vector3 ws = marker.transform.lossyScale;
                float cellWorldW = (triAreaScale / faceData.gridX) * Mathf.Abs(ws.x);
                float cellWorldH = (triAreaScale / effectiveGridY) * Mathf.Abs(ws.y);
                float worldSize  = Mathf.Min(cellWorldW, cellWorldH) * 0.68f;
                newObj.transform.localScale = new Vector3(
                    worldSize / Mathf.Abs(ws.x),
                    worldSize / Mathf.Abs(ws.y),
                    worldSize
                );
                float zOff = worldSize * 0.5f + marker.surfaceOffset;
                newObj.transform.localPosition = new Vector3(localPos.x, localPos.y, -zOff);
            }

            activeSpawnedObjects.Add(newObj);

            DragObject dobj = newObj.GetComponent<DragObject>();
            if (dobj != null)
            {
                dobj.linkId = piece.linkId;
                dobj.canRotate = piece.canRotate;
            }

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
                lt.liquidColor     = piece.liquidColor;
                lt.currentSlices   = piece.currentSlices;
                lt.isShadowTrigger = piece.isShadowTrigger;
                lt.spawnShadowAfterLinkID = piece.spawnShadowAfterLinkID;
                lt.initialGridPos = piece.gridPosition;
                lt.initialFaceIndex = piece.faceIndex;
            }
        }

        foreach (var kvp in groups)
            kvp.Value.InitGroup();
    }

    // ──────────────────────────────────────────────────────────────
    // 3D KÖŞE KENARLARI (wireframe çerçeve)
    // ──────────────────────────────────────────────────────────────

    private void SpawnShapeCorners(GameObject shapeRoot, bool isPrism)
    {
        MeshFilter mf = shapeRoot.GetComponentInChildren<MeshFilter>();
        if (mf == null) return;

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

        List<(Vector3, Vector3)> edges = new List<(Vector3, Vector3)>();

        if (isPrism)
        {
            // Üçgen prizma: 6 köşe, 9 kenar
            Vector3 fBL = new Vector3(mn.x, mn.y, mn.z);
            Vector3 fBR = new Vector3(mx.x, mn.y, mn.z);
            Vector3 fT  = new Vector3(cx,   mx.y, mn.z);
            Vector3 bBL = new Vector3(mn.x, mn.y, mx.z);
            Vector3 bBR = new Vector3(mx.x, mn.y, mx.z);
            Vector3 bT  = new Vector3(cx,   mx.y, mx.z);

            edges.Add((fBL, fBR)); edges.Add((fBL, fT)); edges.Add((fBR, fT));
            edges.Add((bBL, bBR)); edges.Add((bBL, bT)); edges.Add((bBR, bT));
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
            edges.Add((v[0], v[1])); edges.Add((v[2], v[3]));
            edges.Add((v[0], v[2])); edges.Add((v[1], v[3]));
            edges.Add((v[4], v[5])); edges.Add((v[6], v[7]));
            edges.Add((v[4], v[6])); edges.Add((v[5], v[7]));
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
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            seg.transform.localScale = new Vector3(t, len, t);
            activeSpawnedObjects.Add(seg);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 3D KAMERA
    // ──────────────────────────────────────────────────────────────

    private void AdjustShapeCamera(Transform shapeRoot, ShapeDefinition def)
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
            float sizeByHeight = b.size.y * 0.5f;
            float sizeByWidth  = (b.size.x * 0.5f) / cam.aspect;
            float targetSize = Mathf.Max(sizeByHeight, sizeByWidth) * cameraZoomFactor;

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
            float distance = Mathf.Max(5f, maxSize * 2.0f);

            cam.transform.DOMove(target - cam.transform.forward * distance, 0.6f).SetEase(Ease.OutCubic);
        }
    }
}
