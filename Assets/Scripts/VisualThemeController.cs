using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Symmetric3D — Canlı Görsel Tema ve Materyal Kontrolcüsü.
/// Sahnedeki cam, sıvı (cel-shading/menisküs), ızgara yuvaları, çerçeve, sahte gölge
/// ve arka plan zemin materyallerini merkezi olarak yönetir.
/// </summary>
[ExecuteAlways]
public class VisualThemeController : MonoBehaviour
{
    public static VisualThemeController Instance { get; private set; }

    [Header("─── MATERYAL REFERANSLARI ───")]
    [Tooltip("Cam küre materyali (Glass.mat)")]
    public Material glassMat;

    [Tooltip("Sıvı materyali (Shader.mat)")]
    public Material liquidMat;

    [Tooltip("Tahta hücre yuvaları materyali (Grid.mat)")]
    public Material gridMat;

    [Tooltip("Tahtayı çevreleyen kenarlık materyali (Çerçeve.mat)")]
    public Material frameMat;

    [Tooltip("En arkadaki zemin materyali (Plane.mat)")]
    public Material planeMat;

    [Tooltip("Çerçeve altındaki derinlik sahte gölgesi materyali (FrameShadow.mat)")]
    public Material frameShadowMat;

    [Tooltip("Topların altındaki dairesel sahte gölge materyali (BallShadow.mat)")]
    public Material ballShadowMat;

    [Header("─── 1. CAM KÜRE AYARLARI (Glass) ───")]
    [Tooltip("Camın gövde tonu ve saydamlığı (Alpha = 0 tam berrak, solukluğu önler)")]
    public Color glassTint = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    [Tooltip("Cam üzerinde beyaz parlama noktası olsun mu?")]
    public bool glassUseSpecular = true;

    [Tooltip("Cam üzerindeki karikatür parıltı rengi (Specular Highlight)")]
    public Color glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    [Range(0.0f, 0.1f)]
    [Tooltip("Parıltı noktasının boyutu (Düşük = ince zarif nokta, Yüksek = büyük parlama)")]
    public float glassSpecularSize = 0.035f;

    [Range(0.001f, 0.05f)]
    [Tooltip("Parıltı noktasının kenar keskinliği (Düşük = jilet gibi keskin çizgi)")]
    public float glassSpecularSharpness = 0.005f;

    [Tooltip("Camın en dış çeperindeki ince kenar ışıması (Rim Light) rengi")]
    public Color glassRimColor = new Color(1.0f, 1.0f, 1.0f, 0.85f);

    [Range(0.5f, 8.0f)]
    [Tooltip("Kenar ışımasının inceliği / gücü (Yüksek = sadece en dış sınır, Düşük = geniş hale)")]
    public float glassRimPower = 2.0f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Cam kürenin sınırlarını belirginleştiren silüet kontur gücü (Toon çizgisi)")]
    public float glassEdgeDarkness = 0.35f;

    [Tooltip("Cam sınır konturunun rengi")]
    public Color glassEdgeOutlineColor = new Color(0.18f, 0.24f, 0.36f, 1.0f);

    [Header("─── 2. SIVI AYARLARI (Liquid & Cel-Shading) ───")]
    [Range(0.1f, 0.9f)]
    [Tooltip("Toon cel-shading gölge eşiği (Işık ve gölge sınırının konumu)")]
    public float liquidRampThreshold = 0.5f;

    [Range(0.001f, 0.2f)]
    [Tooltip("Toon gölge geçişinin yumuşaklığı (Düşük = net çizgi film geçişi)")]
    public float liquidRampSmooth = 0.05f;

    [Range(0.8f, 2.0f)]
    [Tooltip("Sıvı renginin canlılık / doygunluk çarpanı")]
    public float liquidColorBoost = 1.35f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Akıllı renk canlılığı normalizasyonu (Koyu yeşil, mor gibi renkleri canlı şeker rengine yükseltir)")]
    public float liquidVibranceNorm = 0.88f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Sıvının iç ışıması / Candy glow (Sıvıya içten gelen zengin bir canlılık kazandırır, çamurlu gölgeleri engeller)")]
    public float liquidInnerGlow = 0.38f;

