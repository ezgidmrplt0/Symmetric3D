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
        groups.Clear(); // Yeni level için grupları temizle
        int totalBottles = level.pieces != null ? level.pieces.Count : 0;

        for (int i = 0; i < totalBottles; i++)
        {
            var piece = level.pieces[i];
            Vector3 piecePos = GetBottlePosition(i, totalBottles);
            piecePos.z = -objectOffset;

            GameObject newObj = Instantiate(objectPrefab, transform.position + piecePos,
                Quaternion.identity, transform);
            newObj.transform.localRotation = Quaternion.identity;
            activeSpawnedObjects.Add(newObj);

            DragObject dobj = newObj.GetComponent<DragObject>();
            if (dobj != null)
            {
                dobj.linkId = piece.linkId;
                dobj.canRotate = false;
                dobj.SetFrozen(false);
            }

            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                lt.InitializeSlices(piece.sliceColors, piece.liquidColor, piece.currentSlices);
                lt.initialGridPos  = new Vector2Int(i, 0);
                lt.initialFaceIndex = piece.faceIndex;
            }

            if (piece.isFrozen)
            {
                FrozenBottle fb = newObj.GetComponent<FrozenBottle>();
                if (fb == null) fb = newObj.AddComponent<FrozenBottle>();
                fb.Initialize(piece.requiredMatches);
            }
        }

        foreach (var kvp in groups)
            kvp.Value.InitGroup();

        StartCoroutine(AdjustViewportCoroutine(level, totalBottles));
    }

    // ──────────────────────────────────────────────────────────────
    // 2D KAMERA (coroutine — aspect ratio için 1 kare bekler)
    // ──────────────────────────────────────────────────────────────

    private IEnumerator AdjustViewportCoroutine(LevelData level, int totalBottles)
    {
        yield return new WaitForEndOfFrame();

        foreach (var seg in activeFrameSegments) if (seg != null) Destroy(seg);
        activeFrameSegments.Clear();

        // Şişelerin kapladığı alanı hesapla (Grid ve arka plaka oluşturulmaz, zemin tamamen temiz kalır)
        Bounds combinedBounds = new Bounds(transform.position, new Vector3(3.2f, 4.4f, 1f));
        if (totalBottles > 0)
        {
            combinedBounds = new Bounds(transform.position + GetBottlePosition(0, totalBottles), new Vector3(1.2f, 2.0f, 1f));
            for (int i = 1; i < totalBottles; i++)
            {
                Vector3 worldPos = transform.position + GetBottlePosition(i, totalBottles);
                combinedBounds.Encapsulate(new Bounds(worldPos, new Vector3(1.2f, 2.0f, 1f)));
            }
        }

        // Kamera ayarla
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null)
        {
            float h = combinedBounds.size.y + cameraPadding * 2f;
            float w = combinedBounds.size.x + cameraPadding * 2f;

            float uiMargin = Mathf.Clamp01(uiTopMarginNormalized);

            if (cam.orthographic)
            {
                float playableHeightRatio = 1f - uiMargin;
                float sizeByHeight = (h / 2f) / playableHeightRatio;
                float sizeByWidth = (w / 2f) / cam.aspect;
                float targetSize = Mathf.Max(sizeByHeight, sizeByWidth) * cameraZoomFactor;

                cam.DOOrthoSize(targetSize, 0.6f).SetEase(Ease.OutCubic);

                Vector3 camTarget = combinedBounds.center;
                camTarget.y -= targetSize * uiMargin;
                camTarget.y += cameraVerticalOffset;
                camTarget.z = cam.transform.position.z;
                cam.transform.DOMove(camTarget, 0.6f).SetEase(Ease.OutCubic);
            }
            else
            {
                float playableHeightRatio = 1f - uiMargin;
                float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distByHeight = (h / 2f) / (Mathf.Tan(halfFovRad) * playableHeightRatio);
                float distByWidth  = (w / 2f) / (Mathf.Tan(halfFovRad) * cam.aspect);
                float targetDistance = Mathf.Max(distByHeight, distByWidth) * cameraZoomFactor;

                Vector3 baseTarget = combinedBounds.center;
                baseTarget.y -= (targetDistance * Mathf.Tan(halfFovRad)) * uiMargin;
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
