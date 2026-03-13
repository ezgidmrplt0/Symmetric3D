using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    public Color liquidColor = Color.white;
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;
    public float maxAdjacencyDistance = 1.5f; // Sadece 1 grid (yaklaşık 1.4 birim) mesafeye izin verir

    [Header("Dilim (Slice) Ayarları")]
    public int currentSlices = 1;
    public int maxSlices = 4;
    public bool isShadowTrigger = false;
    public bool isShadowChild = false;
    public bool shadowSpawned = false;


    [HideInInspector]
    public bool transferring = false;

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

        if (levelType == LevelData.LevelType.ColorMix)
        {
            CheckColorMix();
        }
        else
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
        if (transferring) return;

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
                    // Debug.Log($"[ColorMix] Karışım bulundu ({this.liquidColor} + {other.liquidColor}) ama bakış yönleri veya mesafe uygun değil!");
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
        
        // Ekrana/Düzleme göre hizalı mı? (En az iki eksende yakın olmalı ya da 3D'de 1 birim mesafe)
        // Küp üzerinde komşular arası mesafe yaklaşık 'spacing + gridSize' kadardır.
        if (dist >= maxAdjacencyDistance || dist <= 0.1f) return false;

        Vector3 dirToOther = (otherPos - myPos).normalized;
        Vector3 myFace = transform.up;
        Vector3 otherFace = other.transform.up;

        // Benim baktığım yönde 'öteki' var mı? 
        // Ve 'öteki' bana mı bakıyor?
        return Vector3.Dot(myFace, dirToOther) > 0.8f &&
               Vector3.Dot(otherFace, -dirToOther) > 0.8f;
    }

    // ── ColorMix Transfer ────────────────────────────────────────
    void StartColorMix(LiquidTransfer giver, Color mixedColor)
    {
        transferring = true;
        giver.transferring = true;

        // Receiver (this) tam dolacak, giver tamamen boşalacak
        float receiverTargetFill = 0.5f;   // maxSlices / maxSlices = 1 → Lerp(-0.5, 0.5, 1) = 0.5
        float giverTargetFill    = -0.5f;  // 0 / maxSlices = 0 → Lerp(-0.5, 0.5, 0) = -0.5

        // Receiver'ın rengini hemen karışım rengine çevir
        if (liquidMat != null)
        {
            liquidMat.SetColor("_LiquidColor", mixedColor);
            liquidMat.SetColor("_ColorA",      mixedColor);
        }

        Sequence seq = DOTween.Sequence();

        // Giver boşalsın
        seq.Join(DOTween.To(() => giver.fillAmount, x => giver.fillAmount = x, giverTargetFill, transferDuration)
            .OnUpdate(() => { if (giver != null && giver.liquidMat != null) giver.liquidMat.SetFloat("_FillAmount", giver.fillAmount); }));

        // Receiver karışım rengiyle dolsun
        seq.Join(DOTween.To(() => this.fillAmount, x => this.fillAmount = x, receiverTargetFill, transferDuration)
            .OnUpdate(() => { if (this != null && this.liquidMat != null) this.liquidMat.SetFloat("_FillAmount", this.fillAmount); }));

        // Dolunca ikisi de yok olsun
        seq.OnComplete(() =>
        {
            // Giver
            if (giver != null && giver.transform.parent != null)
                giver.transform.parent.DOScale(0f, 0.2f).OnComplete(() =>
                {
                    Destroy(giver.transform.parent.gameObject);
                    CheckLevelComplete();
                });

            // Receiver (mix rengiyle dolu)
            if (this != null && this.transform.parent != null)
                this.transform.parent.DOScale(0f, 0.2f).OnComplete(() =>
                {
                    this.transferring = false;
                    Destroy(this.transform.parent.gameObject);
                    CheckLevelComplete();
                });
        });
    }

    // ── Classic Transfer ─────────────────────────────────────────
    void StartTransfer(LiquidTransfer giver)
    {
        transferring = true;
        giver.transferring = true;

        int needed = maxSlices - this.currentSlices;
        int takeAmount = Mathf.Min(needed, giver.currentSlices);

        this.currentSlices += takeAmount;
        giver.currentSlices -= takeAmount;

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

                Debug.Log($"<color=green>[LiquidTransfer]</color> Kalan parça yok. Transferring var mı: {anyTransferring}");

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
                Debug.Log($"<color=orange>[LiquidTransfer]</color> Level devam ediyor. Kalan aktif parça sayısı: {remaining.Count}");
                // Hâlâ parça var, ama hepsi ShadowTrigger mı?
                bool anyRegularLeft = false;
                List<LiquidTransfer> triggers = new List<LiquidTransfer>();

                foreach (var obj in remaining)
                {
                    LiquidTransfer lt = obj.GetComponentInChildren<LiquidTransfer>();
                    if (lt != null)
                    {
                        if (!lt.isShadowTrigger || lt.shadowSpawned || lt.isShadowChild)
                            anyRegularLeft = true;
                        else if (lt.isShadowTrigger && !lt.shadowSpawned)
                            triggers.Add(lt);
                    }
                }

                if (!anyRegularLeft && triggers.Count > 0)
                {
                    // Sadece gölge bekleyen tetikleyiciler kaldı
                    GridSpawner spawner = FindObjectOfType<GridSpawner>();
                    if (spawner != null)
                    {
                        foreach (var t in triggers)
                        {
                            t.shadowSpawned = true;
                            spawner.SpawnShadowFor(t);
                        }
                    }
                }
                else
                {
                    // Hâlâ normal objeler var, hamle kalıp kalmadığını kontrol et
                    FindObjectOfType<GridSpawner>()?.CheckForFail();
                }
            }
        });
    }

}