using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

/// <summary>
/// Magic Sort — Buzlu Cam Şişe Mekaniği (Ultra Belirgin Yazı & Premium Kristal Dondurma Efekti).
/// Şişe donmuşken kilitlidir; sahnedeki diğer şişeler tek renkle tamamlandıkça sayacı düşer ve 0 olunca kristal patlamasıyla erir.
/// </summary>
public class FrozenBottle : MonoBehaviour
{
    public static List<FrozenBottle> activeFrozenBottles = new List<FrozenBottle>();

    [Header("Buz Durumu")]
    public bool isFrozen = false;
    public int requiredMatches = 2;
    public int remainingMatches = 2;

    [Header("Görsel Bileşenler")]
    private MeshRenderer outerGlassRenderer;
    private MeshRenderer innerLiquidRenderer;
    private Material originalGlassMat;
    private static Material sharedIceMat;

    // Rozet Hiyerarşisi
    private GameObject badgeRoot;
    private GameObject bottleFrostAura;
    private SpriteRenderer haloRenderer;
    private SpriteRenderer medallionRenderer;
    private SpriteRenderer lockRenderer;
    private TextMeshPro counterText;
    private Tween idleFloatTween;
    private Tween idlePulseTween;

    // Önbelleğe alınmış prosedürel spritelar (Tüm şişeler paylaşır, performanslı ve bağımsızdır)
    private static Sprite s_MedallionSprite;
    private static Sprite s_LockSprite;
    private static Sprite s_AuraSprite;

    private MaterialPropertyBlock _liquidPropBlock;

    private void Awake()
    {
        if (!activeFrozenBottles.Contains(this))
            activeFrozenBottles.Add(this);
    }

    private void OnDestroy()
    {
        activeFrozenBottles.Remove(this);
        KillTweens();
    }

    private void KillTweens()
    {
        if (idleFloatTween != null) idleFloatTween.Kill();
        if (idlePulseTween != null) idlePulseTween.Kill();
    }

    /// <summary>
    /// Şişeyi donmuş buzlu cam olarak başlatır.
    /// </summary>
    public void Initialize(int matches)
    {
        if (matches <= 0) matches = 1;
        requiredMatches = matches;
        remainingMatches = matches;
        isFrozen = true;

        if (!activeFrozenBottles.Contains(this))
            activeFrozenBottles.Add(this);

        SetupGlassAndLiquidMaterials();
        CreateBottleFrostAura();
        CreateBadgeVisual();
    }

    // ──────────────────────────────────────────────────────────────
    // 1. BUZ VE SIVI MATERYAL KURULUMU
    // ──────────────────────────────────────────────────────────────

