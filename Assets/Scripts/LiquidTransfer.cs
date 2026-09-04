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
    public int currentSlices = 2;
    public int maxSlices = 4;

    private static MaterialPropertyBlock _propBlock;
    private Renderer[] _renderers;
    private DragObject _parentDrag;

    [HideInInspector]
    public bool transferring = false;
    
    private bool IsParentDragging()
    {
        if (_parentDrag == null) _parentDrag = GetComponentInParent<DragObject>();
        return _parentDrag != null && _parentDrag.IsDragging;
    }
    
    [Header("Başlangıç Konumu (Trigger İçin)")]
    public Vector2Int initialGridPos;
    public int initialFaceIndex;

    void Start()
    {
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>();
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (currentSlices > 0 && currentSlices < 2) currentSlices = 2;
        float t = (float)currentSlices / maxSlices;
        float visualT = t + 0.12f * (1f - t); // Az doluysa daha fazla boost, çok doluysa az boost
        fillAmount = Mathf.Lerp(-0.5f, 0.5f, visualT);

        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        foreach (Renderer r in _renderers)
        {
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_FillAmount", fillAmount);
            _propBlock.SetColor("_LiquidColor", liquidColor);
            _propBlock.SetColor("_ColorA", liquidColor);
            r.SetPropertyBlock(_propBlock);
        }

        LiquidTilt tiltCode = GetComponent<LiquidTilt>();
        if (tiltCode != null) tiltCode.liquidMat = liquidMat;
    }

    public void ApplyPropertyBlock()
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        foreach (Renderer r in _renderers)
        {
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_FillAmount", fillAmount);
            _propBlock.SetColor("_LiquidColor", liquidColor);
            _propBlock.SetColor("_ColorA", liquidColor);
            r.SetPropertyBlock(_propBlock);
        }
    }



    public void CheckSymmetry()
    {
        if (this == null || transferring || IsParentDragging()) return;

        CheckClassicSymmetry();

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
            if (other == this || other == null || other.transferring || other.IsParentDragging() || other.currentSlices <= 0) continue;

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

    // ── Classic Transfer ─────────────────────────────────────────
    public void StartTransfer(LiquidTransfer giver)
    {
        transferring = true;
        giver.transferring = true;

        VibrationManager.TryVibrate();
        AudioManager.PlayTransfer();
        FrozenGridCell.NotifyMatchCompleted();
        GameManager.Instance?.RegisterMatch();

        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.SpawnGlowPulse(this.transform, this.liquidColor);
            EffectsManager.Instance.SpawnTransferParticles(
                giver.transform.position, this.transform.position, liquidColor, transferDuration);
        }

        int needed = maxSlices - this.currentSlices;
        int takeAmount = Mathf.Min(needed, giver.currentSlices);

        this.currentSlices += takeAmount;
        giver.currentSlices -= takeAmount;

        float myTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)this.currentSlices / maxSlices);
        float giverTargetFill = Mathf.Lerp(-0.5f, 0.5f, (float)giver.currentSlices / maxSlices);

        Sequence seq = DOTween.Sequence();

        seq.Join(DOTween.To(() => giver.fillAmount, x => giver.fillAmount = x, giverTargetFill, transferDuration)
            .OnUpdate(() => { if (giver != null) giver.ApplyPropertyBlock(); }));

        seq.Join(DOTween.To(() => this.fillAmount, x => this.fillAmount = x, myTargetFill, transferDuration)
            .OnUpdate(() => { if (this != null) this.ApplyPropertyBlock(); }));

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
                    if (EffectsManager.Instance != null)
                    {
                        EffectsManager.Instance.SpawnSnapParticles(this.transform.position, this.liquidColor);
                        EffectsManager.Instance.SpawnSplash(this.transform.position, this.liquidColor);
                    }

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
            // Sadece aktif, yok edilmeyen ve geçerli dilime sahip objeleri say
            DragObject[] allObjects = FindObjectsOfType<DragObject>();
            List<DragObject> remaining = new List<DragObject>();
            foreach(var obj in allObjects)
            {
                if (obj == null || !obj.gameObject.activeInHierarchy) continue;
                if (obj.transform.localScale.x <= 0.05f) continue;

                LiquidTransfer lt = obj.GetComponentInChildren<LiquidTransfer>();
                if(lt != null && !lt.transferring && lt.currentSlices > 0 && lt.currentSlices < lt.maxSlices)
                {
                    remaining.Add(obj);
                }
            }

            if (remaining.Count == 0)
            {
                // Eğer gerçekten hiç parça kalmadıysa (transferring olanlar dahil hepsi bittiyse)
                LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();
                bool anyTransferring = false;
                foreach(var l in allLiquids)
                {
                    if (l != null && l.gameObject != null && l.gameObject.activeInHierarchy && l.transferring)
                        anyTransferring = true;
                }

                if (!anyTransferring)
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.LevelComplete();
                    return;
                }
            }
            
            // Hâlâ parça varsa hamle kalıp kalmadığını kontrol et
            if (remaining.Count > 0)
            {
                FindObjectOfType<GridSpawner>()?.CheckForFail();
            }
        });
    }
}