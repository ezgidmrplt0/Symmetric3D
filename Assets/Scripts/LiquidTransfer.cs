using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    public Color liquidColor = Color.white;
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;
    public float maxAdjacencyDistance = 1.6f; 

    [Header("Dilim (Slice) Ayarları")]
    public int currentSlices = 1;
    public int maxSlices = 4;
    public bool isShadowTrigger = false;
    public int spawnShadowAfterLinkID = 0; // Bu Link Grubu temizlendiğinde gölgeyi doğur
    public bool isShadowChild = false;
    public bool shadowSpawned = false;


    [HideInInspector]
    public bool transferring = false;
    
    [Header("Başlangıç Konumu (Trigger İçin)")]
    public Vector2Int initialGridPos;
    public int initialFaceIndex;

    void Start()
    {
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        // Başlangıç doluluğunu -0.5 (boş) ile 0.5 (dolu) arasına oranla
        fillAmount = Mathf.Lerp(-0.5f, 0.5f, (float)currentSlices / maxSlices);

        if (liquidMat != null)
        {
            // Runtime'da her zaman instance üzerinden çalışmalıyız
            if (Application.isPlaying && !liquidMat.name.Contains("(Instance)"))
            {
                liquidMat = new Material(liquidMat);
            }
            
            liquidMat.SetFloat("_FillAmount", fillAmount);
            liquidMat.SetColor("_LiquidColor", liquidColor);
            liquidMat.SetColor("_ColorA", liquidColor);

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material[] sharedMats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    // Shader ismine göre hedef materyali bul ve değiştir
                    if (sharedMats[i] != null && sharedMats[i].shader.name == "Custom/LiquidFullControl")
                    {
                        if (sharedMats[i] != liquidMat)
                        {
                            sharedMats[i] = liquidMat;
                            changed = true;
                        }
                    }
                }
                if (changed) r.sharedMaterials = sharedMats;
            }

            LiquidTilt tiltCode = GetComponent<LiquidTilt>();
            if (tiltCode != null) tiltCode.liquidMat = liquidMat;
        }
    }



    public void CheckSymmetry()
    {
        if (this == null || transferring) return;

        // Aktif level türünü al
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        LevelData.LevelType levelType = spawner != null
            ? spawner.CurrentLevelType
            : LevelData.LevelType.Classic;

        if (levelType.HasFlag(LevelData.LevelType.ColorMix))
        {
            CheckColorMix();
        }
        
        if (levelType.HasFlag(LevelData.LevelType.Classic))
        {
            CheckClassicSymmetry();
        }

        // Eğer bir hamle (transfer) başlamadıysa, oyunun tıkanıp tıkanmadığını kontrol et
        if (!transferring)
        {
            FindObjectOfType<GridSpawner>()?.CheckForFail();
        }
    }

    // ── Classic Mod ──────────────────────────────────────────────
    void CheckClassicSymmetry()
    {
        if (transferring || currentSlices >= maxSlices) return;

        LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();

        foreach (LiquidTransfer other in allLiquids)
        {
            if (other == this || other == null || other.transferring || other.currentSlices <= 0) continue;

            // Aynı renk, aynı dilim sayısı
            if (!ColorMixData.ColorsMatch(other.liquidColor, this.liquidColor) ||
                other.currentSlices != this.currentSlices) continue;

            if (IsAdjacentFaceToFace(other))
            {
                StartTransfer(other);
                break;
            }
        }
    }

    // ── ColorMix Mod ─────────────────────────────────────────────
    void CheckColorMix()
    {
        if (transferring || currentSlices >= maxSlices) return;

        LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();

        foreach (LiquidTransfer other in allLiquids)
        {
            if (other == this || other == null || other.transferring || other.currentSlices <= 0) continue;

            // Aynı renk birleşmez (Kullanıcı isteğiyle kapatıldı)
            if (ColorMixData.ColorsMatch(other.liquidColor, this.liquidColor)) continue;

            // Tarife göre karışım kontrolü
            if (ColorMixData.TryGetMix(this.liquidColor, other.liquidColor, out Color mixResult))
            {
                // Karışım var ama bakış yönleri mi tutmuyor?
                if (IsAdjacentFaceToFace(other))
                {
                    StartColorMix(other, mixResult);
                    break;
                }
                else
                {
                }
            }
        }
    }

    // ── Ortak Konum/Yön Kontrolü ────────────────────────────────
    bool IsAdjacentFaceToFace(LiquidTransfer other)
    {
        Vector3 myPos = transform.position;
        Vector3 otherPos = other.transform.position;

        float dist = Vector3.Distance(myPos, otherPos);

        // Mesafe kontrolü — parça dünya boyutuna göre dinamik eşik (gridStep * 1.2)
        // Shape3D'de gridStep ≈ lossyScale.x / 0.55; sabit maxAdjacencyDistance 3D için fazla büyük
        float adjDist = transform.lossyScale.x > 0.001f
            ? (transform.lossyScale.x / 0.55f) * 1.2f
            : maxAdjacencyDistance;
        if (dist >= adjDist || dist <= 0.1f) return false;

        Vector3 dirToOther = (otherPos - myPos).normalized;
        Vector3 myFace = transform.up;
        Vector3 otherFace = other.transform.up;

        // --- ÇAPRAZ ENGELEME (DIAGONAL PREVENTION) ---
        float maxAxisOverlap = Mathf.Max(Mathf.Abs(dirToOther.x), Mathf.Max(Mathf.Abs(dirToOther.y), Mathf.Abs(dirToOther.z)));
        if (maxAxisOverlap < 0.85f) return false;

        // --- AYNA/SİMETRİ KONTROLÜ (FACING EACH OTHER) ---
        bool dot1 = Vector3.Dot(myFace, dirToOther) > 0.8f;
        bool dot2 = Vector3.Dot(otherFace, -dirToOther) > 0.8f;

        if (dot1 && dot2)
        {
            return true;
        }

        return false;
    }

    // ── ColorMix Transfer ────────────────────────────────────────
    void StartColorMix(LiquidTransfer giver, Color mixedColor)
    {
        transferring = true;
        giver.transferring = true;

        VibrationManager.TryVibrate();

        int needed = maxSlices - this.currentSlices;
        int takeAmount = Mathf.Min(needed, giver.currentSlices);

        this.currentSlices += takeAmount;
        giver.currentSlices -= takeAmount;
        
        // Receiver'ın yeni rengini ve miktarını güncelle
        this.liquidColor = mixedColor;

        CheckTriggerPairs(giver);

        float myTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)this.currentSlices / maxSlices);
        float giverTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)giver.currentSlices / maxSlices);

        // Receiver'ın rengini materyal üzerinden hemen güncelle
        if (liquidMat != null)
        {
            liquidMat.SetColor("_LiquidColor", mixedColor);
            liquidMat.SetColor("_ColorA",      mixedColor);
        }

        Sequence seq = DOTween.Sequence();

        // Giver boşalsın
        seq.Join(DOTween.To(() => giver.fillAmount, x => giver.fillAmount = x, giverTargetFill, transferDuration)
            .OnUpdate(() => { if (giver != null && giver.liquidMat != null) giver.liquidMat.SetFloat("_FillAmount", giver.fillAmount); }));

        // Receiver yeni rengiyle dolsun
        seq.Join(DOTween.To(() => this.fillAmount, x => this.fillAmount = x, myTargetFill, transferDuration)
            .OnUpdate(() => { if (this != null && this.liquidMat != null) this.liquidMat.SetFloat("_FillAmount", this.fillAmount); }));

        seq.OnComplete(() =>
        {
            // Giver tamamen boşaldıysa yok et
            if (giver != null)
            {
                if (giver.currentSlices <= 0)
                {
                    if (giver.transform.parent != null)
                        giver.transform.parent.DOScale(0f, 0.2f).OnComplete(() =>
                        {
                            giver.transferring = false;
                            Destroy(giver.transform.parent.gameObject);
                            CheckLevelComplete();
                        });
                }
                else
                {
                    giver.transferring = false;
                }
            }

            // Receiver tamamen dolduysa yok et
            if (this != null)
            {
                if (this.currentSlices >= maxSlices)
                {
                    if (this.transform.parent != null)
                        this.transform.parent.DOScale(0f, 0.2f).OnComplete(() =>
                        {
                            this.transferring = false;
                            Destroy(this.transform.parent.gameObject);
                            CheckLevelComplete();
                        });
                }
                else
                {
                    this.transferring = false;
                }
            }
        });
    }

    // ── Classic Transfer ─────────────────────────────────────────
    void StartTransfer(LiquidTransfer giver)
    {
        transferring = true;
        giver.transferring = true;

        VibrationManager.TryVibrate();

        int needed = maxSlices - this.currentSlices;
        int takeAmount = Mathf.Min(needed, giver.currentSlices);

        this.currentSlices += takeAmount;
        giver.currentSlices -= takeAmount;

        CheckTriggerPairs(giver);

        float myTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)this.currentSlices / maxSlices);
        float giverTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)giver.currentSlices / maxSlices);

        Sequence seq = DOTween.Sequence();

        seq.Join(DOTween.To(() => giver.fillAmount, x => giver.fillAmount = x, giverTargetFill, transferDuration)
            .OnUpdate(() => { if (giver != null && giver.liquidMat != null) giver.liquidMat.SetFloat("_FillAmount", giver.fillAmount); }));

        seq.Join(DOTween.To(() => this.fillAmount, x => this.fillAmount = x, myTargetFill, transferDuration)
            .OnUpdate(() => { if (this != null && this.liquidMat != null) this.liquidMat.SetFloat("_FillAmount", this.fillAmount); }));

        seq.OnComplete(() =>
        {
            if (giver != null)
            {
                if (giver.currentSlices <= 0)
                {
                    if (giver.transform.parent != null)
                        giver.transform.parent.DOScale(0, 0.2f).OnComplete(() =>
                        {
                            giver.transferring = false;
                            Destroy(giver.transform.parent.gameObject);
                            CheckLevelComplete();
                        });
                }
                else
                {
                    giver.transferring = false;
                }
            }

            if (this != null)
            {
                if (this.currentSlices >= maxSlices)
                {
                    // Tamamlandığında objeyi patlat/yok et
                    if (this.transform.parent != null)
                        this.transform.parent.DOScale(0, 0.2f).OnComplete(() =>
                        {
                            this.transferring = false;
                            Destroy(this.transform.parent.gameObject);
                            CheckLevelComplete();
                        });
                }
                else
                {
                    this.transferring = false;
                }
            }
        });
    }

    void CheckLevelComplete()
    {
        // Sahnede hâlâ DragObject var mı? (Destroy 1 frame sonra gerçekleşir, o yüzden kısa delay)
        DOVirtual.DelayedCall(0.15f, () =>
        {
            // Sadece DragObject olan ve yok edilmeyen (scaling down olmayan) objeleri say
            DragObject[] allObjects = FindObjectsOfType<DragObject>();
            List<DragObject> remaining = new List<DragObject>();
            foreach(var obj in allObjects)
            {
                LiquidTransfer lt = obj.GetComponentInChildren<LiquidTransfer>();
                if(lt != null && !lt.transferring)
                {
                    remaining.Add(obj);
                }
            }

            if (remaining.Count == 0)
            {
                // Eğer gerçekten hiç parça kalmadıysa (transferring olanlar dahil hepsi bittiyse)
                LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();
                bool anyTransferring = false;
                foreach(var l in allLiquids) if(l != null && l.transferring) anyTransferring = true;


                if (!anyTransferring)
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.LevelComplete();
                    return;
                }
            }
            
            // Hâlâ parça var or some pieces are still transferring
            if (remaining.Count > 0)
            {
                
                GridSpawner spawner = FindObjectOfType<GridSpawner>();
                DragObject[] allObjectsOnBoard = FindObjectsOfType<DragObject>();

                // Bekleyen parçaları spawn et
                HashSet<int> existingLinkIds = new HashSet<int>();
                foreach(var ao in allObjectsOnBoard) if(ao != null && ao.linkId > 0) existingLinkIds.Add(ao.linkId);

                if (spawner != null)
                {
                    foreach (int id in spawner.GetPendingSpawnIds())
                    {
                        if (!existingLinkIds.Contains(id))
                            spawner.TrySpawnPending(id);
                    }
                }

                // Hamle kalıp kalmadığını kontrol et
                FindObjectOfType<GridSpawner>()?.CheckForFail();
            }
        });
    }

    private void CheckTriggerPairs(LiquidTransfer other)
    {
        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        if (spawner == null || spawner.levels == null || spawner.currentLevelIndex >= spawner.levels.Count) return;

        LevelData level = spawner.levels[spawner.currentLevelIndex];

        if (level.shadowTransferPairs == null || level.shadowTransferPairs.Count == 0) return;

        foreach (var pair in level.shadowTransferPairs)
        {
            bool match = false;
            if (this.initialGridPos == pair.posA && this.initialFaceIndex == pair.faceA &&
                other.initialGridPos == pair.posB && other.initialFaceIndex == pair.faceB)
                match = true;
            else if (this.initialGridPos == pair.posB && this.initialFaceIndex == pair.faceB &&
                     other.initialGridPos == pair.posA && other.initialFaceIndex == pair.faceA)
                match = true;

            if (!match) continue;

            if (pair.isDynamic)
            {
                // Alıcı (this) = birleşim sonucu; verilerini yakala, animasyon bittikten sonra spawn et.
                // Değer tiplerini yerel değişkene kopyala — giver yok edilmeden önce yakala.
                Color capturedColor = this.liquidColor;
                int capturedSlices = this.currentSlices; // takeAmount eklendikten SONRA (2 slice)
                float capturedRot = this.transform.eulerAngles.z;
                DG.Tweening.DOVirtual.DelayedCall(0.75f, () => spawner.SpawnDynamicShadow(capturedColor, capturedSlices, capturedRot));
            }
            else
            {
                // Klasik sistem: sadece alıcıyı (this) mirror olarak geç.
                // Giver transfer sonrası 0 slice'a düşer; TrySpawnPending'e geçilirse
                // sıralama giver'ı önce seçip Clamp(0,1,3)=1 → yanlış çeyrek shadow üretir.
                spawner.TrySpawnPending(pair.shadowToSpawnLinkId, this, null);
            }
        }
    }
}