    [Range(0.005f, 0.06f)]
    [Tooltip("Sıvı üst yüzeyindeki kavis / menisküs çizgisi kalınlığı")]
    public float liquidMeniscusWidth = 0.025f;

    [Range(0.0f, 2.0f)]
    [Tooltip("Sıvı yüzeyindeki menisküs kavis çizgisinin parlaklığı")]
    public float liquidMeniscusIntensity = 1.0f;

    [Range(0.0f, 3.0f)]
    [Tooltip("Sıvı yüzeyindeki ışık parlama gücü (0 = parlama yok)")]
    public float liquidHighlightIntensity = 0.0f;

    [Range(0.1f, 8.0f)]
    [Tooltip("Sıvı kenar ışığı (Rim) odağı")]
    public float liquidRimPower = 2.0f;

    [Range(0.0f, 5.0f)]
    [Tooltip("Sıvı kenar ışığı (Rim) şiddeti")]
    public float liquidRimIntensity = 1.4f;

    [Header("─── 3. IZGARA YUVALARI (Grid) ───")]
    [Tooltip("Izgara hücrelerinin ana zemin rengi")]
    public Color gridBaseColor = new Color(0.74f, 0.80f, 0.88f, 1.0f);

    [Tooltip("Izgara hücrelerinin derinlik hissi veren gölge rengi")]
    public Color gridShadowColor = new Color(0.58f, 0.65f, 0.75f, 1.0f);

    [Tooltip("Izgara hücre kenarlarının parıltı rengi")]
    public Color gridRimColor = new Color(0.82f, 0.88f, 0.95f, 0.20f);

    [Tooltip("Boş yuvalardaki beyaz parlama (İstenmediğinde kapalı tutulur)")]
    public bool gridUseSpecular = false;

    [Header("─── 4. ÇERÇEVE AYARLARI (Frame) ───")]
    [Tooltip("Tahtayı saran ana çerçevenin rengi")]
    public Color frameBaseColor = new Color(0.18f, 0.20f, 0.32f, 1.0f);

    [Tooltip("Çerçevenin gölge tonu")]
    public Color frameShadowColor = new Color(0.10f, 0.11f, 0.18f, 1.0f);

    [Tooltip("Çerçevenin kenar parıltısı (Parlama olmaması için siyah/saydam bırakın)")]
    public Color frameRimColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Çerçevede beyaz parlama noktası olsun mu? (Göz almaması için varsayılan: kapalı)")]
    public bool frameUseSpecular = false;

    [Header("─── 5. SAHTE GÖLGELER (Fake Shadows) ───")]
    [Tooltip("Çerçevenin altına derinlik kazandıran sahte gölge açık olsun mu?")]
    public bool enableFrameFakeShadow = true;

    [Tooltip("Çerçeve sahte gölgesinin rengi ve saydamlığı (Alpha)")]
    public Color frameFakeShadowColor = new Color(0.05f, 0.08f, 0.20f, 0.48f);

    [Tooltip("Çerçeve gölgesinin ışık açısına göre X ve Y konumu (Offset)")]
    public Vector2 frameFakeShadowOffset = new Vector2(0.06f, -0.08f);

    [Range(0.5f, 10.0f)]
    [Tooltip("Çerçeve gölge kenarlarının yumuşaklık / dağılma derecesi")]
    public float frameFakeShadowSoftness = 2.8f;

    [Range(1.0f, 1.4f)]
    [Tooltip("Çerçeve gölgesinin dışa doğru büyüme çarpanı")]
    public float frameShadowScaleMultiplier = 1.14f;

    [Tooltip("Topların / kürelerin altındaki dairesel sahte gölgeler açık olsun mu?")]
    public bool enableBallFakeShadow = true;

    [Tooltip("Top sahte gölgesinin rengi ve saydamlığı (Alpha)")]
    public Color ballFakeShadowColor = new Color(0.08f, 0.12f, 0.22f, 0.38f);

    [Tooltip("Top gölgesinin ışık geliş açısına göre kayma mesafesi")]
    public Vector2 ballFakeShadowOffset = new Vector2(0.025f, -0.035f);

    [Range(0.2f, 1.0f)]
    [Tooltip("Top gölgesinin taban çapı")]
    public float ballFakeShadowSize = 0.55f;