    private void SetupGlassAndLiquidMaterials()
    {
        // Dış cam renderer'ı kök MainObject üzerinde yer alır
        outerGlassRenderer = GetComponent<MeshRenderer>();
        if (outerGlassRenderer == null)
            outerGlassRenderer = GetComponentInChildren<MeshRenderer>();

        if (outerGlassRenderer != null)
        {
            originalGlassMat = outerGlassRenderer.sharedMaterial;

            if (sharedIceMat == null)
            {
                Shader iceShader = Shader.Find("Custom/HypercasualIceShader");
                if (iceShader == null) iceShader = Shader.Find("Custom/HypercasualCrispGlass");
                if (iceShader == null) iceShader = Shader.Find("Standard");

                sharedIceMat = new Material(iceShader);
                sharedIceMat.name = "RuntimeFrostedGlassMat";

                // Kristal buz parametreleri (canlı, parlak, donuk kristal)
                if (sharedIceMat.HasProperty("_Color"))
                    sharedIceMat.SetColor("_Color", new Color(0.70f, 0.94f, 1.0f, 0.58f));
                if (sharedIceMat.HasProperty("_DeepColor"))
                    sharedIceMat.SetColor("_DeepColor", new Color(0.12f, 0.55f, 0.92f, 0.85f));
                if (sharedIceMat.HasProperty("_RimColor"))
                    sharedIceMat.SetColor("_RimColor", new Color(0.95f, 1.0f, 1.0f, 1.0f));
                if (sharedIceMat.HasProperty("_RimPower"))
                    sharedIceMat.SetFloat("_RimPower", 1.5f);
                if (sharedIceMat.HasProperty("_FrostStrength"))
                    sharedIceMat.SetFloat("_FrostStrength", 0.65f);
                if (sharedIceMat.HasProperty("_CrackScale"))
                    sharedIceMat.SetFloat("_CrackScale", 6.8f);
                if (sharedIceMat.HasProperty("_Shininess"))
                    sharedIceMat.SetFloat("_Shininess", 0.85f);
            }

            outerGlassRenderer.material = sharedIceMat;
        }

        // İç sıvı renderer'ı
        LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            innerLiquidRenderer = lt.GetComponent<MeshRenderer>();
            if (innerLiquidRenderer != null)
            {
                if (_liquidPropBlock == null) _liquidPropBlock = new MaterialPropertyBlock();
                innerLiquidRenderer.GetPropertyBlock(_liquidPropBlock);
                _liquidPropBlock.SetFloat("_IsFrozen", 1f);
                innerLiquidRenderer.SetPropertyBlock(_liquidPropBlock);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 2. ŞİŞE ETRAFINDAKİ SOĞUK BUZ AURASI (COLD VAPOR HALO)
    // ──────────────────────────────────────────────────────────────

    private void CreateBottleFrostAura()
    {
        if (bottleFrostAura != null) Destroy(bottleFrostAura);

        if (s_AuraSprite == null) s_AuraSprite = CreateAuraDiscSprite(128);

        bottleFrostAura = new GameObject("BottleFrostAura");
        bottleFrostAura.transform.SetParent(transform, false);
        bottleFrostAura.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        bottleFrostAura.transform.localRotation = Quaternion.identity;
        bottleFrostAura.transform.localScale = Vector3.one * 1.35f;

        SpriteRenderer sr = bottleFrostAura.AddComponent<SpriteRenderer>();
        sr.sprite = s_AuraSprite;
        sr.color = new Color(0.35f, 0.88f, 1.0f, 0.22f);
        sr.sortingOrder = -2;

        // Soğuk buhar nefes alma animasyonu
        bottleFrostAura.transform.DOScale(Vector3.one * 1.48f, 1.6f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
        sr.DOFade(0.14f, 1.6f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. ULTRA BELİRGİN ROZET VE SAYAÇ METNİ (MEDALLION & NUMBER)
    // ──────────────────────────────────────────────────────────────

    private void CreateBadgeVisual()
    {
        if (badgeRoot != null) Destroy(badgeRoot);

        // Rozet Kökü (Şişe yüzeyinin net olarak önünde, z-fighting/clipping olmadan durur)
        badgeRoot = new GameObject("FrostedBadgeRoot");
        badgeRoot.transform.SetParent(transform, false);
        // Parent MainObject scale = 0.5 olduğundan, -0.68f local Z = -0.34f world Z (küre yüzeyi -0.25f'tir)
        badgeRoot.transform.localPosition = new Vector3(0f, 0.06f, -0.68f);
        badgeRoot.transform.localRotation = Quaternion.identity;
        badgeRoot.transform.localScale = Vector3.zero;

        // Prosedürel Spriteları hazırla
        if (s_AuraSprite == null) s_AuraSprite = CreateAuraDiscSprite(128);
        if (s_MedallionSprite == null) s_MedallionSprite = CreateMedallionSprite(128);
        if (s_LockSprite == null) s_LockSprite = CreateLockSprite(64);

        // A. Arka Işık Halesi (Soft Cyan Halo)
        GameObject haloObj = new GameObject("AuraHalo");
        haloObj.transform.SetParent(badgeRoot.transform, false);
        haloObj.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        haloObj.transform.localScale = Vector3.one * 0.95f;
        haloRenderer = haloObj.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = s_AuraSprite;
        haloRenderer.color = new Color(0.25f, 0.88f, 1.0f, 0.50f);
        haloRenderer.sortingOrder = 58;

        // B. Madalyon Gövdesi (Koyu Lacivert Kontrast Taban + Neon Buz Çift Çemberi)
        GameObject medallionObj = new GameObject("MedallionPlate");
        medallionObj.transform.SetParent(badgeRoot.transform, false);
        medallionObj.transform.localPosition = Vector3.zero;
        medallionObj.transform.localScale = Vector3.one * 0.72f;
        medallionRenderer = medallionObj.AddComponent<SpriteRenderer>();
        medallionRenderer.sprite = s_MedallionSprite;
        medallionRenderer.color = Color.white;
        medallionRenderer.sortingOrder = 60;

        // C. Parlak Kristal Kilit İkonu (Madalyonun üst yarısında)
        GameObject lockObj = new GameObject("LockIcon");
        lockObj.transform.SetParent(badgeRoot.transform, false);
        lockObj.transform.localPosition = new Vector3(0f, 0.12f, -0.02f);
        lockObj.transform.localScale = Vector3.one * 0.28f;
        lockRenderer = lockObj.AddComponent<SpriteRenderer>();
        lockRenderer.sprite = s_LockSprite;
        lockRenderer.color = new Color(0.85f, 0.98f, 1.0f, 1.0f);
        lockRenderer.sortingOrder = 62;

        // D. Ultra Belirgin Sayaç Numarası (Madalyonun alt yarısında, dev ve kalın)
        GameObject textObj = new GameObject("CounterText");
        textObj.transform.SetParent(badgeRoot.transform, false);
        textObj.transform.localPosition = new Vector3(0f, -0.10f, -0.04f);
        textObj.transform.localScale = Vector3.one * 0.26f;

        counterText = textObj.AddComponent<TextMeshPro>();
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.fontSize = 12.0f;
        counterText.fontStyle = FontStyles.Bold;
        counterText.color = Color.white;
        counterText.outlineWidth = 0.38f;
        counterText.outlineColor = new Color32(2, 8, 20, 255); // Koyu lacivert/siyah net kontur
        counterText.sortingOrder = 64;
        counterText.text = remainingMatches.ToString();

        // Giriş Pop Animasyonu
        badgeRoot.transform.DOScale(1f, 0.40f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            StartIdleAnimations();
        });
    }

    private void StartIdleAnimations()
    {
        if (badgeRoot == null) return;

        KillTweens();

        // Hafif havada süzülme (bobbing)
        idleFloatTween = badgeRoot.transform.DOLocalMoveY(0.06f + 0.035f, 1.4f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);

        // Arka ışık halesinin yumuşak nabız atışı
        if (haloRenderer != null)
        {
            idlePulseTween = haloRenderer.transform.DOScale(Vector3.one * 1.08f, 1.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 4. PROSEDÜREL SPRITE ÜRETECİLERİ (YÜKSEK ÇÖZÜNÜRLÜK & KONTRAST)
    // ──────────────────────────────────────────────────────────────

    private static Sprite CreateMedallionSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 4f;
        float innerRadius = radius - 5f;
        float silverRadius = innerRadius - 2.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                if (dist <= silverRadius)
                {
                    // İç kısım: Çok yüksek kontrast sağlayan derin lacivert zemin
                    float t = dist / silverRadius;
                    Color coreColor = Color.Lerp(new Color(0.06f, 0.15f, 0.28f, 0.96f), new Color(0.02f, 0.06f, 0.14f, 0.96f), t);
                    tex.SetPixel(x, y, coreColor);
                }
                else if (dist <= innerRadius)
                {
                    // İç gümüş/buz halkası
                    tex.SetPixel(x, y, new Color(0.85f, 0.97f, 1.0f, 0.98f));
                }
                else if (dist <= radius)
                {
                    // Dış neon buz çemberi
                    tex.SetPixel(x, y, new Color(0.30f, 0.92f, 1.0f, 1.0f));
                }
                else
                {
                    // Kenar yumuşatma (Anti-aliasing)
                    float aa = Mathf.Clamp01(1f - (dist - radius) / 2.5f);
                    tex.SetPixel(x, y, new Color(0.30f, 0.92f, 1.0f, aa));
                }
            }
        }

        // 4 Ana Yöne Kristal Pırlanta Noktaları ekle
        DrawDiamondStud(tex, (int)center, (int)(center + radius - 1), 3);
        DrawDiamondStud(tex, (int)center, (int)(center - radius + 1), 3);
        DrawDiamondStud(tex, (int)(center + radius - 1), (int)center, 3);
        DrawDiamondStud(tex, (int)(center - radius + 1), (int)center, 3);

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    private static void DrawDiamondStud(Texture2D tex, int cx, int cy, int rad)
    {
        for (int dy = -rad; dy <= rad; dy++)
        {
            for (int dx = -rad; dx <= rad; dx++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= rad)
                {
                    int px = Mathf.Clamp(cx + dx, 0, tex.width - 1);
                    int py = Mathf.Clamp(cy + dy, 0, tex.height - 1);
                    tex.SetPixel(px, py, new Color(1f, 1f, 1f, 1f));
                }
            }
        }
    }

    private static Sprite CreateLockSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        Color lockCyan = new Color(0.55f, 0.93f, 1.0f, 1.0f);
        Color lockHighlight = new Color(0.92f, 0.99f, 1.0f, 1.0f);
        Color keyholeNavy = new Color(0.03f, 0.09f, 0.18f, 1.0f);

        // 1. Kilit Kemeri (Shackle - Inverted U-Arch)
        Vector2 archCenter = new Vector2(32, 37);
        float outerArchR = 13.5f;
        float innerArchR = 7.5f;

        for (int y = 24; y <= 52; y++)
        {
            for (int x = 18; x <= 46; x++)
            {
                if (y >= 37)
                {
                    float d = Vector2.Distance(new Vector2(x, y), archCenter);
                    if (d <= outerArchR && d >= innerArchR)
                    {
                        tex.SetPixel(x, y, (x < 32) ? lockHighlight : lockCyan);
                    }
                }
                else
                {
                    // Bacaklar
                    bool inLeftLeg = (x >= 18 && x <= 24);
                    bool inRightLeg = (x >= 40 && x <= 46);
                    if (inLeftLeg) tex.SetPixel(x, y, lockHighlight);
                    if (inRightLeg) tex.SetPixel(x, y, lockCyan);
                }
            }
        }

        // 2. Kilit Gövdesi (Body - Yuvarlatılmış Kutu)
        for (int y = 11; y <= 33; y++)
        {
            for (int x = 16; x <= 48; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 32) - 13);
                float dy = Mathf.Max(0, Mathf.Abs(y - 22) - 8);
                if (dx * dx + dy * dy <= 12f)
                {
                    Color c = (y > 22) ? lockHighlight : lockCyan;
                    tex.SetPixel(x, y, c);
                }
            }
        }

        // 3. Anahtar Deliği (Keyhole)
        for (int y = 16; y <= 25; y++)
        {
            for (int x = 30; x <= 34; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 23));
                if (d <= 2.8f || (y >= 17 && y <= 21 && Mathf.Abs(x - 32) <= 1.5f))
                {
                    tex.SetPixel(x, y, keyholeNavy);
                }
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    private static Sprite CreateAuraDiscSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.SmoothStep(1f, 0f, t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. ETKİLEŞİM GERİ BİLDİRİMLERİ (SHAKE / PUNCH / DUST)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Oyuncu donmuş şişeye tıkladığında kilitli olduğunu hissettiren titreme, parıltı ve buz tozu efekti.
    /// </summary>
    public void PlayShakeFeedback()
    {
        VibrationManager.TryVibrate();
        AudioManager.PlayPickup();

        transform.DOKill(true);
        transform.DOShakePosition(0.28f, new Vector3(0.10f, 0.02f, 0f), 20, 90, false, true);

        if (badgeRoot != null)
        {
            badgeRoot.transform.DOKill(true);
            badgeRoot.transform.DOPunchScale(Vector3.one * 0.35f, 0.28f, 4, 0.5f).OnComplete(StartIdleAnimations);
        }

        // Buz tozu patlaması
        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.SpawnSnapParticles(transform.position, new Color(0.75f, 0.95f, 1.0f));
        }
    }

    /// <summary>
    /// Sahnedeki herhangi bir şişe tek renkle 4/4 tamamlandığında çağrılır.
    /// </summary>
    public static void NotifyBottleCompleted()
    {
        List<FrozenBottle> list = new List<FrozenBottle>(activeFrozenBottles);
        foreach (var fb in list)
        {
            if (fb != null && fb.isFrozen)
            {
                fb.OnOneMatchAchieved();
            }
        }
    }

    /// <summary>
    /// Bir şişe tamamlandığında sayacı 1 düşürür, çatlama sesi ve elastik sayı pop animasyonu uygular.
    /// </summary>
    public void OnOneMatchAchieved()
    {
        if (!isFrozen) return;

        remainingMatches--;

        if (remainingMatches > 0)
        {
            VibrationManager.TryVibrate();
            AudioManager.PlayTransfer();

            // Sayı elastik pop animasyonuyla güncellenir
            if (counterText != null)
            {
                counterText.transform.DOKill(true);
                counterText.transform.DOScale(Vector3.one * 0.38f, 0.12f).OnComplete(() =>
                {
                    counterText.text = remainingMatches.ToString();
                    counterText.transform.DOScale(Vector3.one * 0.26f, 0.25f).SetEase(Ease.OutBounce);
                });
            }

            if (badgeRoot != null)
            {
                badgeRoot.transform.DOKill(true);
                badgeRoot.transform.DOPunchScale(Vector3.one * 0.45f, 0.35f, 4, 0.5f).OnComplete(StartIdleAnimations);
            }

            // Buz çatlağı parçacığı
            if (EffectsManager.Instance != null)
            {
                EffectsManager.Instance.SpawnSnapParticles(transform.position, new Color(0.65f, 0.95f, 1.0f));
            }
        }
        else
        {
            MeltAndDefrost();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 6. ERİME VE ÇÖZÜLME SÜRECİ (MUHTEŞEM KRİSTAL PATLAMASI)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sayaç 0 olduğunda buzun kırılması, rozetin patlaması ve camın/sıvının pürüzsüzce çözülme süreci.
    /// </summary>
    public void MeltAndDefrost()
    {
        isFrozen = false;
        activeFrozenBottles.Remove(this);
        KillTweens();

        AudioManager.PlayTransfer();
        VibrationManager.VibrateSuccess();

        // 1. Şiddetli Çatlama Titremesi
        transform.DOShakePosition(0.25f, new Vector3(0.08f, 0.08f, 0f), 25, 90, false, true);

        // 2. Rozet Patlaması (Önce büyür sonra sıfıra fırlar)
        if (badgeRoot != null)
        {
            badgeRoot.transform.DOKill(true);
            badgeRoot.transform.DOScale(1.35f, 0.15f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                badgeRoot.transform.DOScale(0f, 0.20f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    if (badgeRoot != null) Destroy(badgeRoot);
                });
            });
        }

        // 3. Devasa Kristal Patlaması ve Sıçrama Parçacıkları
        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.SpawnSnapParticles(transform.position, new Color(0.85f, 0.98f, 1.0f));
            EffectsManager.Instance.SpawnSplash(transform.position, new Color(0.65f, 0.92f, 1.0f));
        }

        // 4. Soğuk buhar aurasını yavaşça yok et
        if (bottleFrostAura != null)
        {
            SpriteRenderer auraSr = bottleFrostAura.GetComponent<SpriteRenderer>();
            if (auraSr != null)
            {
                auraSr.DOFade(0f, 0.45f).OnComplete(() =>
                {
                    if (bottleFrostAura != null) Destroy(bottleFrostAura);
                });
            }
            else
            {
                Destroy(bottleFrostAura);
            }
        }

        // 5. Sıvının Çözülmesi (MaterialPropertyBlock _IsFrozen değerini 1 -> 0 pürüzsüz indirir)
        if (innerLiquidRenderer != null)
        {
            DOVirtual.Float(1f, 0f, 0.50f, val =>
            {
                if (innerLiquidRenderer != null)
                {
                    if (_liquidPropBlock == null) _liquidPropBlock = new MaterialPropertyBlock();
                    innerLiquidRenderer.GetPropertyBlock(_liquidPropBlock);
                    _liquidPropBlock.SetFloat("_IsFrozen", val);
                    innerLiquidRenderer.SetPropertyBlock(_liquidPropBlock);
                }
            });
        }

        // 6. Cam Materyalini Orijinal Saydam Cama Döndür
        if (outerGlassRenderer != null && originalGlassMat != null)
        {
            DOVirtual.DelayedCall(0.35f, () =>
            {
                if (outerGlassRenderer != null)
                    outerGlassRenderer.material = originalGlassMat;
            });
        }

        // 7. Sevinç Zıplaması
        transform.DOPunchScale(new Vector3(0.18f, 0.22f, 0.18f), 0.55f, 4, 0.4f);

        // 8. Çözüldükten sonra hamle veya kazanma durumunu tekrar kontrol et
        LiquidTransfer lt = GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            lt.CheckLevelComplete();
        }
    }
}
