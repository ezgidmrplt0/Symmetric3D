using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Magic Sort — Modern, Kullanıcı Dostu Seviye Tasarımcısı.
/// Temiz görsel telefon önizlemesi, tek tıkla katman boyama,
/// otomatik çözülebilirlik kontrolü ve pratik seviye yönetimi.
/// </summary>
public class LevelDesignerWindow : EditorWindow
{
    private LevelData currentLevel;
    private int selectedBottleIndex = 0;
    private Vector2 scrollPos;

    // Katlanabilir gelişmiş bölümler (varsayılan kapalı - karmaşayı önler)
    private bool showAdvancedSettings = false;
    private bool showLevelGenerator = false;

    // Hızlı Renk Paleti (Water / Magic Sort standart renkleri)
    public static readonly Color[] QuickColors = new Color[]
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

    public static readonly string[] QuickColorNames = new string[]
    {
        "Kırmızı", "Mavi", "Yeşil", "Sarı", "Mor", "Turuncu", "Turkuaz", "Pembe"
    };

    // Otomatik seviye üretici ayarları
    private int genColorCount = 4;
    private int genEmptyCount = 2;

    // Sürükle-Bırak & Kopyala Panosu
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
        window.minSize = new Vector2(460, 680);
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

        DrawTopBar();
        GUILayout.Space(6);

