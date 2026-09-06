using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Magic Sort — Modern Seviye Tasarımcısı.
/// Eliptik halka dizilimi, şişe sıvı renk ve dilim editörü,
/// otomatik çözülebilirlik doğrulaması ve rastgele dengeli seviye üretici içerir.
/// </summary>
public class LevelDesignerWindow : EditorWindow
{
    private LevelData currentLevel;
    private int selectedBottleIndex = 0;
    private Vector2 scrollPos;

    // Hızlı Renk Paleti
    private static readonly Color[] QuickColors = new Color[]
    {
        new Color(0.92f, 0.22f, 0.22f), // Kırmızı
        new Color(0.18f, 0.52f, 0.95f), // Mavi
        new Color(0.22f, 0.78f, 0.35f), // Yeşil
        new Color(0.95f, 0.82f, 0.15f), // Sarı
        new Color(0.68f, 0.26f, 0.92f), // Mor
        new Color(0.95f, 0.55f, 0.15f), // Turuncu
        new Color(0.20f, 0.82f, 0.88f), // Turkuaz
        new Color(0.95f, 0.35f, 0.65f), // Pembe
    };

    private static readonly string[] QuickColorNames = new string[]
    {
        "Kırmızı", "Mavi", "Yeşil", "Sarı", "Mor", "Turuncu", "Turkuaz", "Pembe"
    };

    // Otomatik seviye üretici ayarları
    private int genColorCount = 4;
    private int genEmptyCount = 2;

    // Slot Sürükle-Bırak & Kopyala Panosu
    private int dragSourceBottleIndex = -1;
    private int hoverTargetBottleIndex = -1;
    private bool isSlotDragging = false;
    private Vector2 dragStartMousePos;
    private static LevelData.PieceData clipboardPiece = null;
    private readonly List<Rect> currentBottleRects = new List<Rect>();

    [MenuItem("Magic Sort/Level Tasarımcısı")]
    [MenuItem("Symmetric3D/Level Tasarımcısı")]
    public static void ShowWindow()
    {
        var window = GetWindow<LevelDesignerWindow>("Magic Sort Tasarımcı");
        window.minSize = new Vector2(480, 700);
        window.Show();
    }

    private void OnEnable()
    {
        if (currentLevel == null)
        {
            LoadFirstAvailableLevel();
        }
    }

