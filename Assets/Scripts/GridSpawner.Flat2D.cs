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
            // Seviyenin layout moduna göre pozisyon (StaggeredV, Grid veya AutoFlow)
            Vector3 piecePos = GetBottlePositionForLevel(level, i, totalBottles);

            GameObject newObj = Instantiate(objectPrefab, transform.position + piecePos,
                Quaternion.identity, transform);
            newObj.transform.localRotation = Quaternion.identity;

            // Şişe boyutunu uygula (Level bazlı veya global 2D boyutu)
            float levelScale = (level != null && level.bottleScale > 0.1f) ? level.bottleScale : 1.0f;
            float currentBottleScale = (bottleScale2D > 0.1f ? bottleScale2D : 1.65f) * levelScale;
            newObj.transform.localScale = Vector3.one * currentBottleScale;

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

                if (lt.cork == null)
                    lt.cork = newObj.GetComponentInChildren<BottleCork>(true);
                if (lt.label == null)
                    lt.label = newObj.GetComponentInChildren<BottleLabel>(true);
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
    // 3D MASA VE KAMERA PERSPESKTİFİ
    // ──────────────────────────────────────────────────────────────

    private IEnumerator AdjustViewportCoroutine(LevelData level, int totalBottles)
    {
        yield return new WaitForEndOfFrame();

        foreach (var seg in activeFrameSegments) if (seg != null) Destroy(seg);
        activeFrameSegments.Clear();

        // 1. Şişelerin kapladığı 2D alanı hesapla
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float levelScale = (level != null && level.bottleScale > 0.1f) ? level.bottleScale : 1.0f;
        float currentBottleScale = (bottleScale2D > 0.1f ? bottleScale2D : 1.65f) * levelScale;
        float bottleHeight = 1.0f * currentBottleScale;

        if (totalBottles > 0)
        {
            for (int i = 0; i < totalBottles; i++)
            {
                Vector3 pos = GetBottlePositionForLevel(level, i, totalBottles);
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
        }
        else
        {
            minX = -1f; maxX = 1f;
            minY = -1f; maxY = 1f;
        }

        Vector3 boundsCenter = transform.position + new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f + bottleHeight * 0.5f, 0f);
        Vector3 boundsSize = new Vector3(Mathf.Max(2.8f, (maxX - minX) + 1.6f), Mathf.Max(2.8f, (maxY - minY) + bottleHeight + 1.0f), 1f);
        Bounds combinedBounds = new Bounds(boundsCenter, boundsSize);

        // 2. Kamera Hizalaması (Klasik 2D Düz Bakış - Açı ve Masa Yok)
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        float targetOrthoSize = 8f;

        if (cam != null)
        {
            cam.orthographic = true;
            cam.transform.DORotate(Vector3.zero, 0.5f).SetEase(Ease.OutCubic);

            float uiMargin = Mathf.Clamp01(uiTopMarginNormalized);
            float playableHeightRatio = Mathf.Max(0.5f, 1f - uiMargin);

            float h = combinedBounds.size.y + cameraPadding * 1.5f;
            float w = combinedBounds.size.x + cameraPadding * 1.5f;

            float sizeByHeight = (h / 2f) / playableHeightRatio;
            float sizeByWidth  = (w / 2f) / cam.aspect;

            // Geniş ızgaralarda (ör. 7 sütunlu 25 şişe) kameranın kenarları kesmemesi için güvenli zoom
            bool isLargeLayout = totalBottles > 8 || (level != null && level.flatLayoutMode == LevelData.FlatLayoutMode.StaggeredV);
            float zoom = isLargeLayout ? 1.05f : (cameraZoomFactor > 0 ? cameraZoomFactor : 0.82f) * 1.05f;

            targetOrthoSize = Mathf.Max(sizeByHeight, sizeByWidth) * zoom;

            cam.DOOrthoSize(targetOrthoSize, 0.5f).SetEase(Ease.OutCubic);

            Vector3 targetCamPos = combinedBounds.center;
            targetCamPos.z = -10f;
            targetCamPos.y += cameraVerticalOffset;

            cam.transform.DOMove(targetCamPos, 0.5f).SetEase(Ease.OutCubic);
        }

        // 3. Arkaplan "Zemin" objesini arkaplan resmi (arkaplan.jpeg) olarak hizala ve ESNEMEYİ (Stretching) ÖNLE
        GameObject zeminObj = GameObject.Find("Zemin");
        if (zeminObj != null)
        {
            float bgZ = 15f;
            Renderer zeminRen = zeminObj.GetComponent<Renderer>();
            float texAspect = 390f / 844f; // arkaplan.jpeg orijinal aspect ratio (Portrait 390x844)

            if (zeminRen != null && zeminRen.material != null)
            {
                if (zeminRen.material.mainTexture == null)
                {
                    Texture2D bgTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/arkaplan.jpeg");
                    if (bgTex != null)
                    {
                        zeminRen.material.mainTexture = bgTex;
                        texAspect = (float)bgTex.width / bgTex.height;
                    }
                }
                else if (zeminRen.material.mainTexture is Texture2D t2d && t2d.height > 0)
                {
                    texAspect = (float)t2d.width / t2d.height;
                }
                zeminRen.material.color = Color.white;
                zeminRen.material.mainTextureScale = new Vector2(1f, -1f);
                zeminRen.material.mainTextureOffset = new Vector2(0f, 1f);
            }

            // Kameranın görüş alanını kaplayacak boyutu orijinal oranları koruyarak (Aspect Cover) hesapla
            float camAspect = cam != null ? cam.aspect : 0.5625f;
            float camOrthoHeight = targetOrthoSize * 2f;
            float camOrthoWidth = camOrthoHeight * camAspect;

            // Orijinal en/boy oranını bozmadan ekranı tamamen dolduracak boyut (Aspect Fill / Cover)
            float bgHeight = Mathf.Max(camOrthoHeight * 1.5f, (camOrthoWidth / texAspect) * 1.5f);
            float bgWidth = bgHeight * texAspect;

            // Dynamic mesh bounds scaling (Cube = 1x1, Plane = 10x10)
            MeshFilter mf = zeminObj.GetComponent<MeshFilter>();
            float meshWidth = (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.x > 0f) ? mf.sharedMesh.bounds.size.x : 1f;
            float meshHeight = (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.z > 0f) ? mf.sharedMesh.bounds.size.z : 1f;

            zeminObj.transform.position = new Vector3(combinedBounds.center.x, combinedBounds.center.y, bgZ);
            zeminObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            zeminObj.transform.localScale = new Vector3(bgWidth / meshWidth, 1f, bgHeight / meshHeight);
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
