using UnityEngine;
using DG.Tweening;

/// <summary>
/// DragObject — Shape3D (küp/prizma) modu için sürükleme ve bırakma mantığı.
/// Bu dosya SADECE 3D'ye özgü kodu içerir; 2D koduna dokunmaz.
/// Ana sınıf: DragObject.cs
/// </summary>
public partial class DragObject
{
    // ──────────────────────────────────────────────────────────────
    // 3D SÜRÜKLEME
    // ──────────────────────────────────────────────────────────────

    private void DragShape3D(Vector3 screenPos, Vector3 desiredPos, DragObject[] allObjects)
    {
        // Çarpışma mesafesini parça dünya boyutuna göre ayarla:
        float shapeDist = cachedWorldSize * 1.0f;

        Vector3 currentPos = transform.position;
        Vector3 moveDir = desiredPos - currentPos;
        float dist = moveDir.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.04f));
        Vector3 stepVec = moveDir / steps;

        // PERFORMANCE: Filter objects on the same face once at the start
        System.Collections.Generic.List<DragObject> localObjects = new System.Collections.Generic.List<DragObject>();
        foreach(var obj in allObjects)
        {
            if (obj != null && obj != this && obj.gameObject.activeInHierarchy && (obj.transform.parent == startParent || startParent == null))
                localObjects.Add(obj);
        }

        for (int s = 0; s < steps; s++)
        {
            Vector3 nextPos = currentPos + stepVec;
            bool collisionFound = false;

            foreach (DragObject obj in localObjects)
            {
                float d = Vector3.Distance(nextPos, obj.transform.position);
                if (d < shapeDist)
                {
                    collisionFound = true;
                    break;
                }
            }

            if (!collisionFound)
                collisionFound = IsDiagonallyBlockedCached(currentPos, nextPos, localObjects);

            if (collisionFound)
            {
                Vector3 tryX = currentPos + new Vector3(stepVec.x, 0f, 0f);
                Vector3 tryY = currentPos + new Vector3(0f, stepVec.y, 0f);
                bool blockX = false, blockY = false;

                foreach (DragObject obj in localObjects)
                {
                    if (Vector3.Distance(tryX, obj.transform.position) < shapeDist) blockX = true;
                    if (Vector3.Distance(tryY, obj.transform.position) < shapeDist) blockY = true;
                }

                if (!blockX) blockX = IsDiagonallyBlockedCached(currentPos, tryX, localObjects);
                if (!blockY) blockY = IsDiagonallyBlockedCached(currentPos, tryY, localObjects);

                if (!blockX && Mathf.Abs(stepVec.x) > 0.001f) nextPos = tryX;
                else if (!blockY && Mathf.Abs(stepVec.y) > 0.001f) nextPos = tryY;
                else break;
            }

            currentPos = nextPos;
        }

        transform.position = currentPos;

        // Küp/prizma rotasyonu: parmak ekran kenarına yaklaşınca döndür
        if (wrapCooldown <= 0f)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 offset = (Vector2)screenPos - screenCenter;

            float hThreshold = Screen.width * 0.28f;
            float vThreshold = Screen.height * 0.22f;

            Vector3 rotAxis = Vector3.zero;
            float screenHeightFactor = screenPos.y / Screen.height;

            CubeRotator rotator = activeSpawner?.GetComponentInChildren<CubeRotator>();
            bool isPrism = rotator != null && rotator.isPrism;

            if (Mathf.Abs(offset.x) > hThreshold)
            {
                if (!isPrism && screenHeightFactor < 0.4f)
                    rotAxis = offset.x > 0 ? Vector3.forward : Vector3.back;
                else
                    rotAxis = offset.x > 0 ? Vector3.down : Vector3.up;
            }
            else if (Mathf.Abs(offset.y) > vThreshold)
            {
                rotAxis = offset.y > 0 ? Vector3.right : Vector3.left;
            }

            if (rotAxis != Vector3.zero && rotator != null && !rotator.IsRotating)
            {
                rotator.RotateByAngle(rotAxis, 90f);
                wrapCooldown = rotator.rotationDuration * 0.8f;
            }
            else if (rotAxis != Vector3.zero && rotator != null && rotator.IsRotating)
            {
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 3D BIRAKMA
    // ──────────────────────────────────────────────────────────────

    private void DropShape3D(Transform targetGrid, GridSpawner spawner)
    {
        ShapeFaceMarker dropFaceMarker = targetGrid.parent?.GetComponent<ShapeFaceMarker>();
        float surfaceOff = dropFaceMarker?.surfaceOffset ?? 0.01f;

        float oz = -(cachedWorldSize * 0.5f + surfaceOff);


        transform.SetParent(targetGrid.parent, true);

        // Scale: aynı yüzeye bırakıldıysa orijinal scale; farklı yüzeye bırakıldıysa yeniden hesapla
        if (targetGrid.parent == startParent)
        {
            transform.localScale = cachedLocalScale;
        }
        else
        {
            float accX = 1f, accY = 1f;
            Transform cur = targetGrid.parent;
            while (cur != null) { accX *= cur.localScale.x; accY *= cur.localScale.y; cur = cur.parent; }
            float sx = Mathf.Abs(accX) > 0.001f ? cachedWorldSize / Mathf.Abs(accX) : cachedLocalScale.x;
            float sy = Mathf.Abs(accY) > 0.001f ? cachedWorldSize / Mathf.Abs(accY) : cachedLocalScale.y;
            transform.localScale = new Vector3(sx, sy, cachedLocalScale.z);
        }

        // Dünya rotasyonunu yüzeyin local uzayına çevir — küp dönse de yön korunur
        Quaternion localRot = Quaternion.Inverse(targetGrid.parent.rotation) * cachedWorldRotation;
        float snappedZ = Mathf.Round(localRot.eulerAngles.z / 90f) * 90f;

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
