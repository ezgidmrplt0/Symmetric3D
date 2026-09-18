using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// VisualThemeController için özel, tamamen Türkçeleştirilmiş, açıklamalı ve estetik Inspector arayüzü.
/// </summary>
[CustomEditor(typeof(VisualThemeController))]
public class VisualThemeControllerEditor : Editor
{
    private static bool foldGlass = true;
    private static bool foldLiquid = true;
    private static bool foldGrid = true;
    private static bool foldFrame = true;
    private static bool foldShadow = true;
    private static bool foldPlane = true;
    private static bool foldMaterials = false;

    // Önizleme ayarları
    public enum PreviewTargetMode
    {
        Level1_Sabit,
        TestPaleti_9Renk,
        OzelSeviye
    }

    public static PreviewTargetMode previewMode = PreviewTargetMode.Level1_Sabit;
    public static LevelData customPreviewLevel = null;

    public override void OnInspectorGUI()
    {
        VisualThemeController controller = (VisualThemeController)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.25f, 0.75f, 1f) }
        };

        GUIStyle subTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.78f, 0.28f) }
        };

        GUIStyle noteStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎨 GÖRSEL TEMA VE MATERYAL KONTROLCÜSÜ", titleStyle);
        EditorGUILayout.HelpBox("Değerleri değiştirdiğiniz anda sahnedeki cam küreler, sıvı cel-shading, ızgara yuvaları, çerçeve ve zemin anında canlı olarak güncellenir.", MessageType.Info);

        // ──────────────────────────────────────────────────────────────
        // 📌 1. SEVİYE SABİTLİ CANLI SAHNE ÖNİZLEMESİ
        // ──────────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("👁️ CANLI SAHNE ÖNİZLEMESİ (Editör Modu)", subTitleStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool isPreviewing = VisualThemePreviewManager.IsPreviewActive;

        // Önizleme Seviye Seçimi
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🎯 Önizleme Hedefi:", EditorStyles.boldLabel, GUILayout.Width(125));
        
        string[] modeLabels = new string[] { "📌 1. Seviye (Sabit)", "🎨 9 Renkli Test Tahtası", "📂 Başka Seviye" };
        previewMode = (PreviewTargetMode)GUILayout.Toolbar((int)previewMode, modeLabels, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        if (previewMode == PreviewTargetMode.Level1_Sabit)
        {
            EditorGUILayout.HelpBox("📌 1. Seviye Sabitlendi: Kamera otomatik olarak oyun alanına hizalanır. 2x2 grid yuvaları, beyaz alt plakalar, 2 adet kırmızı cam parça ve çerçeve tam olarak oyundaki yerinde gösterilir.", MessageType.None);
        }
        else if (previewMode == PreviewTargetMode.TestPaleti_9Renk)
        {
            EditorGUILayout.HelpBox("🎨 9 Renkli Test Tahtası: Mavi, kırmızı, sarı, yeşil, mor, turuncu gibi tüm renklerin cam ve cel-shading uyumunu tek seferde test edin.", MessageType.None);
        }
        else
        {
            customPreviewLevel = (LevelData)EditorGUILayout.ObjectField("Seviye Asseti:", customPreviewLevel, typeof(LevelData), false);
            if (customPreviewLevel == null)
            {
                EditorGUILayout.HelpBox("Lütfen test etmek istediğiniz Level Data dosyasını buraya sürükleyin.", MessageType.Warning);
            }
        }

        GUILayout.Space(4);

        // Durum Göstergesi
        string statusText = isPreviewing
            ? "● Durum: Canlı Sahne Önizlemesi AÇIK (Kamera ve Tahta Odaklandı)"
            : "○ Durum: Önizleme Kapalı";
        EditorGUILayout.LabelField(statusText, isPreviewing ? EditorStyles.boldLabel : EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (!isPreviewing)
        {
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            string btnText = previewMode == PreviewTargetMode.Level1_Sabit 
                ? "▶ 1. Seviye Önizlemesini Aç (Hem Game Hem Scene)" 
                : "▶ Sahne Önizlemesini Aç";

            if (GUILayout.Button(btnText, GUILayout.Height(36)))
            {
                LevelData targetLvl = GetSelectedPreviewLevel();
                VisualThemePreviewManager.SpawnPreview(controller, targetLvl, previewMode == PreviewTargetMode.TestPaleti_9Renk);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("⏹ Önizlemeyi Kapat", GUILayout.Height(36)))
            {
                VisualThemePreviewManager.DestroyPreview();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🎯 Kamerayı Odakla", GUILayout.Height(36), GUILayout.Width(130)))
            {
                VisualThemePreviewManager.FocusCamera();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("🔄 Yenile", GUILayout.Height(36), GUILayout.Width(75)))
            {
                LevelData targetLvl = GetSelectedPreviewLevel();
                VisualThemePreviewManager.SpawnPreview(controller, targetLvl, previewMode == PreviewTargetMode.TestPaleti_9Renk);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // ──────────────────────────────────────────────────────────────
        // ⚡ HAZIR ÖNAYARLAR (PRESETS)
        // ──────────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("⚡ Hızlı Hazır Önayarlar (Presets)", subTitleStyle);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("✨ Belirgin Toon Cam", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Visible Toon Glass");
            controller.Preset_VisibleToonGlass();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        if (GUILayout.Button("💎 Kristal Cam", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Crystal Glass");
            controller.Preset_CrystalGlass();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        if (GUILayout.Button("🎨 Soft Pastel", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Soft Pastel");
            controller.Preset_SoftPastel();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        if (GUILayout.Button("🍬 Tatlı Jelibon", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Candy Gloss");
            controller.Preset_CandyGloss();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // ──────────────────────────────────────────────────────────────
        // 🛠️ TÜRKÇELEŞTİRİLMİŞ VE AÇIKLAMALI DETAYLI AYARLAR
        // ──────────────────────────────────────────────────────────────
        EditorGUI.BeginChangeCheck();

        // ── 1. CAM KÜRE AYARLARI ──
        foldGlass = DrawCategoryHeader("💎 1. CAM KÜRE AYARLARI (Glass Shader)", foldGlass, new Color(0.4f, 0.85f, 1f));
        if (foldGlass)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            controller.glassTint = EditorGUILayout.ColorField("Cam Renk Tonu ve Saydamlık (Tint)", controller.glassTint);
            EditorGUILayout.LabelField("   └ İpucu: Alpha (A) değerini düşürdükçe cam kristal gibi berraklaşır, yükselttikçe gövde rengi koyulaşır.", noteStyle);
            GUILayout.Space(3);

            controller.glassUseSpecular = EditorGUILayout.Toggle("Cam Parlama Noktasını Aktif Et", controller.glassUseSpecular);
            EditorGUILayout.LabelField("   └ İpucu: Cam kürenin üstündeki beyaz karikatür parlama noktasını açar/kapatır.", noteStyle);
            GUILayout.Space(3);

            if (controller.glassUseSpecular)
            {
                controller.glassSpecularColor = EditorGUILayout.ColorField("Karikatür Parlama Rengi", controller.glassSpecularColor);
                controller.glassSpecularSize = EditorGUILayout.Slider("Parlama Noktası Boyutu", controller.glassSpecularSize, 0.005f, 0.1f);
                controller.glassSpecularSharpness = EditorGUILayout.Slider("Parlama Kenar Keskinliği", controller.glassSpecularSharpness, 0.001f, 0.05f);
                GUILayout.Space(3);
            }

            controller.glassRimColor = EditorGUILayout.ColorField("Kenar Işıması Rengi (Rim Light)", controller.glassRimColor);
            EditorGUILayout.LabelField("   └ İpucu: Cam kürenin dış çeperini aydınlatan hale ışığı rengi.", noteStyle);
            GUILayout.Space(3);

            controller.glassRimPower = EditorGUILayout.Slider("Kenar Işıması Gücü / İnceliği", controller.glassRimPower, 0.5f, 8.0f);
            EditorGUILayout.LabelField("   └ İpucu: Yüksek = sadece en dış sınırda ince hat | Düşük = kürenin içine doğru yayılan geniş ışık.", noteStyle);
            GUILayout.Space(3);

            controller.glassEdgeDarkness = EditorGUILayout.Slider("Dış Kontur / Silüet Çizgisi Gücü", controller.glassEdgeDarkness, 0.0f, 1.0f);
            EditorGUILayout.LabelField("   └ İpucu: Cam kürenin sınırlarını belirginleştiren ve arka plandan ayıran dış çizgi kuvveti.", noteStyle);
            GUILayout.Space(3);

            controller.glassEdgeOutlineColor = EditorGUILayout.ColorField("Dış Kontur Rengi", controller.glassEdgeOutlineColor);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 2. SIVI VE CEL-SHADING AYARLARI ──
        foldLiquid = DrawCategoryHeader("🧪 2. SIVI VE CEL-SHADING AYARLARI (Liquid Shader)", foldLiquid, new Color(1f, 0.55f, 0.45f));
        if (foldLiquid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            controller.liquidRampThreshold = EditorGUILayout.Slider("Çizgi Film Gölge Sınır Eşiği", controller.liquidRampThreshold, 0.1f, 0.9f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvı üzerindeki aydınlık ve gölge alanın sınır çizgisi. Düşük = geniş aydınlık | Yüksek = geniş gölge.", noteStyle);
            GUILayout.Space(3);

            controller.liquidRampSmooth = EditorGUILayout.Slider("Gölge Geçiş Yumuşaklığı", controller.liquidRampSmooth, 0.001f, 0.2f);
            EditorGUILayout.LabelField("   └ İpucu: Düşük = sert ve keskin çizgi film Toon geçişi | Yüksek = yumuşak gradyan.", noteStyle);
            GUILayout.Space(3);

            controller.liquidColorBoost = EditorGUILayout.Slider("Sıvı Renk Canlılığı (Boost)", controller.liquidColorBoost, 0.8f, 2.0f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvı renklerinin canlılığını, doygunluğunu ve patlama parlaklığını artırır.", noteStyle);
            GUILayout.Space(3);

            controller.liquidVibranceNorm = EditorGUILayout.Slider("Akıllı Renk Canlılığı (Vibrance)", controller.liquidVibranceNorm, 0.0f, 1.0f);
            EditorGUILayout.LabelField("   └ İpucu: Koyu yeşil, mor gibi sönük kalan renkleri parlak şeker rengine yükseltir; siyahı bozmaz.", noteStyle);
            GUILayout.Space(3);

            controller.liquidInnerGlow = EditorGUILayout.Slider("Sıvı İç Işıması (Candy Glow)", controller.liquidInnerGlow, 0.0f, 1.0f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvının içten dışa kendi renginde ışımasını sağlar; çamurlu gölgeleri önler.", noteStyle);
            GUILayout.Space(3);

            controller.liquidMeniscusWidth = EditorGUILayout.Slider("Yüzey Kavis Çizgisi Kalınlığı (Menisküs)", controller.liquidMeniscusWidth, 0.005f, 0.06f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvının üst kavisli yüzeyindeki ince ışık çizgisi kalınlığı.", noteStyle);
            GUILayout.Space(3);

            controller.liquidMeniscusIntensity = EditorGUILayout.Slider("Yüzey Kavis Parlaklığı", controller.liquidMeniscusIntensity, 0.0f, 2.0f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvı menisküs kavis çizgisinin parlaklık şiddeti.", noteStyle);
            GUILayout.Space(3);

            controller.liquidHighlightIntensity = EditorGUILayout.Slider("Sıvı Üzeri Parlama Işığı", controller.liquidHighlightIntensity, 0.0f, 3.0f);
            EditorGUILayout.LabelField("   └ İpucu: Sıvının gövdesindeki ışık yansıması gücü.", noteStyle);
            GUILayout.Space(3);

            controller.liquidRimIntensity = EditorGUILayout.Slider("Sıvı Kenar Işık Şiddeti (Rim)", controller.liquidRimIntensity, 0.0f, 5.0f);
            controller.liquidRimPower = EditorGUILayout.Slider("Sıvı Kenar Işık Odağı (Power)", controller.liquidRimPower, 0.1f, 8.0f);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 3. IZGARA YUVALARI AYARLARI ──
        foldGrid = DrawCategoryHeader("🔲 3. IZGARA VE TAHTA YUVALARI (Grid Shader)", foldGrid, new Color(0.7f, 0.85f, 0.95f));
        if (foldGrid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            controller.gridBaseColor = EditorGUILayout.ColorField("Izgara Ana Zemin Rengi", controller.gridBaseColor);
            EditorGUILayout.LabelField("   └ İpucu: Hücre yuvalarının aydınlık üst yüzey rengi.", noteStyle);
            GUILayout.Space(3);

            controller.gridShadowColor = EditorGUILayout.ColorField("Izgara Derinlik Gölge Rengi", controller.gridShadowColor);
            EditorGUILayout.LabelField("   └ İpucu: Yuvaların çukur ve iç kısımlarına derinlik veren gölge tonu.", noteStyle);
            GUILayout.Space(3);

            controller.gridRimColor = EditorGUILayout.ColorField("Izgara Kenar Parıltısı", controller.gridRimColor);
            EditorGUILayout.LabelField("   └ İpucu: Yuva kenarlarındaki zarif ışık çerçevesi.", noteStyle);
            GUILayout.Space(3);

            controller.gridUseSpecular = EditorGUILayout.Toggle("Boş Yuvalarda Beyaz Parlama Olsun", controller.gridUseSpecular);
            EditorGUILayout.LabelField("   └ İpucu: Topsuz boş hücrelerde göz alan beyaz noktayı engellemek için kapalı tutulması önerilir.", noteStyle);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 4. OYUN ÇERÇEVESİ AYARLARI ──
        foldFrame = DrawCategoryHeader("🖼️ 4. OYUN ALANI ÇERÇEVESİ (Frame Shader)", foldFrame, new Color(0.6f, 0.7f, 0.95f));
        if (foldFrame)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            controller.frameBaseColor = EditorGUILayout.ColorField("Çerçeve Ana Rengi", controller.frameBaseColor);
            EditorGUILayout.LabelField("   └ İpucu: Tahtayı saran şık dış çerçevenin rengi.", noteStyle);
            GUILayout.Space(3);

            controller.frameShadowColor = EditorGUILayout.ColorField("Çerçeve Gölge Tonu", controller.frameShadowColor);
            EditorGUILayout.LabelField("   └ İpucu: Çerçevenin alt ve iç kenar gölgesi.", noteStyle);
            GUILayout.Space(3);

            controller.frameUseSpecular = EditorGUILayout.Toggle("Çerçevede Beyaz Parlama Olsun", controller.frameUseSpecular);
            EditorGUILayout.LabelField("   └ İpucu: Çerçevenin köşelerindeki göz alan beyaz parlamayı engellemek için kapalı tutulması önerilir.", noteStyle);
            GUILayout.Space(3);

            controller.frameRimColor = EditorGUILayout.ColorField("Çerçeve Kenar Işıması (Parlama)", controller.frameRimColor);
            EditorGUILayout.LabelField("   └ İpucu: Çerçevenin üst kenarındaki parlama çizgisi (Mat çerçeve için siyah/saydam yapın).", noteStyle);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 5. SAHTE GÖLGELER ──
        foldShadow = DrawCategoryHeader("🌑 5. SAHTE GÖLGELER (Fake Drop Shadows)", foldShadow, new Color(0.65f, 0.65f, 0.8f));
        if (foldShadow)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Çerçeve Sahte Gölgesi
            EditorGUILayout.LabelField("─── ÇERÇEVE SAHTE GÖLGESİ ───", EditorStyles.boldLabel);
            controller.enableFrameFakeShadow = EditorGUILayout.Toggle("Çerçeve Gölgesi Aktif", controller.enableFrameFakeShadow);

            if (controller.enableFrameFakeShadow)
            {
                controller.frameFakeShadowColor = EditorGUILayout.ColorField("Gölge Rengi ve Alpha", controller.frameFakeShadowColor);
                controller.frameFakeShadowOffset = EditorGUILayout.Vector2Field("Gölge Düşüş Açısı (X / Y Offset)", controller.frameFakeShadowOffset);
                controller.frameFakeShadowSoftness = EditorGUILayout.Slider("Gölge Kenar Yumuşaklığı", controller.frameFakeShadowSoftness, 0.5f, 10.0f);
                controller.frameShadowScaleMultiplier = EditorGUILayout.Slider("Gölge Boyut Çarpanı", controller.frameShadowScaleMultiplier, 1.0f, 1.35f);
            }

            EditorGUILayout.Space(6);

            // Top Sahte Gölgesi
            EditorGUILayout.LabelField("─── TOP / KÜRE SAHTE GÖLGELERİ ───", EditorStyles.boldLabel);
            controller.enableBallFakeShadow = EditorGUILayout.Toggle("Top Gölgeleri Aktif", controller.enableBallFakeShadow);
            EditorGUILayout.LabelField("   └ İpucu: Her kürenin altına tahtaya temas hissi veren dairesel yumuşak gölge ekler.", noteStyle);

            if (controller.enableBallFakeShadow)
            {
                controller.ballFakeShadowColor = EditorGUILayout.ColorField("Top Gölge Rengi ve Alpha", controller.ballFakeShadowColor);
                controller.ballFakeShadowOffset = EditorGUILayout.Vector2Field("Top Gölge Konumu (X / Y Offset)", controller.ballFakeShadowOffset);
                controller.ballFakeShadowSize = EditorGUILayout.Slider("Top Gölge Boyutu", controller.ballFakeShadowSize, 0.2f, 1.0f);
                controller.ballFakeShadowSoftness = EditorGUILayout.Slider("Top Gölge Yumuşaklığı", controller.ballFakeShadowSoftness, 0.5f, 10.0f);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 6. ARKA PLAN ZEMİN AYARLARI ──
        foldPlane = DrawCategoryHeader("⚪ 6. ARKA PLAN ZEMİN AYARLARI (Plane Shader)", foldPlane, new Color(0.85f, 0.9f, 0.95f));
        if (foldPlane)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            controller.planeBaseColor = EditorGUILayout.ColorField("Zemin Ana Rengi", controller.planeBaseColor);
            EditorGUILayout.LabelField("   └ İpucu: Oyunun en arkasındaki geniş zemin düzlüğünün rengi.", noteStyle);
            GUILayout.Space(3);

            controller.planeShadowColor = EditorGUILayout.ColorField("Zemin Gölge Tonu", controller.planeShadowColor);
            EditorGUILayout.LabelField("   └ İpucu: Zemin üzerindeki atmosferik ışık/gölge derinliği.", noteStyle);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 7. MATERYAL REFERANSLARI (GELİŞMİŞ) ──
        foldMaterials = DrawCategoryHeader("📁 MATERYAL DOSYA REFERANSLARI (Gelişmiş)", foldMaterials, new Color(0.75f, 0.75f, 0.75f));
        if (foldMaterials)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            controller.glassMat = (Material)EditorGUILayout.ObjectField("💎 Cam Materyali (Glass.mat)", controller.glassMat, typeof(Material), false);
            controller.liquidMat = (Material)EditorGUILayout.ObjectField("🧪 Sıvı Materyali (Shader.mat)", controller.liquidMat, typeof(Material), false);
            controller.gridMat = (Material)EditorGUILayout.ObjectField("🔲 Izgara Materyali (Grid.mat)", controller.gridMat, typeof(Material), false);
            controller.frameMat = (Material)EditorGUILayout.ObjectField("🖼️ Çerçeve Materyali (Çerçeve.mat)", controller.frameMat, typeof(Material), false);
            controller.planeMat = (Material)EditorGUILayout.ObjectField("⚪ Zemin Materyali (Plane.mat)", controller.planeMat, typeof(Material), false);
            controller.frameShadowMat = (Material)EditorGUILayout.ObjectField("🌑 Çerçeve Gölgesi (FrameShadow.mat)", controller.frameShadowMat, typeof(Material), false);
            controller.ballShadowMat = (Material)EditorGUILayout.ObjectField("⚫ Top Gölgesi (BallShadow.mat)", controller.ballShadowMat, typeof(Material), false);
            EditorGUILayout.EndVertical();
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(controller, "Görsel Tema Ayarı Değiştirildi");
            controller.ApplyToMaterials();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }

            // Önizleme aktifse sahte gölge veya çerçeve değişikliklerini hemen yansıtmak için
            if (isPreviewing)
            {
                VisualThemePreviewManager.SyncMaterials(controller);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 💾 KAYDET VE YÖNET BUTONLARI
        // ──────────────────────────────────────────────────────────────
        EditorGUILayout.Space(12);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("💾 Ayarları ve Materyalleri Kaydet", GUILayout.Height(36)))
        {
            controller.ApplyToMaterials();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                EditorSceneManager.SaveScene(controller.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Kaydedildi", "Tüm görsel tema ayarları sahneye ve materyal dosyalarına başarıyla kaydedildi.", "Tamam");
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("🔄 Materyallerden Geri Oku", GUILayout.Height(36), GUILayout.Width(170)))
        {
            controller.ReadFromMaterials();
            EditorUtility.SetDirty(controller);
            if (!Application.isPlaying && controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
    }

    private bool DrawCategoryHeader(string label, bool foldState, Color color)
    {
        GUIStyle foldStyle = new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };
        return EditorGUILayout.Foldout(foldState, label, true, foldStyle);
    }

    private LevelData GetSelectedPreviewLevel()
    {
        if (previewMode == PreviewTargetMode.Level1_Sabit)
        {
            return AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/Level_01.asset");
        }
        else if (previewMode == PreviewTargetMode.OzelSeviye && customPreviewLevel != null)
        {
            return customPreviewLevel;
        }
        return null;
    }
}

/// <summary>
/// Görsel Tema Stüdyosu Penceresi (Menüden açılan ana stüdyo paneli)
/// </summary>
public class VisualThemeStudioWindow : EditorWindow
{
    private VisualThemeController controller;
    private Vector2 scrollPos;
    private Editor cachedEditor;

    [MenuItem("Tools/Görsel Tema Stüdyosu (Visual Theme Studio)")]
    public static void ShowWindow()
    {
        VisualThemeStudioWindow window = GetWindow<VisualThemeStudioWindow>("Tema Stüdyosu");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }

    void OnEnable()
    {
        FindOrCreateController();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        // Pencere kapandığında sahnede kalan geçici önizlemeyi temizle
        VisualThemePreviewManager.DestroyPreview();
        if (cachedEditor != null)
        {
            DestroyImmediate(cachedEditor);
            cachedEditor = null;
        }
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (controller != null)
            {
                controller.ApplyToMaterials();
                EditorUtility.SetDirty(controller);
                if (!Application.isPlaying && controller.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                }
            }
            AssetDatabase.SaveAssets();
            VisualThemePreviewManager.DestroyPreview();
        }
    }

    private void FindOrCreateController()
    {
        controller = FindObjectOfType<VisualThemeController>();
        if (controller == null)
        {
            GameObject go = GameObject.Find("[Visual Theme Controller]");
            if (go == null)
            {
                go = new GameObject("[Visual Theme Controller]");
                Undo.RegisterCreatedObjectUndo(go, "Create Theme Controller");
            }
            controller = go.GetComponent<VisualThemeController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<VisualThemeController>(go);
            }
            controller.EnsureMaterials();
            controller.ReadFromMaterials();
        }
        else
        {
            controller.EnsureMaterials();
        }
    }

    void OnGUI()
    {
        if (controller == null)
        {
            FindOrCreateController();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("Sahnede VisualThemeController bulunamadı.", MessageType.Warning);
                return;
            }
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (cachedEditor == null || cachedEditor.target != controller)
        {
            if (cachedEditor != null) DestroyImmediate(cachedEditor);
            cachedEditor = Editor.CreateEditor(controller);
        }

        if (cachedEditor != null)
        {
            cachedEditor.OnInspectorGUI();
        }

        EditorGUILayout.EndScrollView();
    }
}

/// <summary>
/// 1. Seviye (veya seçili seviye) canlı sahne önizleme motoru.
/// GridSpawner ile tam aynı dünya koordinatlarını, aynı prefabları (Çerçeve.prefab, Plaka.prefab, Grid.prefab)
/// kullanır ve hem SceneView hem de Game/Simulator kamerasını tam tahtanın üzerine hizalar.
/// </summary>
public static class VisualThemePreviewManager
{
    private const string PREVIEW_ROOT_NAME = "[VisualTheme_PreviewRoot]";

    public static bool IsPreviewActive => GameObject.Find(PREVIEW_ROOT_NAME) != null;

    private static Vector3 originalCamPos;
    private static Quaternion originalCamRot;
    private static float originalCamOrthoSize;
    private static Color originalCamBgColor;
    private static bool hasSavedCam = false;

    private static GameObject cachedTutRoot = null;
    private static bool cachedTutActive = false;

    public static void DestroyPreview()
    {
        GameObject root = GameObject.Find(PREVIEW_ROOT_NAME);
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }

        // Kamerayı eski konumuna geri döndür
        Camera cam = Camera.main;
        if (cam != null && hasSavedCam)
        {
            cam.transform.position = originalCamPos;
            cam.transform.rotation = originalCamRot;
            cam.orthographicSize = originalCamOrthoSize;
            cam.backgroundColor = originalCamBgColor;
            hasSavedCam = false;
        }

        // Tutorial elini eski durumuna getir
        if (cachedTutRoot != null)
        {
            cachedTutRoot.SetActive(cachedTutActive);
            cachedTutRoot = null;
        }

        SceneView.RepaintAll();
    }

    public static void SyncMaterials(VisualThemeController controller)
    {
        controller.ApplyToMaterials();

        // Kamera arka plan rengini zemin rengiyle eşle
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = controller.planeBaseColor;
        }

        SceneView.RepaintAll();
    }

    public static void FocusCamera()
    {
        GameObject root = GameObject.Find(PREVIEW_ROOT_NAME);
        if (root == null) return;

        Bounds b = CalculatePreviewBounds(root);

        // 1. SceneView Kamerasını Odakla
        if (SceneView.lastActiveSceneView != null)
        {
            Bounds sceneBounds = b;
            sceneBounds.Expand(1.5f);
            SceneView.lastActiveSceneView.Frame(sceneBounds, false);
        }

        // 2. Game / Simulator Kamerasını Odakla
        Camera cam = Camera.main;
        if (cam != null)
        {
            AlignMainCameraToBoard(cam, b);
        }
    }

    private static Bounds CalculatePreviewBounds(GameObject root)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(root.transform.position, new Vector3(3f, 3f, 2f));

        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool inited = false;
        foreach (var r in rends)
        {
            if (r.name.Contains("BackgroundPlane") || r.name.Contains("Shadow")) continue;
            if (!inited) { b = r.bounds; inited = true; }
            else b.Encapsulate(r.bounds);
        }
        return inited ? b : new Bounds(root.transform.position, new Vector3(3f, 3f, 2f));
    }

    /// <summary>
    /// Seviye 1 veya belirtilen seviyeyi Editör Sahnesinde canlı olarak oluşturur.
    /// </summary>
    public static void SpawnPreview(VisualThemeController controller, LevelData level = null, bool useColorPaletteMode = false)
    {
        DestroyPreview();

        // Sahnede TutorialHand varsa geçici gizle (ekranı kapatmasın)
        cachedTutRoot = GameObject.Find("TransferTutorialRoot");
        if (cachedTutRoot != null)
        {
            cachedTutActive = cachedTutRoot.activeSelf;
            cachedTutRoot.SetActive(false);
        }

        // Ana kameranın orijinal durumunu sakla
        Camera cam = Camera.main;
        if (cam != null && !hasSavedCam)
        {
            originalCamPos = cam.transform.position;
            originalCamRot = cam.transform.rotation;
            originalCamOrthoSize = cam.orthographicSize;
            originalCamBgColor = cam.backgroundColor;
            hasSavedCam = true;
        }

        // Sahnede GridManager var mı? Varsa onun tam dünya pozisyonunu ve ayarlarını kullan
        GridSpawner spawner = Object.FindObjectOfType<GridSpawner>();
        Vector3 rootWorldPos = spawner != null ? spawner.transform.position : new Vector3(0.014f, 1.6395396f, -0.114f);

        GameObject previewRoot = new GameObject(PREVIEW_ROOT_NAME);
        previewRoot.hideFlags = HideFlags.DontSave;
        previewRoot.transform.position = rootWorldPos;

        // Prefab referansları (GUID ile garanti yükleme)
        GameObject gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grid.prefab");
        GameObject piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainObject.prefab");
        
        // Çerçeve prefabı (Çerçeve.prefab)
        GameObject framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("27a9482575df76f6095aaf58024fda5b"));
        if (framePrefab == null) framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Çerçeve.prefab");

        // Plaka prefabı (Plaka.prefab)
        GameObject platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("c3e229a9c7edd294c840d7268bc203de"));
        if (platePrefab == null) platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Plaka.prefab");

        // GridSpawner parametreleri (GridManager'daki ile birebir aynı değerler)
        float gridSize = gridPrefab != null ? gridPrefab.transform.localScale.x : 0.5f;
        float spacing = spawner != null ? spawner.spacing : 0.2f;
        float objectOffset = spawner != null ? spawner.objectOffset : 0.3f;
        float frameThickness = spawner != null ? spawner.frameThickness : 0.2f;
        float framePadding = spawner != null ? spawner.framePadding : 0.1f;
        float step = gridSize + spacing; // 0.5 + 0.2 = 0.7f

        // 9 Renkli Test Paleti Modu
        if (useColorPaletteMode)
        {
            SpawnColorPaletteMode(controller, previewRoot, gridPrefab, piecePrefab, framePrefab, platePrefab, step, gridSize, frameThickness, framePadding, objectOffset);
            controller.ApplyToMaterials();
            SyncMaterials(controller);
            FocusCamera();
            return;
        }

        // Seviye belirtilmemişse 1. Seviyeyi (Level_01) sabit yükle
        if (level == null)
        {
            level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/Level_01.asset");
        }

        if (level == null)
        {
            SpawnColorPaletteMode(controller, previewRoot, gridPrefab, piecePrefab, framePrefab, platePrefab, step, gridSize, frameThickness, framePadding, objectOffset);
            controller.ApplyToMaterials();
            SyncMaterials(controller);
            FocusCamera();
            return;
        }

        // ──────────────────────────────────────────────────────────────
        // 1. GERÇEK SEVİYE MATEMATİĞİ (Flat2D Grid)
        // ──────────────────────────────────────────────────────────────
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        if (level.customGridPositions != null && level.customGridPositions.Count > 0)
        {
            foreach (var p in level.customGridPositions) occupied.Add(p);
        }
        else
        {
            for (int x = 0; x < level.gridX; x++)
                for (int y = 0; y < level.gridY; y++)
                    occupied.Add(new Vector2Int(x, y));
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var pos in occupied)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        float offsetX = (minX + maxX) * step / 2f;
        float offsetY = (minY + maxY) * step / 2f;

        // 1.1 Zemin Arka Plan Plakası (Plane.mat)
        float boardWidth = (maxX - minX + 1) * step + 4.0f;
        float boardHeight = (maxY - minY + 1) * step + 4.0f;
        GameObject bgPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgPlate.name = "Preview_BackgroundPlane";
        Object.DestroyImmediate(bgPlate.GetComponent<BoxCollider>());
        bgPlate.transform.SetParent(previewRoot.transform);
        bgPlate.transform.localPosition = new Vector3(0, 0, 0.4f);
        bgPlate.transform.localScale = new Vector3(Mathf.Max(boardWidth, 10f), Mathf.Max(boardHeight, 10f), 0.02f);
        if (controller.planeMat != null)
            bgPlate.GetComponent<Renderer>().sharedMaterial = controller.planeMat;

        // 1.2 Grid Yuvaları ve Beyaz Zemin Altlıkları (Plaka.prefab)
        float localPlateZ = 0.015f;
        foreach (var pos in occupied)
        {
            Vector3 tileLocalPos = new Vector3(pos.x * step - offsetX, pos.y * step - offsetY, 0);

            // Yuva arkası beyaz plaka (Plaka.prefab)
            GameObject bgTile = null;
            if (platePrefab != null)
            {
                bgTile = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, previewRoot.transform);
                Object.DestroyImmediate(bgTile.GetComponent<Collider>());
            }
            else
            {
                bgTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(bgTile.GetComponent<BoxCollider>());
                bgTile.transform.SetParent(previewRoot.transform);
            }
            bgTile.name = $"Preview_GridBG_{pos.x}_{pos.y}";
            bgTile.transform.localPosition = new Vector3(tileLocalPos.x, tileLocalPos.y, localPlateZ);
            bgTile.transform.localScale = new Vector3(step, step, 0.01f);

            // Yuva (Grid.prefab)
            if (gridPrefab != null)
            {
                GameObject gridObj = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, previewRoot.transform);
                gridObj.name = $"Preview_Grid_{pos.x}_{pos.y}";
                gridObj.transform.localPosition = tileLocalPos;
                Object.DestroyImmediate(gridObj.GetComponent<Collider>());

                Renderer gr = gridObj.GetComponent<Renderer>();
                if (gr != null && controller.gridMat != null)
                {
                    gr.sharedMaterial = controller.gridMat;
                }
            }
        }

        // 1.3 Seviyenin Gerçek Parçaları (Cam Küreler ve Sıvılar)
        if (piecePrefab != null && level.pieces != null)
        {
            foreach (var piece in level.pieces)
            {
                Vector3 piecePos = new Vector3(
                    piece.gridPosition.x * step - offsetX,
                    piece.gridPosition.y * step - offsetY,
                    -objectOffset
                );

                GameObject pieceObj = (GameObject)PrefabUtility.InstantiatePrefab(piecePrefab, previewRoot.transform);
                pieceObj.name = $"Preview_Piece_{piece.gridPosition.x}_{piece.gridPosition.y}";
                pieceObj.transform.localPosition = piecePos;
                pieceObj.transform.localRotation = Quaternion.Euler(0, 0, piece.rotationZ);

                // Editörde collider ve drag'ı temizle
                var colliders = pieceObj.GetComponentsInChildren<Collider>();
                foreach (var col in colliders) Object.DestroyImmediate(col);

                var dragCode = pieceObj.GetComponent<DragObject>();
                if (dragCode != null) Object.DestroyImmediate(dragCode);

                // Cam materyalini doğrudan uygula
                MeshRenderer mr = pieceObj.GetComponent<MeshRenderer>();
                if (mr != null && controller.glassMat != null)
                {
                    mr.sharedMaterial = controller.glassMat;
                }

                // Sıvı özelliklerini (renk, dilim vb.) uygula
                LiquidTransfer lt = pieceObj.GetComponentInChildren<LiquidTransfer>();
                if (lt != null)
                {
                    lt.liquidColor = piece.liquidColor;
                    lt.currentSlices = piece.currentSlices > 0 ? piece.currentSlices : 2;
                    lt.UpdateVisuals();
                }

                // Top Sahte Gölgesi (Preview)
                if (controller.enableBallFakeShadow)
                {
                    GameObject bShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    bShadow.name = $"Preview_BallShadow_{piece.gridPosition.x}_{piece.gridPosition.y}";
                    Object.DestroyImmediate(bShadow.GetComponent<Collider>());
                    bShadow.transform.SetParent(previewRoot.transform);
                    bShadow.transform.localPosition = piecePos + new Vector3(controller.ballFakeShadowOffset.x, controller.ballFakeShadowOffset.y, 0.28f);
                    bShadow.transform.localScale = new Vector3(controller.ballFakeShadowSize, controller.ballFakeShadowSize, 0.005f);
                    if (controller.ballShadowMat != null)
                        bShadow.GetComponent<Renderer>().sharedMaterial = controller.ballShadowMat;
                }
            }
        }

        // 1.4 Çerçeve Segmentleri ve Sahte Gölge (Çerçeve.prefab & FrameShadow.mat)
        SpawnFrameSegments(controller, previewRoot.transform, framePrefab, occupied, step, gridSize, frameThickness, framePadding, offsetX, offsetY);

        controller.ApplyToMaterials();
        SyncMaterials(controller);
        FocusCamera();
    }

    /// <summary>
    /// Çerçeve segmentlerini ve sahte gölgelerini GridSpawner mantığı ile oluşturur.
    /// </summary>
    private static void SpawnFrameSegments(
        VisualThemeController controller,
        Transform parent,
        GameObject framePrefab,
        HashSet<Vector2Int> occupied,
        float step,
        float gridSize,
        float t,
        float framePadding,
        float offsetX,
        float offsetY)
    {
        float edge = gridSize / 2f + framePadding;
        Vector2 shadowOff = controller.frameFakeShadowOffset;

        void CreateSegment(Vector3 localPos, Vector3 scale, string name)
        {
            // Sahte gölge
            if (controller.enableFrameFakeShadow)
            {
                GameObject shadowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shadowObj.name = name + "_Shadow";
                Object.DestroyImmediate(shadowObj.GetComponent<BoxCollider>());
                shadowObj.transform.SetParent(parent);
                shadowObj.transform.localPosition = localPos + new Vector3(shadowOff.x, shadowOff.y, 0.015f);
                shadowObj.transform.localScale = new Vector3(scale.x * controller.frameShadowScaleMultiplier, scale.y * controller.frameShadowScaleMultiplier, 0.005f);
                if (controller.frameShadowMat != null)
                    shadowObj.GetComponent<Renderer>().sharedMaterial = controller.frameShadowMat;
            }

            // Ana çerçeve (Çerçeve.prefab)
            GameObject frameObj = null;
            if (framePrefab != null)
            {
                frameObj = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, parent);
                Object.DestroyImmediate(frameObj.GetComponent<Collider>());
            }
            else
            {
                frameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(frameObj.GetComponent<BoxCollider>());
                frameObj.transform.SetParent(parent);
            }

            frameObj.name = name;
            frameObj.transform.localPosition = localPos;
            frameObj.transform.localScale = scale;
            if (controller.frameMat != null)
            {
                Renderer fr = frameObj.GetComponent<Renderer>();
                if (fr != null) fr.sharedMaterial = controller.frameMat;
            }
        }

        foreach (var pos in occupied)
        {
            Vector3 center = new Vector3(pos.x * step - offsetX, pos.y * step - offsetY, 0);

            bool left = occupied.Contains(pos + Vector2Int.left);
            bool right = occupied.Contains(pos + Vector2Int.right);
            bool up = occupied.Contains(pos + Vector2Int.up);
            bool down = occupied.Contains(pos + Vector2Int.down);

            // ÜST (TOP)
            if (!up)
            {
                float len = step;
                if (!left) len += t;
                if (!right) len += t;
                float xOff = 0;
                if (!left && right) xOff = -t / 2f;
                if (!right && left) xOff = t / 2f;
                CreateSegment(center + new Vector3(xOff, edge + t / 2f, 0), new Vector3(len, t, t), $"Frame_Top_{pos.x}_{pos.y}");
            }

            // ALT (BOTTOM)
            if (!down)
            {
                float len = step;
                if (!left) len += t;
                if (!right) len += t;
                float xOff = 0;
                if (!left && right) xOff = -t / 2f;
                if (!right && left) xOff = t / 2f;
                CreateSegment(center + new Vector3(xOff, -edge - t / 2f, 0), new Vector3(len, t, t), $"Frame_Bottom_{pos.x}_{pos.y}");
            }

            // SOL (LEFT)
            if (!left)
            {
                float len = step;
                if (!up) len += t;
                if (!down) len += t;
                float yOff = 0;
                if (!down && up) yOff = -t / 2f;
                if (!up && down) yOff = t / 2f;
                CreateSegment(center + new Vector3(-edge - t / 2f, yOff, 0), new Vector3(t, len, t), $"Frame_Left_{pos.x}_{pos.y}");
            }

            // SAĞ (RIGHT)
            if (!right)
            {
                float len = step;
                if (!up) len += t;
                if (!down) len += t;
                float yOff = 0;
                if (!down && up) yOff = -t / 2f;
                if (!up && down) yOff = t / 2f;
                CreateSegment(center + new Vector3(edge + t / 2f, yOff, 0), new Vector3(t, len, t), $"Frame_Right_{pos.x}_{pos.y}");
            }

            // İç köşe dolguları (Concave corners)
            if (up && right && !occupied.Contains(pos + new Vector2Int(1, 1)))
                CreateSegment(center + new Vector3(edge + t / 2f, edge + t / 2f, 0), new Vector3(t, t, t), $"Frame_Corner_TR_{pos.x}_{pos.y}");
            if (up && left && !occupied.Contains(pos + new Vector2Int(-1, 1)))
                CreateSegment(center + new Vector3(-edge - t / 2f, edge + t / 2f, 0), new Vector3(t, t, t), $"Frame_Corner_TL_{pos.x}_{pos.y}");
            if (down && right && !occupied.Contains(pos + new Vector2Int(1, -1)))
                CreateSegment(center + new Vector3(edge + t / 2f, -edge - t / 2f, 0), new Vector3(t, t, t), $"Frame_Corner_BR_{pos.x}_{pos.y}");
            if (down && left && !occupied.Contains(pos + new Vector2Int(-1, -1)))
                CreateSegment(center + new Vector3(-edge - t / 2f, -edge - t / 2f, 0), new Vector3(t, t, t), $"Frame_Corner_BL_{pos.x}_{pos.y}");
        }
    }

    /// <summary>
    /// 9 farklı rengi aynı anda gösteren test paleti tahtası.
    /// </summary>
    private static void SpawnColorPaletteMode(
        VisualThemeController controller,
        GameObject previewRoot,
        GameObject gridPrefab,
        GameObject piecePrefab,
        GameObject framePrefab,
        GameObject platePrefab,
        float step,
        float gridSize,
        float frameThickness,
        float framePadding,
        float objectOffset)
    {
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                occupied.Add(new Vector2Int(x, y));

        float offsetX = 0;
        float offsetY = 0;

        // Plaka ve Grid
        float localPlateZ = 0.015f;
        Color[] sampleColors = new Color[]
        {
            ColorMixData.Kirmizi,  ColorMixData.Mavi,     ColorMixData.Sari,
            ColorMixData.Yesil,    Color.clear,           ColorMixData.Mor,
            ColorMixData.Turuncu,  ColorMixData.AcikMavi, ColorMixData.Pembe
        };

        int colorIdx = 0;
        for (int y = 1; y >= -1; y--)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 tilePos = new Vector3(x * step, y * step, 0);

                if (platePrefab != null)
                {
                    GameObject bgTile = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, previewRoot.transform);
                    bgTile.name = $"Preview_GridBG_{x}_{y}";
                    bgTile.transform.localPosition = new Vector3(tilePos.x, tilePos.y, localPlateZ);
                    bgTile.transform.localScale = new Vector3(step, step, 0.01f);
                    Object.DestroyImmediate(bgTile.GetComponent<Collider>());
                }

                if (gridPrefab != null)
                {
                    GameObject gridObj = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, previewRoot.transform);
                    gridObj.name = $"Preview_Grid_{x}_{y}";
                    gridObj.transform.localPosition = tilePos;
                    Object.DestroyImmediate(gridObj.GetComponent<Collider>());
                    Renderer gr = gridObj.GetComponent<Renderer>();
                    if (gr != null && controller.gridMat != null) gr.sharedMaterial = controller.gridMat;
                }

                Color c = sampleColors[colorIdx++];
                if (x == 0 && y == 0) continue; // merkez boş

                if (piecePrefab != null)
                {
                    GameObject pieceObj = (GameObject)PrefabUtility.InstantiatePrefab(piecePrefab, previewRoot.transform);
                    pieceObj.name = $"Preview_Piece_{x}_{y}";
                    pieceObj.transform.localPosition = new Vector3(tilePos.x, tilePos.y, -objectOffset);

                    var colliders = pieceObj.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders) Object.DestroyImmediate(col);
                    var drag = pieceObj.GetComponent<DragObject>();
                    if (drag != null) Object.DestroyImmediate(drag);

                    MeshRenderer mr = pieceObj.GetComponent<MeshRenderer>();
                    if (mr != null && controller.glassMat != null) mr.sharedMaterial = controller.glassMat;

                    LiquidTransfer lt = pieceObj.GetComponentInChildren<LiquidTransfer>();
                    if (lt != null)
                    {
                        lt.liquidColor = c;
                        lt.currentSlices = 2;
                        lt.UpdateVisuals();
                    }

                    // Top Sahte Gölgesi (Preview)
                    if (controller.enableBallFakeShadow)
                    {
                        GameObject bShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        bShadow.name = $"Preview_BallShadow_{x}_{y}";
                        Object.DestroyImmediate(bShadow.GetComponent<Collider>());
                        bShadow.transform.SetParent(previewRoot.transform);
                        bShadow.transform.localPosition = new Vector3(tilePos.x + controller.ballFakeShadowOffset.x, tilePos.y + controller.ballFakeShadowOffset.y, -objectOffset + 0.28f);
                        bShadow.transform.localScale = new Vector3(controller.ballFakeShadowSize, controller.ballFakeShadowSize, 0.005f);
                        if (controller.ballShadowMat != null)
                            bShadow.GetComponent<Renderer>().sharedMaterial = controller.ballShadowMat;
                    }
                }
            }
        }

        // Çerçeve
        SpawnFrameSegments(controller, previewRoot.transform, framePrefab, occupied, step, gridSize, frameThickness, framePadding, offsetX, offsetY);
    }

    /// <summary>
    /// Game/Simulator kamerasını ve açısını tahtaya mükemmel hizalar.
    /// </summary>
    private static void AlignMainCameraToBoard(Camera cam, Bounds combinedBounds)
    {
        GridSpawner spawner = Object.FindObjectOfType<GridSpawner>();

        float cameraPadding = spawner != null ? spawner.cameraPadding : 1f;
        float cameraZoomFactor = spawner != null ? spawner.cameraZoomFactor : 0.75f;
        float cameraVerticalOffset = spawner != null ? spawner.cameraVerticalOffset : 0.5f;
        float uiTopMarginNormalized = spawner != null ? spawner.uiTopMarginNormalized : 0.12f;

        float h = combinedBounds.size.y + cameraPadding * 2f;
        float w = combinedBounds.size.x + cameraPadding * 2f;
        float uiMargin = Mathf.Clamp01(uiTopMarginNormalized);

        if (cam.orthographic)
        {
            float playableHeightRatio = 1f - uiMargin;
            float sizeByHeight = (h / 2f) / playableHeightRatio;
            float sizeByWidth = (w / 2f) / cam.aspect;
            cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth) * cameraZoomFactor;

            Vector3 camTarget = combinedBounds.center;
            camTarget.y -= cam.orthographicSize * uiMargin;
            camTarget.y += cameraVerticalOffset;
            camTarget.z = cam.transform.position.z;
            cam.transform.position = camTarget;
        }
        else
        {
            float playableHeightRatio = 1f - uiMargin;
            float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distByHeight = (h / 2f) / (Mathf.Tan(halfFovRad) * playableHeightRatio);
            float distByWidth = (w / 2f) / (Mathf.Tan(halfFovRad) * cam.aspect);
            float targetDistance = Mathf.Max(distByHeight, distByWidth) * cameraZoomFactor;

            Vector3 baseTarget = combinedBounds.center;
            baseTarget.y -= (targetDistance * Mathf.Tan(halfFovRad)) * uiMargin;
            baseTarget.y += cameraVerticalOffset;
            cam.transform.position = baseTarget - cam.transform.forward * targetDistance;
        }
    }
}
