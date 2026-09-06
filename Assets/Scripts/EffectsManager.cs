using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class EffectsManager : MonoBehaviour
{
    private static EffectsManager _instance;
    public static EffectsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EffectsManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("EffectsManager");
                    _instance = go.AddComponent<EffectsManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Drag Glow")]
    public Color glowColor = new Color(1f, 1f, 1f, 0.4f);
    public float glowScale = 1.8f;

    [Header("Snap Particles")]
    public int snapParticleCount = 12;
    public float snapParticleSpeed = 2.5f;

    [Header("Transfer Particles")]
    public int transferParticleCount = 10;

    [Header("Splash")]
    public int splashParticleCount = 16;
    public float splashSpeed = 3.5f;

    private Sprite circleGlowSprite;
    private Sprite ringGlowSprite;
    private Material additiveMaterial;

    private Dictionary<Transform, GameObject> activeSelectionGlows = new Dictionary<Transform, GameObject>();

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAssets();
    }

    private void InitializeAssets()
    {
        if (circleGlowSprite == null) circleGlowSprite = CreateSoftCircleSprite();
        if (ringGlowSprite == null) ringGlowSprite = CreateRingSprite();

        if (additiveMaterial == null)
        {
            Shader glowShader = Shader.Find("Custom/GlowAdditive");
            if (glowShader == null) glowShader = Shader.Find("Mobile/Particles/Additive");
            if (glowShader == null) glowShader = Shader.Find("Sprites/Default");

            additiveMaterial = new Material(glowShader);
            if (additiveMaterial.HasProperty("_Intensity"))
                additiveMaterial.SetFloat("_Intensity", 2.2f);
        }
    }

    // ── Prosedürel Yumuşak Işık Halesi Sprite (128x128) ─────────────
    static Sprite CreateSoftCircleSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / radius;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * (3f - 2f * alpha);
                alpha = Mathf.Pow(alpha, 1.4f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ── Prosedürel Parlak Halka Sprite (128x128) ───────────────────
    static Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float ring = Mathf.Abs(dist - 0.70f);
                float alpha = Mathf.Clamp01(1f - ring * 4.5f);
                alpha = alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. SEÇİLİ ŞİŞE PARILDAYAN AURA EFEKTİ (SELECTION GLOW)
    // ═══════════════════════════════════════════════════════════════

    public void SpawnSelectionGlow(Transform target, Color color)
    {
        if (target == null) return;
        InitializeAssets();
        RemoveSelectionGlow(target);

        GameObject rootGlow = new GameObject("SelectionGlowAura");
        rootGlow.transform.SetParent(target, false);
        rootGlow.transform.localPosition = Vector3.zero;
        rootGlow.transform.localRotation = Quaternion.identity;

        // A) Masa Üstü Taban Halka Işığı (Ground Ring)
        GameObject groundRing = new GameObject("GroundGlowRing");
        groundRing.transform.SetParent(rootGlow.transform, false);
        groundRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        groundRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundRing.transform.localScale = Vector3.one * 1.5f;

        SpriteRenderer srRing = groundRing.AddComponent<SpriteRenderer>();
        srRing.sprite = ringGlowSprite;
        srRing.material = additiveMaterial;
        Color cRing = color; cRing.a = 0.85f;
        srRing.color = cRing;
        srRing.sortingOrder = 8;

        groundRing.transform.DOScale(Vector3.one * 1.75f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        // B) Şişe Gövde Arkası Parlak Halo Aura (Body Billboard Halo)
        GameObject bodyHalo = new GameObject("BodyHalo");
        bodyHalo.transform.SetParent(rootGlow.transform, false);
        bodyHalo.transform.localPosition = new Vector3(0f, 0.55f, 0.05f);
        bodyHalo.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);

        SpriteRenderer srHalo = bodyHalo.AddComponent<SpriteRenderer>();
        srHalo.sprite = circleGlowSprite;
        srHalo.material = additiveMaterial;
        Color cHalo = color; cHalo.a = 0.65f;
        srHalo.color = cHalo;
        srHalo.sortingOrder = 7;

        srHalo.DOFade(0.40f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        activeSelectionGlows[target] = rootGlow;
    }

    public void RemoveSelectionGlow(Transform target)
    {
        if (target == null) return;
        if (activeSelectionGlows.TryGetValue(target, out GameObject glow))
        {
            if (glow != null)
            {
                DOTween.Kill(glow.transform);
                SpriteRenderer[] renderers = glow.GetComponentsInChildren<SpriteRenderer>();
                foreach (var r in renderers)
                {
                    r.DOFade(0f, 0.2f);
                }
                Destroy(glow, 0.22f);
            }
            activeSelectionGlows.Remove(target);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. DRAG GLOW — Sürükleme Aurası
    // ═══════════════════════════════════════════════════════════════

    public GameObject CreateDragGlow(Transform target)
    {
        InitializeAssets();
        GameObject glow = new GameObject("DragGlow");
        glow.transform.SetParent(target, false);
        glow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * glowScale;

        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = circleGlowSprite;
        sr.material = additiveMaterial;
        sr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        sr.sortingOrder = 9;

        sr.DOColor(glowColor, 0.15f);
        glow.transform.DOScale(Vector3.one * glowScale * 1.15f, 0.7f)
            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        return glow;
    }

    public void DestroyDragGlow(GameObject glow)
    {
        if (glow == null) return;
        SpriteRenderer sr = glow.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            DOTween.Kill(glow.transform);
            sr.DOColor(new Color(sr.color.r, sr.color.g, sr.color.b, 0f), 0.15f)
                .OnComplete(() => Destroy(glow));
        }
        else
        {
            Destroy(glow);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. GLOW PULSE — Tıklama / Aktarım Parlaması
    // ═══════════════════════════════════════════════════════════════

    public void SpawnGlowPulse(Transform target, Color color)
    {
        if (target == null) return;
        InitializeAssets();

        GameObject glow = new GameObject("GlowPulse");
        glow.transform.SetParent(target, false);
        glow.transform.localPosition = new Vector3(0f, 0.55f, -0.05f);
        glow.transform.localScale = Vector3.one * 0.4f;

        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = circleGlowSprite;
        sr.material = additiveMaterial;
        Color c = color; c.a = 0.9f;
        sr.color = c;
        sr.sortingOrder = 12;

        Sequence seq = DOTween.Sequence();
        seq.Append(glow.transform.DOScale(new Vector3(1.8f, 2.2f, 1.8f), 0.35f).SetEase(Ease.OutCubic));
        seq.Join(sr.DOFade(0f, 0.35f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(glow));
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. TRANSFER PARTICLES — Sıvı Akışı Büyülü Işık Tanecikleri
    // ═══════════════════════════════════════════════════════════════

    public void SpawnTransferParticles(Vector3 from, Vector3 to, Color color, float duration)
    {
        InitializeAssets();
        int count = transferParticleCount;
        for (int i = 0; i < count; i++)
        {
            float delay = (duration / count) * i;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (this == null) return;
                float pSize = Random.Range(0.08f, 0.14f);
                GameObject p = CreateParticle(from, color, pSize, additiveMaterial);

                Vector3 mid = (from + to) * 0.5f + new Vector3(
                    Random.Range(-0.25f, 0.25f),
                    Random.Range(-0.1f, 0.25f),
                    Random.Range(-0.25f, 0.25f));

                Vector3[] path = { from, mid, to };
                p.transform.DOPath(path, duration * 0.55f, PathType.CatmullRom)
                    .SetEase(Ease.InOutQuad);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                sr.DOFade(0f, duration * 0.45f).SetDelay(duration * 0.15f)
                    .OnComplete(() => Destroy(p));
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. SPLASH — Dökülme Renkli Damlacıkları
    // ═══════════════════════════════════════════════════════════════

    public void SpawnSplash(Vector3 position, Color color)
    {
        InitializeAssets();
        for (int i = 0; i < splashParticleCount; i++)
        {
            float size = Random.Range(0.06f, 0.14f);
            GameObject p = CreateParticle(position, color, size, additiveMaterial);

            float angle = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(Random.Range(-30f, 30f), angle, 0f) * Vector3.forward;
            float speed = splashSpeed * Random.Range(0.4f, 1.1f);
            Vector3 targetPos = position + dir * speed * 0.35f + Vector3.up * Random.Range(0.2f, 0.6f);

            float dur = Random.Range(0.35f, 0.65f);
            p.transform.DOMove(targetPos, dur).SetEase(Ease.OutCubic);
            p.transform.DOScale(0f, dur).SetEase(Ease.InQuad);

            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            sr.DOFade(0f, dur * 0.8f).SetDelay(dur * 0.2f)
                .OnComplete(() => Destroy(p));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. SNAP PARTICLES & MATCH EXPLOSION — Şişe Tamamlanma Kutlaması
    // ═══════════════════════════════════════════════════════════════

    public void SpawnSnapParticles(Vector3 position, Color color)
    {
        InitializeAssets();
        for (int i = 0; i < snapParticleCount; i++)
        {
            float size = Random.Range(0.08f, 0.16f);
            GameObject p = CreateParticle(position + Vector3.up * 0.6f, color, size, additiveMaterial);

            float angle = (360f / snapParticleCount) * i + Random.Range(-15f, 15f);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            float speed = snapParticleSpeed * Random.Range(0.7f, 1.4f);

            p.transform.DOMove(position + Vector3.up * 0.6f + dir * speed * 0.4f + Vector3.up * Random.Range(0.2f, 0.8f), 0.5f).SetEase(Ease.OutCubic);
            p.transform.DOScale(0f, 0.5f).SetEase(Ease.InCubic);

            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            sr.DOFade(0f, 0.45f).OnComplete(() => Destroy(p));
        }
    }

    public void ShakeTransform(Transform target)
    {
        if (target == null) return;
        target.DOShakePosition(0.3f, 0.08f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic);
    }

    // ── Yardımcı: Parçacık Objesi Oluştur ─────────────────────────
    private GameObject CreateParticle(Vector3 pos, Color color, float size, Material mat = null)
    {
        GameObject p = new GameObject("GlowParticle");
        p.transform.position = pos;
        p.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
        sr.sprite = circleGlowSprite;
        if (mat != null) sr.material = mat;
        sr.color = color;
        sr.sortingOrder = 15;
        return p;
    }
}
