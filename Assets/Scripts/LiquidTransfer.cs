using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class LiquidTransfer : MonoBehaviour
{
    public Material liquidMat;

    [Header("Katmanlı Sıvı (Slices)")]
    public List<Color> slices = new List<Color>();
    public int maxSlices = 4;
    public int currentSlices = 0;
    [Header("Kapak & Etiket (Dekoratif)")]
    public BottleCork cork;
    public BottleLabel label;
    public Color liquidColor = Color.white;
    public float fillAmount = 0f; 
    public float transferDuration = 0.5f;
    public float maxAdjacencyDistance = 1.6f; 

    private static MaterialPropertyBlock _propBlock;
    private Renderer[] _renderers;
    private DragObject _parentDrag;

    [HideInInspector]
    public bool transferring = false;

    // ── Magic Sort Seçim Durumu ──────────────────────────────────
    public static LiquidTransfer SelectedBottle { get; private set; }
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    public Vector3 OriginalLocalPos => originalLocalPos;
    public Quaternion OriginalLocalRot => originalLocalRot;
    private bool isSelected = false;
    public bool IsSelected => isSelected;
    
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
        Transform rootT = transform.parent != null ? transform.parent : transform;
        rootT.localRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
        originalLocalPos = rootT.localPosition;
        originalLocalRot = Quaternion.identity;

        // Eğer slices boşsa ama inspector'dan currentSlices girildiyse geriye dönük doldur
        if (slices.Count == 0 && currentSlices > 0)
        {
            for (int i = 0; i < Mathf.Min(maxSlices, currentSlices); i++)
                slices.Add(liquidColor);
        }

        currentSlices = slices.Count;
        liquidColor = GetTopColor();
        UpdateVisuals();
    }

    public void InitializeSlices(List<Color> initSliceColors, Color fallbackColor, int initialCount)
    {
        slices.Clear();
        if (initSliceColors != null && initSliceColors.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(maxSlices, initSliceColors.Count); i++)
                slices.Add(initSliceColors[i]);
        }
        else if (initialCount > 0)
        {
            for (int i = 0; i < Mathf.Min(maxSlices, initialCount); i++)
                slices.Add(fallbackColor);
        }

        currentSlices = slices.Count;
        liquidColor = GetTopColor();
        UpdateVisuals();
    }

    public Color GetTopColor()
    {
        if (slices != null && slices.Count > 0)
            return slices[slices.Count - 1];
        return liquidColor;
    }

    public int GetContiguousTopCount()
    {
        if (slices == null || slices.Count == 0) return 0;
        Color top = slices[slices.Count - 1];
        int count = 1;
        for (int i = slices.Count - 2; i >= 0; i--)
        {
            if (ColorMixData.ColorsMatch(slices[i], top))
                count++;
            else
                break;
        }
        return count;
    }

    public bool IsMonochrome()
    {
        if (slices == null || slices.Count == 0) return true;
        Color first = slices[0];
        for (int i = 1; i < slices.Count; i++)
        {
            if (!ColorMixData.ColorsMatch(slices[i], first))
                return false;
        }
        return true;
    }

    public bool IsComplete()
    {
        return slices != null && slices.Count == maxSlices && IsMonochrome();
    }

    private static readonly float[] FILL_LEVELS =
    {
        0.17f, 0.28f, 0.39f, 0.50f
    };

    public float GetTargetFill()
    {
        int count = slices != null ? slices.Count : currentSlices;
        if (count <= 0) return 0.0f;

        int idx = Mathf.Clamp(count, 1, FILL_LEVELS.Length) - 1;
        return FILL_LEVELS[idx];
    }

    public void UpdateVisuals()
    {
        fillAmount = GetTargetFill();
        ApplyPropertyBlock();

        LiquidTilt tiltCode = GetComponent<LiquidTilt>();
        if (tiltCode != null) tiltCode.liquidMat = liquidMat;

        if (cork == null) cork = GetComponentInChildren<BottleCork>(true);
        if (cork == null && transform.parent != null) cork = transform.parent.GetComponentInChildren<BottleCork>(true);

        if (IsComplete() && cork != null)
        {
            cork.PlayCloseAnimation();
        }
    }

    public void ApplyPropertyBlock()
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        Color c0 = (slices != null && slices.Count > 0) ? slices[0] : Color.clear;
        Color c1 = (slices != null && slices.Count > 1) ? slices[1] : c0;
        Color c2 = (slices != null && slices.Count > 2) ? slices[2] : c1;
        Color c3 = (slices != null && slices.Count > 3) ? slices[3] : c2;

        Color topColor = GetTopColor();
        int count = slices != null ? slices.Count : currentSlices;

        foreach (Renderer r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_FillAmount", fillAmount);
            _propBlock.SetFloat("_Mode", 0f); // 0 = Y ekseni
            _propBlock.SetFloat("_SliceCount", count);

            // 4 bağımsız katmanın rengi (aşağıdan yukarıya)
            _propBlock.SetColor("_Color0", c0);
            _propBlock.SetColor("_Color1", c1);
            _propBlock.SetColor("_Color2", c2);
            _propBlock.SetColor("_Color3", c3);

            // Geriye dönük fallback
            _propBlock.SetColor("_LiquidColor", topColor);
            _propBlock.SetColor("_ColorA", topColor);

            FrozenBottle fb = GetComponentInParent<FrozenBottle>();
            _propBlock.SetFloat("_IsFrozen", (fb != null && fb.isFrozen) ? 1f : 0f);

            r.SetPropertyBlock(_propBlock);
        }
    }

    // ── MAGIC SORT SEÇİM (SELECT / DESELECT) ─────────────────────

    public void Select()
    {
        if (transferring || (slices != null && slices.Count <= 0)) return;

        FrozenBottle myFb = GetComponentInParent<FrozenBottle>();
        if (myFb != null && myFb.isFrozen)
        {
            myFb.PlayShakeFeedback();
            return;
        }

        if (SelectedBottle != null && SelectedBottle != this)
        {
            SelectedBottle.Deselect();
        }

        SelectedBottle = this;
        isSelected = true;

        Transform rootT = transform.parent != null ? transform.parent : transform;
        originalLocalPos = rootT.localPosition;
        originalLocalRot = rootT.localRotation;

        AudioManager.PlayPickup();
        VibrationManager.TryVibrate();

        rootT.DOKill();
        rootT.DOLocalMove(originalLocalPos + Vector3.up * 0.45f, 0.2f).SetEase(Ease.OutBack);

        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.SpawnGlowPulse(transform, GetTopColor());
        }
    }

    public void Deselect()
    {
        isSelected = false;
        if (SelectedBottle == this) SelectedBottle = null;

        Transform rootT = transform.parent != null ? transform.parent : transform;
        rootT.DOKill();
        rootT.DOLocalMove(originalLocalPos, 0.2f).SetEase(Ease.OutQuad);
        rootT.DOLocalRotateQuaternion(Quaternion.identity, 0.2f).SetEase(Ease.OutQuad);
    }

    public static void ClearSelection()
    {
        if (SelectedBottle != null)
        {
            SelectedBottle.Deselect();
        }
    }

    // ── KAPASİTE VE UYGUNLUK KONTROLÜ ─────────────────────────────

    public bool CanPourInto(LiquidTransfer target)
    {
        if (target == null || target == this) return false;
        if (this.transferring || target.transferring) return false;
        if (this.slices.Count <= 0) return false;

        // Donmuş şişeler sıvı alamaz veya veremez
        FrozenBottle myFb = GetComponentInParent<FrozenBottle>();
        if (myFb != null && myFb.isFrozen) return false;

        FrozenBottle targetFb = target.GetComponentInParent<FrozenBottle>();
        if (targetFb != null && targetFb.isFrozen) return false;

        // Hedef şişe zaten 4 dilimle tamamen doluysa dökülemez
        if (target.slices.Count >= target.maxSlices) return false;

        // Eğer bu şişe zaten 4/4 tam dolu ve tek renk ise (çözülmüş), bozulmasını önle
        // RENK UYUMLULUK KONTROLÜ (Water Sort Kuralı):
        // Hedef şişe ya BOMBOŞ olmalı, ya da en üstteki sıvının rengi dökülen sıvının rengiyle EŞİT olmalı!
        if (target.slices != null && target.slices.Count > 0)
        {
            Color myTopColor = this.GetTopColor();
            Color targetTopColor = target.GetTopColor();

            if (!ColorMixData.ColorsMatch(myTopColor, targetTopColor))
            {
                return false; // Farklı renkler birbirinin üzerine dökülemez!
            }
        }

        return true;
    }

    // ── DÖKÜLME VE AKTARIM (POUR INTO) ───────────────────────────

    public void PourInto(LiquidTransfer target, System.Action onComplete = null)
    {
        if (!CanPourInto(target)) return;

        transferring = true;
        target.transferring = true;

        isSelected = false;
        if (SelectedBottle == this) SelectedBottle = null;

        Transform mover = transform.parent != null ? transform.parent : transform;
        Transform receiver = target.transform.parent != null ? target.transform.parent : target.transform;

        Vector3 startPos = mover.position;
        Quaternion startRot = mover.rotation;

        // İksir şişesi ağzından dökülme konumu ve açısı
        bool pourFromLeft = mover.position.x <= receiver.position.x;
        float tiltAngle = pourFromLeft ? -75f : 75f;
        Quaternion pourRot = Quaternion.Euler(0, 0, tiltAngle);

        // Şişenin dünya ölçeği (transform.lossyScale.y ~ 1.35)
        float scale = mover.lossyScale.y > 0.01f ? mover.lossyScale.y : 1.35f;
        float tiltRad = Mathf.Abs(tiltAngle) * Mathf.Deg2Rad;

        // Hedef şişenin ağzının hafifçe üstü (Düz dik akıntı için Y yüksekliği)
        float targetSpoutY = 1.08f * scale;

        // Dökülen şişe 75° eğildiğinde ağzının kendi tabanına göre dünya offset'i
        float spoutOffsetX = Mathf.Sin(tiltRad) * (0.95f * scale);
        float spoutOffsetY = Mathf.Cos(tiltRad) * (0.95f * scale);

        // Şişe ağzının tam hedef şişe ağız merkezine hizalanması:
        float xOffset = pourFromLeft ? -spoutOffsetX : spoutOffsetX;
        float yOffset = targetSpoutY - spoutOffsetY;
        Vector3 pourPos = receiver.position + new Vector3(xOffset, yOffset, -0.10f);

        Color pourColor = this.GetTopColor();
        int contiguousTop = this.GetContiguousTopCount();
        int targetSpace = target.maxSlices - target.slices.Count;
        int takeAmount = Mathf.Clamp(Mathf.Min(targetSpace, contiguousTop), 1, 4);

        Sequence seq = DOTween.Sequence();
        seq.SetTarget(mover.gameObject);

        // 1. Şişe hedef şişenin ağzına uçar ve eğilir
        seq.Append(mover.DOMove(pourPos, 0.28f).SetEase(Ease.OutQuad));
        seq.Join(mover.DORotateQuaternion(pourRot, 0.28f).SetEase(Ease.OutQuad));

        // 2. Sıvı transferi
        seq.AppendCallback(() =>
        {
            AudioManager.PlayTransfer();
            VibrationManager.TryVibrate();
            GameManager.Instance?.RegisterMatch();

            // Kaynaktan takeAmount kadar en üstteki dilimi alıp hedefin üstüne ekle
            for (int k = 0; k < takeAmount; k++)
            {
                if (this.slices.Count > 0)
                    this.slices.RemoveAt(this.slices.Count - 1);
                target.slices.Add(pourColor);
            }

            this.currentSlices = this.slices.Count;
            this.liquidColor = this.GetTopColor();

            target.currentSlices = target.slices.Count;
            target.liquidColor = target.GetTopColor();

            float myTargetFill = this.GetTargetFill();
            float targetTargetFill = target.GetTargetFill();

            DOTween.To(() => this.fillAmount, x => this.fillAmount = x, myTargetFill, transferDuration)
                .SetTarget(this.gameObject)
                .OnUpdate(() => { if (this != null) this.ApplyPropertyBlock(); });

            DOTween.To(() => target.fillAmount, x => target.fillAmount = x, targetTargetFill, transferDuration)
                .SetTarget(target.gameObject)
                .OnUpdate(() => { if (target != null) target.ApplyPropertyBlock(); });

            // Sıvı Akış Efekti — Şişenin tam üstünden (0, targetSpoutY) dosdoğru aşağıya dökülen dik akıntı
            Vector3 sourceSpout = receiver.position + Vector3.up * targetSpoutY;
            Vector3 targetInside = receiver.position + Vector3.up * (0.45f * scale);
            LiquidStreamEffect.CreateStream(sourceSpout, targetInside, pourColor, transferDuration);

            if (EffectsManager.Instance != null)
            {
                EffectsManager.Instance.SpawnGlowPulse(target.transform, pourColor);
                EffectsManager.Instance.SpawnTransferParticles(sourceSpout, targetInside, pourColor, transferDuration);
            }
        });

        seq.AppendInterval(transferDuration);

        // 3. Şişe eski yerine döner ve doğrulur
        seq.Append(mover.DOMove(startPos, 0.25f).SetEase(Ease.InOutQuad));
        seq.Join(mover.DORotateQuaternion(startRot, 0.25f).SetEase(Ease.InOutQuad));

        seq.OnComplete(() =>
        {
            this.transferring = false;
            target.transferring = false;

            mover.localPosition = originalLocalPos;
            mover.localRotation = originalLocalRot;

            this.UpdateVisuals();
            target.UpdateVisuals();

            // Hedef tamamlandıysa (tam dolduysa ve tek renkse) tıpa (kapağı) kapat, kutlama partikülü at ve donmuş şişe sayacını azalt
            if (target.IsComplete())
            {
                BottleCork corkComp = target.cork;
                if (corkComp == null) corkComp = target.GetComponentInChildren<BottleCork>(true);
                if (corkComp == null && target.transform.parent != null) corkComp = target.transform.parent.GetComponentInChildren<BottleCork>(true);

                if (corkComp != null)
                {
                    corkComp.PlayCloseAnimation();
                }

                FrozenBottle.NotifyBottleCompleted();

                if (EffectsManager.Instance != null)
                {
                    EffectsManager.Instance.SpawnSnapParticles(target.transform.position, target.GetTopColor());
                    EffectsManager.Instance.SpawnSplash(target.transform.position, target.GetTopColor());
                }
            }

            CheckLevelComplete();
            onComplete?.Invoke();
        });
    }

    // ── ESKİ ÇAĞRILAR İÇİN UYUMLULUK STUB'LARI ──────────────────
    public void CheckSymmetry()
    {
        CheckLevelComplete();
    }

    public void StartTransfer(LiquidTransfer giver)
    {
        if (giver != null && giver.CanPourInto(this))
        {
            giver.PourInto(this);
        }
    }

    // ── BÖLÜM BİTİŞ KONTROLÜ ─────────────────────────────────────
    public void CheckLevelComplete()
    {
        DOVirtual.DelayedCall(0.15f, () =>
        {
            LiquidTransfer[] allLiquids = FindObjectsOfType<LiquidTransfer>();
            bool anyTransferring = false;
            bool hasIncompleteBottles = false;
            int totalCompletedBottles = 0;

            foreach (var lt in allLiquids)
            {
                if (lt == null || !lt.gameObject.activeInHierarchy) continue;
                if (lt.transferring) anyTransferring = true;

                if (lt.slices.Count > 0)
                {
                    // Şişe tamamen dolu (4/4) VE tek renk mi?
                    if (lt.IsComplete())
                    {
                        totalCompletedBottles++;
                    }
                    else
                    {
                        hasIncompleteBottles = true;
                    }
                }
            }

            if (anyTransferring) return;

            // Tüm sıvılar tek renkli ve tam dolu şişelerde toplandıysa
            if (!hasIncompleteBottles && totalCompletedBottles > 0)
            {
                if (GameManager.Instance != null && !GameManager.Instance.IsLevelCompleting)
                {
                    GameManager.Instance.LevelComplete();
                    return;
                }
            }

            // Hamle kalıp kalmadığını kontrol et
            FindObjectOfType<GridSpawner>()?.CheckForFail();
        });
    }
}