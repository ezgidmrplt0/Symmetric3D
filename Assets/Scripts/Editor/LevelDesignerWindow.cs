using UnityEngine;
using UnityEditor;

public class LevelDesignerWindow : EditorWindow
{
    private LevelData currentLevel;
    private int gridX = 3;
    private int gridY = 3;

    // Brush settings
    private Color brushColor = new Color(0.8f, 0.1f, 0.1f);
    private int brushSlices = 1;
    private float brushRotationZ = 0f;

    private Vector2 scrollPos;

    [MenuItem("Symmetric3D/Level Tasarımcısı")]
    public static void ShowWindow()
    {
        GetWindow<LevelDesignerWindow>("Level Tasarımcısı");
    }

    void OnGUI()
    {
        // ── Başlık ──────────────────────────────────────────────
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        GUILayout.Label("🎮 Symmetric3D — Level Tasarımcısı", titleStyle);
        GUILayout.Space(4);

        // ── Level Seçimi ─────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        currentLevel = (LevelData)EditorGUILayout.ObjectField("Düzenlenen Level", currentLevel, typeof(LevelData), false);
        if (EditorGUI.EndChangeCheck() && currentLevel != null)
        {
            gridX = currentLevel.gridX;
            gridY = currentLevel.gridY;
        }

        GUILayout.Space(6);

        if (currentLevel == null)
        {
            EditorGUILayout.HelpBox("Çizim yapmak için bir Level Data seçin veya yeni oluşturun.", MessageType.Info);
            if (GUILayout.Button("Yeni Level Dosyası Oluştur", GUILayout.Height(36)))
                CreateNewLevel();
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // ── 1. BÖLÜM: Level Bilgileri ────────────────────────────
        DrawSectionHeader("📋 Level Bilgileri");

        EditorGUI.BeginChangeCheck();

        string newName = EditorGUILayout.TextField("Seviye Adı", currentLevel.levelDisplayName);
        LevelData.LevelType newType = (LevelData.LevelType)EditorGUILayout.EnumPopup("Level Türü", currentLevel.levelType);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Level Bilgisi Değiştir");
            currentLevel.levelDisplayName = newName;
            currentLevel.levelType = newType;
            EditorUtility.SetDirty(currentLevel);
        }

        // Türe göre açıklama göster (açılma % bilgisi LevelFlowWindow'da yönetiliyor)
        switch (currentLevel.levelType)
        {
            case LevelData.LevelType.Classic:
                EditorGUILayout.HelpBox("Classic — Kaydır ve eşleştir.  |  Açılma: Her zaman açık", MessageType.None);
                break;
            case LevelData.LevelType.QuarterFill:
                EditorGUILayout.HelpBox("QuarterFill — Çeyrek dolu obje mekaniği.  |  Açılma koşulu Level Akış Yöneticisi'nde ayarlanır.", MessageType.None);
                break;
            default:
                EditorGUILayout.HelpBox("Açılma koşulu Level Akış Yöneticisi'nde ayarlanır.", MessageType.None);
                break;
        }

        GUILayout.Space(6);

        // ── 2. BÖLÜM: Grid Boyutu ────────────────────────────────
        DrawSectionHeader("📐 Grid Boyutu");

        EditorGUI.BeginChangeCheck();
        gridX = EditorGUILayout.IntSlider("Genişlik (X)", gridX, 1, 10);
        gridY = EditorGUILayout.IntSlider("Yükseklik (Y)", gridY, 1, 10);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Grid Boyutu Değiştir");
            currentLevel.gridX = gridX;
            currentLevel.gridY = gridY;
            EditorUtility.SetDirty(currentLevel);
        }

        GUILayout.Space(6);

        // ── 3. BÖLÜM: Fırça Ayarları ─────────────────────────────
        DrawSectionHeader("🖌️ Fırça (Brush) Ayarları");

        brushColor = EditorGUILayout.ColorField("Renk", brushColor);
        brushSlices = EditorGUILayout.IntSlider("Dilim (Slices)", brushSlices, 1, 4);

