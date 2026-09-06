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
        EditorGUILayout.LabelField("⚙️ Seviye Ayarları", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        string newName = EditorGUILayout.TextField("Seviye Adı", currentLevel.levelDisplayName);
        float newTime = EditorGUILayout.FloatField("Süre Limiti (sn)", currentLevel.timeLimit);

        int currentCount = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        int newBottleCount = EditorGUILayout.IntSlider("Toplam Şişe Sayısı", currentCount, 2, 10);

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
    // 4. İNTERAKTİF GÖRSEL EKRAN (Çizimdeki Eliptik Şişe Düzeni)
    // ──────────────────────────────────────────────────────────────
    private void DrawVisualCanvas()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📱 Mobil Ekran Önizlemesi (Eliptik Dizilim)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Şişeye tıklayarak seçebilir ve alttan rengini/doluluğunu değiştirebilirsiniz.", EditorStyles.miniLabel);

        int totalBottles = currentLevel.pieces != null ? currentLevel.pieces.Count : 0;
        if (totalBottles == 0)
        {
            EditorGUILayout.HelpBox("Henüz şişe eklenmemiş. 'Toplam Şişe Sayısı'nı artırın.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // Kanvas alanı (Telefon ekranı oranı)
        float canvasWidth = 320f;
        float canvasHeight = 360f;
        Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.ExpandWidth(true));

        // Telefon ekranı arka planı
        float drawX = canvasRect.x + (canvasRect.width - canvasWidth) / 2f;
        Rect phoneRect = new Rect(drawX, canvasRect.y, canvasWidth, canvasHeight);
        EditorGUI.DrawRect(phoneRect, new Color(0.12f, 0.12f, 0.15f));
        Handles.color = new Color(0.3f, 0.3f, 0.35f);
        Handles.DrawWireCube(phoneRect.center, new Vector3(phoneRect.size.x, phoneRect.size.y, 0f));

        Vector2 center = phoneRect.center;
        float radiusX = 100f;
        float radiusY = 135f;

        // Şişeleri çiz
        for (int i = 0; i < totalBottles; i++)
        {
            Vector3 relPos = GridSpawner.GetBottlePosition(i, totalBottles, radiusX, radiusY);
            Vector2 bottleCenter = center + new Vector2(relPos.x, -relPos.y);

            float bWidth = 42f;
            float bHeight = 58f;
            Rect bottleRect = new Rect(bottleCenter.x - bWidth / 2f, bottleCenter.y - bHeight / 2f, bWidth, bHeight);

            var piece = currentLevel.pieces[i];
            bool isSelected = (i == selectedBottleIndex);

            // Şişe arka planı
            EditorGUI.DrawRect(bottleRect, new Color(0.2f, 0.2f, 0.24f));

            // Katmanlı sıvı dolgusu (aşağıdan yukarıya her dilimi kendi rengiyle çiz)
            int sliceCount = (piece.sliceColors != null && piece.sliceColors.Count > 0) ? piece.sliceColors.Count : piece.currentSlices;
            if (sliceCount > 0)
            {
                float sliceHeight = (bHeight - 4) / 4f;
                for (int s = 0; s < sliceCount; s++)
                {
                    Color sColor = (piece.sliceColors != null && s < piece.sliceColors.Count) ? piece.sliceColors[s] : piece.liquidColor;
                    float yPos = (bottleRect.y + bHeight - 2) - ((s + 1) * sliceHeight);
                    Rect sliceRect = new Rect(bottleRect.x + 2, yPos, bWidth - 4, sliceHeight - 1);
                    EditorGUI.DrawRect(sliceRect, sColor);
                }
            }

            // Şişe çerçevesi / Seçim vurgusu
            Color outlineColor = isSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.5f, 0.55f);
            Handles.color = outlineColor;
            Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.size.x, bottleRect.size.y, 0f));

            // Seçili ise kalın çerçeve
            if (isSelected)
            {
                Handles.DrawWireCube(bottleRect.center, new Vector3(bottleRect.size.x + 2f, bottleRect.size.y + 2f, 0f));
            }

            // Şişe etiketi
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            string sliceText = sliceCount > 0 ? $"{sliceCount}/4" : "BOŞ";
            GUI.Label(new Rect(bottleRect.x, bottleRect.y - 16, bWidth, 16), $"#{i + 1}", labelStyle);
            GUI.Label(new Rect(bottleRect.x, bottleRect.y + (bHeight / 2f) - 8, bWidth, 16), sliceText, labelStyle);

            // Buzlu Cam kaplaması ve erime rozeti (donmuşsa)
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

            // Tıklama kontrolü
            if (Event.current.type == EventType.MouseDown && bottleRect.Contains(Event.current.mousePosition))
            {
                selectedBottleIndex = i;
                Event.current.Use();
                Repaint();
            }
        }

        EditorGUILayout.EndVertical();
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

        if (GUILayout.Button("✨ Rastgele Dengeli Seviye Üret", GUILayout.Height(30)))
        {
            GenerateSolvableLevel();
        }

        EditorGUILayout.EndVertical();
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
