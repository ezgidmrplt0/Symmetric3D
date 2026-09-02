using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class FrozenGridCell : MonoBehaviour
{
    public static List<FrozenGridCell> activeFrozenCells = new List<FrozenGridCell>();

    [Header("Buz Durumu")]
    public Vector2Int gridPosition;
    public int faceIndex = 0;
    public int requiredMatches = 3;
    public int remainingMatches = 3;
    public bool isDefrosted = false;

    [Header("Referanslar")]
    public DragObject frozenPiece;

    private GameObject iceVisualRoot;
    private TextMeshPro counterText;

    private static Material sharedIceMaterial;
    private static Sprite circularBadgeSprite;
    private static Sprite circularGlowSprite;

    private void Awake()
    {
        if (!activeFrozenCells.Contains(this))
            activeFrozenCells.Add(this);
    }

    private void OnDestroy()
    {
        activeFrozenCells.Remove(this);
    }

    public void Initialize(Vector2Int pos, int face, int matches)
    {
        gridPosition = pos;
        faceIndex = face;
        requiredMatches = matches;
        remainingMatches = matches;
        isDefrosted = false;

        CreateIceVisual();
        UpdateCounterText();
    }

    private Material GetIceMaterial()
    {
        if (sharedIceMaterial == null)
        {
            Shader iceShader = Shader.Find("Custom/HypercasualIceShader");
            if (iceShader == null) iceShader = Shader.Find("Custom/HypercasualCrispGlass");
            if (iceShader == null) iceShader = Shader.Find("Standard");

            sharedIceMaterial = new Material(iceShader);
            sharedIceMaterial.name = "RuntimeIceMaterial";

            if (sharedIceMaterial.HasProperty("_Color"))
                sharedIceMaterial.SetColor("_Color", new Color(0.65f, 0.92f, 1.0f, 0.5f));
            if (sharedIceMaterial.HasProperty("_DeepColor"))
                sharedIceMaterial.SetColor("_DeepColor", new Color(0.18f, 0.62f, 0.95f, 0.7f));
            if (sharedIceMaterial.HasProperty("_RimColor"))
                sharedIceMaterial.SetColor("_RimColor", new Color(0.95f, 1.0f, 1.0f, 0.95f));
            if (sharedIceMaterial.HasProperty("_RimPower"))
                sharedIceMaterial.SetFloat("_RimPower", 2.0f);
            if (sharedIceMaterial.HasProperty("_FrostStrength"))
                sharedIceMaterial.SetFloat("_FrostStrength", 0.22f);
            if (sharedIceMaterial.HasProperty("_CrackScale"))
                sharedIceMaterial.SetFloat("_CrackScale", 5.5f);
        }
        return sharedIceMaterial;
    }

    private void CreateIceVisual()
    {
        if (iceVisualRoot != null) Destroy(iceVisualRoot);

        GridSpawner spawner = FindObjectOfType<GridSpawner>();
        float zOffset = spawner != null ? spawner.objectOffset : 0.3f;

        iceVisualRoot = new GameObject("IceVisualRoot");
        iceVisualRoot.transform.SetParent(transform, false);
        // Kürenin tam merkezine oturacak şekilde Z offset'e yerleştir
        iceVisualRoot.transform.localPosition = new Vector3(0f, 0f, -zOffset);
        iceVisualRoot.transform.localRotation = Quaternion.identity;
        iceVisualRoot.transform.localScale = Vector3.one;

        Material iceMat = GetIceMaterial();

        // 1. Arka Yumuşak Buzlu Halka / Parıltı (Soft Circular Ice Glow)
        if (circularGlowSprite == null) circularGlowSprite = CreateSoftCircularSprite(64, 0.45f);

        GameObject backGlowObj = new GameObject("BackIceGlow");
        backGlowObj.transform.SetParent(iceVisualRoot.transform, false);
        backGlowObj.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        backGlowObj.transform.localScale = Vector3.one * 1.6f;
        SpriteRenderer bgSr = backGlowObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = circularGlowSprite;
        bgSr.color = new Color(0.35f, 0.8f, 1.0f, 0.45f);
        bgSr.sortingOrder = 2;

        // 2. Ana Organik 3D Kristal Buz Küresi (Smooth Ice Sphere Shell)
        GameObject iceSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        iceSphere.name = "MainIceSphere";
        iceSphere.transform.SetParent(iceVisualRoot.transform, false);
        iceSphere.transform.localPosition = Vector3.zero;
        iceSphere.transform.localRotation = Quaternion.identity;
        // Küreyi hafifçe genişçe saran pürüzsüz buz kubbesi
        iceSphere.transform.localScale = new Vector3(1.26f, 1.26f, 1.15f);

        Collider col = iceSphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer mr = iceSphere.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material = iceMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // 3. İnce Dış Kristal Halka (Frosty Ring Edge)
        GameObject frostRing = new GameObject("FrostRing");
        frostRing.transform.SetParent(iceVisualRoot.transform, false);
        frostRing.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        frostRing.transform.localScale = Vector3.one * 1.38f;
        SpriteRenderer frSr = frostRing.AddComponent<SpriteRenderer>();
        frSr.sprite = circularGlowSprite;
        frSr.color = new Color(0.85f, 0.98f, 1.0f, 0.35f);
        frSr.sortingOrder = 10;

        // 4. Ön Yüz Şık Yuvarlak Kristal Rozet (Center Circular Badge)
        GameObject badgeObj = new GameObject("CounterBadge");
        badgeObj.transform.SetParent(iceVisualRoot.transform, false);
        badgeObj.transform.localPosition = new Vector3(0f, 0f, -0.62f);
        badgeObj.transform.localRotation = Quaternion.identity;
        badgeObj.transform.localScale = Vector3.one * 0.72f;

        if (circularBadgeSprite == null) circularBadgeSprite = CreateCrispBadgeSprite(64);

        SpriteRenderer badgeSr = badgeObj.AddComponent<SpriteRenderer>();
        badgeSr.sprite = circularBadgeSprite;
        badgeSr.color = new Color(0.12f, 0.58f, 0.92f, 0.94f);
        badgeSr.sortingOrder = 35;

        // Rozet dış parlaklık halkası
        GameObject glowObj = new GameObject("BadgeOuterRing");
        glowObj.transform.SetParent(badgeObj.transform, false);
        glowObj.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        glowObj.transform.localScale = Vector3.one * 1.18f;
        SpriteRenderer glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = circularGlowSprite;
        glowSr.color = new Color(0.9f, 0.98f, 1.0f, 0.75f);
        glowSr.sortingOrder = 34;

        // 5. TextMeshPro Sayaç Metni
        GameObject textObj = new GameObject("CounterText");
        textObj.transform.SetParent(badgeObj.transform, false);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one * 0.52f;

        counterText = textObj.AddComponent<TextMeshPro>();
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.fontSize = 11.5f;
        counterText.fontStyle = FontStyles.Bold;
        counterText.color = Color.white;
        counterText.outlineWidth = 0.32f;
        counterText.outlineColor = new Color(0.04f, 0.25f, 0.5f, 1f);
        counterText.sortingOrder = 45;

        if (spawner != null && spawner.globalFont != null)
        {
            counterText.font = spawner.globalFont;
        }

        // Yumuşak büyüme animasyonu
        iceVisualRoot.transform.localScale = Vector3.zero;
        iceVisualRoot.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
    }

    private void UpdateCounterText()
    {
        if (counterText != null)
        {
            counterText.text = remainingMatches.ToString();
        }
    }

    public void OnMatchProgress()
    {
        if (isDefrosted) return;

        remainingMatches--;

        // Animasyon: Buz küresi ve sayaç darbe (pulse) animasyonu
        if (iceVisualRoot != null)
        {
            iceVisualRoot.transform.DOKill();
            iceVisualRoot.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.5f);
        }

        if (counterText != null)
        {
            counterText.transform.DOKill();
            counterText.transform.DOPunchScale(Vector3.one * 0.35f, 0.2f, 5, 0.5f);
            counterText.text = Mathf.Max(0, remainingMatches).ToString();
        }

        // Işıltılı buz çatlağı parçacıkları
        if (EffectsManager.Instance != null)
        {
            EffectsManager.Instance.SpawnSnapParticles(transform.position + new Vector3(0f, 0f, -0.3f), new Color(0.6f, 0.95f, 1f));
        }

        if (remainingMatches <= 0)
        {
            Defrost();
        }
    }

    public void Defrost()
    {
        if (isDefrosted) return;
        isDefrosted = true;
        activeFrozenCells.Remove(this);

        VibrationManager.TryVibrate();
        AudioManager.PlayPlace();

        // Bu hücrede duran kürenin kilidini aç
        UnlockPieceOnThisCell();

        // Patlama / Buz kırılma parçacık efektleri
        if (EffectsManager.Instance != null)
        {
            Vector3 centerPos = transform.position + new Vector3(0f, 0f, -0.3f);
            EffectsManager.Instance.SpawnSplash(centerPos, new Color(0.7f, 0.95f, 1f));
            EffectsManager.Instance.SpawnSnapParticles(centerPos, new Color(0.85f, 1f, 1f));
        }

        if (iceVisualRoot != null)
        {
            iceVisualRoot.transform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(iceVisualRoot.transform.DOScale(Vector3.one * 1.3f, 0.15f).SetEase(Ease.OutQuad));
            seq.Append(iceVisualRoot.transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack));
            seq.OnComplete(() =>
            {
                if (iceVisualRoot != null) Destroy(iceVisualRoot);
                Destroy(this);
            });
        }
        else
        {
            Destroy(this);
        }
    }

    private void UnlockPieceOnThisCell()
    {
        if (frozenPiece != null)
        {
            frozenPiece.SetFrozen(false);
        }
    }

    public static void NotifyMatchCompleted()
    {
        List<FrozenGridCell> cells = new List<FrozenGridCell>(activeFrozenCells);
        foreach (var cell in cells)
        {
            if (cell != null && !cell.isDefrosted)
            {
                cell.OnMatchProgress();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // YARDIMCILAR: Yumuşak Yuvarlak Sprite Üreticileri
    // ──────────────────────────────────────────────────────────────
    private static Sprite CreateSoftCircularSprite(int size, float falloffPower)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.Pow(1.0f - t, falloffPower);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateCrispBadgeSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) / 1.8f);

                // Kenara doğru parlayan cam halka
                float edgeHighlight = Mathf.SmoothStep(radius - 3.5f, radius, dist);
                Color col = Color.Lerp(new Color(0.9f, 0.98f, 1f, alpha), new Color(1f, 1f, 1f, alpha), edgeHighlight);
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
