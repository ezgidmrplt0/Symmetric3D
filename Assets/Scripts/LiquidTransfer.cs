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

    private MaterialPropertyBlock _propBlock;
    private Renderer[] _renderers;
    private DragObject _parentDrag;

    [HideInInspector]
    public bool transferring = false;

    [HideInInspector]
    public float currentTiltX = 0f;

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

        if (label == null) label = GetComponentInChildren<BottleLabel>(true);
        if (label == null && transform.parent != null) label = transform.parent.GetComponentInChildren<BottleLabel>(true);

        if (IsComplete() && cork != null)
        {
            cork.PlayCloseAnimation();
        }

        if (IsComplete() && label != null)
        {
            label.ShowLabel(GetTopColor());
        }
    }

    public void ApplyPropertyBlock()
    {
        ApplyPropertyBlockWithSlices(this.slices, this.fillAmount, this.currentTiltX);
    }

    public void ApplyPropertyBlockWithSlices(List<Color> sliceList, float customFill, float tiltX = 0f)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        Color c0 = (sliceList != null && sliceList.Count > 0) ? sliceList[0] : Color.clear;
        Color c1 = (sliceList != null && sliceList.Count > 1) ? sliceList[1] : c0;
        Color c2 = (sliceList != null && sliceList.Count > 2) ? sliceList[2] : c1;
        Color c3 = (sliceList != null && sliceList.Count > 3) ? sliceList[3] : c2;

        Color topColor = (sliceList != null && sliceList.Count > 0) ? sliceList[sliceList.Count - 1] : liquidColor;
        int count = sliceList != null ? sliceList.Count : 0;

        foreach (Renderer r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_FillAmount", customFill);
            _propBlock.SetFloat("_Mode", 0f); // 0 = Y ekseni
            _propBlock.SetFloat("_SliceCount", count);
            _propBlock.SetFloat("_TiltX", tiltX);
            _propBlock.SetFloat("_TiltZ", 0f);

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

        TutorialManager.Instance?.OnBottleSelected(this);
    }

    public void Deselect()
    {
        isSelected = false;
        if (SelectedBottle == this) SelectedBottle = null;

        this.currentTiltX = 0f;
        ApplyPropertyBlock();

        Transform rootT = transform.parent != null ? transform.parent : transform;
        rootT.DOKill();
        rootT.DOLocalMove(originalLocalPos, 0.2f).SetEase(Ease.OutQuad);
        rootT.DOLocalRotateQuaternion(Quaternion.identity, 0.2f).SetEase(Ease.OutQuad);

        TutorialManager.Instance?.OnBottleDeselected();
    }

    public static void ClearSelection()
    {
        if (SelectedBottle != null)
        {
            SelectedBottle.Deselect();
            TutorialManager.Instance?.OnBottleDeselected();
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

        TutorialManager.Instance?.HideTutorial();

        transferring = true;
        target.transferring = true;

        isSelected = false;
        if (SelectedBottle == this) SelectedBottle = null;

        Transform mover = transform.parent != null ? transform.parent : transform;
        Transform receiver = target.transform.parent != null ? target.transform.parent : target.transform;

        Vector3 startPos = mover.position;
        Quaternion startRot = mover.rotation;

        Color pourColor = this.GetTopColor();
        int contiguousTop = this.GetContiguousTopCount();
        int targetSpace = target.maxSlices - target.slices.Count;
        int takeAmount = Mathf.Clamp(Mathf.Min(targetSpace, contiguousTop), 1, 4);

        // Geometri ve Kinematik
        bool pourFromLeft = mover.position.x <= receiver.position.x;
        float scale = mover.lossyScale.y > 0.01f ? mover.lossyScale.y : 1.35f;

        // Şişe ağzının local koordinatındaki akış dudağı (sıvının döküldüğü alt kenar)
        Vector3 localSpout = new Vector3(pourFromLeft ? 0.07f : -0.07f, 1.10f, 0f);

        // Hedef şişenin ağzının hafifçe üstü ve yanı (mesh çakışması olmadan tam dökme konumu)
        Vector3 mouthTargetWorld = receiver.position + new Vector3(
            pourFromLeft ? -0.20f * scale : 0.20f * scale,
            1.28f * scale,
            -0.06f
        );

        float initialTilt = pourFromLeft ? -68f : 68f;
        float deepTilt = pourFromLeft ? -78f : 78f;

        // Sıvının dökülen şişe içinde yerçekimine/ağza doğru çok hafif ve gerçekçi eğilmesi
        float initialLiquidTilt = pourFromLeft ? -0.14f : 0.14f;
        float deepLiquidTilt = pourFromLeft ? -0.17f : 0.17f;

        Quaternion initialPourRot = Quaternion.Euler(0, 0, initialTilt);
        Vector3 initialPourPos = mouthTargetWorld - (initialPourRot * (localSpout * scale));

        // Sıvı miktarına göre doğal süre (1 dilim ~0.50s, 2 dilim ~0.65s)
        float pourDuration = 0.48f + (takeAmount - 1) * 0.14f;

        // Seviye hesaplamaları
        float sourceStartFill = this.fillAmount;
        int sourceRemainingCount = Mathf.Max(0, this.slices.Count - takeAmount);
        float sourceTargetFill = (sourceRemainingCount <= 0) ? 0f : FILL_LEVELS[Mathf.Clamp(sourceRemainingCount, 1, FILL_LEVELS.Length) - 1];

        float targetStartFill = target.fillAmount;
        int targetFinalCount = target.slices.Count + takeAmount;
        float targetTargetFill = FILL_LEVELS[Mathf.Clamp(targetFinalCount, 1, FILL_LEVELS.Length) - 1];

        // Hedef şişe için transfer sırasında render edilecek önizleme renk listesi
        List<Color> targetPreviewSlices = new List<Color>(target.slices);
        for (int k = 0; k < takeAmount; k++)
        {
            targetPreviewSlices.Add(pourColor);
        }

        // Kaynak şişenin mevcut renkleri (boşaltma boyunca rengin korunması için)
        List<Color> sourceActiveSlices = new List<Color>(this.slices);

        Sequence seq = DOTween.Sequence();
        seq.SetTarget(mover.gameObject);

        // 1. Şişe hedef şişenin ağzına uçar ve ilk dökülme açısına eğilir (0.30s)
        seq.Append(mover.DOMove(initialPourPos, 0.30f).SetEase(Ease.OutQuad));
        seq.Join(mover.DORotateQuaternion(initialPourRot, 0.30f).SetEase(Ease.OutQuad));
        seq.Join(DOTween.To(() => this.currentTiltX, x =>
        {
            this.currentTiltX = x;
            if (this != null)
            {
                this.ApplyPropertyBlockWithSlices(sourceActiveSlices, this.fillAmount, this.currentTiltX);
            }
        }, initialLiquidTilt, 0.30f).SetEase(Ease.OutQuad));

        // 2. Sıvı transferi ve akıntı animasyonu
        seq.AppendCallback(() =>
        {
            AudioManager.PlayTransfer();
            VibrationManager.TryVibrate();
            GameManager.Instance?.RegisterMatch();

            // Hedef şişeyi yeni renk katmanıyla hazırlar (dik durduğu için tilt = 0f)
            target.ApplyPropertyBlockWithSlices(targetPreviewSlices, targetStartFill, 0f);

            // Akıntı efekti oluştur (şişe ağzından hedef sıvı yüzeyine)
            Vector3 initialTargetInside = new Vector3(0f, Mathf.Max(0.12f, targetStartFill), 0f);
            LiquidStreamEffect stream = LiquidStreamEffect.CreateStream(
                mover, localSpout,
                receiver, initialTargetInside,
                pourColor, pourDuration + 0.08f);

            // Akıntı ucu hedef şişede yükselen sıvı yüzeyini dinamik takip eder
            if (stream != null)
            {
                stream.dynamicStartPosition = () =>
                {
                    return mover != null ? mover.TransformPoint(localSpout) : mouthTargetWorld;
                };

                stream.dynamicEndPosition = () =>
                {
                    if (target != null && receiver != null)
                    {
                        float surfaceY = Mathf.Max(0.12f, target.fillAmount * 1.02f);
                        return receiver.TransformPoint(new Vector3(0f, surfaceY, 0f));
                    }
                    return receiver != null ? receiver.position : mouthTargetWorld;
                };
            }

            // Dökülen şişenin döküldükçe hafifçe daha da eğilmesi (Spout Pivot Kinematics)
            float tiltProgress = initialTilt;
            DOTween.To(() => tiltProgress, a =>
            {
                tiltProgress = a;
                if (mover != null)
                {
                    mover.rotation = Quaternion.Euler(0, 0, a);
                    mover.position = mouthTargetWorld - (mover.rotation * (localSpout * scale));
                }
            }, deepTilt, pourDuration).SetTarget(mover.gameObject).SetEase(Ease.InOutSine);

            // Dökülen şişedeki sıvının eğiminin akış süresince hafifçe artması
            DOTween.To(() => this.currentTiltX, x =>
            {
                this.currentTiltX = x;
                if (this != null)
                {
                    this.ApplyPropertyBlockWithSlices(sourceActiveSlices, this.fillAmount, this.currentTiltX);
                }
            }, deepLiquidTilt, pourDuration).SetTarget(this.gameObject).SetEase(Ease.InOutSine);

            // Kaynak şişenin sıvısının boşalması
            DOTween.To(() => this.fillAmount, x =>
            {
                this.fillAmount = x;
                if (this != null)
                {
                    this.ApplyPropertyBlockWithSlices(sourceActiveSlices, this.fillAmount, this.currentTiltX);
                }
            }, sourceTargetFill, pourDuration)
            .SetTarget(this.gameObject)
            .SetEase(Ease.InOutSine);

            // Hedef şişenin sıvısının yükselmesi (dik durduğu için tilt 0f)
            float fillDelay = 0.08f;
            float fillRiseDuration = Mathf.Max(0.15f, pourDuration - fillDelay);
            DOTween.To(() => target.fillAmount, x =>
            {
                target.fillAmount = x;
                if (target != null)
                {
                    target.ApplyPropertyBlockWithSlices(targetPreviewSlices, target.fillAmount, 0f);
                }
            }, targetTargetFill, fillRiseDuration)
            .SetTarget(target.gameObject)
            .SetDelay(fillDelay)
            .SetEase(Ease.InOutSine);
        });

        seq.AppendInterval(pourDuration + 0.10f);

        // 3. Şişe eski yerine döner ve doğrulur (0.28s)
        seq.Append(mover.DOMove(startPos, 0.28f).SetEase(Ease.InOutQuad));
        seq.Join(mover.DORotateQuaternion(startRot, 0.28f).SetEase(Ease.InOutQuad));
        seq.Join(DOTween.To(() => this.currentTiltX, x =>
        {
            this.currentTiltX = x;
            if (this != null)
            {
                this.ApplyPropertyBlockWithSlices(this.slices, this.fillAmount, this.currentTiltX);
            }
        }, 0f, 0.28f).SetEase(Ease.InOutQuad));

        seq.OnComplete(() =>
        {
            this.transferring = false;
            target.transferring = false;
            this.currentTiltX = 0f;
            target.currentTiltX = 0f;

            mover.localPosition = originalLocalPos;
            mover.localRotation = originalLocalRot;

            // Gerçek dilim listelerini kalıcı olarak güncelle
            for (int k = 0; k < takeAmount; k++)
            {
                if (this.slices.Count > 0)
                    this.slices.RemoveAt(this.slices.Count - 1);
                target.slices.Add(pourColor);
            }

            this.currentSlices = this.slices.Count;
            this.liquidColor = this.GetTopColor();
            this.fillAmount = this.GetTargetFill();

            target.currentSlices = target.slices.Count;
            target.liquidColor = target.GetTopColor();
            target.fillAmount = target.GetTargetFill();

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

                BottleLabel labelComp = target.label;
                if (labelComp == null) labelComp = target.GetComponentInChildren<BottleLabel>(true);
                if (labelComp == null && target.transform.parent != null) labelComp = target.transform.parent.GetComponentInChildren<BottleLabel>(true);

                if (labelComp != null)
                {
                    labelComp.ShowLabel(target.GetTopColor());
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