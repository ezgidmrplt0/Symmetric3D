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

    // Bu komut Unity'nin tepe menüsüne "Symmetric3D" sekmesi ekler
    [MenuItem("Symmetric3D/Level Tasarımcısı")]
    public static void ShowWindow()
    {
        GetWindow<LevelDesignerWindow>("Level Tasarımcısı");
    }

    void OnGUI()
    {
        GUILayout.Label("Symmetric3D Bölüm (Level) Tasarımcısı", EditorStyles.boldLabel);
        
        currentLevel = (LevelData)EditorGUILayout.ObjectField("Düzenlenen Level", currentLevel, typeof(LevelData), false);
        
        if (currentLevel != null)
        {
            gridX = currentLevel.gridX;
            gridY = currentLevel.gridY;
        }

        GUILayout.Space(10);
        gridX = EditorGUILayout.IntSlider("Grid Genişliği (X)", gridX, 1, 10);
        gridY = EditorGUILayout.IntSlider("Grid Yüksekliği (Y)", gridY, 1, 10);

        if (currentLevel != null && (currentLevel.gridX != gridX || currentLevel.gridY != gridY))
        {
            currentLevel.gridX = gridX;
            currentLevel.gridY = gridY;
            EditorUtility.SetDirty(currentLevel);
        }

        GUILayout.Space(15);
        GUILayout.Label("🖌️ Fırça (Brush) Ayarları", EditorStyles.boldLabel);
        brushColor = EditorGUILayout.ColorField("Renk", brushColor);
        brushSlices = EditorGUILayout.IntSlider("Dilim (Slices)", brushSlices, 1, 4);
        
        // Rotasyon yönü
        string[] rotOptions = new string[] { "Yukarı (0°)", "Sağa (90°)", "Aşağı (180°)", "Sola (-90°)" };
        int[] rotValues = new int[] { 0, 90, 180, -90 };
        
        int currentRotIndex = System.Array.IndexOf(rotValues, (int)brushRotationZ);
        if(currentRotIndex < 0) currentRotIndex = 0;
        
        currentRotIndex = EditorGUILayout.Popup("Baktığı Yön", currentRotIndex, rotOptions);
        brushRotationZ = rotValues[currentRotIndex];

        GUILayout.Space(20);
        GUILayout.Label("🗺️ Harita", EditorStyles.boldLabel);
        GUILayout.Label("Sol Tık = Boya / Oraya Yerleştir");
        GUILayout.Label("Sağ Tık = Sil");

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (currentLevel != null)
        {
            DrawGrid();
        }
        else
        {
            EditorGUILayout.HelpBox("Çizim yapmak için yukarıdan var olan bir Level Data seçin veya yeni bir tane oluşturun.", MessageType.Info);
            if (GUILayout.Button("Yeni Level Dosyası Oluştur", GUILayout.Height(40)))
            {
                CreateNewLevel();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawGrid()
    {
        // Y=0 aşağıda, GridY-1 yukarıda olacak şekilde renderla
        for (int y = gridY - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Ortala
            for (int x = 0; x < gridX; x++)
            {
                LevelData.PieceData piece = GetPieceAt(x, y);

                string buttonText = "Boş\n(+)";
                Color bgColor = Color.gray;

                if (piece != null)
                {
                    bgColor = piece.liquidColor;
                    string yon = "";
                    if(piece.rotationZ == 0) yon = "↑";
                    else if(piece.rotationZ == 90) yon = "→";
                    else if(piece.rotationZ == 180) yon = "↓";
                    else if(piece.rotationZ == -90) yon = "←";
                    
                    buttonText = $"{piece.currentSlices}/4\n{yon}";
                }

                GUI.backgroundColor = bgColor;

                // Buton tıklandığında sol mu sağ mı kontrolü (Pointer logic)
                Rect bRect = GUILayoutUtility.GetRect(new GUIContent(buttonText), GUI.skin.button, GUILayout.Width(65), GUILayout.Height(65));
                if (GUI.Button(bRect, buttonText))
                {
                    // Sağ Tık (Context Click veya Event.button == 1) algılanmıyorsa, standart sol tıktır
                    if (Event.current.button == 1) 
                    {
                        if (piece != null)
                        {
                            currentLevel.pieces.Remove(piece);
                            EditorUtility.SetDirty(currentLevel);
                        }
                    }
                    else
                    {
                        // Sol tık - Boya
                        if (piece == null)
                        {
                            piece = new LevelData.PieceData();
                            piece.gridPosition = new Vector2Int(x, y);
                            currentLevel.pieces.Add(piece);
                        }
                        
                        piece.liquidColor = brushColor;
                        piece.currentSlices = brushSlices;
                        piece.rotationZ = brushRotationZ;
                        
                        EditorUtility.SetDirty(currentLevel);
                    }
                    GUI.FocusControl(null); // Odaklanmayı sil ki UI yenilensin
                }
                
                // Unity GUI.Button sometimes eats right clicks. Alternatif manuel kontrol:
                Event e = Event.current;
                if (e.isMouse && e.type == EventType.MouseDown && bRect.Contains(e.mousePosition))
                {
                    if (e.button == 1 && piece != null)
                    {
                        currentLevel.pieces.Remove(piece);
                        EditorUtility.SetDirty(currentLevel);
                        e.Use(); // Olayı tüket
                    }
                }

                GUI.backgroundColor = Color.white;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    LevelData.PieceData GetPieceAt(int x, int y)
    {
        return currentLevel.pieces.Find(p => p.gridPosition.x == x && p.gridPosition.y == y);
    }

    void CreateNewLevel()
    {
        // Unity içinde nereye kaydedeceği sorulur
        string path = EditorUtility.SaveFilePanelInProject("Yeni Level Kaydet", "Level_01", "asset", "Level dosyasını nereye kaydetmek istersiniz? (Örn: Assets/Levels veya Assets klasörüne)");
        if (!string.IsNullOrEmpty(path))
        {
            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            newLevel.gridX = this.gridX;
            newLevel.gridY = this.gridY;
            
            AssetDatabase.CreateAsset(newLevel, path);
            AssetDatabase.SaveAssets();
            currentLevel = newLevel;
            EditorGUIUtility.PingObject(newLevel);
        }
    }
}