        if (currentLevel == null)
        {
            EditorGUILayout.HelpBox("Düzenlemek için bir Level seçin veya '➕ Yeni Seviye' butonuna basın.", MessageType.Info);
            if (GUILayout.Button("⚡ Hemen İlk Seviyeyi Oluştur", GUILayout.Height(36)))
            {
                CreateNewLevel();
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawLevelHeaderCard();
        GUILayout.Space(8);

        DrawVisualCanvas();
        GUILayout.Space(6);

        DrawSolvabilityStatusBar();
        GUILayout.Space(8);

        DrawSelectedBottleEditor();
        GUILayout.Space(8);

        DrawAdvancedFoldouts();
        GUILayout.Space(14);

        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────────────────────
    // 1. ÜST YÖNETİM BARI
    // ──────────────────────────────────────────────────────────────
    private void DrawTopBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        LevelData newSelected = (LevelData)EditorGUILayout.ObjectField("Aktif Seviye", currentLevel, typeof(LevelData), false);
        if (newSelected != currentLevel)
        {
            currentLevel = newSelected;
            selectedBottleIndex = 0;
            GUI.FocusControl(null);
        }

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("➕ Yeni", GUILayout.Width(65), GUILayout.Height(22)))
        {
            CreateNewLevel();
        }
        GUI.backgroundColor = Color.white;

        if (currentLevel != null)
        {
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1.0f);
            if (GUILayout.Button("💾 Kaydet", GUILayout.Width(70), GUILayout.Height(22)))
            {
                SaveCurrentLevel();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1.0f, 0.55f, 0.55f);
            if (GUILayout.Button("🗑️ Sil", GUILayout.Width(50), GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("Seviyeyi Sil", $"'{currentLevel.name}' silinecek. Emin misiniz?", "Evet, Sil", "İptal"))
                {
                    DeleteCurrentLevel();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 2. TEMEL SEVİYE BİLGİLERİ (Kompakt ve Temiz)
    // ──────────────────────────────────────────────────────────────
    private void DrawLevelHeaderCard()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUI.BeginChangeCheck();

        // 1. Satır: Seviye Adı ve Süre Limiti
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Seviye Adı:", GUILayout.Width(75));
        string newName = EditorGUILayout.TextField(currentLevel.levelDisplayName);

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Süre (sn):", GUILayout.Width(60));
        float newTime = EditorGUILayout.FloatField(currentLevel.timeLimit, GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 2. Satır: Şişe Sayısı Arttır/Azalt & Dizilim Modu
        EditorGUILayout.BeginHorizontal();
        int currentCount = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        EditorGUILayout.LabelField("Şişe Sayısı:", GUILayout.Width(75));

        GUI.enabled = currentCount > 2;
        if (GUILayout.Button("➖", GUILayout.Width(28), GUILayout.Height(20)))
        {
            EnsureBottleCount(Mathf.Max(2, currentCount - 1));
        }
        GUI.enabled = true;

        GUILayout.Label($"<b>{currentCount}</b> Şişe", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(60));

        if (GUILayout.Button("➕", GUILayout.Width(28), GUILayout.Height(20)))
        {
            EnsureBottleCount(currentCount + 1);
        }

        GUILayout.Space(14);
        EditorGUILayout.LabelField("Dizilim:", GUILayout.Width(50));
        currentLevel.flatLayoutMode = (LevelData.FlatLayoutMode)EditorGUILayout.EnumPopup(currentLevel.flatLayoutMode);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Seviye Bilgilerini Değiştir");
            currentLevel.levelDisplayName = newName;
            currentLevel.timeLimit = newTime;
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
            Color defaultColor = QuickColors[idx % QuickColors.Length];
            currentLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(idx, 0),
                liquidColor = defaultColor,
                currentSlices = 0,
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
        EditorUtility.SetDirty(currentLevel);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. GÖRSEL TELEFON KANVASI (Gerçekçi Şişeler & Sezgisel Raf Düzeni)
    // ──────────────────────────────────────────────────────────────
    private void DrawVisualCanvas()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        int totalBottles = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        if (totalBottles == 0)
        {
            EditorGUILayout.HelpBox("Henüz şişe yok. Yukarıdan '➕' butonuna basarak şişe ekleyin.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // Kanvas boyutu
        float canvasWidth = 330f;
        float canvasHeight = 410f;
        Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.ExpandWidth(true));

        float drawX = canvasRect.x + (canvasRect.width - canvasWidth) / 2f;
        Rect phoneRect = new Rect(drawX, canvasRect.y, canvasWidth, canvasHeight);

        // Arka plan görseli
        Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/arkaplan.jpeg");
        if (bgTex != null)
        {
            GUI.DrawTexture(phoneRect, bgTex, ScaleMode.ScaleAndCrop);
            EditorGUI.DrawRect(phoneRect, new Color(0.02f, 0.03f, 0.08f, 0.35f));
        }
        else
        {
            EditorGUI.DrawRect(phoneRect, new Color(0.10f, 0.12f, 0.18f));
        }

        Handles.color = new Color(0.40f, 0.50f, 0.70f, 0.8f);
        Handles.DrawWireCube(phoneRect.center, new Vector3(phoneRect.size.x, phoneRect.size.y, 0f));

        Vector2 center = phoneRect.center;
        Event currentEvent = Event.current;

        // Şişe boyutları
        float bWidth = (totalBottles <= 6) ? 44f : ((totalBottles <= 10) ? 38f : 32f);
        float bHeight = bWidth * 1.55f;

        // 1. Şişe Pozisyonlarını Hesapla (Doğal Raf Dağılımı)
        currentBottleRects.Clear();
        bool isStaggered = (currentLevel.flatLayoutMode == LevelData.FlatLayoutMode.StaggeredV && totalBottles >= 20);

        if (isStaggered)
        {
            for (int i = 0; i < totalBottles; i++)
            {
                Vector3 relPos = GridSpawner.GetStaggeredVPosition(i, totalBottles, 42f, 68f);
                Vector2 bCenter = center + new Vector2(relPos.x, -relPos.y);
                currentBottleRects.Add(new Rect(bCenter.x - bWidth / 2f, bCenter.y - bHeight / 2f, bWidth, bHeight));
            }
        }
        else
        {
            // Dengeli Raf Dağılımı: 1, 2 veya 3 rafa ortalayarak yerleştirir
            int rowCount = 1;
            if (totalBottles >= 9) rowCount = 3;
            else if (totalBottles >= 4) rowCount = 2;

            int[] rowCapacities = new int[rowCount];
            int baseC = totalBottles / rowCount;
            int rem = totalBottles % rowCount;
            for (int r = 0; r < rowCount; r++)
                rowCapacities[r] = baseC + (r < rem ? 1 : 0);

            float rowSpacingY = (rowCount == 1) ? 0f : ((rowCount == 2) ? 130f : 100f);
            int bottleIdx = 0;

            for (int r = 0; r < rowCount; r++)
            {
                int countInThisRow = rowCapacities[r];
                float rowY = center.y + (r - (rowCount - 1) * 0.5f) * rowSpacingY;
                float spacingX = Mathf.Min(65f, (canvasWidth - 40f) / Mathf.Max(1, countInThisRow));

                for (int c = 0; c < countInThisRow; c++)
                {
                    if (bottleIdx >= totalBottles) break;
                    float posX = center.x + (c - (countInThisRow - 1) * 0.5f) * spacingX;
                    currentBottleRects.Add(new Rect(posX - bWidth / 2f, rowY - bHeight / 2f, bWidth, bHeight));
                    bottleIdx++;
                }
            }
        }

        // Sürükleme hedef tespiti
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

        // 2. Şişeleri Çiz (Zarif Şişe Silueti & Katmanlar)
        for (int i = 0; i < totalBottles; i++)
        {
            if (i >= currentBottleRects.Count) break;
            Rect bRect = currentBottleRects[i];
            var piece = currentLevel.pieces[i];
            bool isSelected = (i == selectedBottleIndex);
            bool isDragSource = (isSlotDragging && i == dragSourceBottleIndex);
            bool isHoverTarget = (isSlotDragging && i == hoverTargetBottleIndex);

            // Şişe Boynu (Neck)
            float neckW = bWidth * 0.38f;
            float neckH = 10f;
            Rect neckRect = new Rect(bRect.center.x - neckW / 2f, bRect.y, neckW, neckH);
            EditorGUI.DrawRect(neckRect, new Color(0.40f, 0.55f, 0.70f, 0.45f));
            Handles.color = new Color(0.60f, 0.75f, 0.90f, 0.75f);
            Handles.DrawWireCube(neckRect.center, new Vector3(neckW, neckH, 0f));

            // Şişe Gövdesi (Body)
            Rect bodyRect = new Rect(bRect.x, bRect.y + neckH, bWidth, bHeight - neckH);
            EditorGUI.DrawRect(bodyRect, isDragSource ? new Color(0.12f, 0.15f, 0.22f, 0.4f) : new Color(0.12f, 0.16f, 0.24f, 0.85f));

            // Sıvı Katmanları (Aşağıdan Yukarıya 4 Dilim Yuvası)
            int sliceCount = (piece.sliceColors != null && piece.sliceColors.Count > 0) ? piece.sliceColors.Count : piece.currentSlices;
            float slotH = (bodyRect.height - 4f) / 4f;

            for (int s = 0; s < 4; s++)
            {
                float slotY = (bodyRect.y + bodyRect.height - 2f) - ((s + 1) * slotH);
                Rect slotRect = new Rect(bodyRect.x + 2f, slotY, bodyRect.width - 4f, slotH - 1f);

                if (s < sliceCount)
                {
                    Color sCol = (piece.sliceColors != null && s < piece.sliceColors.Count) ? piece.sliceColors[s] : piece.liquidColor;
                    float alpha = isDragSource ? 0.45f : 1.0f;
                    EditorGUI.DrawRect(slotRect, new Color(sCol.r, sCol.g, sCol.b, alpha));
                }
                else
                {
                    // Boş katman yuvası
                    EditorGUI.DrawRect(slotRect, new Color(0.20f, 0.26f, 0.35f, 0.25f));
                }
            }

            // Çerçeve Vurguları (Seçili / Hedef / Normal)
            if (isHoverTarget)
            {
                Handles.color = new Color(0.25f, 1.0f, 0.5f, 1.0f);
                Handles.DrawWireCube(bodyRect.center, new Vector3(bodyRect.width + 5f, bodyRect.height + 5f, 0f));
            }
            else if (isSelected)
            {
                Handles.color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                Handles.DrawWireCube(bodyRect.center, new Vector3(bodyRect.width + 4f, bodyRect.height + 4f, 0f));
                Handles.DrawWireCube(bodyRect.center, new Vector3(bodyRect.width + 2f, bodyRect.height + 2f, 0f));
            }
            else
            {
                Handles.color = new Color(0.50f, 0.65f, 0.80f, 0.70f);
                Handles.DrawWireCube(bodyRect.center, new Vector3(bodyRect.width, bodyRect.height, 0f));
            }

            // Şişe Numarası (#1, #2)
            GUIStyle numStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isSelected ? new Color(1.0f, 0.88f, 0.25f) : Color.white },
                fontSize = 10
            };
            GUI.Label(new Rect(bRect.x - 4, bRect.y - 17, bWidth + 8, 16), $"#{i + 1}", numStyle);

            // Buzlu Şişe Rozeti
            if (piece.isFrozen)
            {
                EditorGUI.DrawRect(bodyRect, new Color(0.45f, 0.85f, 1.0f, 0.35f));
                Rect iceBadge = new Rect(bodyRect.center.x - 16, bodyRect.center.y - 11, 32, 22);
                EditorGUI.DrawRect(iceBadge, new Color(0.05f, 0.15f, 0.30f, 0.95f));
                Handles.color = new Color(0.40f, 0.90f, 1.0f);
                Handles.DrawWireCube(iceBadge.center, new Vector3(iceBadge.width, iceBadge.height, 0f));
                GUIStyle iceTxt = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = Color.cyan }
                };
                GUI.Label(iceBadge, $"❄️{piece.requiredMatches}", iceTxt);
            }
        }

        // 3. Sürüklenen Hayalet Şişe
        if (isSlotDragging && dragSourceBottleIndex >= 0 && dragSourceBottleIndex < totalBottles)
        {
            Vector2 mousePos = currentEvent.mousePosition;
            Rect ghostRect = new Rect(mousePos.x + 10f, mousePos.y - 25f, 32f, 50f);
            EditorGUI.DrawRect(ghostRect, new Color(0.10f, 0.15f, 0.25f, 0.92f));
            Handles.color = new Color(0.3f, 1.0f, 0.5f, 1.0f);
            Handles.DrawWireCube(ghostRect.center, new Vector3(ghostRect.width, ghostRect.height, 0f));

            var srcP = currentLevel.pieces[dragSourceBottleIndex];
            int sCount = (srcP.sliceColors != null && srcP.sliceColors.Count > 0) ? srcP.sliceColors.Count : srcP.currentSlices;
            float gSlotH = (ghostRect.height - 4f) / 4f;
            for (int s = 0; s < sCount; s++)
            {
                Color sc = (srcP.sliceColors != null && s < srcP.sliceColors.Count) ? srcP.sliceColors[s] : srcP.liquidColor;
                float sy = (ghostRect.y + ghostRect.height - 2f) - ((s + 1) * gSlotH);
                EditorGUI.DrawRect(new Rect(ghostRect.x + 2f, sy, ghostRect.width - 4f, gSlotH - 1f), sc);
            }

            GUI.Label(new Rect(ghostRect.x - 20, ghostRect.y - 16, 72, 16), $"⇄ Taşı", EditorStyles.boldLabel);
        }

        // 4. Fare Olayları (Tıklama, Sürükleme, Bırakma)
        if (currentEvent.type == EventType.MouseDown)
        {
            for (int i = 0; i < currentBottleRects.Count; i++)
            {
                if (currentBottleRects[i].Contains(currentEvent.mousePosition))
                {
                    selectedBottleIndex = i;
                    if (currentEvent.button == 0)
                    {
                        dragSourceBottleIndex = i;
                        isSlotDragging = true;
                        dragStartMousePos = currentEvent.mousePosition;
                        currentEvent.Use();
                        Repaint();
                        break;
                    }
                    else if (currentEvent.button == 1)
                    {
                        ShowBottleContextMenu(i);
                        currentEvent.Use();
                        break;
                    }
                }
            }
        }
        else if (currentEvent.type == EventType.MouseDrag && isSlotDragging)
        {
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
                Undo.RecordObject(currentLevel, "Şişeleri Değiştir");
                SwapPieceContent(currentLevel.pieces[dragSourceBottleIndex], currentLevel.pieces[targetIdx]);
                selectedBottleIndex = targetIdx;
                EditorUtility.SetDirty(currentLevel);
                ShowNotification(new GUIContent($"⇄ #{dragSourceBottleIndex + 1} ile #{targetIdx + 1} Yer Değiştirildi"));
            }

            isSlotDragging = false;
            dragSourceBottleIndex = -1;
            hoverTargetBottleIndex = -1;
            currentEvent.Use();
            Repaint();
        }

