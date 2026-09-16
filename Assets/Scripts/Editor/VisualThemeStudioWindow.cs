using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(VisualThemeController))]
public class VisualThemeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        VisualThemeController controller = (VisualThemeController)target;

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.2f, 0.7f, 1f) }
        };

        GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.75f, 0.2f) }
        };

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎨 GÖRSEL TEMA VE MATERYAL KONTROLCÜSÜ", headerStyle);
        EditorGUILayout.HelpBox("Değerleri değiştirdiğiniz anda sahnedeki cam, sıvı, ızgara ve zemin anında güncellenir.", MessageType.Info);

        // ── Canlı Sahne Önizlemesi (Oyunu Başlatmadan Görme) ──
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("👁️ CANLI SAHNE ÖNİZLEMESİ (Editör Modu)", subHeaderStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        bool isPreviewing = VisualThemePreviewManager.IsPreviewActive;
        EditorGUILayout.LabelField(isPreviewing 
            ? "● Durum: Canlı Önizleme Aktif (Sahnede örnek tahta gösteriliyor)" 
            : "○ Durum: Önizleme Kapalı", 
            isPreviewing ? EditorStyles.boldLabel : EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (!isPreviewing)
        {
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button("▶ Sahne Önizlemesini Aç (Örnek Tahta)", GUILayout.Height(32)))
            {
                VisualThemePreviewManager.SpawnPreview(controller);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("⏹ Önizlemeyi Kapat", GUILayout.Height(32)))
            {
                VisualThemePreviewManager.DestroyPreview();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("🎯 Kamerayı Odakla", GUILayout.Height(32), GUILayout.Width(130)))
            {
                VisualThemePreviewManager.FocusCamera();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("⚡ Hızlı Hazır Önayarlar (Presets)", subHeaderStyle);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("✨ Belirgin Toon Cam", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Visible Toon Glass");
            controller.Preset_VisibleToonGlass();
        }
        if (GUILayout.Button("💎 Kristal Cam", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Crystal Glass");
            controller.Preset_CrystalGlass();
        }
        if (GUILayout.Button("🎨 Soft Pastel", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Preset Soft Pastel");
            controller.Preset_SoftPastel();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 Materyal Değişikliklerini Diske Kaydet", GUILayout.Height(32)))
        {
            controller.ApplyToMaterials();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Kaydedildi", "Tüm materyal ayarları başarıyla proje dosyalarına kaydedildi.", "Tamam");
        }
        if (GUILayout.Button("🔄 Materyallerden Oku", GUILayout.Height(32)))
        {
            controller.ReadFromMaterials();
        }
        EditorGUILayout.EndHorizontal();
    }
}

public class VisualThemeStudioWindow : EditorWindow
{
    private VisualThemeController controller;
    private Vector2 scrollPos;

    [MenuItem("Tools/Görsel Tema Stüdyosu (Visual Theme Studio)")]
    public static void ShowWindow()
    {
        VisualThemeStudioWindow window = GetWindow<VisualThemeStudioWindow>("Tema Stüdyosu");
        window.minSize = new Vector2(380, 560);
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
        // Pencere kapandığında geçici önizlemeyi sahneden temizle
        VisualThemePreviewManager.DestroyPreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
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
        }
        controller.EnsureMaterials();
        controller.ReadFromMaterials();
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

        Editor editor = Editor.CreateEditor(controller);
        if (editor != null)
        {
            editor.OnInspectorGUI();
        }

        EditorGUILayout.EndScrollView();
    }
}

/// <summary>
/// Oyunu başlatmadan (Editör modunda) sahnede canlı 3x3 örnek tahta, çerçeve, sahte gölge,
/// parlaklığı kaldırılmış boş hücre ve renkli cam küreleri gösteren önizleme yöneticisi.
/// </summary>
public static class VisualThemePreviewManager
{
    private const string PREVIEW_ROOT_NAME = "[VisualTheme_PreviewRoot]";

    public static bool IsPreviewActive => GameObject.Find(PREVIEW_ROOT_NAME) != null;

    public static void DestroyPreview()
    {
        GameObject root = GameObject.Find(PREVIEW_ROOT_NAME);
        if (root != null)
        {
            Object.DestroyImmediate(root);
            SceneView.RepaintAll();
        }
    }

    public static void FocusCamera()
    {
        GameObject root = GameObject.Find(PREVIEW_ROOT_NAME);
        if (root != null && SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Frame(new Bounds(root.transform.position, new Vector3(4.5f, 4.5f, 2f)), false);
        }
    }

    public static void SpawnPreview(VisualThemeController controller)
    {
        DestroyPreview();

        GameObject previewRoot = new GameObject(PREVIEW_ROOT_NAME);
        previewRoot.hideFlags = HideFlags.DontSave;
        previewRoot.transform.position = Vector3.zero;

        // 1. Zemin Arka Plan Plakası (Plane.mat)
        GameObject bgPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgPlate.name = "Preview_Background";
        Object.DestroyImmediate(bgPlate.GetComponent<BoxCollider>());
        bgPlate.transform.SetParent(previewRoot.transform);
        bgPlate.transform.localPosition = new Vector3(0, 0, 0.1f);
        bgPlate.transform.localScale = new Vector3(4.6f, 4.6f, 0.02f);
        if (controller.planeMat != null)
            bgPlate.GetComponent<Renderer>().sharedMaterial = controller.planeMat;

        // 2. Çerçeve ve Sahte Gölge Segmentleri
        float boardSize = 3.6f;
        float frameThick = 0.22f;
        float halfBoard = boardSize / 2f;
        Vector2 shadowOff = controller.frameFakeShadowOffset;

        // Üst, Alt, Sol, Sağ çerçeve tanımları
        var frameDefs = new (Vector3 pos, Vector3 scale)[]
        {
            (new Vector3(0, halfBoard + frameThick/2f, 0), new Vector3(boardSize + frameThick*2f, frameThick, frameThick)),
            (new Vector3(0, -halfBoard - frameThick/2f, 0), new Vector3(boardSize + frameThick*2f, frameThick, frameThick)),
            (new Vector3(-halfBoard - frameThick/2f, 0, 0), new Vector3(frameThick, boardSize, frameThick)),
            (new Vector3(halfBoard + frameThick/2f, 0, 0), new Vector3(frameThick, boardSize, frameThick)),
        };

        foreach (var def in frameDefs)
        {
            // 2.1 Sahte Gölge (Fake Shadow)
            if (controller.enableFrameFakeShadow)
            {
                GameObject shadowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shadowObj.name = "Preview_FrameFakeShadow";
                Object.DestroyImmediate(shadowObj.GetComponent<BoxCollider>());
                shadowObj.transform.SetParent(previewRoot.transform);
                shadowObj.transform.localPosition = def.pos + new Vector3(shadowOff.x, shadowOff.y, 0.04f);
                shadowObj.transform.localScale = new Vector3(def.scale.x * 1.08f, def.scale.y * 1.08f, 0.02f);
                if (controller.frameShadowMat != null)
                    shadowObj.GetComponent<Renderer>().sharedMaterial = controller.frameShadowMat;
            }

            // 2.2 Ana Çerçeve Segmenti
            GameObject frameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameObj.name = "Preview_FrameSegment";
            Object.DestroyImmediate(frameObj.GetComponent<BoxCollider>());
            frameObj.transform.SetParent(previewRoot.transform);
            frameObj.transform.localPosition = def.pos;
            frameObj.transform.localScale = def.scale;
            if (controller.frameMat != null)
                frameObj.GetComponent<Renderer>().sharedMaterial = controller.frameMat;
        }

        // 3. Hücreler ve Örnek Parçalar (3x3 Düzen)
        // Merkez (0,0) boş hücredir (parlaklığı kaldırılmış Grid.mat sergilenir)
        GameObject gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grid.prefab");
        GameObject piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainObject.prefab");

        Color[] sampleColors = new Color[]
        {
            ColorMixData.Kirmizi,  ColorMixData.Mavi,     ColorMixData.Sari,
            ColorMixData.Yesil,    Color.clear,           ColorMixData.Mor,
            ColorMixData.Turuncu,  ColorMixData.AcikMavi, ColorMixData.Pembe
        };

        float spacing = 1.15f;
        int colorIdx = 0;

        for (int y = 1; y >= -1; y--)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 cellPos = new Vector3(x * spacing, y * spacing, 0);

                // Grid girintisi (Grid.prefab)
                if (gridPrefab != null)
                {
                    GameObject gridInstance = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, previewRoot.transform);
                    gridInstance.transform.localPosition = cellPos;
                    Object.DestroyImmediate(gridInstance.GetComponent<Collider>());
                }

                Color c = sampleColors[colorIdx++];
                // Merkez (0,0) boş hücre: üzerinde parça yok!
                if (x == 0 && y == 0)
                {
                    continue;
                }

                // Diğer hücrelerde renkli sıvı ve cam küreler
                if (piecePrefab != null)
                {
                    GameObject pieceInstance = (GameObject)PrefabUtility.InstantiatePrefab(piecePrefab, previewRoot.transform);
                    pieceInstance.transform.localPosition = cellPos;

                    // Editörde physics veya drag scriptlerinin tetiklenmemesi için
                    var colliders = pieceInstance.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders) Object.DestroyImmediate(col);

                    var dragCode = pieceInstance.GetComponent<DragObject>();
                    if (dragCode != null) Object.DestroyImmediate(dragCode);

                    // Sıvı rengi ata
                    LiquidTransfer lt = pieceInstance.GetComponent<LiquidTransfer>();
                    if (lt != null)
                    {
                        lt.liquidColor = c;
                        lt.currentSlices = 2;
                        lt.ApplyPropertyBlock();
                    }
                }
            }
        }

        controller.ApplyToMaterials();
        FocusCamera();
        SceneView.RepaintAll();
    }
}
