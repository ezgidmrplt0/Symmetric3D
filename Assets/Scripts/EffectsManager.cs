using UnityEngine;
using DG.Tweening;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    [Header("Drag Glow")]
    public Color glowColor = new Color(1f, 1f, 1f, 0.25f);
    public float glowScale = 1.8f;

    [Header("Snap Particles")]
    public int snapParticleCount = 8;
    public float snapParticleSpeed = 2f;

    [Header("Transfer Particles")]
    public int transferParticleCount = 6;

    [Header("Splash")]
    public int splashParticleCount = 12;
    public float splashSpeed = 3.5f;

    private Sprite circleSprite;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        circleSprite = CreateCircleSprite();
    }

    // ── Prosedürel beyaz daire sprite ─────────────────────────────
    static Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. DRAG GLOW — sürüklerken parçanın altında yumuşak ışık
    // ═══════════════════════════════════════════════════════════════

    public GameObject CreateDragGlow(Transform target)
    {
        GameObject glow = new GameObject("DragGlow");
        glow.transform.SetParent(target, false);
        glow.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        glow.transform.localScale = Vector3.one * glowScale;
        glow.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        sr.sortingOrder = -1;

        sr.DOColor(glowColor, 0.15f);
        glow.transform.DOScale(Vector3.one * glowScale * 1.1f, 0.8f)
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
    // 2. SNAP PARTICLES — yerine oturunca parçacık patlaması
    // ═══════════════════════════════════════════════════════════════

    public void SpawnSnapParticles(Vector3 position, Color color)
    {
        for (int i = 0; i < snapParticleCount; i++)
        {
            GameObject p = CreateParticle(position, color, 0.08f);
            float angle = (360f / snapParticleCount) * i + Random.Range(-15f, 15f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            float speed = snapParticleSpeed * Random.Range(0.7f, 1.3f);

            p.transform.DOMove(position + dir * speed * 0.3f, 0.4f).SetEase(Ease.OutCubic);
            p.transform.DOScale(0f, 0.4f).SetEase(Ease.InCubic);

            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            sr.DOFade(0f, 0.35f).OnComplete(() => Destroy(p));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. WRONG PLACE SHAKE — yanlış yere bırakınca titreşim
    // ═══════════════════════════════════════════════════════════════

    public void ShakeTransform(Transform target)
    {
        target.DOShakePosition(0.3f, 0.08f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. TRANSFER PARTICLES — sıvı akışı sırasında parçacıklar
    // ═══════════════════════════════════════════════════════════════

    public void SpawnTransferParticles(Vector3 from, Vector3 to, Color color, float duration)
    {
        int count = transferParticleCount;
        for (int i = 0; i < count; i++)
        {
            float delay = (duration / count) * i;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (this == null) return;
                GameObject p = CreateParticle(from, color, 0.06f);
                Vector3 mid = (from + to) * 0.5f + new Vector3(
                    Random.Range(-0.15f, 0.15f),
                    Random.Range(-0.15f, 0.15f),
                    -0.2f);

                Vector3[] path = { from, mid, to };
                p.transform.DOPath(path, duration * 0.5f, PathType.CatmullRom)
                    .SetEase(Ease.InOutSine);
                p.transform.DOScale(Random.Range(0.03f, 0.08f), duration * 0.3f)
                    .SetDelay(duration * 0.2f)
                    .SetEase(Ease.InCubic);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                sr.DOFade(0f, duration * 0.4f).SetDelay(duration * 0.3f)
                    .OnComplete(() => Destroy(p));
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. SPLASH — dolu parça patlarken renkli damlacıklar
    // ═══════════════════════════════════════════════════════════════

    public void SpawnSplash(Vector3 position, Color color)
    {
        for (int i = 0; i < splashParticleCount; i++)
        {
            float size = Random.Range(0.04f, 0.1f);
            GameObject p = CreateParticle(position, color, size);

            float angle = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            float speed = splashSpeed * Random.Range(0.5f, 1.2f);
            Vector3 target = position + dir * speed * 0.4f;
            target.z = position.z - 0.1f;

            float dur = Random.Range(0.3f, 0.6f);
            p.transform.DOMove(target, dur).SetEase(Ease.OutCubic);
            p.transform.DOScale(0f, dur).SetEase(Ease.InQuad);

            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            sr.DOFade(0f, dur * 0.8f).SetDelay(dur * 0.2f)
                .OnComplete(() => Destroy(p));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. GLOW PULSE — renk eşleşince kısa parlama
    // ═══════════════════════════════════════════════════════════════

    public void SpawnGlowPulse(Transform target, Color color)
    {
        if (target == null) return;
        GameObject glow = new GameObject("GlowPulse");
        glow.transform.SetParent(target, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        glow.transform.localScale = Vector3.one * 0.5f;

        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        Color c = color;
        c.a = 0.6f;
        sr.color = c;
        sr.sortingOrder = 5;

        Sequence seq = DOTween.Sequence();
        seq.Append(glow.transform.DOScale(Vector3.one * 2.5f, 0.3f).SetEase(Ease.OutCubic));
        seq.Join(sr.DOFade(0f, 0.3f).SetEase(Ease.InCubic));
        seq.OnComplete(() => Destroy(glow));
    }

    // ── Yardımcı: tek parçacık oluştur ───────────────────────────
    GameObject CreateParticle(Vector3 pos, Color color, float size)
    {
        GameObject p = new GameObject("Particle");
        p.transform.position = pos;
        p.transform.localScale = Vector3.one * size;
        SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = color;
        sr.sortingOrder = 10;
        return p;
    }
}
