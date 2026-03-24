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
    private bool brushIsShadowTrigger = false;
    private int brushLinkId = 0;
    private int brushSpawnShadowAfterLinkID = 0;
    private bool brushCanRotate = false;


    private bool isGridEditMode = false;
    private bool isShadowPairMode = false;
    private bool isPickingFirst = true;
    private bool isPickingAB = true;
    private Vector2Int firstPickPos;
    private int firstPickFace;
    private Vector2Int secondPickPos;
    private int secondPickFace;

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
                case KeyCode.UpArrow:    newRot = 180; break;
                case KeyCode.RightArrow: newRot = -90; break;
                case KeyCode.DownArrow:  newRot = 0;   break;
                case KeyCode.LeftArrow:  newRot = 90;  break;
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

        // ── Level Seçimi ─────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        currentLevel = (LevelData)EditorGUILayout.ObjectField("Düzenlenen Level", currentLevel, typeof(LevelData), false);
        if (EditorGUI.EndChangeCheck() && currentLevel != null)
        {
            if (currentLevel.boardMode == LevelData.BoardMode.Flat2D)
            {
                gridX = currentLevel.gridX;
                gridY = currentLevel.gridY;
            }
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

        // Türe göre açıklama göster
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Classic))
            EditorGUILayout.HelpBox("Classic — Kaydır ve eşleştir.  |  Açılma: Her zaman açık", MessageType.None);
        
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.QuarterFill))
            EditorGUILayout.HelpBox("QuarterFill — Çeyrek dolu obje mekaniği.  |  Açılma: %100", MessageType.None);
        
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.ColorMix))
            EditorGUILayout.HelpBox("ColorMix — Farklı renklerin birleşimiyle yeni renkler oluşturun.", MessageType.None);
        
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Shadow))
            EditorGUILayout.HelpBox("Shadow — Tek kalan parçalar (isShadowTrigger) diğer eşleşmeler bittiğinde eşlerini doğurur.", MessageType.None);
        
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

        // ── 3. BÖLÜM: Grid Şekillendirme ────────────────────────────
        DrawSectionHeader("🧱 Grid Şekillendirme");
        
        Color oldGuiColor = GUI.backgroundColor;
        GUI.backgroundColor = isGridEditMode ? Color.green : Color.white;
        if (GUILayout.Button(isGridEditMode ? "✅ Grid Düzenleme Modu: AÇIK" : "⬛ Grid Düzenleme Modu: KAPALI", GUILayout.Height(30)))
        {
            isGridEditMode = !isGridEditMode;
        }
        GUI.backgroundColor = oldGuiColor;
        
        if (isGridEditMode)
        {
            EditorGUILayout.HelpBox("Grid Düzenleme Modu: Grid üzerindeki hücrelere tıklayarak onları aktif/pasif yapabilirsiniz.", MessageType.Info);
        }

        GUILayout.Space(6);
        DrawSectionHeader("🔗 Shadow Transfer Triggers");

        GUI.backgroundColor = isShadowPairMode ? Color.magenta : Color.white;
        if (GUILayout.Button(isShadowPairMode ? "✅ Shadow Pair Seçme Modu: AÇIK" : "⬛ Shadow Pair Seçme Modu: KAPALI", GUILayout.Height(30)))
        {
            isShadowPairMode = !isShadowPairMode;
            isPickingFirst = true;
            if (isShadowPairMode) isGridEditMode = false;
        }
        GUI.backgroundColor = oldGuiColor;

        if (isShadowPairMode)
        {
            if (isPickingFirst)
                EditorGUILayout.HelpBox("1/3. TETİKLEYİCİ - Birinci Objeyi Seçin (Haritadan tıklayın)", MessageType.Info);
            else if (isPickingAB)
                EditorGUILayout.HelpBox($"1. Obje Seçildi: ({firstPickPos.x},{firstPickPos.y}). 2/3. TETİKLEYİCİ - İkinci Objeyi Seçin.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"İki Tetikleyici Seçildi. 3/3. GÖLGE - Doğacak Shadow parçasına (S) tıklayın.", MessageType.Error);

            if (GUILayout.Button("Seçimi Sıfırla")) { isPickingFirst = true; isPickingAB = true; }
        }

        if (currentLevel.shadowTransferPairs.Count > 0)
        {
            for (int i = 0; i < currentLevel.shadowTransferPairs.Count; i++)
            {
                var pair = currentLevel.shadowTransferPairs[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                string label = $"Pair {i+1}: ({pair.posA.x},{pair.posA.y})[F{pair.faceA}] ↔ ({pair.posB.x},{pair.posB.y})[F{pair.faceB}]";
                EditorGUILayout.LabelField(label);

                EditorGUI.BeginChangeCheck();
                int newSpawnId = EditorGUILayout.IntSlider("Spawn LinkID", pair.shadowToSpawnLinkId, 0, 99);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(currentLevel, "Change Spawn LinkID");
                    pair.shadowToSpawnLinkId = newSpawnId;
                    EditorUtility.SetDirty(currentLevel);
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    Undo.RecordObject(currentLevel, "Remove Pair");
                    currentLevel.shadowTransferPairs.RemoveAt(i);
                    EditorUtility.SetDirty(currentLevel);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Henüz transfer trigger tanımlanmamış.", MessageType.None);
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

        string[] rotOptions = { "Yukarı (180°)", "Sağa (-90°)", "Aşağı (0°)", "Sola (90°)" };
        int[] rotValues = { 180, -90, 0, 90 };
        int currentRotIndex = System.Array.IndexOf(rotValues, (int)brushRotationZ);
        if (currentRotIndex < 0) currentRotIndex = 0;
        currentRotIndex = EditorGUILayout.Popup("Baktığı Yön", currentRotIndex, rotOptions);
        brushRotationZ = rotValues[currentRotIndex];
        
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Shadow))
        {
            brushIsShadowTrigger = EditorGUILayout.Toggle("Shadow Trigger?", brushIsShadowTrigger);
            if (brushIsShadowTrigger)
            {
                brushSpawnShadowAfterLinkID = EditorGUILayout.IntSlider("Hangi Linkten Sonra? (0=Sonra)", brushSpawnShadowAfterLinkID, 0, 99);
                if (brushSpawnShadowAfterLinkID > 0)
                    EditorGUILayout.HelpBox($"Bu gölge, Link {brushSpawnShadowAfterLinkID} grubu temizlendiğinde doğacak.", MessageType.None);
                else
                    EditorGUILayout.HelpBox("Bu gölge, en sonda (hiç parça kalmadığında) doğacak.", MessageType.None);
            }
        }
        else
        {
            brushIsShadowTrigger = false;
        }

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

        GUILayout.Space(6);

        // ── 4. BÖLÜM: Harita / Grid ──────────────────────────────
        DrawSectionHeader("🗺️ Harita");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox("Sol Tık = Boya/Yerleştir     Sağ Tık = Sil", MessageType.None);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawGrid();

        GUILayout.Space(6);

        // ── Shadow Validasyonu ────────────────────────────────────
        if (currentLevel.levelType.HasFlag(LevelData.LevelType.Shadow))
        {
            foreach (var piece in currentLevel.pieces)
            {
                if (!piece.isShadowTrigger || piece.spawnShadowAfterLinkID <= 0) continue;
                
                bool hasLink = currentLevel.pieces.Exists(p => p.linkId == piece.spawnShadowAfterLinkID && !p.isShadowTrigger);
                bool hasTransferTrigger = currentLevel.shadowTransferPairs.Exists(pair => pair.shadowToSpawnLinkId == piece.spawnShadowAfterLinkID);

                if (hasTransferTrigger)
                {
                    EditorGUILayout.HelpBox($"✅ ({piece.gridPosition.x},{piece.gridPosition.y}) shadow trigger → Bir Transfer Çiftine bağlı durumda.", MessageType.Info);
                }
                else if (!hasLink)
                {
                    EditorGUILayout.HelpBox(
                        $"⚠️ ({piece.gridPosition.x},{piece.gridPosition.y}) shadow trigger → Link {piece.spawnShadowAfterLinkID}'i bekliyor ama bu linkId'ye sahip normal parça yok. İlk transferde hemen spawn olabilir.",
                        MessageType.Warning);
                }
            }
        }

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
                float uiXOffset = 0;

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

                string buttonText = "";
                Color bgColor = isCellActive ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.15f, 0.15f, 0.15f);

                if (isGridEditMode)
                {
                    buttonText = isCellActive ? "AÇIK" : "KAPALI";
                }
                else if (isCellActive)
                {
                    if (piece != null)
                    {
                        bgColor = piece.liquidColor;
                        string yon = piece.rotationZ switch
                        {
                            -90   => "→",
                            0     => "↓",
                            90    => "←",
                            _     => "↑"
                        };
                        string sliceLabel = piece.currentSlices switch {
                            1 => "1/4",
                            2 => "2/4",
                            4 => "4/4",
                            _ => piece.currentSlices.ToString() + "/4"
                        };
                        string linkTxt = piece.linkId > 0 ? $"\n[L{piece.linkId}]" : "";
                        string shadowTxt = piece.isShadowTrigger ? "\n(S)" : "";
                        if (piece.isShadowTrigger && piece.spawnShadowAfterLinkID > 0)
                            shadowTxt += $"[A:{piece.spawnShadowAfterLinkID}]";
                        string rotateTxt = (currentLevel.levelType.HasFlag(LevelData.LevelType.Rotation) && piece.canRotate) ? "\n[R]" : "";

                        buttonText = $"{sliceLabel}\n{yon}{linkTxt}{shadowTxt}{rotateTxt}";
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

                // Highlight in shadow pair mode
                if (isShadowPairMode)
                {
                    bool isA = !isPickingFirst && pos == firstPickPos && currentFaceIndex == firstPickFace;
                    bool isB = !isPickingAB && pos == secondPickPos && currentFaceIndex == secondPickFace;
                    if (isA || isB) bgColor = Color.magenta;
                }

                GUI.backgroundColor = bgColor;
                Rect bRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), GUI.skin.button,
                    GUILayout.Width(65), GUILayout.Height(65));

                Event e = Event.current;
                
                // Sağ tık: Silme (Edit modunda değilse) - GUI.Button'dan önce yakalamalıyız
                if (!isGridEditMode && e.type == EventType.MouseDown && e.button == 1 && bRect.Contains(e.mousePosition))
                {
                    if (piece != null)
                    {
                        Undo.RecordObject(currentLevel, "Parça Sil");
                        currentLevel.pieces.Remove(piece);
                        EditorUtility.SetDirty(currentLevel);
                        e.Use();
                    }
                }

                if (GUI.Button(bRect, buttonText))
                {
                    if (isGridEditMode)
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
                        }
                        else
                        {
                            targetCustomGrid.Add(pos);
                        }
                        EditorUtility.SetDirty(currentLevel);
                    }
                    else if (isShadowPairMode)
                    {
                        if (piece == null)
                        {
                            Debug.LogWarning("Boş bir hücreyi seçim için kullanamazsınız.");
                        }
                        else
                        {
                            if (isPickingFirst)
                            {
                                firstPickPos = piece.gridPosition;
                                firstPickFace = piece.faceIndex;
                                isPickingFirst = false;
                            }
                            else if (isPickingAB)
                            {
                                // İkinci obje (B) seçildi
                                if (piece.gridPosition == firstPickPos && piece.faceIndex == firstPickFace)
                                {
                                    Debug.LogWarning("Aynı objeyi iki kez seçemezsiniz.");
                                }
                                else
                                {
                                    secondPickPos = piece.gridPosition;
                                    secondPickFace = piece.faceIndex;
                                    isPickingAB = false;
                                }
                            }
                            else
                            {
                                // Üçüncü obje (Shadow) seçildi
                                if (!piece.isShadowTrigger)
                                {
                                    Debug.LogWarning("Seçtiğiniz parça bir Shadow Trigger değil! Lütfen (S) işaretli bir parça seçin.");
                                }
                                else
                                {
                                    Undo.RecordObject(currentLevel, "Add Shadow Pair With Shadow");
                                    currentLevel.shadowTransferPairs.Add(new LevelData.ShadowTransferPair
                                    {
                                        posA = firstPickPos,
                                        faceA = firstPickFace,
                                        posB = secondPickPos,
                                        faceB = secondPickFace,
                                        shadowToSpawnLinkId = piece.spawnShadowAfterLinkID // Seçilen gölgenin beklediği link ID'yi al
                                    });
                                    EditorUtility.SetDirty(currentLevel);
                                    isPickingFirst = true;
                                    isPickingAB = true;
                                    Debug.Log($"Shadow Transfer Pair eklendi! Tetikleyenler: ({firstPickPos.x},{firstPickPos.y}) & ({secondPickPos.x},{secondPickPos.y}) | Gölge Link: {piece.spawnShadowAfterLinkID}");
                                }
                            }
                        }
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
                            piece.isShadowTrigger = brushIsShadowTrigger;
                            piece.linkId = brushLinkId;
                            piece.spawnShadowAfterLinkID = brushSpawnShadowAfterLinkID;
                            piece.canRotate = brushCanRotate;
                            if (currentLevel.boardMode == LevelData.BoardMode.Shape3D) piece.faceIndex = currentFaceIndex;

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
            "Yeni Level Kaydet", "Level_02", "asset",
            "Level dosyasını nereye kaydetmek istersiniz?");

        if (!string.IsNullOrEmpty(path))
        {
            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.gridX = gridX;
            newLevel.gridY = gridY;

            AssetDatabase.CreateAsset(newLevel, path);
            newLevel.levelDisplayName = newLevel.name;
            AssetDatabase.SaveAssets();
            currentLevel = newLevel;
            EditorGUIUtility.PingObject(newLevel);
        }
    }


    private int[] GetAvailableSlices(LevelData.LevelType type)
    {
        if (type.HasFlag(LevelData.LevelType.QuarterFill))
            return new int[] { 1, 2, 4 };
        
        return new int[] { 2, 4 }; // Classic ve ColorMix modlarında sadece Yarım ve Tam
    }
}
