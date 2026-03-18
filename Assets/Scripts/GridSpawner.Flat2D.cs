using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// GridSpawner — 2D Düzlem (Flat2D) modu için spawn, çerçeve ve kamera mantığı.
/// Bu dosya SADECE 2D'ye özgü kodu içerir; 3D koduna dokunmaz.
/// Ana sınıf: GridSpawner.cs
/// </summary>
public partial class GridSpawner
{
    // ──────────────────────────────────────────────────────────────
    // 2D LEVEL SPAWN
    // ──────────────────────────────────────────────────────────────

    private void SpawnFlat2DLevel(LevelData level, float gridSize)
    {
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

        // Grid zeminlerini çiz
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

        // Oyuncu objelerini çiz
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

            if (piece.linkId > 0)
            {
                if (!groups.ContainsKey(piece.linkId))
                {
                    GameObject groupObj = new GameObject("LinkedGroup_" + piece.linkId);
                    groupObj.transform.parent = transform;
                    groupObj.transform.position = transform.position;
                    LinkedObjectGroup log = groupObj.AddComponent<LinkedObjectGroup>();
                    groups[piece.linkId] = log;
                    activeSpawnedObjects.Add(groupObj);
                }
                newObj.transform.parent = groups[piece.linkId].transform;
            }

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.liquidColor     = piece.liquidColor;
                lt.currentSlices   = piece.currentSlices;
                lt.isShadowTrigger = piece.isShadowTrigger;
            }
        }

        foreach (var kvp in groups)
            kvp.Value.InitGroup();

        StartCoroutine(AdjustViewportCoroutine(level, minX, maxX, minY, maxY, gridSize));
    }

    // ──────────────────────────────────────────────────────────────
    // 2D KAMERA + ÇERÇEVE (coroutine — aspect ratio için 1 kare bekler)
    // ──────────────────────────────────────────────────────────────

    private IEnumerator AdjustViewportCoroutine(LevelData level, float minX, float maxX, float minY, float maxY, float gridSize)
    {
        yield return new WaitForEndOfFrame();

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

        foreach (var seg in activeFrameSegments) if (seg != null) Destroy(seg);
        activeFrameSegments.Clear();

        float step = gridSize + spacing;
        float offsetX = (minX + maxX) * step / 2f;
        float offsetY = (minY + maxY) * step / 2f;

        bool boundsInit = false;
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var pos in occupied)
        {
            Vector3 tileWorldPos = transform.position + new Vector3(
                pos.x * step - offsetX,
                pos.y * step - offsetY,
                0
            );

            if (!boundsInit) { combinedBounds = new Bounds(tileWorldPos, Vector3.one * gridSize); boundsInit = true; }
            else combinedBounds.Encapsulate(new Bounds(tileWorldPos, Vector3.one * gridSize));
        }

        SpawnFlat2DFrameSegments(occupied, step, gridSize, offsetX, offsetY);

        // Arka zemin plakaları
        float localPlateZ = 0.015f;
        float plateSize = step;
        foreach (var pos in occupied)
        {
            GameObject bgTile = null;
            Vector3 localPos = new Vector3(pos.x * step - offsetX, pos.y * step - offsetY, localPlateZ);

            if (backgroundPlatePrefab != null)
            {
                bgTile = Instantiate(backgroundPlatePrefab, transform);
            }
            else
            {
                bgTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bgTile.GetComponent<BoxCollider>());
                bgTile.transform.SetParent(transform);
                Renderer r = bgTile.GetComponent<Renderer>();
                if (r != null) r.material.color = Color.white;
            }

            bgTile.name = $"GridBG_{pos.x}_{pos.y}";
            bgTile.transform.localRotation = Quaternion.identity;
            bgTile.transform.localPosition = localPos;
            bgTile.transform.localScale = new Vector3(plateSize, plateSize, 0.01f);
            activeSpawnedObjects.Add(bgTile);
        }

        // Kamera ayarla
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null)
        {
            float frameFullEdge = framePadding + frameThickness;
            combinedBounds.Expand(frameFullEdge * 2f);

            float h = combinedBounds.size.y + cameraPadding * 2f;
            float w = combinedBounds.size.x + cameraPadding * 2f;

            if (cam.orthographic)
            {
                float sizeByHeight = h / 2f;
                float sizeByWidth = (w / 2f) / cam.aspect;
                float targetSize = Mathf.Max(sizeByHeight, sizeByWidth) * cameraZoomFactor;

                cam.DOOrthoSize(targetSize, 0.6f).SetEase(Ease.OutCubic);

                Vector3 camTarget = combinedBounds.center;
                camTarget.y += cameraVerticalOffset;
                camTarget.z = cam.transform.position.z;
                cam.transform.DOMove(camTarget, 0.6f).SetEase(Ease.OutCubic);
            }
            else
            {
                float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distByHeight = (h / 2f) / Mathf.Tan(halfFovRad);
                float distByWidth  = (w / 2f) / (Mathf.Tan(halfFovRad) * cam.aspect);
                float targetDistance = Mathf.Max(distByHeight, distByWidth) * cameraZoomFactor;

                Vector3 baseTarget = combinedBounds.center;
                baseTarget.y += cameraVerticalOffset;
                cam.transform.DOMove(baseTarget - cam.transform.forward * targetDistance, 0.6f).SetEase(Ease.OutCubic);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 2D ÇERÇEVE SEGMENTLERİ (dünya uzayı)
    // ──────────────────────────────────────────────────────────────

    private void SpawnFlat2DFrameSegments(HashSet<Vector2Int> occupied, float step, float gridSize, float offsetX, float offsetY)
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

            bool left  = occupied.Contains(pos + Vector2Int.left);
            bool right = occupied.Contains(pos + Vector2Int.right);
            bool up    = occupied.Contains(pos + Vector2Int.up);
            bool down  = occupied.Contains(pos + Vector2Int.down);

            // TOP
            if (!up)
            {
                float len = step;
                if (!left) len += t;
                if (!right) len += t;
                float xOff = 0;
                if (!left && right) xOff = -t / 2f;
                if (!right && left) xOff = t / 2f;
                Spawn2DSegment(center + new Vector3(xOff, edge + t / 2f, 0), new Vector3(len, t, t));
            }
            // BOTTOM
            if (!down)
            {
                float len = step;
                if (!left) len += t;
                if (!right) len += t;
                float xOff = 0;
                if (!left && right) xOff = -t / 2f;
                if (!right && left) xOff = t / 2f;
                Spawn2DSegment(center + new Vector3(xOff, -edge - t / 2f, 0), new Vector3(len, t, t));
            }
            // LEFT
            if (!left)
            {
                float len = step;
                if (!up) len += t;
                if (!down) len += t;
                float yOff = 0;
                if (!down && up) yOff = -t / 2f;
                if (!up && down) yOff = t / 2f;
                Spawn2DSegment(center + new Vector3(-edge - t / 2f, yOff, 0), new Vector3(t, len, t));
            }
            // RIGHT
            if (!right)
            {
                float len = step;
                if (!up) len += t;
                if (!down) len += t;
                float yOff = 0;
                if (!down && up) yOff = -t / 2f;
                if (!up && down) yOff = t / 2f;
                Spawn2DSegment(center + new Vector3(edge + t / 2f, yOff, 0), new Vector3(t, len, t));
            }

            // İç köşe dolguları (concave corners)
            if (up   && right && !occupied.Contains(pos + new Vector2Int( 1,  1)))
                Spawn2DSegment(center + new Vector3( edge + t / 2f,  edge + t / 2f, 0), new Vector3(t, t, t));
            if (up   && left  && !occupied.Contains(pos + new Vector2Int(-1,  1)))
                Spawn2DSegment(center + new Vector3(-edge - t / 2f,  edge + t / 2f, 0), new Vector3(t, t, t));
            if (down && right && !occupied.Contains(pos + new Vector2Int( 1, -1)))
                Spawn2DSegment(center + new Vector3( edge + t / 2f, -edge - t / 2f, 0), new Vector3(t, t, t));
            if (down && left  && !occupied.Contains(pos + new Vector2Int(-1, -1)))
                Spawn2DSegment(center + new Vector3(-edge - t / 2f, -edge - t / 2f, 0), new Vector3(t, t, t));
        }
    }

    private void Spawn2DSegment(Vector3 worldPos, Vector3 scale)
    {
        GameObject seg;
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
}