    [Range(0.5f, 10.0f)]
    [Tooltip("Top gölge kenarlarının yumuşaklığı")]
    public float ballFakeShadowSoftness = 2.2f;

    [Header("─── 6. ZEMİN ARKA PLAN (Plane) ───")]
    [Tooltip("En arkadaki geniş zemin rengi")]
    public Color planeBaseColor = new Color(0.88f, 0.91f, 0.95f, 1.0f);

    [Tooltip("Zeminin gölge tonu")]
    public Color planeShadowColor = new Color(0.78f, 0.82f, 0.89f, 1.0f);

    void Awake()
    {
        Instance = this;
        EnsureMaterials();
        ApplyToMaterials();
    }

    void OnEnable()
    {
        Instance = this;
        EnsureMaterials();
        ApplyToMaterials();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureMaterials();
        ApplyToMaterials();
    }
#endif

    public void EnsureMaterials()
    {
#if UNITY_EDITOR
        if (glassMat == null)
            glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass.mat");
        if (liquidMat == null)
            liquidMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Shader.mat");
        if (gridMat == null)
            gridMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Grid.mat");
        if (frameMat == null)
        {
            string p = AssetDatabase.GUIDToAssetPath("fe0d3048897f79f4bb1ad8408799ef6a");
            if (!string.IsNullOrEmpty(p))
                frameMat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (frameMat == null)
            {
                var guids = AssetDatabase.FindAssets("Çerçeve t:Material");
                if (guids.Length == 0) guids = AssetDatabase.FindAssets("er eve t:Material");
                if (guids.Length > 0)
                    frameMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        if (planeMat == null)
            planeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Plane.mat");
        if (frameShadowMat == null)
            frameShadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FrameShadow.mat");
        if (ballShadowMat == null)
            ballShadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BallShadow.mat");
#endif
    }

    [ContextMenu("Değerleri Materyallere Uygula")]
    public void ApplyToMaterials()
    {
        if (glassMat != null)
        {
            if (glassMat.HasProperty("_Color")) glassMat.SetColor("_Color", glassTint);
            if (glassMat.HasProperty("_SpecColor")) glassMat.SetColor("_SpecColor", glassUseSpecular ? glassSpecularColor : Color.clear);
            if (glassMat.HasProperty("_SpecSize")) glassMat.SetFloat("_SpecSize", glassUseSpecular ? glassSpecularSize : 0f);
            if (glassMat.HasProperty("_SpecularHighlights")) glassMat.SetFloat("_SpecularHighlights", glassUseSpecular ? 1f : 0f);
            if (glassMat.HasProperty("_SpecSmoothness")) glassMat.SetFloat("_SpecSmoothness", glassSpecularSharpness);
            if (glassMat.HasProperty("_RimColor")) glassMat.SetColor("_RimColor", glassRimColor);
            if (glassMat.HasProperty("_RimPower")) glassMat.SetFloat("_RimPower", glassRimPower);
            if (glassMat.HasProperty("_EdgeDarkness")) glassMat.SetFloat("_EdgeDarkness", glassEdgeDarkness);
            if (glassMat.HasProperty("_EdgeOutlineColor")) glassMat.SetColor("_EdgeOutlineColor", glassEdgeOutlineColor);
#if UNITY_EDITOR
            EditorUtility.SetDirty(glassMat);
#endif
        }

        if (liquidMat != null)
        {
            if (liquidMat.HasProperty("_RampThreshold")) liquidMat.SetFloat("_RampThreshold", liquidRampThreshold);
            if (liquidMat.HasProperty("_RampSmooth")) liquidMat.SetFloat("_RampSmooth", liquidRampSmooth);
            if (liquidMat.HasProperty("_ColorBoost")) liquidMat.SetFloat("_ColorBoost", liquidColorBoost);
            if (liquidMat.HasProperty("_VibranceNorm")) liquidMat.SetFloat("_VibranceNorm", liquidVibranceNorm);
            if (liquidMat.HasProperty("_InnerGlow")) liquidMat.SetFloat("_InnerGlow", liquidInnerGlow);
            if (liquidMat.HasProperty("_MeniscusWidth")) liquidMat.SetFloat("_MeniscusWidth", liquidMeniscusWidth);
            if (liquidMat.HasProperty("_MeniscusIntensity")) liquidMat.SetFloat("_MeniscusIntensity", liquidMeniscusIntensity);
            if (liquidMat.HasProperty("_HighlightIntensity")) liquidMat.SetFloat("_HighlightIntensity", liquidHighlightIntensity);
            if (liquidMat.HasProperty("_RimPower")) liquidMat.SetFloat("_RimPower", liquidRimPower);
            if (liquidMat.HasProperty("_RimIntensity")) liquidMat.SetFloat("_RimIntensity", liquidRimIntensity);
#if UNITY_EDITOR
            EditorUtility.SetDirty(liquidMat);
#endif
        }

        if (gridMat != null)
        {
            if (gridMat.HasProperty("_BaseColor")) gridMat.SetColor("_BaseColor", gridBaseColor);
            if (gridMat.HasProperty("_Color")) gridMat.SetColor("_Color", gridBaseColor);
            if (gridMat.HasProperty("_SColor")) gridMat.SetColor("_SColor", gridShadowColor);
            if (gridMat.HasProperty("_RimColor")) gridMat.SetColor("_RimColor", gridRimColor);
            if (gridMat.HasProperty("_UseSpecular")) gridMat.SetFloat("_UseSpecular", gridUseSpecular ? 1f : 0f);
            if (gridMat.HasProperty("_SpecularColor")) gridMat.SetColor("_SpecularColor", gridUseSpecular ? new Color(1f, 1f, 1f, 0.5f) : Color.clear);
#if UNITY_EDITOR
            EditorUtility.SetDirty(gridMat);
#endif
        }

        if (frameMat != null)
        {
            if (frameMat.HasProperty("_BaseColor")) frameMat.SetColor("_BaseColor", frameBaseColor);
            if (frameMat.HasProperty("_Color")) frameMat.SetColor("_Color", frameBaseColor);
            if (frameMat.HasProperty("_SColor")) frameMat.SetColor("_SColor", frameShadowColor);
            if (frameMat.HasProperty("_RimColor")) frameMat.SetColor("_RimColor", frameRimColor);

            bool hasRim = frameRimColor.a > 0.001f && (frameRimColor.r > 0.001f || frameRimColor.g > 0.001f || frameRimColor.b > 0.001f);
            if (frameMat.HasProperty("_UseRim")) frameMat.SetFloat("_UseRim", hasRim ? 1f : 0f);
            if (hasRim)
                frameMat.EnableKeyword("TCP2_RIM_LIGHTING");
            else
                frameMat.DisableKeyword("TCP2_RIM_LIGHTING");

            if (frameMat.HasProperty("_UseSpecular")) frameMat.SetFloat("_UseSpecular", frameUseSpecular ? 1f : 0f);
            if (frameMat.HasProperty("_SpecularColor")) frameMat.SetColor("_SpecularColor", frameUseSpecular ? new Color(0.9f, 0.95f, 1f, 1f) : Color.clear);
            if (frameUseSpecular)
                frameMat.EnableKeyword("TCP2_SPECULAR");
            else
                frameMat.DisableKeyword("TCP2_SPECULAR");

#if UNITY_EDITOR
            EditorUtility.SetDirty(frameMat);
#endif
        }

        if (frameShadowMat != null)
        {
            if (frameShadowMat.HasProperty("_Color")) frameShadowMat.SetColor("_Color", frameFakeShadowColor);
            if (frameShadowMat.HasProperty("_Softness")) frameShadowMat.SetFloat("_Softness", frameFakeShadowSoftness);
            if (frameShadowMat.HasProperty("_IsCircle")) frameShadowMat.SetFloat("_IsCircle", 0f);
#if UNITY_EDITOR
            EditorUtility.SetDirty(frameShadowMat);
#endif
        }

        if (ballShadowMat != null)
        {
            if (ballShadowMat.HasProperty("_Color")) ballShadowMat.SetColor("_Color", ballFakeShadowColor);
            if (ballShadowMat.HasProperty("_Softness")) ballShadowMat.SetFloat("_Softness", ballFakeShadowSoftness);
            if (ballShadowMat.HasProperty("_IsCircle")) ballShadowMat.SetFloat("_IsCircle", 1f);
#if UNITY_EDITOR
            EditorUtility.SetDirty(ballShadowMat);
#endif
        }

        var spawner = FindObjectOfType<GridSpawner>();
        if (spawner != null)
        {
            spawner.enableFrameFakeShadow = enableFrameFakeShadow;
            spawner.frameShadowOffset = frameFakeShadowOffset;
            spawner.frameShadowScaleMultiplier = frameShadowScaleMultiplier;
            if (spawner.frameShadowMaterial == null)
                spawner.frameShadowMaterial = frameShadowMat;
#if UNITY_EDITOR
            EditorUtility.SetDirty(spawner);
#endif
        }

        if (planeMat != null)
        {
            if (planeMat.HasProperty("_BaseColor")) planeMat.SetColor("_BaseColor", planeBaseColor);
            if (planeMat.HasProperty("_Color")) planeMat.SetColor("_Color", planeBaseColor);
            if (planeMat.HasProperty("_SColor")) planeMat.SetColor("_SColor", planeShadowColor);
#if UNITY_EDITOR
            EditorUtility.SetDirty(planeMat);
#endif
        }

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    [ContextMenu("Materyallerden Değerleri Oku")]
    public void ReadFromMaterials()
    {
        EnsureMaterials();

        if (glassMat != null)
        {
            if (glassMat.HasProperty("_Color")) glassTint = glassMat.GetColor("_Color");
            if (glassMat.HasProperty("_SpecColor")) glassSpecularColor = glassMat.GetColor("_SpecColor");
            if (glassMat.HasProperty("_SpecSize")) glassSpecularSize = glassMat.GetFloat("_SpecSize");
            glassUseSpecular = glassSpecularSize > 0.001f && glassSpecularColor.a > 0.001f;
            if (glassMat.HasProperty("_SpecSmoothness")) glassSpecularSharpness = glassMat.GetFloat("_SpecSmoothness");
            if (glassMat.HasProperty("_RimColor")) glassRimColor = glassMat.GetColor("_RimColor");
            if (glassMat.HasProperty("_RimPower")) glassRimPower = glassMat.GetFloat("_RimPower");
            if (glassMat.HasProperty("_EdgeDarkness")) glassEdgeDarkness = glassMat.GetFloat("_EdgeDarkness");
            if (glassMat.HasProperty("_EdgeOutlineColor")) glassEdgeOutlineColor = glassMat.GetColor("_EdgeOutlineColor");
        }

        if (liquidMat != null)
        {
            if (liquidMat.HasProperty("_RampThreshold")) liquidRampThreshold = liquidMat.GetFloat("_RampThreshold");
            if (liquidMat.HasProperty("_RampSmooth")) liquidRampSmooth = liquidMat.GetFloat("_RampSmooth");
            if (liquidMat.HasProperty("_ColorBoost")) liquidColorBoost = liquidMat.GetFloat("_ColorBoost");
            if (liquidMat.HasProperty("_VibranceNorm")) liquidVibranceNorm = liquidMat.GetFloat("_VibranceNorm");
            if (liquidMat.HasProperty("_InnerGlow")) liquidInnerGlow = liquidMat.GetFloat("_InnerGlow");
            if (liquidMat.HasProperty("_MeniscusWidth")) liquidMeniscusWidth = liquidMat.GetFloat("_MeniscusWidth");
            if (liquidMat.HasProperty("_MeniscusIntensity")) liquidMeniscusIntensity = liquidMat.GetFloat("_MeniscusIntensity");
            if (liquidMat.HasProperty("_HighlightIntensity")) liquidHighlightIntensity = liquidMat.GetFloat("_HighlightIntensity");
            if (liquidMat.HasProperty("_RimPower")) liquidRimPower = liquidMat.GetFloat("_RimPower");
            if (liquidMat.HasProperty("_RimIntensity")) liquidRimIntensity = liquidMat.GetFloat("_RimIntensity");
        }

        if (gridMat != null)
        {
            if (gridMat.HasProperty("_BaseColor")) gridBaseColor = gridMat.GetColor("_BaseColor");
            if (gridMat.HasProperty("_SColor")) gridShadowColor = gridMat.GetColor("_SColor");
            if (gridMat.HasProperty("_RimColor")) gridRimColor = gridMat.GetColor("_RimColor");
            if (gridMat.HasProperty("_UseSpecular")) gridUseSpecular = gridMat.GetFloat("_UseSpecular") > 0.5f;
        }

        if (frameShadowMat != null)
        {
            if (frameShadowMat.HasProperty("_Color")) frameFakeShadowColor = frameShadowMat.GetColor("_Color");
            if (frameShadowMat.HasProperty("_Softness")) frameFakeShadowSoftness = frameShadowMat.GetFloat("_Softness");
        }

        if (ballShadowMat != null)
        {
            if (ballShadowMat.HasProperty("_Color")) ballFakeShadowColor = ballShadowMat.GetColor("_Color");
            if (ballShadowMat.HasProperty("_Softness")) ballFakeShadowSoftness = ballShadowMat.GetFloat("_Softness");
        }

        if (frameMat != null)
        {
            if (frameMat.HasProperty("_BaseColor")) frameBaseColor = frameMat.GetColor("_BaseColor");
            if (frameMat.HasProperty("_SColor")) frameShadowColor = frameMat.GetColor("_SColor");
            if (frameMat.HasProperty("_RimColor")) frameRimColor = frameMat.GetColor("_RimColor");
            if (frameMat.HasProperty("_UseSpecular")) frameUseSpecular = frameMat.GetFloat("_UseSpecular") > 0.5f;
        }

        if (planeMat != null)
        {
            if (planeMat.HasProperty("_BaseColor")) planeBaseColor = planeMat.GetColor("_BaseColor");
            if (planeMat.HasProperty("_SColor")) planeShadowColor = planeMat.GetColor("_SColor");
        }
    }

    // ── Hızlı Hazır Önayarlar (Presets) ──────────────────────────────
    public void Preset_CrystalGlass()
    {
        glassTint = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        glassSpecularSize = 0.035f;
        glassSpecularSharpness = 0.005f;
        glassRimColor = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        glassRimPower = 2.0f;
        glassEdgeDarkness = 0.35f;
        glassEdgeOutlineColor = new Color(0.18f, 0.24f, 0.36f, 1.0f);

        liquidRampThreshold = 0.5f;
        liquidRampSmooth = 0.05f;
        liquidColorBoost = 1.15f;
        liquidVibranceNorm = 0.85f;
        liquidInnerGlow = 0.35f;
        liquidMeniscusWidth = 0.025f;
        liquidMeniscusIntensity = 1.0f;
        liquidHighlightIntensity = 1.2f;

        gridBaseColor = new Color(0.74f, 0.80f, 0.88f, 1.0f);
        gridShadowColor = new Color(0.58f, 0.65f, 0.75f, 1.0f);
        gridRimColor = new Color(0.82f, 0.88f, 0.95f, 0.20f);
        gridUseSpecular = false;

        frameBaseColor = new Color(0.18f, 0.20f, 0.32f, 1.0f);
        frameShadowColor = new Color(0.10f, 0.11f, 0.18f, 1.0f);
        frameRimColor = new Color(0f, 0f, 0f, 0f);
        frameUseSpecular = false;

        planeBaseColor = new Color(0.88f, 0.91f, 0.95f, 1.0f);
        planeShadowColor = new Color(0.78f, 0.82f, 0.89f, 1.0f);

        ApplyToMaterials();
    }

    public void Preset_VisibleToonGlass()
    {
        glassTint = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        glassSpecularSize = 0.038f;
        glassSpecularSharpness = 0.005f;
        glassRimColor = new Color(1.0f, 1.0f, 1.0f, 0.90f);
        glassRimPower = 2.0f;
        glassEdgeDarkness = 0.45f;
        glassEdgeOutlineColor = new Color(0.15f, 0.20f, 0.32f, 1.0f);

        liquidRampThreshold = 0.52f;
        liquidRampSmooth = 0.03f;
        liquidColorBoost = 1.25f;
        liquidVibranceNorm = 0.85f;
        liquidInnerGlow = 0.35f;
        liquidMeniscusWidth = 0.03f;
        liquidMeniscusIntensity = 1.3f;
        liquidHighlightIntensity = 1.5f;

        gridBaseColor = new Color(0.72f, 0.78f, 0.86f, 1.0f);
        gridShadowColor = new Color(0.55f, 0.62f, 0.72f, 1.0f);
        gridRimColor = new Color(0.85f, 0.90f, 0.98f, 0.35f);
        gridUseSpecular = false;

        frameBaseColor = new Color(0.15f, 0.18f, 0.28f, 1.0f);
        frameShadowColor = new Color(0.08f, 0.09f, 0.15f, 1.0f);
        frameRimColor = new Color(0f, 0f, 0f, 0f);
        frameUseSpecular = false;

        planeBaseColor = new Color(0.85f, 0.89f, 0.94f, 1.0f);
        planeShadowColor = new Color(0.75f, 0.80f, 0.88f, 1.0f);

        ApplyToMaterials();
    }

    public void Preset_SoftPastel()
    {
        glassTint = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        glassSpecularSize = 0.03f;
        glassSpecularSharpness = 0.006f;
        glassRimColor = new Color(1.0f, 1.0f, 1.0f, 0.70f);
        glassRimPower = 2.2f;
        glassEdgeDarkness = 0.28f;
        glassEdgeOutlineColor = new Color(0.25f, 0.30f, 0.42f, 1.0f);

        liquidRampThreshold = 0.45f;
        liquidRampSmooth = 0.08f;
        liquidColorBoost = 1.0f;
        liquidVibranceNorm = 0.75f;
        liquidInnerGlow = 0.25f;
        liquidMeniscusWidth = 0.02f;
        liquidMeniscusIntensity = 0.8f;
        liquidHighlightIntensity = 1.0f;

        gridBaseColor = new Color(0.80f, 0.84f, 0.90f, 1.0f);
        gridShadowColor = new Color(0.68f, 0.72f, 0.80f, 1.0f);
        gridRimColor = new Color(0.88f, 0.92f, 0.96f, 0.15f);
        gridUseSpecular = false;

        frameBaseColor = new Color(0.24f, 0.26f, 0.36f, 1.0f);
        frameShadowColor = new Color(0.14f, 0.15f, 0.22f, 1.0f);
        frameRimColor = new Color(0f, 0f, 0f, 0f);
        frameUseSpecular = false;

        planeBaseColor = new Color(0.92f, 0.94f, 0.97f, 1.0f);
        planeShadowColor = new Color(0.82f, 0.85f, 0.90f, 1.0f);

        ApplyToMaterials();
    }

    public void Preset_CandyGloss()
    {
        glassTint = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        glassSpecularSize = 0.04f;
        glassSpecularSharpness = 0.005f;
        glassRimColor = new Color(1.0f, 1.0f, 1.0f, 0.90f);
        glassRimPower = 2.0f;
        glassEdgeDarkness = 0.40f;
        glassEdgeOutlineColor = new Color(0.15f, 0.22f, 0.35f, 1.0f);

        liquidRampThreshold = 0.55f;
        liquidRampSmooth = 0.02f;
        liquidColorBoost = 1.4f;
        liquidVibranceNorm = 0.92f;
        liquidInnerGlow = 0.42f;
        liquidMeniscusWidth = 0.035f;
        liquidMeniscusIntensity = 1.6f;
        liquidHighlightIntensity = 1.8f;

        gridBaseColor = new Color(0.70f, 0.76f, 0.88f, 1.0f);
        gridShadowColor = new Color(0.50f, 0.58f, 0.72f, 1.0f);
        gridRimColor = new Color(0.88f, 0.94f, 1.0f, 0.4f);
        gridUseSpecular = false;

        frameBaseColor = new Color(0.12f, 0.15f, 0.25f, 1.0f);
        frameShadowColor = new Color(0.06f, 0.08f, 0.14f, 1.0f);
        frameRimColor = new Color(0f, 0f, 0f, 0f);
        frameUseSpecular = false;

        planeBaseColor = new Color(0.86f, 0.90f, 0.96f, 1.0f);
        planeShadowColor = new Color(0.74f, 0.80f, 0.90f, 1.0f);

        ApplyToMaterials();
    }
}