    private void LoadFirstAvailableLevel()
    {
        string folder = "Assets/Levels";
        if (AssetDatabase.IsValidFolder(folder))
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                currentLevel = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            }
        }
    }

    private void OnGUI()
    {
        HandleKeyboardShortcuts();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();
        GUILayout.Space(8);

        DrawLevelSelectorBar();
        GUILayout.Space(8);

        if (currentLevel == null)
        {
            EditorGUILayout.HelpBox("Düzenlemek için bir Level seçin veya '➕ Yeni Level Oluştur' butonuna basın.", MessageType.Info);
            if (GUILayout.Button("⚡ Hemen İlk Leveli Oluştur", GUILayout.Height(36)))
            {
                CreateNewLevel();
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawLevelSettings();
        GUILayout.Space(10);

        DrawVisualCanvas();
        GUILayout.Space(6);

        DrawQuickActionToolbar();
        GUILayout.Space(10);

        DrawSelectedBottleInspector();
        GUILayout.Space(10);

        DrawSolvabilityAnalyzer();
        GUILayout.Space(10);

        DrawLevelGenerator();
        GUILayout.Space(16);

        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────────────────────
    // 1. BAŞLIK
    // ──────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("🧪 Magic Sort — Level Tasarımcısı", titleStyle);
        EditorGUILayout.LabelField("Eliptik dizilim, şişe doluluk ayarları ve çözülebilirlik denetimi", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 2. SEVİYE SEÇİCİ & YÖNETİM BARİ
    // ──────────────────────────────────────────────────────────────
    private void DrawLevelSelectorBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        LevelData newSelected = (LevelData)EditorGUILayout.ObjectField("Aktif Level", currentLevel, typeof(LevelData), false);
        if (newSelected != currentLevel)
        {
            currentLevel = newSelected;
            selectedBottleIndex = 0;
            GUI.FocusControl(null);
        }

        if (GUILayout.Button("➕ Yeni Level", GUILayout.Width(100), GUILayout.Height(22)))
        {
            CreateNewLevel();
        }

        if (currentLevel != null)
        {
            if (GUILayout.Button("💾 Kaydet", GUILayout.Width(75), GUILayout.Height(22)))
            {
                SaveCurrentLevel();
            }

            if (GUILayout.Button("🗑️ Sil", GUILayout.Width(55), GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("Seviyeyi Sil", $"'{currentLevel.name}' silinecek. Emin misiniz?", "Evet, Sil", "İptal"))
                {
                    DeleteCurrentLevel();
                }
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 3. SEVİYE TEMEL AYARLARI
    // ──────────────────────────────────────────────────────────────
    private void DrawLevelSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚙️ Seviye ve Dizilim Ayarları", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        string newName = EditorGUILayout.TextField("Seviye Adı", currentLevel.levelDisplayName);
        float newTime = EditorGUILayout.FloatField("Süre Limiti (sn)", currentLevel.timeLimit);

        currentLevel.flatLayoutMode = (LevelData.FlatLayoutMode)EditorGUILayout.EnumPopup("Dizilim Modu", currentLevel.flatLayoutMode);

        if (currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.Grid)
        {
            currentLevel.gridX = EditorGUILayout.IntSlider("Sütun Sayısı (X)", currentLevel.gridX, 2, 10);
            currentLevel.gridY = EditorGUILayout.IntSlider("Satır Sayısı (Y)", currentLevel.gridY, 1, 8);
        }

        currentLevel.bottleScale = EditorGUILayout.Slider("Şişe Boyutu (Bottle Scale)", currentLevel.bottleScale > 0.1f ? currentLevel.bottleScale : 1.0f, 0.6f, 2.2f);
        currentLevel.customSpacingX = EditorGUILayout.Slider("Yatay Aralık (Spacing X)", currentLevel.customSpacingX > 0.1f ? currentLevel.customSpacingX : 1.85f, 0.8f, 3.2f);
        currentLevel.customSpacingY = EditorGUILayout.Slider("Dikey Aralık (Spacing Y)", currentLevel.customSpacingY > 0.1f ? currentLevel.customSpacingY : 2.6f, 1.0f, 4.0f);
        currentLevel.rowStaggerX = EditorGUILayout.Slider("Kademeli Satır Ofseti (Row Stagger)", currentLevel.rowStaggerX, -1.5f, 1.5f);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("✨ 7 Sütunlu V (25 Şişe)", GUILayout.Height(24)))
        {
            currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.StaggeredV;
            EnsureBottleCount(25);
            currentLevel.customSpacingX = 1.22f;
            currentLevel.customSpacingY = 1.95f;
            EditorUtility.SetDirty(currentLevel);
        }
        if (GUILayout.Button("📐 4-4 Raf (8 Şişe)", GUILayout.Height(24)))
        {
            currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.AutoFlow;
            EnsureBottleCount(8);
            currentLevel.customSpacingX = 1.85f;
            currentLevel.customSpacingY = 2.6f;
            currentLevel.customRowDistribution = new List<int> { 4, 4 };
            EditorUtility.SetDirty(currentLevel);
        }
        if (GUILayout.Button("📐 3-3-2 Raf (8 Şişe)", GUILayout.Height(24)))
        {
            currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.AutoFlow;
            EnsureBottleCount(8);
            currentLevel.customSpacingX = 1.95f;
            currentLevel.customSpacingY = 2.4f;
            currentLevel.customRowDistribution = new List<int> { 3, 3, 2 };
            EditorUtility.SetDirty(currentLevel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎛️ Pozisyonları Serbest Yap (Bake to Custom)", GUILayout.Height(24)))
        {
            Undo.RecordObject(currentLevel, "Bake to Custom Positions");
            int cnt = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 p = GridSpawner.GetBottlePositionForLevel(currentLevel, i, cnt);
                currentLevel.pieces[i].customPosition = new Vector2(p.x, p.y);
            }
            currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.CustomPositions;
            EditorUtility.SetDirty(currentLevel);
        }
        if (GUILayout.Button("🔄 Klasik Otomatik Düzen", GUILayout.Height(24)))
        {
            currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.AutoFlow;
            EditorUtility.SetDirty(currentLevel);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        int currentCount = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        int newBottleCount = EditorGUILayout.IntSlider("Toplam Şişe Sayısı", currentCount, 2, 32);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Seviye Ayarlarını Değiştir");
            currentLevel.levelDisplayName = newName;
            currentLevel.timeLimit = newTime;

            EnsureBottleCount(newBottleCount);
            EditorUtility.SetDirty(currentLevel);
        }

        EditorGUILayout.EndVertical();
    }

    private void EnsureBottleCount(int targetCount)
    {
        if (currentLevel.pieces == null) currentLevel.pieces = new List<LevelData.PieceData>();

        while (currentLevel.pieces.Count < targetCount)
        {
            int idx = currentLevel.pieces.Count;
            // Yeni şişe varsayılan olarak boş veya sıradaki renkte eklenir
            Color defaultColor = QuickColors[idx % QuickColors.Length];
            currentLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(idx, 0),
                liquidColor = defaultColor,
                currentSlices = 0, // Boş başlasın
                rotationZ = 0f,
                canRotate = false
            });
        }

        while (currentLevel.pieces.Count > targetCount)
        {
            currentLevel.pieces.RemoveAt(currentLevel.pieces.Count - 1);
        }

        if (selectedBottleIndex >= currentLevel.pieces.Count)
        {
            selectedBottleIndex = Mathf.Max(0, currentLevel.pieces.Count - 1);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 4. İNTERAKTİF GÖRSEL EKRAN (Sürükle-Bırak, Kopyala, Sağ Tık)
    // ──────────────────────────────────────────────────────────────
    private void DrawVisualCanvas()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string modeTitle = currentLevel.flatLayoutMode switch
        {
            LevelData.FlatLayoutMode.StaggeredV => "📱 Mobil Ekran Önizlemesi (7 Sütunlu Kademeli V Düzeni)",
            LevelData.FlatLayoutMode.CustomPositions => "📱 Mobil Ekran Önizlemesi (🎯 Serbest Sürükle-Bırak Modu)",
            _ => "📱 Mobil Ekran Önizlemesi"
        };
        EditorGUILayout.LabelField(modeTitle, EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "💡 Hızlı Tasarım Rehberi:\n" +
            "• ⇄ Taşı / Yer Değiştir: Bir şişeyi tutup başka bir şişenin üstüne bırakın (İçerikleri yer değiştirir).\n" +
            "• 📋 Kopyala / Çoğalt: [Ctrl] veya [Alt] basılı tutarak başka bir şişeye sürükleyin (Üzerine kopyalar).\n" +
            "• 🖱️ Sağ Tık: Şişeye sağ tıklayarak Boşalt, Kopyala, Yapıştır, Çoğalt, Dondur veya Tek Renkle Doldurun.\n" +
            "• ⌨️ Kısayollar: [Del] = Boşalt, [Ctrl+C] = Kopyala, [Ctrl+V] = Yapıştır, [Ctrl+D] = Çoğalt, [Shift+Del] = Slotu Sil.",
            MessageType.None);

        int totalBottles = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        if (totalBottles == 0)
        {
            EditorGUILayout.HelpBox("Henüz şişe eklenmemiş. 'Toplam Şişe Sayısı'nı artırın.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        bool isStaggered = currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.StaggeredV;
        bool isCustom = currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.CustomPositions;

        // Kanvas alanı (Telefon ekranı oranı)
        float canvasWidth = isStaggered ? 340f : 320f;
        float canvasHeight = isStaggered ? 430f : 400f;
        Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.ExpandWidth(true));

        float drawX = canvasRect.x + (canvasRect.width - canvasWidth) / 2f;
        Rect phoneRect = new Rect(drawX, canvasRect.y, canvasWidth, canvasHeight);

        // Arka plan resmi
        Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/arkaplan.jpeg");
        if (bgTex != null)
        {
            GUI.DrawTexture(phoneRect, bgTex, ScaleMode.ScaleAndCrop);
            EditorGUI.DrawRect(phoneRect, new Color(0.04f, 0.05f, 0.10f, 0.45f));
        }
        else
        {
            EditorGUI.DrawRect(phoneRect, new Color(0.10f, 0.11f, 0.16f));
        }

        Handles.color = new Color(0.35f, 0.40f, 0.60f);
        Handles.DrawWireCube(phoneRect.center, new Vector3(phoneRect.size.x, phoneRect.size.y, 0f));

        Vector2 center = phoneRect.center;
        Event currentEvent = Event.current;
        float bottleScaleMult = currentLevel.bottleScale > 0.1f ? currentLevel.bottleScale : 1.0f;

        // 1. ADIM: Tüm şişe Rect alanlarını hesapla
        currentBottleRects.Clear();
        for (int i = 0; i < totalBottles; i++)
        {
            Vector3 relPos;
            float bWidth, bHeight;

            if (isStaggered)
            {
                float previewSpacingX = 42f;
                float previewSpacingY = 70f;
                relPos = GridSpawner.GetStaggeredVPosition(i, totalBottles, previewSpacingX, previewSpacingY);
                bWidth = 34f * bottleScaleMult;
                bHeight = 52f * bottleScaleMult;
            }
            else if (isCustom)
            {
                float previewScale = 38f;
                var p = currentLevel.pieces[i];
                relPos = new Vector3(p.customPosition.x * previewScale, p.customPosition.y * previewScale, 0f);
                bWidth = 42f * bottleScaleMult;
                bHeight = 58f * bottleScaleMult;
            }
            else if (currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.Grid)
            {
                float previewSpacingX = (currentLevel.customSpacingX > 0.1f ? currentLevel.customSpacingX : 1.85f) * 26f;
                float previewSpacingY = (currentLevel.customSpacingY > 0.1f ? currentLevel.customSpacingY : 2.6f) * 26f;
                var p = currentLevel.pieces[i];
                Vector2Int gp = (p != null) ? p.gridPosition : new Vector2Int(i % currentLevel.gridX, i / currentLevel.gridX);
                float gx = (gp.x - (currentLevel.gridX - 1) * 0.5f) * previewSpacingX;
                float gy = ((currentLevel.gridY - 1) * 0.5f - gp.y) * previewSpacingY;
                relPos = new Vector3(gx, gy, 0f);
                bWidth = 40f * bottleScaleMult;
                bHeight = 56f * bottleScaleMult;
            }
            else
            {
                float previewSpacingX = (currentLevel.customSpacingX > 0.1f ? currentLevel.customSpacingX : 1.85f) * 26f;
                float previewSpacingY = (currentLevel.customSpacingY > 0.1f ? currentLevel.customSpacingY : 2.6f) * 26f;
                relPos = GridSpawner.GetFlexibleBottlePosition(currentLevel, i, totalBottles, previewSpacingX, previewSpacingY);
                bWidth = 42f * bottleScaleMult;
                bHeight = 58f * bottleScaleMult;
            }

            Vector2 bottleCenter = center + new Vector2(relPos.x, -relPos.y);
            currentBottleRects.Add(new Rect(bottleCenter.x - bWidth / 2f, bottleCenter.y - bHeight / 2f, bWidth, bHeight));
        }

        // Sürükleme sırasında hedef şişeyi tespit et
        hoverTargetBottleIndex = -1;
        if (isSlotDragging && dragSourceBottleIndex >= 0)
        {
            for (int j = 0; j < currentBottleRects.Count; j++)
            {
                if (j != dragSourceBottleIndex && currentBottleRects[j].Contains(currentEvent.mousePosition))
                {
                    hoverTargetBottleIndex = j;
                    break;
                }
            }
        }

        // 2. ADIM: Şişeleri Çiz
        for (int i = 0; i < totalBottles; i++)
        {
            Rect bottleRect = currentBottleRects[i];
            var piece = currentLevel.pieces[i];
            bool isSelected = (i == selectedBottleIndex);
            bool isDragSource = (isSlotDragging && i == dragSourceBottleIndex);
            bool isHoverTarget = (isSlotDragging && i == hoverTargetBottleIndex);

            // Şişe arka planı
            EditorGUI.DrawRect(bottleRect, isDragSource ? new Color(0.12f, 0.15f, 0.20f, 0.45f) : new Color(0.18f, 0.22f, 0.28f, 0.92f));

            // Katmanlı sıvı dolgusu
            int sliceCount = (piece.sliceColors != null && piece.sliceColors.Count > 0) ? piece.sliceColors.Count : piece.currentSlices;
            if (sliceCount > 0)
            {
                float sliceHeight = (bottleRect.height - 4) / 4f;
                for (int s = 0; s < sliceCount; s++)
                {
                    Color sColor = (piece.sliceColors != null && s < piece.sliceColors.Count) ? piece.sliceColors[s] : piece.liquidColor;
                    float yPos = (bottleRect.y + bottleRect.height - 2) - ((s + 1) * sliceHeight);
                    Rect sliceRect = new Rect(bottleRect.x + 2, yPos, bottleRect.width - 4, sliceHeight - 1);
                    float alpha = isDragSource ? 0.45f : 1.0f;
                    EditorGUI.DrawRect(sliceRect, new Color(sColor.r, sColor.g, sColor.b, alpha));
                }
            }

            // Şişe Çerçevesi / Seçim & Hedef Vurguları
            if (isHoverTarget)
            {
                bool isCopy = currentEvent.control || currentEvent.alt;
                Color targetCol = isCopy ? new Color(0.2f, 1.0f, 0.4f, 1.0f) : new Color(0.25f, 0.85f, 1.0f, 1.0f);
                Handles.color = targetCol;
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.width + 6f, bottleRect.height + 6f, 0f));
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.width + 4f, bottleRect.height + 4f, 0f));
            }
            else if (isSelected)
            {
                Handles.color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.width + 3f, bottleRect.height + 3f, 0f));
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.width, bottleRect.height, 0f));
            }
            else
            {
                Handles.color = new Color(0.5f, 0.6f, 0.75f, 0.8f);
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.width, bottleRect.height, 0f));
            }

            // Şişe Etiketi (#Numara ve Doluluk)
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isHoverTarget ? Color.yellow : Color.white }
            };
            string sliceText = sliceCount > 0 ? $"{sliceCount}/4" : "BOŞ";
            GUI.Label(new Rect(bottleRect.x, bottleRect.y - 16, bottleRect.width, 16), $"#{i + 1}", labelStyle);
            GUI.Label(new Rect(bottleRect.x, bottleRect.y + (bottleRect.height / 2f) - 8, bottleRect.width, 16), sliceText, labelStyle);

            // Buzlu Cam Kaplaması
            if (piece.isFrozen)
            {
                EditorGUI.DrawRect(bottleRect, new Color(0.55f, 0.88f, 1.0f, 0.38f));
                Rect badgeBorder = new Rect(bottleRect.center.x - 20, bottleRect.center.y - 14, 40, 28);
                EditorGUI.DrawRect(badgeBorder, new Color(0.30f, 0.92f, 1.0f, 0.95f));
                Rect badgeRect = new Rect(badgeBorder.x + 2, badgeBorder.y + 2, badgeBorder.width - 4, badgeBorder.height - 4);
                EditorGUI.DrawRect(badgeRect, new Color(0.04f, 0.12f, 0.24f, 0.98f));
                GUIStyle iceStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    normal = { textColor = new Color(0.85f, 0.98f, 1f) }
                };
                GUI.Label(badgeRect, $"❄️ {piece.requiredMatches}", iceStyle);
            }

            // Hedef slot üzerine aksiyon rozeti çiz
            if (isHoverTarget)
            {
                bool isCopy = currentEvent.control || currentEvent.alt;
                string actionText = isCopy ? "📋 KOPYALA" : "⇄ DEĞİŞTİR";
                Color badgeBg = isCopy ? new Color(0.2f, 0.95f, 0.4f, 0.95f) : new Color(0.25f, 0.85f, 1.0f, 0.95f);
                Rect badgeR = new Rect(bottleRect.center.x - 45, bottleRect.y + bottleRect.height - 18, 90, 18);
                EditorGUI.DrawRect(badgeR, badgeBg);
                GUIStyle actStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    normal = { textColor = Color.black }
                };
                GUI.Label(badgeR, actionText, actStyle);
            }
        }

        // 3. ADIM: Sürükleme Sırasında İmleci Takip Eden Hayalet Şişe
        if (isSlotDragging && dragSourceBottleIndex >= 0 && dragSourceBottleIndex < totalBottles)
        {
            var srcPiece = currentLevel.pieces[dragSourceBottleIndex];
            Vector2 mousePos = currentEvent.mousePosition;
            float ghostW = 34f * bottleScaleMult;
            float ghostH = 50f * bottleScaleMult;
            Rect ghostRect = new Rect(mousePos.x + 14f, mousePos.y - ghostH / 2f, ghostW, ghostH);

            EditorGUI.DrawRect(ghostRect, new Color(0.10f, 0.14f, 0.22f, 0.95f));

            int srcSlices = (srcPiece.sliceColors != null && srcPiece.sliceColors.Count > 0) ? srcPiece.sliceColors.Count : srcPiece.currentSlices;
            if (srcSlices > 0)
            {
                float sliceH = (ghostH - 4) / 4f;
                for (int s = 0; s < srcSlices; s++)
                {
                    Color sCol = (srcPiece.sliceColors != null && s < srcPiece.sliceColors.Count) ? srcPiece.sliceColors[s] : srcPiece.liquidColor;
                    float yPos = (ghostRect.y + ghostH - 2) - ((s + 1) * sliceH);
                    Rect sRect = new Rect(ghostRect.x + 2, yPos, ghostW - 4, sliceH - 1);
                    EditorGUI.DrawRect(sRect, new Color(sCol.r, sCol.g, sCol.b, 0.95f));
                }
            }

            bool isCopy = currentEvent.control || currentEvent.alt;
            Color ghostBorder = isCopy ? new Color(0.3f, 1.0f, 0.45f) : new Color(1.0f, 0.85f, 0.25f);
            Handles.color = ghostBorder;
            Handles.DrawWireCube(ghostRect.center, new Vector3(ghostW, ghostH, 0f));

            string tagText = isCopy ? $"📋 #{dragSourceBottleIndex + 1} (Kopyala)" : $"⇄ #{dragSourceBottleIndex + 1} (Taşı)";
            GUIStyle tagStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = ghostBorder }
            };
            GUI.Label(new Rect(ghostRect.x - 30, ghostRect.y - 18, ghostW + 60, 16), tagText, tagStyle);
        }

        // 4. ADIM: Fare Olayları (Tıklama, Sürükleme, Bırakma, Sağ Tık)
        if (currentEvent.type == EventType.MouseDown)
        {
            for (int i = 0; i < currentBottleRects.Count; i++)
            {
                if (currentBottleRects[i].Contains(currentEvent.mousePosition))
                {
                    selectedBottleIndex = i;

                    if (currentEvent.button == 0) // Sol Tık: Seç & Sürüklemeyi Başlat
                    {
                        dragSourceBottleIndex = i;
                        isSlotDragging = true;
                        dragStartMousePos = currentEvent.mousePosition;
                        currentEvent.Use();
                        Repaint();
                        break;
                    }
                    else if (currentEvent.button == 1) // Sağ Tık: Doğrudan Menüyü Aç
                    {
                        ShowBottleContextMenu(i);
                        currentEvent.Use();
                        break;
                    }
                }
            }
        }
        else if (currentEvent.type == EventType.ContextClick)
        {
            for (int i = 0; i < currentBottleRects.Count; i++)
            {
                if (currentBottleRects[i].Contains(currentEvent.mousePosition))
                {
                    selectedBottleIndex = i;
                    ShowBottleContextMenu(i);
                    currentEvent.Use();
                    break;
                }
            }
        }
        else if (currentEvent.type == EventType.MouseDrag && isSlotDragging)
        {
            if (isCustom && currentEvent.shift && dragSourceBottleIndex >= 0 && dragSourceBottleIndex < totalBottles)
            {
                float previewScale = 38f;
                Vector2 delta = new Vector2(currentEvent.delta.x, -currentEvent.delta.y) / previewScale;
                currentLevel.pieces[dragSourceBottleIndex].customPosition += delta;
                EditorUtility.SetDirty(currentLevel);
            }
            currentEvent.Use();
            Repaint();
        }
        else if (currentEvent.type == EventType.MouseUp && isSlotDragging)
        {
            int targetIdx = -1;
            for (int j = 0; j < currentBottleRects.Count; j++)
            {
                if (j != dragSourceBottleIndex && currentBottleRects[j].Contains(currentEvent.mousePosition))
                {
                    targetIdx = j;
                    break;
                }
            }

            if (targetIdx >= 0 && dragSourceBottleIndex >= 0 && dragSourceBottleIndex < totalBottles)
            {
                bool isCopy = currentEvent.control || currentEvent.alt;
                Undo.RecordObject(currentLevel, isCopy ? "Şişeyi Kopyala" : "Şişeleri Değiştir");

                if (isCopy)
                {
                    CopyPieceContent(currentLevel.pieces[dragSourceBottleIndex], currentLevel.pieces[targetIdx]);
                    ShowNotification(new GUIContent($"📋 #{dragSourceBottleIndex + 1} -> #{targetIdx + 1} Kopyalandı"));
                }
                else
                {
                    SwapPieceContent(currentLevel.pieces[dragSourceBottleIndex], currentLevel.pieces[targetIdx]);
                    ShowNotification(new GUIContent($"⇄ #{dragSourceBottleIndex + 1} ile #{targetIdx + 1} Yer Değiştirildi"));
                }

                selectedBottleIndex = targetIdx;
                EditorUtility.SetDirty(currentLevel);
            }
            else if (isCustom && targetIdx == -1 && dragSourceBottleIndex >= 0 && dragSourceBottleIndex < totalBottles)
            {
                Vector2 totalDelta = currentEvent.mousePosition - dragStartMousePos;
                if (totalDelta.sqrMagnitude > 36f)
                {
                    float previewScale = 38f;
                    Vector2 deltaCoord = new Vector2(totalDelta.x, -totalDelta.y) / previewScale;
                    Undo.RecordObject(currentLevel, "Şişe Pozisyonunu Taşı");
                    currentLevel.pieces[dragSourceBottleIndex].customPosition += deltaCoord;
                    EditorUtility.SetDirty(currentLevel);
                }
            }

            isSlotDragging = false;
            dragSourceBottleIndex = -1;
            hoverTargetBottleIndex = -1;
            currentEvent.Use();
            Repaint();
        }

        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 4.1 HIZLI İŞLEM ÇUBUĞU (Kopyala, Yapıştır, Çoğalt, Boşalt, Sil)
    // ──────────────────────────────────────────────────────────────
    private void DrawQuickActionToolbar()
    {
        if (currentLevel == null || currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label($"⚡ #{selectedBottleIndex + 1}:", EditorStyles.boldLabel, GUILayout.Width(65));

        if (GUILayout.Button(new GUIContent("📋 Kopyala", "Şişe içeriğini panoya kopyala (Ctrl+C)"), GUILayout.Height(24)))
        {
            CopyBottle(selectedBottleIndex);
        }

        GUI.enabled = clipboardPiece != null;
        if (GUILayout.Button(new GUIContent("📥 Yapıştır", "Panodaki şişeyi buraya yapıştır (Ctrl+V)"), GUILayout.Height(24)))
        {
            PasteBottle(selectedBottleIndex);
        }
        GUI.enabled = true;

        if (GUILayout.Button(new GUIContent("✨ Çoğalt", "İlk boş slota bu şişeyi kopyala (Ctrl+D)"), GUILayout.Height(24)))
        {
            DuplicateBottle(selectedBottleIndex);
        }

        GUI.backgroundColor = new Color(1f, 0.65f, 0.65f);
        if (GUILayout.Button(new GUIContent("🗑️ Boşalt", "Şişeyi tamamen boşalt (Del)"), GUILayout.Height(24)))
        {
            ClearBottleContent(selectedBottleIndex);
        }
        GUI.backgroundColor = Color.white;

        var p = selectedBottleIndex < currentLevel.pieces.Count ? currentLevel.pieces[selectedBottleIndex] : null;
        bool isFrozen = p != null && p.isFrozen;
        GUI.backgroundColor = isFrozen ? new Color(0.6f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(new GUIContent(isFrozen ? "❄️ Buzu Çöz" : "❄️ Buzla", "Buzlu cam durumunu aç/kapa"), GUILayout.Height(24)))
        {
            ToggleFrozen(selectedBottleIndex);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);

        if (GUILayout.Button(new GUIContent("➕ Slot Ekle", "Listenin sonuna yeni bir boş slot ekler"), GUILayout.Height(24)))
        {
            InsertBottleSlot(currentLevel.pieces.Count);
        }

        if (GUILayout.Button(new GUIContent("❌ Slotu Sil", "Seçili slotu tamamen kaldırır (Shift+Del)"), GUILayout.Height(24)))
        {
            DeleteBottleSlot(selectedBottleIndex);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 4.2 SAĞ TIK BAĞLAM MENÜSÜ
    // ──────────────────────────────────────────────────────────────
    private void ShowBottleContextMenu(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        var piece = currentLevel.pieces[index];
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent($"🗑️ Şişeyi Boşalt #{index + 1} [Del]"), false, () => ClearBottleContent(index));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent($"📋 Kopyala (Ctrl+C)"), false, () => CopyBottle(index));
        if (clipboardPiece != null)
        {
            menu.AddItem(new GUIContent($"📥 Yapıştır (Ctrl+V)"), false, () => PasteBottle(index));
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("📥 Yapıştır (Pano Boş)"));
        }

        menu.AddItem(new GUIContent($"✨ Boş Slota Çoğalt (Ctrl+D)"), false, () => DuplicateBottle(index));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent(piece.isFrozen ? "❄️ Buz Kilidini Kaldır" : "❄️ Buzlu Cam Şişe Yap"), piece.isFrozen, () => ToggleFrozen(index));
        menu.AddSeparator("");

        for (int c = 0; c < QuickColors.Length; c++)
        {
            Color col = QuickColors[c];
            string cName = QuickColorNames[c];
            menu.AddItem(new GUIContent($"🎨 Hızlı Tek Renkle Doldur (4 Dilim)/{cName}"), false, () => FillBottleWithColor(index, col));
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("➕ Hemen Sonrasına Slot Ekle"), false, () => InsertBottleSlot(index + 1));
        menu.AddItem(new GUIContent($"❌ Bu Slotu Kaldır #{index + 1} [Shift+Del]"), false, () => DeleteBottleSlot(index));

        menu.ShowAsContext();
    }

    // ──────────────────────────────────────────────────────────────
    // 4.3 KLAVYE KISAYOLLARI
    // ──────────────────────────────────────────────────────────────
    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;
        if (e == null || currentLevel == null || currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (e.shift)
                {
                    DeleteBottleSlot(selectedBottleIndex);
                }
                else
                {
                    ClearBottleContent(selectedBottleIndex);
                }
                e.Use();
                Repaint();
            }
            else if (e.control || e.command)
            {
                if (e.keyCode == KeyCode.C)
                {
                    CopyBottle(selectedBottleIndex);
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.V)
                {
                    PasteBottle(selectedBottleIndex);
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.D)
                {
                    DuplicateBottle(selectedBottleIndex);
                    e.Use();
                    Repaint();
                }
            }
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow)
            {
                selectedBottleIndex = (selectedBottleIndex + 1) % currentLevel.pieces.Count;
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow)
            {
                selectedBottleIndex = (selectedBottleIndex - 1 + currentLevel.pieces.Count) % currentLevel.pieces.Count;
                e.Use();
                Repaint();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 4.4 ŞİŞE İÇERİK YÖNETİMİ & PANO METODLARI
    // ──────────────────────────────────────────────────────────────
    private void CopyPieceContent(LevelData.PieceData source, LevelData.PieceData target)
    {
        target.currentSlices = source.currentSlices;
        target.liquidColor = source.liquidColor;
        target.isFrozen = source.isFrozen;
        target.requiredMatches = source.requiredMatches;
        target.rotationZ = source.rotationZ;
        target.linkId = source.linkId;
        target.canRotate = source.canRotate;
        target.sliceColors = (source.sliceColors != null) ? new List<Color>(source.sliceColors) : new List<Color>();
    }

    private void SwapPieceContent(LevelData.PieceData a, LevelData.PieceData b)
    {
        int tempSlices = a.currentSlices;
        Color tempColor = a.liquidColor;
        bool tempFrozen = a.isFrozen;
        int tempMatches = a.requiredMatches;
        float tempRot = a.rotationZ;
        int tempLink = a.linkId;
        bool tempRotate = a.canRotate;
        List<Color> tempSlicesList = (a.sliceColors != null) ? new List<Color>(a.sliceColors) : new List<Color>();

        a.currentSlices = b.currentSlices;
        a.liquidColor = b.liquidColor;
        a.isFrozen = b.isFrozen;
        a.requiredMatches = b.requiredMatches;
        a.rotationZ = b.rotationZ;
        a.linkId = b.linkId;
        a.canRotate = b.canRotate;
        a.sliceColors = (b.sliceColors != null) ? new List<Color>(b.sliceColors) : new List<Color>();

        b.currentSlices = tempSlices;
        b.liquidColor = tempColor;
        b.isFrozen = tempFrozen;
        b.requiredMatches = tempMatches;
        b.rotationZ = tempRot;
        b.linkId = tempLink;
        b.canRotate = tempRotate;
        b.sliceColors = tempSlicesList;
    }

    private void CopyBottle(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        var src = currentLevel.pieces[index];
        clipboardPiece = new LevelData.PieceData
        {
            currentSlices = src.currentSlices,
            liquidColor = src.liquidColor,
            isFrozen = src.isFrozen,
            requiredMatches = src.requiredMatches,
            rotationZ = src.rotationZ,
            linkId = src.linkId,
            canRotate = src.canRotate,
            sliceColors = (src.sliceColors != null) ? new List<Color>(src.sliceColors) : new List<Color>()
        };
        ShowNotification(new GUIContent($"📋 #{index + 1} Kopyalandı"));
    }

    private void PasteBottle(int index)
    {
        if (clipboardPiece == null || currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        Undo.RecordObject(currentLevel, "Şişe Yapıştır");
        CopyPieceContent(clipboardPiece, currentLevel.pieces[index]);
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"📥 #{index + 1} Yapıştırıldı"));
    }

    private void DuplicateBottle(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || currentLevel.pieces.Count == 0 || index < 0 || index >= currentLevel.pieces.Count) return;

        var src = currentLevel.pieces[index];

        // İlk boş şişeyi bul
        int targetIndex = -1;
        for (int i = 0; i < currentLevel.pieces.Count; i++)
        {
            var p = currentLevel.pieces[i];
            int sc = (p.sliceColors != null && p.sliceColors.Count > 0) ? p.sliceColors.Count : p.currentSlices;
            if (sc == 0 && i != index)
            {
                targetIndex = i;
                break;
            }
        }

        // Eğer boş şişe yoksa, listenin sonuna yeni slot ekle
        if (targetIndex == -1)
        {
            targetIndex = currentLevel.pieces.Count;
            InsertBottleSlot(targetIndex);
        }

        Undo.RecordObject(currentLevel, "Şişeyi Çoğalt");
        CopyPieceContent(src, currentLevel.pieces[targetIndex]);
        selectedBottleIndex = targetIndex;
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"✨ #{index + 1} -> #{targetIndex + 1} Çoğaltıldı"));
    }

    private void ClearBottleContent(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        Undo.RecordObject(currentLevel, "Şişeyi Boşalt");
        var p = currentLevel.pieces[index];
        p.currentSlices = 0;
        if (p.sliceColors != null) p.sliceColors.Clear();
        p.liquidColor = Color.white;
        p.isFrozen = false;
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"🗑️ #{index + 1} Boşaltıldı"));
    }

    private void DeleteBottleSlot(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;
        if (currentLevel.pieces.Count <= 2)
        {
            ShowNotification(new GUIContent("⚠️ En az 2 şişe bulunmalıdır!"));
            return;
        }

        Undo.RecordObject(currentLevel, "Slotu Sil");
        currentLevel.pieces.RemoveAt(index);
        if (selectedBottleIndex >= currentLevel.pieces.Count)
        {
            selectedBottleIndex = Mathf.Max(0, currentLevel.pieces.Count - 1);
        }
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"❌ #{index + 1} Silindi"));
    }

    private void InsertBottleSlot(int index)
    {
        if (currentLevel == null) return;
        if (currentLevel.pieces == null) currentLevel.pieces = new List<LevelData.PieceData>();

        index = Mathf.Clamp(index, 0, currentLevel.pieces.Count);
        Undo.RecordObject(currentLevel, "Yeni Slot Ekle");

        var newPiece = new LevelData.PieceData
        {
            gridPosition = new Vector2Int(index, 0),
            liquidColor = Color.white,
            currentSlices = 0,
            sliceColors = new List<Color>(),
            rotationZ = 0f,
            canRotate = false
        };

        if (index >= currentLevel.pieces.Count)
            currentLevel.pieces.Add(newPiece);
        else
            currentLevel.pieces.Insert(index, newPiece);

        selectedBottleIndex = index;
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"➕ Slot #{index + 1} Eklendi"));
    }

    private void ToggleFrozen(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        Undo.RecordObject(currentLevel, "Buzlu Cam Durumu Değiştir");
        var p = currentLevel.pieces[index];
        p.isFrozen = !p.isFrozen;
        if (p.isFrozen && p.requiredMatches <= 0) p.requiredMatches = 2;
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent(p.isFrozen ? $"❄️ #{index + 1} Buzlandı" : $"💧 #{index + 1} Buzu Çözüldü"));
    }

    private void FillBottleWithColor(int index, Color color)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        Undo.RecordObject(currentLevel, "Şişeyi Renkle Doldur");
        var p = currentLevel.pieces[index];
        p.currentSlices = 4;
        p.liquidColor = color;
        p.sliceColors = new List<Color> { color, color, color, color };
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent($"🎨 #{index + 1} Dolduruldu"));
    }

    // ──────────────────────────────────────────────────────────────
    // 5. SEÇİLİ ŞİŞE MÜFETTİŞİ
    // ──────────────────────────────────────────────────────────────
    private void DrawSelectedBottleInspector()
    {
        if (currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;
        if (selectedBottleIndex < 0 || selectedBottleIndex >= currentLevel.pieces.Count)
            selectedBottleIndex = 0;

        var piece = currentLevel.pieces[selectedBottleIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"🧪 Seçili Şişe: #{selectedBottleIndex + 1}", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        if (piece.sliceColors == null) piece.sliceColors = new List<Color>();
        while (piece.sliceColors.Count < piece.currentSlices) piece.sliceColors.Add(piece.liquidColor);
        while (piece.sliceColors.Count > piece.currentSlices) piece.sliceColors.RemoveAt(piece.sliceColors.Count - 1);

        int sliceCount = piece.sliceColors.Count;

        // Dilim Sayısı Seçimi
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Doluluk (Dilim):", GUILayout.Width(110));
        for (int s = 0; s <= 4; s++)
        {
            GUI.backgroundColor = (sliceCount == s) ? Color.cyan : Color.white;
            string btnText = (s == 0) ? "0 (Boş)" : s.ToString();
            if (GUILayout.Button(btnText, GUILayout.Height(24)))
            {
                sliceCount = s;
                piece.currentSlices = s;
                while (piece.sliceColors.Count < s) piece.sliceColors.Add(piece.liquidColor);
                while (piece.sliceColors.Count > s) piece.sliceColors.RemoveAt(piece.sliceColors.Count - 1);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (sliceCount > 0)
        {
            EditorGUILayout.LabelField("Katman Renkleri (Aşağıdan Yukarıya):", EditorStyles.boldLabel);
            for (int s = 0; s < piece.sliceColors.Count; s++)
            {
                EditorGUILayout.BeginHorizontal();
                string layerLabel = $"Dilim #{s + 1} " + (s == 0 ? "(En Alt)" : (s == piece.sliceColors.Count - 1 ? "(En Üst)" : ""));
                EditorGUILayout.LabelField(layerLabel, GUILayout.Width(110));
                piece.sliceColors[s] = EditorGUILayout.ColorField(piece.sliceColors[s]);
                EditorGUILayout.EndHorizontal();
            }

            piece.liquidColor = piece.sliceColors[piece.sliceColors.Count - 1];

            // Hızlı Renk Paleti (Tüm Katmanları Tek Renk Yap)
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Hızlı Renk Paleti (Tüm Şişeyi Tek Renk Yap):", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < QuickColors.Length; c++)
            {
                GUI.backgroundColor = QuickColors[c];
                if (GUILayout.Button(QuickColorNames[c], GUILayout.Height(22)))
                {
                    Color chosen = QuickColors[c];
                    for (int s = 0; s < piece.sliceColors.Count; s++) piece.sliceColors[s] = chosen;
                    piece.liquidColor = chosen;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("❄️ Donuk (Buzlu Cam) Ayarları:", EditorStyles.boldLabel);
        piece.isFrozen = EditorGUILayout.ToggleLeft("Buzlu Cam Şişe (Kilitli)", piece.isFrozen);
        if (piece.isFrozen)
        {
            if (piece.requiredMatches <= 0) piece.requiredMatches = 2;
            piece.requiredMatches = EditorGUILayout.IntSlider("Erime İçin Tam Şişe:", piece.requiredMatches, 1, 4);
            EditorGUILayout.HelpBox($"Bu şişe buzlu ve kilitli başlar. Sahnedeki diğer {piece.requiredMatches} şişe tek renkle tam (4/4) dolduğunda buz erir!", MessageType.Info);
        }
        if (currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.CustomPositions)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎯 Serbest Pozisyon (Dünya Koordinatı):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            piece.customPosition = EditorGUILayout.Vector2Field("", piece.customPosition);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Sol (-0.1)")) { piece.customPosition.x -= 0.1f; GUI.changed = true; }
            if (GUILayout.Button("▶ Sağ (+0.1)")) { piece.customPosition.x += 0.1f; GUI.changed = true; }
            if (GUILayout.Button("▲ Yukarı (+0.1)")) { piece.customPosition.y += 0.1f; GUI.changed = true; }
            if (GUILayout.Button("▼ Aşağı (-0.1)")) { piece.customPosition.y -= 0.1f; GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Şişe Özelliklerini Değiştir");
            EditorUtility.SetDirty(currentLevel);
        }

        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 6. ÇÖZÜLEBİLİRLİK & RENK DAĞILIMI ANALİZİ
    // ──────────────────────────────────────────────────────────────
    private void DrawSolvabilityAnalyzer()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚖️ Oyun Dengesi ve Çözülebilirlik Analizi", EditorStyles.boldLabel);

        if (currentLevel.pieces == null || currentLevel.pieces.Count == 0)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        Dictionary<Color, int> colorSlices = new Dictionary<Color, int>();
        int emptyBottleCount = 0;

        foreach (var p in currentLevel.pieces)
        {
            int count = (p.sliceColors != null && p.sliceColors.Count > 0) ? p.sliceColors.Count : p.currentSlices;
            if (count <= 0)
            {
                emptyBottleCount++;
                continue;
            }

            if (p.sliceColors != null && p.sliceColors.Count > 0)
            {
                foreach (var sc in p.sliceColors)
                {
                    AddColorCount(colorSlices, sc, 1);
                }
            }
            else
            {
                AddColorCount(colorSlices, p.liquidColor, count);
            }
        }

        bool hasError = false;

        foreach (var kvp in colorSlices)
        {
            int count = kvp.Value;
            bool isMultipleOf4 = (count % 4 == 0);
            if (!isMultipleOf4) hasError = true;

            EditorGUILayout.BeginHorizontal();
            GUI.color = kvp.Key;
            GUILayout.Label("■", GUILayout.Width(16));
            GUI.color = Color.white;

            string status = isMultipleOf4
                ? $"✅ Toplam {count} dilim ({count / 4} tam şişe)"
                : $"⚠️ Toplam {count} dilim (4'ün katı olmalı, tamamlanamaz!)";
            EditorGUILayout.LabelField(status);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(2);
        if (emptyBottleCount == 0)
        {
            hasError = true;
            EditorGUILayout.HelpBox("⚠️ Hiç boş şişe yok! Oyuncunun dökme alanı olması için en az 1-2 boş şişe bırakmalısınız.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"🧴 Boş Şişe Sayısı: {emptyBottleCount} (Yeterli çalışma alanı ✅)");
        }

        if (!hasError && colorSlices.Count > 0)
        {
            EditorGUILayout.HelpBox("🎉 Seviye Mükemmel! Tüm renkler tam şişe oluşturuyor ve boş şişe mevcut.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void AddColorCount(Dictionary<Color, int> dict, Color col, int amount)
    {
        Color matchedKey = col;
        bool found = false;
        foreach (var k in dict.Keys)
        {
            if (ColorsMatch(k, col))
            {
                matchedKey = k;
                found = true;
                break;
            }
        }
        if (!found) dict[matchedKey] = 0;
        dict[matchedKey] += amount;
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.05f &&
               Mathf.Abs(a.g - b.g) < 0.05f &&
               Mathf.Abs(a.b - b.b) < 0.05f;
    }

    // ──────────────────────────────────────────────────────────────
    // 7. OTOMATİK DENGELİ SEVİYE ÜRETİCİ
    // ──────────────────────────────────────────────────────────────
    private void DrawLevelGenerator()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🎲 Sihirli Seviye Üretici (Tek Tıkla Dengeli Bölüm)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Seçtiğiniz renk ve boş şişe sayısına göre otomatik, garantili çözülebilir bölüm üretir.", EditorStyles.miniLabel);

        genColorCount = EditorGUILayout.IntSlider("Renk Sayısı", genColorCount, 2, QuickColors.Length);
        genEmptyCount = EditorGUILayout.IntSlider("Boş Şişe Sayısı", genEmptyCount, 1, 3);

        int totalGenBottles = genColorCount + genEmptyCount;
        EditorGUILayout.LabelField($"Oluşturulacak Toplam Şişe: {totalGenBottles} ({genColorCount} Renkli + {genEmptyCount} Boş)");

        if (GUILayout.Button("✨ Rastgele Dengeli Seviye Üret", GUILayout.Height(28)))
        {
            GenerateSolvableLevel();
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("🎯 Görseldeki 25 Şişeli Kademeli V Seviyesini Üret", GUILayout.Height(30)))
        {
            Generate25BottleVLevel();
        }

        EditorGUILayout.EndVertical();
    }

    private void Generate25BottleVLevel()
    {
        if (currentLevel == null) return;

        Undo.RecordObject(currentLevel, "25 Şişeli V Seviyesi Üret");
        currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.StaggeredV;
        currentLevel.customSpacingX = 1.22f;
        currentLevel.customSpacingY = 1.95f;
        currentLevel.pieces = new List<LevelData.PieceData>();

        // 21 Dolu şişe, 4 Boş şişe = Toplam 25 Şişe
        // Referans görseldeki gibi boş şişeler: Slot 3 (Col 0 taban), Slot 10 (Col 2 taban), Slot 16 (Col 4 taban), Slot 24 (Col 6 taban)
        HashSet<int> emptySlots = new HashSet<int> { 3, 10, 16, 24 };

        int fullBottleCount = 25 - emptySlots.Count; // 21 şişe = 84 dilim
        // 7 Renk * 3 şişe = 21 tam dolu şişe (Her renk tam 12 dilim = 3 şişe)
        List<Color> slicePool = new List<Color>();
        int colorCount = Mathf.Min(7, QuickColors.Length);
        for (int c = 0; c < colorCount; c++)
        {
            Color col = QuickColors[c];
            for (int s = 0; s < 12; s++) // Her renk 3 tam şişe (12 dilim)
                slicePool.Add(col);
        }

        // Dilimleri karıştır
        for (int i = 0; i < slicePool.Count; i++)
        {
            int rnd = Random.Range(i, slicePool.Count);
            Color temp = slicePool[i];
            slicePool[i] = slicePool[rnd];
            slicePool[rnd] = temp;
        }

        int sliceIdx = 0;
        for (int slot = 0; slot < 25; slot++)
        {
            if (emptySlots.Contains(slot))
            {
                currentLevel.pieces.Add(new LevelData.PieceData
                {
                    gridPosition = new Vector2Int(slot, 0),
                    liquidColor = Color.white,
                    currentSlices = 0,
                    sliceColors = new List<Color>(),
                    rotationZ = 0f,
                    canRotate = false
                });
            }
            else
            {
                List<Color> bottleSlices = new List<Color>();
                for (int s = 0; s < 4; s++)
                {
                    bottleSlices.Add(slicePool[sliceIdx++]);
                }

                currentLevel.pieces.Add(new LevelData.PieceData
                {
                    gridPosition = new Vector2Int(slot, 0),
                    liquidColor = bottleSlices[bottleSlices.Count - 1],
                    currentSlices = 4,
                    sliceColors = bottleSlices,
                    rotationZ = 0f,
                    canRotate = false
                });
            }
        }

        selectedBottleIndex = 0;
        EditorUtility.SetDirty(currentLevel);
        SaveCurrentLevel();
        Debug.Log($"[LevelDesigner] '{currentLevel.name}' 25 şişeli kademeli V düzeniyle başarıyla üretildi!");
    }

    private void GenerateSolvableLevel()
    {
        if (currentLevel == null) return;

        Undo.RecordObject(currentLevel, "Otomatik Seviye Üret");

        int totalBottles = genColorCount + genEmptyCount;
        currentLevel.pieces = new List<LevelData.PieceData>();

        // Her renk için 4 dilim oluştur
        List<Color> slicePool = new List<Color>();
        for (int c = 0; c < genColorCount; c++)
        {
            Color col = QuickColors[c % QuickColors.Length];
            for (int s = 0; s < 4; s++) slicePool.Add(col);
        }

        // Dilimleri rastgele karıştır
        for (int i = 0; i < slicePool.Count; i++)
        {
            int rnd = Random.Range(i, slicePool.Count);
            Color temp = slicePool[i];
            slicePool[i] = slicePool[rnd];
            slicePool[rnd] = temp;
        }

        // Şişeleri doldur (Renkli şişeler - 4 karışık katman)
        int poolIndex = 0;
        for (int b = 0; b < genColorCount; b++)
        {
            List<Color> bottleSlices = new List<Color>();
            for (int s = 0; s < 4; s++)
            {
                bottleSlices.Add(slicePool[poolIndex++]);
            }

            currentLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(b, 0),
                liquidColor = bottleSlices[bottleSlices.Count - 1],
                currentSlices = 4,
                sliceColors = bottleSlices,
                rotationZ = 0f,
                canRotate = false
            });
        }

        // Boş şişeleri ekle
        for (int e = 0; e < genEmptyCount; e++)
        {
            currentLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(genColorCount + e, 0),
                liquidColor = Color.white,
                currentSlices = 0,
                sliceColors = new List<Color>(),
                rotationZ = 0f,
                canRotate = false
            });
        }

        selectedBottleIndex = 0;
        EditorUtility.SetDirty(currentLevel);
        SaveCurrentLevel();
        Debug.Log($"[LevelDesigner] '{currentLevel.name}' başarıyla dengeli çok katmanlı olarak üretildi!");
    }

    // ──────────────────────────────────────────────────────────────
    // DOSYA YÖNETİMİ (YENİ, KAYDET, SİL, SEQUENCE)
    // ──────────────────────────────────────────────────────────────
    private void CreateNewLevel()
    {
        string folder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        int maxNum = 0;
        string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(assetPath);
            if (filename.StartsWith("Level_"))
            {
                string numStr = filename.Substring(6);
                if (int.TryParse(numStr, out int num))
                {
                    if (num > maxNum) maxNum = num;
                }
            }
        }

        int nextNum = maxNum + 1;
        string defaultName = $"Level_{nextNum:D2}";
        string targetPath = $"{folder}/{defaultName}.asset";

        while (File.Exists(targetPath))
        {
            nextNum++;
            defaultName = $"Level_{nextNum:D2}";
            targetPath = $"{folder}/{defaultName}.asset";
        }

        LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
        newLevel.name = defaultName;
        newLevel.levelDisplayName = defaultName;
        newLevel.timeLimit = 150f;
        newLevel.boardMode = LevelData.BoardMode.Flat2D;
        newLevel.pieces = new List<LevelData.PieceData>();

        // Varsayılan 6 şişe oluştur (4 renkli, 2 boş — kullanıcının çizdiği düzen)
        for (int i = 0; i < 4; i++)
        {
            newLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(i, 0),
                liquidColor = QuickColors[i],
                currentSlices = 4,
                rotationZ = 0f,
                canRotate = false
            });
        }
        for (int i = 4; i < 6; i++)
        {
            newLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(i, 0),
                liquidColor = Color.white,
                currentSlices = 0,
                rotationZ = 0f,
                canRotate = false
            });
        }

        AssetDatabase.CreateAsset(newLevel, targetPath);
        AssetDatabase.SaveAssets();

        currentLevel = newLevel;
        selectedBottleIndex = 0;

        AddLevelToSequence(newLevel);
        Debug.Log($"[LevelDesigner] '{defaultName}' başarıyla oluşturuldu ve sequence'e eklendi.");
    }

    private void SaveCurrentLevel()
    {
        if (currentLevel == null) return;
        EditorUtility.SetDirty(currentLevel);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelDesigner] '{currentLevel.name}' kaydedildi.");
    }

    private void DeleteCurrentLevel()
    {
        if (currentLevel == null) return;
        string path = AssetDatabase.GetAssetPath(currentLevel);
        RemoveLevelFromSequence(currentLevel);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        currentLevel = null;
        LoadFirstAvailableLevel();
    }

    private void AddLevelToSequence(LevelData level)
    {
        LevelSequenceData seq = AssetDatabase.LoadAssetAtPath<LevelSequenceData>("Assets/LevelSequence.asset");
        if (seq != null)
        {
            if (seq.levels == null) seq.levels = new List<LevelData>();
            if (!seq.levels.Contains(level))
            {
                seq.levels.Add(level);
                EditorUtility.SetDirty(seq);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private void RemoveLevelFromSequence(LevelData level)
    {
        LevelSequenceData seq = AssetDatabase.LoadAssetAtPath<LevelSequenceData>("Assets/LevelSequence.asset");
        if (seq != null && seq.levels != null)
        {
            seq.levels.Remove(level);
            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssets();
        }
    }
}
