using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class VisualThemeController : MonoBehaviour
{
    public static VisualThemeController Instance { get; private set; }

    [Header("─── MATERYAL REFERANSLARI ───")]
    public Material glassMat;
    public Material liquidMat;
    public Material gridMat;
    public Material frameMat;
    public Material planeMat;
    public Material frameShadowMat;

    [Header("─── 1. CAM AYARLARI (Glass) ───")]
    [Tooltip("Camın gövde tonu ve saydamlığı (Alpha ne kadar düşükse o kadar berrak)")]
    public Color glassTint = new Color(0.9f, 0.95f, 1.0f, 0.02f);
    
    [Tooltip("Cam üzerindeki karikatür parıltı rengi")]
    public Color glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 0.85f);
    
    [Range(0.005f, 0.1f)]
    [Tooltip("Parıltı noktasının boyutu (Düşük = ince zarif nokta, Yüksek = büyük parlama)")]
    public float glassSpecularSize = 0.03f;
    
    [Range(0.001f, 0.05f)]
    [Tooltip("Parıltı noktasının kenar keskinliği")]
    public float glassSpecularSharpness = 0.01f;

    [Tooltip("Camın dış kenarındaki ince ışıma rengi")]
    public Color glassRimColor = new Color(0.9f, 0.95f, 1.0f, 0.4f);
    
    [Range(0.5f, 8.0f)]
    [Tooltip("Kenar ışımasının inceliği (Yüksek = sadece en dış sınır, Düşük = geniş rim)")]
    public float glassRimPower = 3.0f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Cam kürenin sınırlarını belirginleştiren silüet kontur gücü")]
    public float glassEdgeDarkness = 0.35f;

    [Tooltip("Cam sınır konturunun rengi")]
    public Color glassEdgeOutlineColor = new Color(0.35f, 0.45f, 0.6f, 1.0f);

    [Header("─── 2. SIVI AYARLARI (Liquid) ───")]
    [Range(0.1f, 0.9f)]
    [Tooltip("Toon cel-shading gölge geçiş eşiği")]
    public float liquidRampThreshold = 0.5f;

    [Range(0.001f, 0.2f)]
    [Tooltip("Toon gölge geçişinin yumuşaklığı")]
    public float liquidRampSmooth = 0.05f;

    [Range(0.8f, 1.5f)]
    [Tooltip("Sıvı renginin canlılık çarpanı")]
    public float liquidColorBoost = 1.0f;

    [Header("─── 3. IZGARA AYARLARI (Grid) ───")]
    [Tooltip("Izgara hücrelerinin ana zemin rengi")]
    public Color gridBaseColor = new Color(0.74f, 0.80f, 0.88f, 1.0f);

    [Tooltip("Izgara hücrelerinin gölgede kalan cel-shade tonu")]
    public Color gridShadowColor = new Color(0.58f, 0.65f, 0.75f, 1.0f);

    [Tooltip("Izgara hücre kenarlarının parıltı rengi")]
    public Color gridRimColor = new Color(0.82f, 0.88f, 0.95f, 0.20f);

    [Tooltip("Izgara boş küresindeki beyaz parlama (İstenmediği için kapalı tutulur)")]
    public bool gridUseSpecular = false;

    [Header("─── 4. ÇERÇEVE AYARLARI (Frame) ───")]
    [Tooltip("Tahtayı saran ana çerçevenin rengi")]
    public Color frameBaseColor = new Color(0.18f, 0.20f, 0.32f, 1.0f);

    [Tooltip("Çerçevenin gölge tonu")]
    public Color frameShadowColor = new Color(0.10f, 0.11f, 0.18f, 1.0f);

    [Tooltip("Çerçevenin kenar parıltısı")]
    public Color frameRimColor = new Color(0.45f, 0.52f, 0.75f, 0.8f);

    [Header("─── 5. ÇERÇEVE SAHTE GÖLGE (Fake Shadow) ───")]
    [Tooltip("Çerçevenin altına derinlik kazandıran sahte gölge açık olsun mu?")]
    public bool enableFrameFakeShadow = true;

    [Tooltip("Sahte gölgenin rengi ve saydamlığı")]
    public Color frameFakeShadowColor = new Color(0.06f, 0.09f, 0.20f, 0.45f);

    [Tooltip("Gölgenin ışık açısına göre X ve Y konumu")]
    public Vector2 frameFakeShadowOffset = new Vector2(0.04f, -0.06f);

    [Range(1.0f, 10.0f)]
    [Tooltip("Gölge kenarlarının yumuşaklık / dağılma derecesi")]
    public float frameFakeShadowSoftness = 3.5f;

    [Header("─── 5. ZEMİN ARKA PLAN (Plane) ───")]
    [Tooltip("En arkadaki geniş zemin rengi")]
    public Color planeBaseColor = new Color(0.88f, 0.91f, 0.95f, 1.0f);

    [Tooltip("Zeminin gölge rengi")]
    public Color planeShadowColor = new Color(0.78f, 0.82f, 0.89f, 1.0f);

    void Awake()
    {
        Instance = this;
        EnsureMaterials();
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
            var guids = AssetDatabase.FindAssets("Çerçeve t:Material");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("er eve t:Material");
            if (guids.Length > 0)
                frameMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (planeMat == null)
            planeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Plane.mat");
        if (frameShadowMat == null)
            frameShadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FrameShadow.mat");
#endif
    }

    [ContextMenu("Değerleri Materyallere Uygula")]
    public void ApplyToMaterials()
    {
        if (glassMat != null)
        {
            glassMat.SetColor("_Color", glassTint);
            glassMat.SetColor("_SpecColor", glassSpecularColor);
            glassMat.SetFloat("_SpecSize", glassSpecularSize);
            glassMat.SetFloat("_SpecSmoothness", glassSpecularSharpness);
            glassMat.SetColor("_RimColor", glassRimColor);
            glassMat.SetFloat("_RimPower", glassRimPower);
            glassMat.SetFloat("_EdgeDarkness", glassEdgeDarkness);
            glassMat.SetColor("_EdgeOutlineColor", glassEdgeOutlineColor);
#if UNITY_EDITOR
            EditorUtility.SetDirty(glassMat);
#endif
        }

        if (liquidMat != null)
        {
            liquidMat.SetFloat("_RampThreshold", liquidRampThreshold);
            liquidMat.SetFloat("_RampSmooth", liquidRampSmooth);
            liquidMat.SetFloat("_ColorBoost", liquidColorBoost);
#if UNITY_EDITOR
            EditorUtility.SetDirty(liquidMat);
#endif
        }

        if (gridMat != null)
        {
            gridMat.SetColor("_BaseColor", gridBaseColor);
            gridMat.SetColor("_Color", gridBaseColor);
            gridMat.SetColor("_SColor", gridShadowColor);
            gridMat.SetColor("_RimColor", gridRimColor);
            gridMat.SetFloat("_UseSpecular", gridUseSpecular ? 1f : 0f);
            gridMat.SetColor("_SpecularColor", gridUseSpecular ? new Color(1,1,1,0.5f) : Color.clear);
#if UNITY_EDITOR
            EditorUtility.SetDirty(gridMat);
#endif
        }

        if (frameMat != null)
        {
            frameMat.SetColor("_BaseColor", frameBaseColor);
            frameMat.SetColor("_Color", frameBaseColor);
            frameMat.SetColor("_SColor", frameShadowColor);
            frameMat.SetColor("_RimColor", frameRimColor);
#if UNITY_EDITOR
            EditorUtility.SetDirty(frameMat);
#endif
        }

        if (frameShadowMat != null)
        {
            frameShadowMat.SetColor("_Color", frameFakeShadowColor);
            frameShadowMat.SetFloat("_Softness", frameFakeShadowSoftness);
#if UNITY_EDITOR
            EditorUtility.SetDirty(frameShadowMat);
#endif
        }

        var spawner = FindObjectOfType<GridSpawner>();
        if (spawner != null)
        {
            spawner.enableFrameFakeShadow = enableFrameFakeShadow;
            spawner.frameShadowOffset = frameFakeShadowOffset;
            if (spawner.frameShadowMaterial == null)
                spawner.frameShadowMaterial = frameShadowMat;
        }

        if (planeMat != null)
        {
            planeMat.SetColor("_BaseColor", planeBaseColor);
            planeMat.SetColor("_Color", planeBaseColor);
            planeMat.SetColor("_SColor", planeShadowColor);
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

        if (frameMat != null)
        {
            if (frameMat.HasProperty("_BaseColor")) frameBaseColor = frameMat.GetColor("_BaseColor");
            if (frameMat.HasProperty("_SColor")) frameShadowColor = frameMat.GetColor("_SColor");
            if (frameMat.HasProperty("_RimColor")) frameRimColor = frameMat.GetColor("_RimColor");
        }

        if (planeMat != null)
        {
            if (planeMat.HasProperty("_BaseColor")) planeBaseColor = planeMat.GetColor("_BaseColor");
            if (planeMat.HasProperty("_SColor")) planeShadowColor = planeMat.GetColor("_SColor");
        }
    }

    // ── Hızlı Önayarlar ──────────────────────────────
    public void Preset_CrystalGlass()
    {
        glassTint = new Color(0.9f, 0.95f, 1.0f, 0.02f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 0.9f);
        glassSpecularSize = 0.025f;
        glassSpecularSharpness = 0.008f;
        glassRimColor = new Color(0.9f, 0.95f, 1.0f, 0.4f);
        glassRimPower = 3.5f;
        glassEdgeDarkness = 0.45f;
        glassEdgeOutlineColor = new Color(0.25f, 0.35f, 0.5f, 1.0f);

        gridBaseColor = new Color(0.74f, 0.80f, 0.88f, 1.0f);
        gridShadowColor = new Color(0.58f, 0.65f, 0.75f, 1.0f);

        ApplyToMaterials();
    }

    public void Preset_VisibleToonGlass()
    {
        glassTint = new Color(0.82f, 0.92f, 1.0f, 0.08f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        glassSpecularSize = 0.035f;
        glassSpecularSharpness = 0.012f;
        glassRimColor = new Color(0.75f, 0.88f, 1.0f, 0.65f);
        glassRimPower = 2.4f;
        glassEdgeDarkness = 0.55f;
        glassEdgeOutlineColor = new Color(0.2f, 0.32f, 0.48f, 1.0f);

        gridBaseColor = new Color(0.72f, 0.78f, 0.86f, 1.0f);
        gridShadowColor = new Color(0.55f, 0.62f, 0.72f, 1.0f);

        ApplyToMaterials();
    }

    public void Preset_SoftPastel()
    {
        glassTint = new Color(0.95f, 0.95f, 1.0f, 0.04f);
        glassSpecularColor = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        glassSpecularSize = 0.02f;
        glassSpecularSharpness = 0.01f;
        glassRimColor = new Color(0.9f, 0.92f, 1.0f, 0.35f);
        glassRimPower = 3.0f;
        glassEdgeDarkness = 0.3f;
        glassEdgeOutlineColor = new Color(0.4f, 0.45f, 0.55f, 1.0f);

        gridBaseColor = new Color(0.80f, 0.84f, 0.90f, 1.0f);
        gridShadowColor = new Color(0.68f, 0.72f, 0.80f, 1.0f);

        ApplyToMaterials();
    }
}
