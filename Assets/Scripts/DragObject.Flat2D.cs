using UnityEngine;
using DG.Tweening;

/// <summary>
/// DragObject — 2D Düzlem (Flat2D) modu için sürükleme ve bırakma mantığı.
/// Bu dosya SADECE 2D'ye özgü kodu içerir; 3D koduna dokunmaz.
/// Ana sınıf: DragObject.cs
/// </summary>
public partial class DragObject
{
    // ──────────────────────────────────────────────────────────────
    // 2D SÜRÜKLEME
    // ──────────────────────────────────────────────────────────────

    private void DragFlat2D(Vector3 screenPos, Vector3 desiredPos, DragObject[] allObjects)
    {
        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.04f));
        Vector3 stepVec = moveDir / steps;

        // OPTIMIZATION: Cache spawner values once per drag
        float gridSize = activeSpawner != null ? activeSpawner.gridPrefab.transform.localScale.x : 1.4f;
        float step = activeSpawner != null ? gridSize + activeSpawner.spacing : 1.4f;
        float halfStep = step * 0.5f;
        Transform spawnerTransform = activeSpawner != null ? activeSpawner.transform : null;

        for (int s = 0; s < steps; s++)
        {
            Vector3 nextPos = currentPos + stepVec;
            bool collisionFound = false;

            // 2D'de tüm objelerle çarpış (yüzey filtresi yok)
            foreach (DragObject obj in allObjects)
            {
                if (obj == null || obj == this || !obj.gameObject.activeInHierarchy) continue;
                
                // PERFORMANCE: Square distance check is faster
                float dsq = (nextPos - obj.transform.position).sqrMagnitude;
                if (dsq < collisionDistance * collisionDistance)
                {
                    collisionFound = true;
                    break;
                }
            }

            if (!collisionFound)
                collisionFound = IsDiagonallyBlocked(currentPos, nextPos, allObjects, sameParentOnly: false);

            if (collisionFound)
            {
                Vector3 tryX = currentPos + new Vector3(stepVec.x, 0f, 0f);
                Vector3 tryY = currentPos + new Vector3(0f, stepVec.y, 0f);
                bool blockX = false, blockY = false;

                foreach (DragObject obj in allObjects)
                {
                    if (obj == null || obj == this || !obj.gameObject.activeInHierarchy) continue;
                    
                    float dsqX = (tryX - obj.transform.position).sqrMagnitude;
                    if (dsqX < collisionDistance * collisionDistance) blockX = true;
                    
                    float dsqY = (tryY - obj.transform.position).sqrMagnitude;
                    if (dsqY < collisionDistance * collisionDistance) blockY = true;
                    
                    if (blockX && blockY) break;
                }

                if (!blockX) blockX = IsDiagonallyBlocked(currentPos, tryX, allObjects, sameParentOnly: false);
                if (!blockY) blockY = IsDiagonallyBlocked(currentPos, tryY, allObjects, sameParentOnly: false);

                if (!blockX && Mathf.Abs(stepVec.x) > 0.001f) nextPos = tryX;
                else if (!blockY && Mathf.Abs(stepVec.y) > 0.001f) nextPos = tryY;
                else break;
            }

            // 2D sınır kontrolü: aktif grid hücrelerinin dışına çıkma - OPTIMIZED WITH CACHED POSITIONS
            if (activeSpawner != null && cachedGridCellPositions != null)
            {
                Vector3 lp = spawnerTransform.InverseTransformPoint(nextPos);
                bool inBounds = false;
                
                for (int i = 0; i < cachedGridCellPositions.Length; i++)
                {
                    // PERFORMANCE: Check cell tags/names was moved to Cache init if possible,
                    // but for now we rely on the fact that only valid grids are in cachedGridCells
                    Vector3 cellLp = spawnerTransform.InverseTransformPoint(cachedGridCellPositions[i]);
                    if (Mathf.Abs(lp.x - cellLp.x) <= halfStep && Mathf.Abs(lp.y - cellLp.y) <= halfStep)
                    {
                        inBounds = true;
                        break;
                    }
                }
                if (!inBounds) break;
            }

            currentPos = nextPos;
        }

        transform.position = currentPos;
        // 2D modunda küp/prizma rotasyonu yok.
    }

    // ──────────────────────────────────────────────────────────────
    // 2D BIRAKMA
    // ──────────────────────────────────────────────────────────────

    private void DropFlat2D(Transform targetGrid, GridSpawner spawner)
    {
        float oz = -((spawner != null) ? spawner.objectOffset : 0.3f);

        transform.SetParent(targetGrid.parent, true);

        float snappedZ = Mathf.Round(cachedLocalRotZ / 90f) * 90f;

        transform.DOLocalMove(new Vector3(targetGrid.localPosition.x, targetGrid.localPosition.y, oz), 0.25f)
            .SetEase(Ease.OutCubic);

        transform.DOLocalRotate(new Vector3(0, 0, snappedZ), 0.15f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
                if (lt != null) lt.CheckSymmetry();
            });
    }
}
