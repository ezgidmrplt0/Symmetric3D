using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class LevelDesignerWindow : EditorWindow
{
    private LevelData currentLevel;
    private int gridX = 3;
    private int gridY = 3;

    // Brush settings
    private Color brushColor = new Color(0.8f, 0.1f, 0.1f);
    private int brushSlices = 1;
    private float brushRotationZ = 0f;
    private int brushLinkId = 0;
    private bool brushCanRotate = false;

    private bool isGridEditMode = false;
    private bool isFrozenEditMode = false;
    private int frozenRequiredMatches = 3;
    private bool brushIsFrozen = false;
    private int brushFrozenCount = 3;
    private int currentFaceIndex = 0;
    private Vector2 scrollPos;

    [MenuItem("Symmetric3D/Level Tasarımcısı")]
    public static void ShowWindow()
    {
        GetWindow<LevelDesignerWindow>("Level Tasarımcısı");
    }

    void OnGUI()
    {
        // --- KLAVYE KISAYOLLARI (YÖN TUŞLARI) ---
        Event eCurrent = Event.current;
        if (eCurrent.type == EventType.KeyDown)
        {
            float newRot = brushRotationZ;
            bool changed = true;
            switch (eCurrent.keyCode)
            {
                case KeyCode.UpArrow:    newRot = 180;  break;
                case KeyCode.RightArrow: newRot = 90;  break;
                case KeyCode.DownArrow:  newRot = 0;   break;
                case KeyCode.LeftArrow:  newRot = -90; break;
                default: changed = false; break;
            }
            if (changed)
            {
                brushRotationZ = newRot;
                Repaint(); // UI'ı hemen güncelle
            }
        }

        // ── Başlık ──────────────────────────────────────────────
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        GUILayout.Label("🎮 Symmetric3D — Level Tasarımcısı", titleStyle);
        GUILayout.Space(4);

        // ── 🚀 Hızlı Level Yönetim & Oluşturma Paneli ────────────────
        DrawQuickLevelPanel();
        GUILayout.Space(6);

        if (currentLevel == null)
        {
            EditorGUILayout.HelpBox("Çizim yapmak için yukarıdaki '⚡ Hızlı Level Oluştur' butonuna basın veya bir Level Data seçin.", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // ── 1. BÖLÜM: Level Bilgileri ────────────────────────────
        DrawSectionHeader("📋 Level Bilgileri");

        EditorGUI.BeginChangeCheck();

        // Otomatik isim sekronizasyonu (Dosya adı = Seviye Adı)
        if (currentLevel.levelDisplayName != currentLevel.name)
        {
            Undo.RecordObject(currentLevel, "Seviye Adı Güncelle");
            currentLevel.levelDisplayName = currentLevel.name;
            EditorUtility.SetDirty(currentLevel);
        }

        EditorGUILayout.LabelField("Seviye Adı", currentLevel.name);
        LevelData.LevelType newType = (LevelData.LevelType)EditorGUILayout.EnumFlagsField("Level Türü", currentLevel.levelType);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Level Bilgisi Değiştir");
            currentLevel.levelType = newType;
            EditorUtility.SetDirty(currentLevel);
        }

        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Classic))
            EditorGUILayout.HelpBox("Classic — Kaydır ve eşleştir.  |  Açılma: Her zaman açık", MessageType.None);

        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Rotation))
            EditorGUILayout.HelpBox("Rotation — Parçalar tıklandığında 90 derece döner. Sürükleme de aktiftir.", MessageType.None);

        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Linked))
            EditorGUILayout.HelpBox("Linked — Aynı 'Bağlantı Grubu'na sahip objeler birbirine yapışır ve çoklu blok mantığıyla (2'li, 3'lü vb.) grup halinde hareket ederler.", MessageType.None);

        GUILayout.Space(6);

        // ── 2. BÖLÜM: Grid Boyutu ve Shape ────────────────────────────────
        DrawSectionHeader("📐 Grid Boyutu ve Shape Modu");

        EditorGUI.BeginChangeCheck();
        LevelData.BoardMode newMode = (LevelData.BoardMode)EditorGUILayout.EnumPopup("Board Mode", currentLevel.boardMode);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentLevel, "Board Mode Değiştir");
            currentLevel.boardMode = newMode;
            EditorUtility.SetDirty(currentLevel);
        }

        GUILayout.Space(4);

        if (currentLevel.boardMode == LevelData.BoardMode.Shape3D)
        {
            EditorGUI.BeginChangeCheck();
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField("Shape Prefab", currentLevel.shapePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentLevel, "Shape Prefab Değiştir");
                currentLevel.shapePrefab = newPrefab;
                currentLevel.SyncShapeFacesFromPrefab();
                EditorUtility.SetDirty(currentLevel);
            }

            if (currentLevel.shapePrefab != null)
            {
                if (GUILayout.Button("🔄 Prefab'dan Yüzeyleri Senkronize Et"))
                {
                    Undo.RecordObject(currentLevel, "Sync Faces");
                    currentLevel.SyncShapeFacesFromPrefab();
                }

                if (currentLevel.shapeFaces.Count > 0)
                {
                    string[] faceNames = new string[currentLevel.shapeFaces.Count];
                    for (int i = 0; i < faceNames.Length; i++) faceNames[i] = currentLevel.shapeFaces[i].faceId;

                    currentFaceIndex = GUILayout.Toolbar(currentFaceIndex, faceNames, GUILayout.Height(30));
                    GUILayout.Space(4);

                    if (currentFaceIndex >= currentLevel.shapeFaces.Count) currentFaceIndex = 0;

                    LevelData.FaceLayoutData activeFace = currentLevel.shapeFaces[currentFaceIndex];
                    
                    EditorGUI.BeginChangeCheck();
                    bool faceActive = EditorGUILayout.Toggle("Yüzey Aktif mi?", activeFace.isActive);
                    ShapeFaceMarker.FaceSurfaceType surfaceType = (ShapeFaceMarker.FaceSurfaceType)EditorGUILayout.EnumPopup("Yüzey Tipi", activeFace.surfaceType);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(currentLevel, "Yüzey Ayarlarını Değiştir");
                        activeFace.isActive = faceActive;
                        activeFace.surfaceType = surfaceType;
                        EditorUtility.SetDirty(currentLevel);
                    }

                    if (activeFace.isActive)
                    {
                        EditorGUI.BeginChangeCheck();
                        gridX = EditorGUILayout.IntSlider("Genişlik (X)", activeFace.gridX, 1, 10);
                        gridY = EditorGUILayout.IntSlider("Yükseklik (Y)", activeFace.gridY, 1, 10);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(currentLevel, "Yüzey Grid Boyutu Değiştir");
                            activeFace.gridX = gridX;
                            activeFace.gridY = gridY;
                            EditorUtility.SetDirty(currentLevel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Bu yüzey pasif durumda. Parça eklenemez.", MessageType.Warning);
                        EditorGUILayout.EndScrollView();
                        return;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Prefab üzerinde yüzey bulunamadı. ShapeDefinition eklediğinizden emin olun.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Lütfen bir Shape Prefab atayın.", MessageType.Info);
            }
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            gridX = EditorGUILayout.IntSlider("Genişlik (X)", currentLevel.gridX, 1, 10);
            gridY = EditorGUILayout.IntSlider("Yükseklik (Y)", currentLevel.gridY, 1, 10);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentLevel, "Grid Boyutu Değiştir");
                currentLevel.gridX = gridX;
                currentLevel.gridY = gridY;
                EditorUtility.SetDirty(currentLevel);
            }
        }

        GUILayout.Space(6);

        // ── 3. BÖLÜM: Grid & Donukluk Şekillendirme ────────────────────────────
        DrawSectionHeader("🧱 Grid & ❄️ Donukluk Şekillendirme");
        
        EditorGUILayout.BeginHorizontal();
        Color oldGuiColor = GUI.backgroundColor;
        GUI.backgroundColor = isGridEditMode ? Color.green : Color.white;
        if (GUILayout.Button(isGridEditMode ? "✅ Grid Düzenleme: AÇIK" : "⬛ Grid Düzenleme: KAPALI", GUILayout.Height(30)))
        {
            isGridEditMode = !isGridEditMode;
            if (isGridEditMode) isFrozenEditMode = false;
        }

        GUI.backgroundColor = isFrozenEditMode ? new Color(0.35f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button(isFrozenEditMode ? "❄️ Donuk Grid Modu: AÇIK" : "❄️ Donuk Grid Modu: KAPALI", GUILayout.Height(30)))
        {
            isFrozenEditMode = !isFrozenEditMode;
            if (isFrozenEditMode) isGridEditMode = false;
        }
        GUI.backgroundColor = oldGuiColor;
        EditorGUILayout.EndHorizontal();
        
        if (isGridEditMode)
        {
            EditorGUILayout.HelpBox("Grid Düzenleme Modu: Grid üzerindeki hücrelere tıklayarak onları aktif/pasif yapabilirsiniz.", MessageType.Info);
        }
        else if (isFrozenEditMode)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("❄️ Donukluk Çözülme Sayacı (Eşleştirme Sayısı)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Hızlı Seç:");
            int[] countPresets = { 1, 2, 3, 4, 5, 6, 8, 10 };
            foreach (int cnt in countPresets)
            {
                Color pCol = GUI.backgroundColor;
                if (frozenRequiredMatches == cnt) GUI.backgroundColor = new Color(0.35f, 0.9f, 1f);
                if (GUILayout.Button(cnt.ToString(), GUILayout.Width(28), GUILayout.Height(22)))
                {
                    frozenRequiredMatches = cnt;
                }
                GUI.backgroundColor = pCol;
            }
            EditorGUILayout.EndHorizontal();

            frozenRequiredMatches = EditorGUILayout.IntSlider("Gereken Eşleştirme (Açılma Sayacı)", frozenRequiredMatches, 1, 20);
            EditorGUILayout.HelpBox($"❄️ Seçili Sayaç: {frozenRequiredMatches} Eşleştirme\n• Harita hücresine Sol Tık = Donuk yap ({frozenRequiredMatches} eşleştirme)\n• Harita hücresine Sağ Tık = Donukluğu kaldır.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(6);

        // ── 4. BÖLÜM: Fırça Ayarları ─────────────────────────────
        DrawSectionHeader("🖌️ Fırça (Brush) Ayarları");

        // Renk Presetleri
        GUILayout.Label("Hızlı Renk Seç:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        DrawColorPreset("🔵 Mavi",    ColorMixData.Mavi);
        DrawColorPreset("🔴 Kırmızı", ColorMixData.Kirmizi);
        DrawColorPreset("🟡 Sarı",    ColorMixData.Sari);
        DrawColorPreset("🟣 Mor",     ColorMixData.Mor);
        DrawColorPreset("🟠 Turuncu", ColorMixData.Turuncu);
        DrawColorPreset("🟢 Yeşil",   ColorMixData.Yesil);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        DrawColorPreset("🩵 Açık Mavi",   ColorMixData.AcikMavi);
        DrawColorPreset("🩷 Pembe",        ColorMixData.Pembe);
        DrawColorPreset("⚫ Siyah",        ColorMixData.Siyah);
        DrawColorPreset("🔻 K.Kırmızı",   ColorMixData.KoyuKirm);
        DrawColorPreset("🌿 K.Yeşil",     ColorMixData.KoyuYesil);
        DrawColorPreset("🔮 K.Mor",       ColorMixData.KoyuMor);

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        brushColor = EditorGUILayout.ColorField("Renk (manuel)", brushColor);

        // --- Dinamik Slice Seçimi (Butonlar ile) ---
        GUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Dilim (Slices)");
        
        int[] availableSlices = GetAvailableSlices(currentLevel.levelType);
        
        // Fırça dilimi şu anki modda yoksa, ilk geçerli olana çek
        if (System.Array.IndexOf(availableSlices, brushSlices) == -1)
            brushSlices = availableSlices[0];

        string[] sliceLabels = new string[availableSlices.Length];
        for (int i = 0; i < availableSlices.Length; i++)
        {
            sliceLabels[i] = availableSlices[i] switch
            {
                1 => "Çeyrek (1/4)",
                2 => "Yarım (2/4)",
                4 => "Tam (4/4)",
                _ => availableSlices[i] + "/4"
            };
        }

        int currentSliceIndex = System.Array.IndexOf(availableSlices, brushSlices);
        if (currentSliceIndex == -1) currentSliceIndex = 0;

        int newSliceIndex = GUILayout.Toolbar(currentSliceIndex, sliceLabels, GUILayout.Height(25));
        if (newSliceIndex != currentSliceIndex)
            brushSlices = availableSlices[newSliceIndex];

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);

        string[] rotOptions = { "Yukarı (180°)", "Sağa (90°)", "Aşağı (0°)", "Sola (-90°)" };
        int[] rotValues = { 180, 90, 0, -90 };
        int currentRotIndex = System.Array.IndexOf(rotValues, (int)brushRotationZ);
        if (currentRotIndex < 0) currentRotIndex = 0;
        currentRotIndex = EditorGUILayout.Popup("Baktığı Yön", currentRotIndex, rotOptions);
        brushRotationZ = rotValues[currentRotIndex];
        
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Linked))
        {
            GUILayout.Space(2);
            bool useLink = brushLinkId > 0;
            EditorGUI.BeginChangeCheck();
            useLink = EditorGUILayout.Toggle("Grup Yap (Link)", useLink);
            if (EditorGUI.EndChangeCheck())
            {
                brushLinkId = useLink ? 1 : 0;
            }

            if (useLink)
            {
                brushLinkId = EditorGUILayout.IntSlider("Link ID", brushLinkId, 1, 9);
                EditorGUILayout.HelpBox($"Link {brushLinkId} seçili. Aynı ID'ye sahip parçalar grup olarak hareket ederler.", MessageType.Info);
            }
            else
            {
                brushLinkId = 0;
                EditorGUILayout.HelpBox("Bağımsız parça (Grup yok). Link özelliği kapalı olduğu için bu parça tekil hareket eder.", MessageType.None);
            }
        }
        else
        {
            brushLinkId = 0; // Linked modunda değilse sıfırla
        }

        // Rotation: döndürülebilir mi?
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Rotation))
        {
            GUILayout.Space(4);
            brushCanRotate = EditorGUILayout.Toggle("Döndürülebilir?", brushCanRotate);
        }
        else
        {
            brushCanRotate = false;
        }

        // ❄️ Donuk Olarak Yerleştir Seçeneği
        GUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        brushIsFrozen = EditorGUILayout.Toggle("❄️ Bu Parçayı Donuk Olarak Yerleştir", brushIsFrozen);
        if (brushIsFrozen)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("   └ Hızlı Sayaç:");
            int[] bPresets = { 1, 2, 3, 4, 5, 6 };
            foreach (int bcnt in bPresets)
            {
                Color bCol = GUI.backgroundColor;
                if (brushFrozenCount == bcnt) GUI.backgroundColor = new Color(0.35f, 0.9f, 1f);
                if (GUILayout.Button(bcnt.ToString(), GUILayout.Width(28), GUILayout.Height(20)))
                {
                    brushFrozenCount = bcnt;
                }
                GUI.backgroundColor = bCol;
            }
            EditorGUILayout.EndHorizontal();

            brushFrozenCount = EditorGUILayout.IntSlider("   └ Gereken Eşleştirme Sayacı", brushFrozenCount, 1, 20);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(6);

        // ── 5. BÖLÜM: Harita / Grid ──────────────────────────────
        DrawSectionHeader("🗺️ Harita");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox("Sol Tık = Boya/Yerleştir     Sağ Tık = Sil", MessageType.None);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawGrid();
        DrawFrozenCellsList();

        GUILayout.Space(10);

        // ── Alt butonlar ─────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🗑️ Tüm Parçaları Temizle", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog("Emin misin?", "Tüm parçalar silinecek.", "Sil", "İptal"))
            {
                Undo.RecordObject(currentLevel, "Tümünü Temizle");
                currentLevel.pieces.Clear();
                EditorUtility.SetDirty(currentLevel);
            }
        }
        if (GUILayout.Button("💾 Kaydet", GUILayout.Height(32)))
        {
            SaveCurrentLevel();
        }

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.9f); // Mavi aksanlı Seri Kaydet & Yeniye Geç
        if (GUILayout.Button("⚡ Kaydet & Hızlı Yeni Level'a Geç", GUILayout.Height(32)))
        {
            SaveCurrentLevel();
            CreateQuickNewLevel(duplicateCurrent: false);
        }
        GUI.backgroundColor = prevColor;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // ─── HIZLI LEVEL OLUŞTURMA & YÖNETİMİ ─────────────────────────

    private void DrawQuickLevelPanel()
    {
        DrawSectionHeader("✨ Level Yönetimi & Hızlı Oluşturma");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Satır 1: Önceki | Level Objesi Seçici | Sonraki
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("◀ Önceki", GUILayout.Width(75), GUILayout.Height(24)))
        {
            NavigateLevel(-1);
        }

        EditorGUI.BeginChangeCheck();
        currentLevel = (LevelData)EditorGUILayout.ObjectField(currentLevel, typeof(LevelData), false, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck() && currentLevel != null)
        {
            if (currentLevel.boardMode == LevelData.BoardMode.Flat2D)
            {
                gridX = currentLevel.gridX;
                gridY = currentLevel.gridY;
            }
        }

        if (GUILayout.Button("Sonraki ▶", GUILayout.Width(75), GUILayout.Height(24)))
        {
            NavigateLevel(1);
        }

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
        if (GUILayout.Button("▶ Buradan Başlat", GUILayout.Width(130), GUILayout.Height(24)))
        {
            if (currentLevel != null)
            {
                string[] sequenceGuids = AssetDatabase.FindAssets("t:LevelSequenceData");
                int foundIndex = -1;
                if (sequenceGuids.Length > 0)
                {
                    string seqPath = AssetDatabase.GUIDToAssetPath(sequenceGuids[0]);
                    LevelSequenceData sequence = AssetDatabase.LoadAssetAtPath<LevelSequenceData>(seqPath);
                    if (sequence != null && sequence.levels != null)
                    {
                        foundIndex = sequence.levels.IndexOf(currentLevel);
                    }
                }

                if (foundIndex >= 0)
                {
                    PlayerPrefs.SetInt("CurrentLevelIndex", foundIndex);
                    PlayerPrefs.Save();
                }

                if (!Application.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                }
                else
                {
                    GameManager.Instance?.ResetLevelState();
                    FindObjectOfType<GridSpawner>()?.SpawnCurrentLevel();
                }
            }
        }
        GUI.backgroundColor = prevColor;

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Satır 2: ⚡ Hızlı Level Oluştur (Level_XX) | 📋 Kopyala (Çoğalt) | 📁 Manuel Oluştur
        EditorGUILayout.BeginHorizontal();

        string nextLevelName = GetNextLevelDefaultName();

        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f); // Yeşil dikkat çekici hızlı buton
        if (GUILayout.Button($"⚡ Hızlı Level Oluştur ({nextLevelName})", GUILayout.Height(32)))
        {
            CreateQuickNewLevel(duplicateCurrent: false);
        }
        GUI.backgroundColor = oldColor;

        if (GUILayout.Button("📋 Level'ı Çoğalt (Duplicate)", GUILayout.Height(32)))
        {
            if (currentLevel != null)
            {
                CreateQuickNewLevel(duplicateCurrent: true);
            }
            else
            {
                ShowNotification(new GUIContent("Kopyalanacak bir level seçili değil!"));
            }
        }

        if (GUILayout.Button("📁 Manuel Oluştur...", GUILayout.Height(32)))
        {
            CreateNewLevel();
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        // Satır 3: Otomatik Sequence Ekleme Seçeneği
        bool autoAdd = EditorPrefs.GetBool("LevelDesigner_AutoAddSequence", true);
        EditorGUI.BeginChangeCheck();
        autoAdd = EditorGUILayout.ToggleLeft("Yeni level oluşturulduğunda otomatik LevelSequence listesine ekle", autoAdd);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool("LevelDesigner_AutoAddSequence", autoAdd);
        }

        EditorGUILayout.EndVertical();
    }

    private void CreateQuickNewLevel(bool duplicateCurrent)
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

        // Güvenlik: Aynı isimde dosya varsa benzersiz path bulana kadar arttır
        int safetyIndex = nextNum;
        while (File.Exists(targetPath))
        {
            safetyIndex++;
            defaultName = $"Level_{safetyIndex:D2}";
            targetPath = $"{folder}/{defaultName}.asset";
        }

        LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
        newLevel.name = defaultName;
        newLevel.levelDisplayName = defaultName;

        if (duplicateCurrent && currentLevel != null)
        {
            newLevel.levelType = currentLevel.levelType;
            newLevel.timeLimit = currentLevel.timeLimit;
            newLevel.boardMode = currentLevel.boardMode;
            newLevel.gridX = currentLevel.gridX;
            newLevel.gridY = currentLevel.gridY;
            newLevel.shapePrefab = currentLevel.shapePrefab;

            newLevel.customGridPositions = new List<Vector2Int>(currentLevel.customGridPositions);

            newLevel.shapeFaces = new List<LevelData.FaceLayoutData>();
            foreach (var f in currentLevel.shapeFaces)
            {
                newLevel.shapeFaces.Add(new LevelData.FaceLayoutData
                {
                    faceId = f.faceId,
                    surfaceType = f.surfaceType,
                    isActive = f.isActive,
                    gridX = f.gridX,
                    gridY = f.gridY,
                    customGridPositions = new List<Vector2Int>(f.customGridPositions)
                });
            }

            newLevel.pieces = new List<LevelData.PieceData>();
            foreach (var p in currentLevel.pieces)
            {
                newLevel.pieces.Add(new LevelData.PieceData
                {
                    gridPosition = p.gridPosition,
                    faceIndex = p.faceIndex,
                    liquidColor = p.liquidColor,
                    currentSlices = p.currentSlices,
                    rotationZ = p.rotationZ,
                    linkId = p.linkId,
                    canRotate = p.canRotate
                });
            }

            newLevel.frozenCells = new List<LevelData.FrozenCellData>();
            foreach (var fc in currentLevel.frozenCells)
            {
                newLevel.frozenCells.Add(new LevelData.FrozenCellData
                {
                    gridPosition = fc.gridPosition,
                    faceIndex = fc.faceIndex,
                    requiredMatches = fc.requiredMatches
                });
            }
        }
        else if (currentLevel != null)
        {
            // Mevcut level varsa mod ve grid ayarlarını şablon olarak taşı (parçalar temiz kalsın)
            newLevel.levelType = currentLevel.levelType;
            newLevel.timeLimit = currentLevel.timeLimit;
            newLevel.boardMode = currentLevel.boardMode;
            newLevel.gridX = currentLevel.gridX;
            newLevel.gridY = currentLevel.gridY;
            newLevel.shapePrefab = currentLevel.shapePrefab;
            if (currentLevel.boardMode == LevelData.BoardMode.Shape3D)
            {
                newLevel.SyncShapeFacesFromPrefab();
            }
        }
        else
        {
            newLevel.gridX = gridX;
            newLevel.gridY = gridY;
        }

        AssetDatabase.CreateAsset(newLevel, targetPath);

        // Sequence'e otomatik ekleme
        bool autoAdd = EditorPrefs.GetBool("LevelDesigner_AutoAddSequence", true);
        if (autoAdd)
        {
            AddLevelToSequence(newLevel);
        }

        AssetDatabase.SaveAssets();

        currentLevel = newLevel;
        if (currentLevel.boardMode == LevelData.BoardMode.Flat2D)
        {
            gridX = currentLevel.gridX;
            gridY = currentLevel.gridY;
        }

        EditorGUIUtility.PingObject(newLevel);
        ShowNotification(new GUIContent($"✨ Yeni Level Hazır: {defaultName}"));
    }

    private void AddLevelToSequence(LevelData level)
    {
        string[] sequenceGuids = AssetDatabase.FindAssets("t:LevelSequenceData");
        if (sequenceGuids.Length > 0)
        {
            string seqPath = AssetDatabase.GUIDToAssetPath(sequenceGuids[0]);
            LevelSequenceData sequence = AssetDatabase.LoadAssetAtPath<LevelSequenceData>(seqPath);
            if (sequence != null && sequence.levels != null)
            {
                if (!sequence.levels.Contains(level))
                {
                    Undo.RecordObject(sequence, "Hızlı Level Sequence'e Eklendi");
                    sequence.levels.Add(level);
                    EditorUtility.SetDirty(sequence);
                    Debug.Log($"[LevelDesigner] '{level.name}' otomatik olarak '{sequence.name}' sequence'ine eklendi.");
                }
            }
        }
    }

    private string GetNextLevelDefaultName()
    {
        string folder = "Assets/Levels";
        int maxNum = 0;
        if (AssetDatabase.IsValidFolder(folder))
        {
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
        }
        return $"Level_{(maxNum + 1):D2}";
    }

    private void NavigateLevel(int direction)
    {
        List<LevelData> levelList = new List<LevelData>();
        string[] sequenceGuids = AssetDatabase.FindAssets("t:LevelSequenceData");
        if (sequenceGuids.Length > 0)
        {
            string seqPath = AssetDatabase.GUIDToAssetPath(sequenceGuids[0]);
            LevelSequenceData sequence = AssetDatabase.LoadAssetAtPath<LevelSequenceData>(seqPath);
            if (sequence != null && sequence.levels != null && sequence.levels.Count > 0)
            {
                foreach (var l in sequence.levels)
                {
                    if (l != null && !levelList.Contains(l)) levelList.Add(l);
                }
            }
        }

        if (levelList.Count == 0)
        {
            string folder = "Assets/Levels";
            string[] guids = AssetDatabase.FindAssets("t:LevelData", AssetDatabase.IsValidFolder(folder) ? new[] { folder } : null);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelData ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (ld != null && !levelList.Contains(ld)) levelList.Add(ld);
            }
        }

        if (levelList.Count == 0)
        {
            ShowNotification(new GUIContent("Hiç level bulunamadı."));
            return;
        }

        int currentIndex = currentLevel != null ? levelList.IndexOf(currentLevel) : -1;
        int targetIndex = currentIndex + direction;

        if (targetIndex < 0) targetIndex = 0;
        if (targetIndex >= levelList.Count) targetIndex = levelList.Count - 1;

        if (targetIndex != currentIndex && targetIndex >= 0 && targetIndex < levelList.Count)
        {
            currentLevel = levelList[targetIndex];
            if (currentLevel.boardMode == LevelData.BoardMode.Flat2D)
            {
                gridX = currentLevel.gridX;
                gridY = currentLevel.gridY;
            }
            EditorGUIUtility.PingObject(currentLevel);
            ShowNotification(new GUIContent($"Loaded: {currentLevel.name} ({targetIndex + 1}/{levelList.Count})"));
        }
    }

    private void SaveCurrentLevel()
    {
        if (currentLevel != null)
        {
            EditorUtility.SetDirty(currentLevel);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelDesigner] '{currentLevel.levelDisplayName}' kaydedildi.");
            ShowNotification(new GUIContent($"Kaydedildi: {currentLevel.name}"));
        }
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
        var targetCustomGrid = currentLevel.boardMode == LevelData.BoardMode.Shape3D 
            ? (currentLevel.shapeFaces.Count > currentFaceIndex ? currentLevel.shapeFaces[currentFaceIndex].customGridPositions : new System.Collections.Generic.List<Vector2Int>()) 
            : currentLevel.customGridPositions;

        for (int y = gridY - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < gridX; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                
                // --- PRO TRIANGLE UI LAYOUT (Centered Pyramid) ---
                bool isVisualCellDisabled = false;

                if (currentLevel.boardMode == LevelData.BoardMode.Shape3D && currentLevel.shapeFaces.Count > currentFaceIndex)
                {
                    var face = currentLevel.shapeFaces[currentFaceIndex];
                    if (face.surfaceType == ShapeFaceMarker.FaceSurfaceType.Triangle)
                    {
                        int cellsInThisRow = gridX - y; // Y=2 -> 1, Y=1 -> 2, Y=0 -> 3
                        if (x >= cellsInThisRow) isVisualCellDisabled = true;
                    }
                }

                if (isVisualCellDisabled)
                {
                    continue; // Gizli hücreleri tamamen atla
                }

                bool isCellActive = targetCustomGrid.Count == 0 || targetCustomGrid.Contains(pos);
                LevelData.PieceData piece = GetPieceAt(x, y);

                int targetFaceIdx = currentLevel.boardMode == LevelData.BoardMode.Shape3D ? currentFaceIndex : 0;
                LevelData.FrozenCellData frozenData = currentLevel.GetFrozenCell(pos, targetFaceIdx);
                bool isCellFrozen = frozenData != null;

                string buttonText = "";
                Color bgColor = isCellActive ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.15f, 0.15f, 0.15f);

                if (isFrozenEditMode)
                {
                    if (isCellFrozen)
                    {
                        bgColor = new Color(0.25f, 0.75f, 1f);
                        buttonText = $"❄️ {frozenData.requiredMatches}\nDONUK";
                    }
                    else if (isCellActive)
                    {
                        bgColor = new Color(0.28f, 0.28f, 0.28f);
                        buttonText = "Normal\n(+)";
                    }
                    else
                    {
                        buttonText = "—";
                    }
                }
                else if (isGridEditMode)
                {
                    buttonText = isCellActive ? "AÇIK" : "KAPALI";
                }
                else if (isCellActive)
                {
                    string freezeBadge = isCellFrozen ? $" [❄️{frozenData.requiredMatches}]" : "";
                    if (piece != null)
                    {
                        bgColor = piece.liquidColor;
                        string yon = piece.rotationZ switch
                        {
                            180   => "↑",
                            90    => "→",
                            0     => "↓",
                            -90   => "←",
                            _     => "↓"
                        };
                        string sliceLabel = piece.currentSlices switch {
                            1 => "1/4",
                            2 => "2/4",
                            4 => "4/4",
                            _ => piece.currentSlices.ToString() + "/4"
                        };
                        string linkTxt = piece.linkId > 0 ? $"[L{piece.linkId}]" : "";
                        string rotateTxt = (currentLevel.levelType.HasFlag(LevelData.LevelType.Rotation) && piece.canRotate) ? "[R]" : "";

                        buttonText = $"{sliceLabel}{freezeBadge}\n{yon} {linkTxt} {rotateTxt}";
                    }
                    else if (isCellFrozen)
                    {
                        bgColor = new Color(0.2f, 0.65f, 0.95f);
                        buttonText = $"Boş\n❄️ {frozenData.requiredMatches}";
                    }
                    else
                    {
                        buttonText = "Boş\n(+)";
                    }
                }
                else
                {
                    buttonText = "—";
                }

                GUI.backgroundColor = bgColor;
                Rect bRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), GUI.skin.button,
                    GUILayout.Width(65), GUILayout.Height(65));

                Event e = Event.current;
                
                // Sağ tık: Silme - GUI.Button'dan önce yakalamalıyız
                if (e.type == EventType.MouseDown && e.button == 1 && bRect.Contains(e.mousePosition))
                {
                    if (isFrozenEditMode)
                    {
                        if (isCellFrozen)
                        {
                            Undo.RecordObject(currentLevel, "Donukluk Sil");
                            currentLevel.RemoveFrozenCell(pos, targetFaceIdx);
                            EditorUtility.SetDirty(currentLevel);
                            e.Use();
                        }
                    }
                    else if (!isGridEditMode)
                    {
                        if (piece != null)
                        {
                            Undo.RecordObject(currentLevel, "Parça Sil");
                            currentLevel.pieces.Remove(piece);
                            EditorUtility.SetDirty(currentLevel);
                            e.Use();
                        }
                        else if (isCellFrozen)
                        {
                            Undo.RecordObject(currentLevel, "Donukluk Sil");
                            currentLevel.RemoveFrozenCell(pos, targetFaceIdx);
                            EditorUtility.SetDirty(currentLevel);
                            e.Use();
                        }
                    }
                }

                if (GUI.Button(bRect, buttonText))
                {
                    if (isFrozenEditMode && isCellActive)
                    {
                        Undo.RecordObject(currentLevel, "Donukluk Ayarla");
                        currentLevel.SetFrozenCell(pos, targetFaceIdx, frozenRequiredMatches);
                        EditorUtility.SetDirty(currentLevel);
                    }
                    else if (isGridEditMode)
                    {
                        Undo.RecordObject(currentLevel, "Grid Hücresi Tıkla");
                        
                        if (targetCustomGrid.Count == 0)
                        {
                            for (int gx = 0; gx < gridX; gx++)
                                for (int gy = 0; gy < gridY; gy++)
                                    targetCustomGrid.Add(new Vector2Int(gx, gy));
                        }

                        if (targetCustomGrid.Contains(pos))
                        {
                            targetCustomGrid.Remove(pos);
                            if (piece != null) currentLevel.pieces.Remove(piece);
                            currentLevel.RemoveFrozenCell(pos, targetFaceIdx);
                        }
                        else
                        {
                            targetCustomGrid.Add(pos);
                        }
                        EditorUtility.SetDirty(currentLevel);
                    }
                    else if (isCellActive)
                    {
                        if (e.button == 0) // Left click
                        {
                            Undo.RecordObject(currentLevel, "Parça Ekle/Güncelle");
                            if (piece == null)
                            {
                                piece = new LevelData.PieceData { gridPosition = new Vector2Int(x, y) };
                                if (currentLevel.boardMode == LevelData.BoardMode.Shape3D) piece.faceIndex = currentFaceIndex;
                                currentLevel.pieces.Add(piece);
                            }

                            piece.liquidColor = brushColor;
                            piece.currentSlices = brushSlices;
                            piece.rotationZ = brushRotationZ;
                            piece.linkId = brushLinkId;
                            piece.canRotate = brushCanRotate;
                            if (currentLevel.boardMode == LevelData.BoardMode.Shape3D) piece.faceIndex = currentFaceIndex;

                            if (brushIsFrozen)
                            {
                                currentLevel.SetFrozenCell(pos, targetFaceIdx, brushFrozenCount);
                            }

                            EditorUtility.SetDirty(currentLevel);
                        }
                    }
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = Color.white;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawFrozenCellsList()
    {
        if (currentLevel == null || currentLevel.frozenCells == null || currentLevel.frozenCells.Count == 0) return;

        GUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"❄️ Seviyedeki Donuk Gridler ({currentLevel.frozenCells.Count} Adet)", EditorStyles.boldLabel);
        
        for (int i = 0; i < currentLevel.frozenCells.Count; i++)
        {
            var fc = currentLevel.frozenCells[i];
            EditorGUILayout.BeginHorizontal();
            string faceInfo = currentLevel.boardMode == LevelData.BoardMode.Shape3D ? $" [Yüzey {fc.faceIndex}]" : "";
            EditorGUILayout.LabelField($"📍 ({fc.gridPosition.x}, {fc.gridPosition.y}){faceInfo}", GUILayout.Width(110));
            
            EditorGUI.BeginChangeCheck();
            int newMatches = EditorGUILayout.IntSlider(fc.requiredMatches, 1, 20);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentLevel, "Donukluk Sayacı Değiştir");
                fc.requiredMatches = newMatches;
                EditorUtility.SetDirty(currentLevel);
            }
            
            if (GUILayout.Button("🗑️ Sil", GUILayout.Width(45)))
            {
                Undo.RecordObject(currentLevel, "Donukluk Sil");
                currentLevel.frozenCells.RemoveAt(i);
                EditorUtility.SetDirty(currentLevel);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    LevelData.PieceData GetPieceAt(int x, int y) 
    {
        if (currentLevel.boardMode == LevelData.BoardMode.Shape3D)
            return currentLevel.pieces.Find(p => p.faceIndex == currentFaceIndex && p.gridPosition.x == x && p.gridPosition.y == y);
        else
            return currentLevel.pieces.Find(p => p.gridPosition.x == x && p.gridPosition.y == y);
    }

    void DrawColorPreset(string label, Color color)
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = color;
        if (GUILayout.Button(label, GUILayout.Height(22)))
            brushColor = color;
        GUI.backgroundColor = prev;
    }

    void CreateNewLevel()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Yeni Level Kaydet", GetNextLevelDefaultName(), "asset",
            "Level dosyasını nereye kaydetmek istersiniz?");

        if (!string.IsNullOrEmpty(path))
        {
            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.gridX = gridX;
            newLevel.gridY = gridY;

            if (currentLevel != null)
            {
                newLevel.levelType = currentLevel.levelType;
                newLevel.timeLimit = currentLevel.timeLimit;
                newLevel.boardMode = currentLevel.boardMode;
                newLevel.shapePrefab = currentLevel.shapePrefab;
            }

            AssetDatabase.CreateAsset(newLevel, path);
            newLevel.levelDisplayName = newLevel.name;

            bool autoAdd = EditorPrefs.GetBool("LevelDesigner_AutoAddSequence", true);
            if (autoAdd)
            {
                AddLevelToSequence(newLevel);
            }

            AssetDatabase.SaveAssets();
            currentLevel = newLevel;
            EditorGUIUtility.PingObject(newLevel);
        }
    }

    private int[] GetAvailableSlices(LevelData.LevelType type)
    {
        return new int[] { 2, 4 };
    }
}