        string[] rotOptions = { "Yukarı (0°)", "Sağa (90°)", "Aşağı (180°)", "Sola (-90°)" };
        int[] rotValues = { 0, 90, 180, -90 };
        int currentRotIndex = System.Array.IndexOf(rotValues, (int)brushRotationZ);
        if (currentRotIndex < 0) currentRotIndex = 0;
        currentRotIndex = EditorGUILayout.Popup("Baktığı Yön", currentRotIndex, rotOptions);
        brushRotationZ = rotValues[currentRotIndex];

        GUILayout.Space(6);

        // ── 4. BÖLÜM: Harita / Grid ──────────────────────────────
        DrawSectionHeader("🗺️ Harita");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox("Sol Tık = Boya/Yerleştir     Sağ Tık = Sil", MessageType.None);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawGrid();

        GUILayout.Space(10);

        // ── Alt butonlar ─────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🗑️ Tüm Parçaları Temizle", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Emin misin?", "Tüm parçalar silinecek.", "Sil", "İptal"))
            {
                Undo.RecordObject(currentLevel, "Tümünü Temizle");
                currentLevel.pieces.Clear();
                EditorUtility.SetDirty(currentLevel);
            }
        }
        if (GUILayout.Button("💾 Kaydet", GUILayout.Height(30)))
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelDesigner] '{currentLevel.levelDisplayName}' kaydedildi.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // ─── Yardımcılar ─────────────────────────────────────────────

    void DrawSectionHeader(string title)
    {
        GUILayout.Space(4);
        Rect rect = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f));
        GUILayout.Space(2);
        GUILayout.Label(title, EditorStyles.boldLabel);
    }

    void DrawGrid()
    {
        for (int y = gridY - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < gridX; x++)
            {
                LevelData.PieceData piece = GetPieceAt(x, y);

                string buttonText = "Boş\n(+)";
                Color bgColor = new Color(0.3f, 0.3f, 0.3f);

                if (piece != null)
                {
                    bgColor = piece.liquidColor;
                    string yon = piece.rotationZ switch
                    {
                        90    => "→",
                        180   => "↓",
                        -90   => "←",
                        _     => "↑"
                    };
                    buttonText = $"{piece.currentSlices}/4\n{yon}";
                }

                GUI.backgroundColor = bgColor;
                Rect bRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), GUI.skin.button,
                    GUILayout.Width(65), GUILayout.Height(65));

                if (GUI.Button(bRect, buttonText))
                {
                    if (Event.current.button == 1)
                    {
                        if (piece != null)
                        {
                            Undo.RecordObject(currentLevel, "Parça Sil");
                            currentLevel.pieces.Remove(piece);
                            EditorUtility.SetDirty(currentLevel);
                        }
                    }
                    else
                    {
                        Undo.RecordObject(currentLevel, "Parça Ekle/Güncelle");
                        if (piece == null)
                        {
                            piece = new LevelData.PieceData { gridPosition = new Vector2Int(x, y) };
                            currentLevel.pieces.Add(piece);
                        }
                        piece.liquidColor   = brushColor;
                        piece.currentSlices = brushSlices;
                        piece.rotationZ     = brushRotationZ;
                        EditorUtility.SetDirty(currentLevel);
                    }
                    GUI.FocusControl(null);
                }

                // Sağ tık fallback
                Event e = Event.current;
                if (e.isMouse && e.type == EventType.MouseDown && bRect.Contains(e.mousePosition) && e.button == 1)
                {
                    if (piece != null)
                    {
                        Undo.RecordObject(currentLevel, "Parça Sil");
                        currentLevel.pieces.Remove(piece);
                        EditorUtility.SetDirty(currentLevel);
                        e.Use();
                    }
                }

                GUI.backgroundColor = Color.white;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    LevelData.PieceData GetPieceAt(int x, int y) =>
        currentLevel.pieces.Find(p => p.gridPosition.x == x && p.gridPosition.y == y);

    void CreateNewLevel()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Yeni Level Kaydet", "Level_02", "asset",
            "Level dosyasını nereye kaydetmek istersiniz?");

        if (!string.IsNullOrEmpty(path))
        {
            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.gridX = gridX;
            newLevel.gridY = gridY;
            newLevel.levelDisplayName = "Yeni Level";

            AssetDatabase.CreateAsset(newLevel, path);
            AssetDatabase.SaveAssets();
            currentLevel = newLevel;
            EditorGUIUtility.PingObject(newLevel);
        }
    }
}