        EditorGUILayout.HelpBox("💡 İpucu: Şişelere tıklayarak seçebilir, sürükleyerek yerlerini değiştirebilir veya sağ tıklayabilirsiniz.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 4. ÇÖZÜLEBİLİRLİK & DENGELİLİK DURUM ÇUBUĞU
    // ──────────────────────────────────────────────────────────────
    private void DrawSolvabilityStatusBar()
    {
        if (currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;

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
                    AddColorCount(colorSlices, sc, 1);
            }
            else
            {
                AddColorCount(colorSlices, p.liquidColor, count);
            }
        }

        bool hasError = false;
        List<string> errorMessages = new List<string>();

        foreach (var kvp in colorSlices)
        {
            if (kvp.Value % 4 != 0)
            {
                hasError = true;
                string colName = GetColorName(kvp.Key);
                errorMessages.Add($"{colName}: {kvp.Value} dilim (4'ün katı olmalı)");
            }
        }

        if (emptyBottleCount == 0)
        {
            hasError = true;
            errorMessages.Add("Hiç boş şişe yok (en az 1 olmalı)");
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (!hasError && colorSlices.Count > 0)
        {
            GUI.color = new Color(0.85f, 1.0f, 0.88f);
            EditorGUILayout.HelpBox($"✅ Seviye Çözülebilir! ({colorSlices.Count} renk tam 4'lü tamamlanıyor, {emptyBottleCount} boş şişe mevcut)", MessageType.Info);
            GUI.color = Color.white;
        }
        else if (hasError)
        {
            GUI.color = new Color(1.0f, 0.92f, 0.82f);
            EditorGUILayout.HelpBox($"⚠️ Seviye Uyarısı: {string.Join(" • ", errorMessages)}", MessageType.Warning);
            GUI.color = Color.white;
        }
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 5. SEÇİLİ ŞİŞE EDİTÖRÜ (Hızlı, 1-Tıkla Renk Boyama)
    // ──────────────────────────────────────────────────────────────
    private void DrawSelectedBottleEditor()
    {
        if (currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;
        if (selectedBottleIndex < 0 || selectedBottleIndex >= currentLevel.pieces.Count)
            selectedBottleIndex = 0;

        var piece = currentLevel.pieces[selectedBottleIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Başlık ve Şişe İşlem Butonları
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"🧪 Seçili Şişe: #{selectedBottleIndex + 1}", EditorStyles.boldLabel, GUILayout.Width(130));

        if (GUILayout.Button(new GUIContent("📋 Kopyala", "Ctrl+C"), GUILayout.Height(20))) CopyBottle(selectedBottleIndex);
        GUI.enabled = clipboardPiece != null;
        if (GUILayout.Button(new GUIContent("📥 Yapıştır", "Ctrl+V"), GUILayout.Height(20))) PasteBottle(selectedBottleIndex);
        GUI.enabled = true;
        if (GUILayout.Button(new GUIContent("🗑️ Boşalt", "Del"), GUILayout.Height(20))) ClearBottleContent(selectedBottleIndex);

        bool isFrozen = piece.isFrozen;
        GUI.backgroundColor = isFrozen ? new Color(0.6f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(isFrozen ? "❄️ Buzu Çöz" : "❄️ Buzla", GUILayout.Height(20))) ToggleFrozen(selectedBottleIndex);
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("❌ Sil", GUILayout.Height(20), GUILayout.Width(45))) DeleteBottleSlot(selectedBottleIndex);

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        EditorGUI.BeginChangeCheck();

        if (piece.sliceColors == null) piece.sliceColors = new List<Color>();
        while (piece.sliceColors.Count < piece.currentSlices) piece.sliceColors.Add(piece.liquidColor);
        while (piece.sliceColors.Count > piece.currentSlices) piece.sliceColors.RemoveAt(piece.sliceColors.Count - 1);

        int sliceCount = piece.sliceColors.Count;

        // Doluluk Seçimi (5 Temiz Buton)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Doluluk:", GUILayout.Width(65));
        for (int s = 0; s <= 4; s++)
        {
            bool isCurrent = (sliceCount == s);
            GUI.backgroundColor = isCurrent ? new Color(0.35f, 0.85f, 1.0f) : Color.white;
            string label = (s == 0) ? "0 (Boş)" : $"{s} Dilim" + (s == 4 ? " (Tam)" : "");
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                sliceCount = s;
                piece.currentSlices = s;
                while (piece.sliceColors.Count < s) piece.sliceColors.Add(piece.liquidColor);
                while (piece.sliceColors.Count > s) piece.sliceColors.RemoveAt(piece.sliceColors.Count - 1);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Katman Renkleri (Tek tıkla renk çipleri)
        if (sliceCount > 0)
        {
            EditorGUILayout.LabelField("Katman Renkleri (Hızlı Renk Seçimi):", EditorStyles.boldLabel);

            // Yukarıdan aşağıya çiz (en üst katman en üstte görünsün)
            for (int s = piece.sliceColors.Count - 1; s >= 0; s--)
            {
                EditorGUILayout.BeginHorizontal();
                string layerLabel = $"Katman {s + 1}" + (s == piece.sliceColors.Count - 1 ? " (Üst)" : (s == 0 ? " (Alt)" : ""));
                EditorGUILayout.LabelField(layerLabel, GUILayout.Width(90));

                // Mevcut renk kutusu
                Rect colPreview = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(24));
                EditorGUI.DrawRect(colPreview, piece.sliceColors[s]);
                Handles.color = Color.white;
                Handles.DrawWireCube(colPreview.center, new Vector3(colPreview.width, colPreview.height, 0f));

                // 8 Hızlı Renk Çipi Butonu (1 Tıkla Boya!)
                for (int c = 0; c < QuickColors.Length; c++)
                {
                    GUI.backgroundColor = QuickColors[c];
                    if (GUILayout.Button("", GUILayout.Width(20), GUILayout.Height(20)))
                    {
                        piece.sliceColors[s] = QuickColors[c];
                        GUI.changed = true;
                    }
                }
                GUI.backgroundColor = Color.white;

                // İsteğe bağlı özel renk seçici
                piece.sliceColors[s] = EditorGUILayout.ColorField(GUIContent.none, piece.sliceColors[s], false, false, false, GUILayout.Width(45));

                EditorGUILayout.EndHorizontal();
            }

            piece.liquidColor = piece.sliceColors[piece.sliceColors.Count - 1];

            GUILayout.Space(4);

            // Tüm Şişeyi Tek Renkle Doldur
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tüm Şişeyi Boya:", GUILayout.Width(110));
            for (int c = 0; c < QuickColors.Length; c++)
            {
                GUI.backgroundColor = QuickColors[c];
                if (GUILayout.Button("", GUILayout.Width(22), GUILayout.Height(20)))
                {
                    Color chosen = QuickColors[c];
                    for (int s = 0; s < piece.sliceColors.Count; s++) piece.sliceColors[s] = chosen;
                    piece.liquidColor = chosen;
                    GUI.changed = true;
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // Buzlu Cam Ayarı
        if (piece.isFrozen)
        {
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("❄️ Buz Erime Sayacı:", GUILayout.Width(130));
            piece.requiredMatches = EditorGUILayout.IntSlider(piece.requiredMatches, 1, 4);
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
    // 6. GELİŞMİŞ AYARLAR VE ARAÇLAR (Katlanabilir / Foldout)
    // ──────────────────────────────────────────────────────────────
    private void DrawAdvancedFoldouts()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "⚙️ Gelişmiş Düzen Ayarları (Boyut & Aralıklar)", true);
        if (showAdvancedSettings)
        {
            EditorGUI.BeginChangeCheck();
            currentLevel.bottleScale = EditorGUILayout.Slider("Şişe Boyut Çarpanı", currentLevel.bottleScale > 0.1f ? currentLevel.bottleScale : 1.0f, 0.6f, 2.0f);
            currentLevel.customSpacingX = EditorGUILayout.Slider("Yatay Aralık (Spacing X)", currentLevel.customSpacingX > 0.1f ? currentLevel.customSpacingX : 1.85f, 0.8f, 3.2f);
            currentLevel.customSpacingY = EditorGUILayout.Slider("Dikey Aralık (Spacing Y)", currentLevel.customSpacingY > 0.1f ? currentLevel.customSpacingY : 2.6f, 1.0f, 4.0f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentLevel, "Gelişmiş Ayarları Değiştir");
                EditorUtility.SetDirty(currentLevel);
            }
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showLevelGenerator = EditorGUILayout.Foldout(showLevelGenerator, "🎲 Otomatik Seviye Üretici", true);
        if (showLevelGenerator)
        {
            EditorGUILayout.LabelField("Seçtiğiniz renk sayısına göre garantili çözülebilir dengeli bölüm üretir:", EditorStyles.miniLabel);

            genColorCount = EditorGUILayout.IntSlider("Renk Sayısı", genColorCount, 2, QuickColors.Length);
            genEmptyCount = EditorGUILayout.IntSlider("Boş Şişe Sayısı", genEmptyCount, 1, 3);

            int totalGenBottles = genColorCount + genEmptyCount;
            EditorGUILayout.LabelField($"Oluşturulacak Şişe: {totalGenBottles} ({genColorCount} Dolu + {genEmptyCount} Boş)");

            if (GUILayout.Button("✨ Rastgele Dengeli Seviye Üret", GUILayout.Height(28)))
            {
                GenerateSolvableLevel();
            }

            if (GUILayout.Button("🎯 25 Şişeli Kademeli V Düzeni Üret", GUILayout.Height(24)))
            {
                Generate25BottleVLevel();
            }
        }
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────
    // 7. YARDIMCI VE PANO METODLARI
    // ──────────────────────────────────────────────────────────────
    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;
        if (e == null || currentLevel == null || currentLevel.pieces == null || currentLevel.pieces.Count == 0) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (e.shift) DeleteBottleSlot(selectedBottleIndex);
                else ClearBottleContent(selectedBottleIndex);
                e.Use();
                Repaint();
            }
            else if (e.control || e.command)
            {
                if (e.keyCode == KeyCode.C) { CopyBottle(selectedBottleIndex); e.Use(); Repaint(); }
                else if (e.keyCode == KeyCode.V) { PasteBottle(selectedBottleIndex); e.Use(); Repaint(); }
                else if (e.keyCode == KeyCode.D) { DuplicateBottle(selectedBottleIndex); e.Use(); Repaint(); }
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

    private void ShowBottleContextMenu(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;

        var piece = currentLevel.pieces[index];
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent($"🗑️ Şişeyi Boşalt [Del]"), false, () => ClearBottleContent(index));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("📋 Kopyala (Ctrl+C)"), false, () => CopyBottle(index));

        if (clipboardPiece != null) menu.AddItem(new GUIContent("📥 Yapıştır (Ctrl+V)"), false, () => PasteBottle(index));
        else menu.AddDisabledItem(new GUIContent("📥 Yapıştır (Pano Boş)"));

        menu.AddItem(new GUIContent("✨ Çoğalt (Ctrl+D)"), false, () => DuplicateBottle(index));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent(piece.isFrozen ? "❄️ Buz Kilidini Kaldır" : "❄️ Buzlu Şişe Yap"), piece.isFrozen, () => ToggleFrozen(index));
        menu.AddSeparator("");

        for (int c = 0; c < QuickColors.Length; c++)
        {
            Color col = QuickColors[c];
            string cName = QuickColorNames[c];
            menu.AddItem(new GUIContent($"🎨 Tek Renkle Doldur/{cName}"), false, () => FillBottleWithColor(index, col));
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("❌ Bu Şişeyi Sil"), false, () => DeleteBottleSlot(index));

        menu.ShowAsContext();
    }

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
        List<Color> tempSlicesList = (a.sliceColors != null) ? new List<Color>(a.sliceColors) : new List<Color>();

        a.currentSlices = b.currentSlices;
        a.liquidColor = b.liquidColor;
        a.isFrozen = b.isFrozen;
        a.requiredMatches = b.requiredMatches;
        a.sliceColors = (b.sliceColors != null) ? new List<Color>(b.sliceColors) : new List<Color>();

        b.currentSlices = tempSlices;
        b.liquidColor = tempColor;
        b.isFrozen = tempFrozen;
        b.requiredMatches = tempMatches;
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
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;
        var src = currentLevel.pieces[index];

        int targetIndex = -1;
        for (int i = 0; i < currentLevel.pieces.Count; i++)
        {
            var p = currentLevel.pieces[i];
            int sc = (p.sliceColors != null && p.sliceColors.Count > 0) ? p.sliceColors.Count : p.currentSlices;
            if (sc == 0 && i != index) { targetIndex = i; break; }
        }

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

        Undo.RecordObject(currentLevel, "Şişeyi Sil");
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
        Undo.RecordObject(currentLevel, "Yeni Şişe Ekle");

        var newPiece = new LevelData.PieceData
        {
            gridPosition = new Vector2Int(index, 0),
            liquidColor = Color.white,
            currentSlices = 0,
            sliceColors = new List<Color>(),
            rotationZ = 0f,
            canRotate = false
        };

        if (index >= currentLevel.pieces.Count) currentLevel.pieces.Add(newPiece);
        else currentLevel.pieces.Insert(index, newPiece);

        selectedBottleIndex = index;
        EditorUtility.SetDirty(currentLevel);
    }

    private void ToggleFrozen(int index)
    {
        if (currentLevel == null || currentLevel.pieces == null || index < 0 || index >= currentLevel.pieces.Count) return;
        Undo.RecordObject(currentLevel, "Buzlu Cam Durumu");
        var p = currentLevel.pieces[index];
        p.isFrozen = !p.isFrozen;
        if (p.isFrozen && p.requiredMatches <= 0) p.requiredMatches = 2;
        EditorUtility.SetDirty(currentLevel);
        ShowNotification(new GUIContent(p.isFrozen ? $"❄️ #{index + 1} Buzlandı" : $"💧 #{index + 1} Çözüldü"));
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

    private void AddColorCount(Dictionary<Color, int> dict, Color col, int amount)
    {
        Color matchedKey = col;
        bool found = false;
        foreach (var k in dict.Keys)
        {
            if (ColorsMatch(k, col)) { matchedKey = k; found = true; break; }
        }
        if (!found) dict[matchedKey] = 0;
        dict[matchedKey] += amount;
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.06f &&
               Mathf.Abs(a.g - b.g) < 0.06f &&
               Mathf.Abs(a.b - b.b) < 0.06f;
    }

    private string GetColorName(Color col)
    {
        for (int i = 0; i < QuickColors.Length; i++)
        {
            if (ColorsMatch(QuickColors[i], col)) return QuickColorNames[i];
        }
        return "Özel Renk";
    }

    // ──────────────────────────────────────────────────────────────
    // 8. OTOMATİK SEVİYE ÜRETİMİ
    // ──────────────────────────────────────────────────────────────
    private void GenerateSolvableLevel()
    {
        if (currentLevel == null) return;
        Undo.RecordObject(currentLevel, "Otomatik Seviye Üret");

        int totalBottles = genColorCount + genEmptyCount;
        currentLevel.pieces = new List<LevelData.PieceData>();
        currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.AutoFlow;

        List<Color> slicePool = new List<Color>();
        for (int c = 0; c < genColorCount; c++)
        {
            Color col = QuickColors[c % QuickColors.Length];
            for (int s = 0; s < 4; s++) slicePool.Add(col);
        }

        // Karıştır
        for (int i = 0; i < slicePool.Count; i++)
        {
            int rnd = Random.Range(i, slicePool.Count);
            Color temp = slicePool[i];
            slicePool[i] = slicePool[rnd];
            slicePool[rnd] = temp;
        }

        int poolIndex = 0;
        for (int b = 0; b < genColorCount; b++)
        {
            List<Color> bottleSlices = new List<Color>();
            for (int s = 0; s < 4; s++) bottleSlices.Add(slicePool[poolIndex++]);

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
    }

    private void Generate25BottleVLevel()
    {
        if (currentLevel == null) return;
        Undo.RecordObject(currentLevel, "25 Şişeli V Seviyesi Üret");
        currentLevel.flatLayoutMode = LevelData.FlatLayoutMode.StaggeredV;
        currentLevel.customSpacingX = 1.22f;
        currentLevel.customSpacingY = 1.95f;
        currentLevel.pieces = new List<LevelData.PieceData>();

        HashSet<int> emptySlots = new HashSet<int> { 3, 10, 16, 24 };
        List<Color> slicePool = new List<Color>();
        int colorCount = Mathf.Min(7, QuickColors.Length);
        for (int c = 0; c < colorCount; c++)
        {
            Color col = QuickColors[c];
            for (int s = 0; s < 12; s++) slicePool.Add(col);
        }

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
                for (int s = 0; s < 4; s++) bottleSlices.Add(slicePool[sliceIdx++]);

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
    }

    // ──────────────────────────────────────────────────────────────
    // 9. DOSYA YÖNETİMİ
    // ──────────────────────────────────────────────────────────────
    private void CreateNewLevel()
    {
        string folder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Levels");

        int maxNum = 0;
        string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(assetPath);
            if (filename.StartsWith("Level_"))
            {
                string numStr = filename.Substring(6);
                if (int.TryParse(numStr, out int num) && num > maxNum) maxNum = num;
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
        newLevel.timeLimit = 120f;
        newLevel.boardMode = LevelData.BoardMode.Flat2D;
        newLevel.flatLayoutMode = LevelData.FlatLayoutMode.AutoFlow;
        newLevel.pieces = new List<LevelData.PieceData>();

        for (int i = 0; i < 4; i++)
        {
            newLevel.pieces.Add(new LevelData.PieceData
            {
                gridPosition = new Vector2Int(i, 0),
                liquidColor = QuickColors[i],
                currentSlices = 4,
                sliceColors = new List<Color> { QuickColors[i], QuickColors[i], QuickColors[i], QuickColors[i] },
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
                sliceColors = new List<Color>(),
                rotationZ = 0f,
                canRotate = false
            });
        }

        AssetDatabase.CreateAsset(newLevel, targetPath);
        AssetDatabase.SaveAssets();

        currentLevel = newLevel;
        selectedBottleIndex = 0;
        AddLevelToSequence(newLevel);
    }

    private void SaveCurrentLevel()
    {
        if (currentLevel == null) return;
        EditorUtility.SetDirty(currentLevel);
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent($"💾 '{currentLevel.name}' Kaydedildi"));
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
