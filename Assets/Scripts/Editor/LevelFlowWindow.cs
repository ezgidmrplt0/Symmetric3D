using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class LevelFlowWindow : EditorWindow
{
    private LevelSequenceData sequence;
    private Vector2 scrollPos;

    // Sürükle-bırak takibi için
    private int draggingIndex = -1;
    private int hoverIndex = -1;
    private int draggingTypeIndex = -1;
    private int hoverTypeIndex = -1;

    // Her LevelType için renk kodu
    private static readonly Dictionary<LevelData.LevelType, Color> typeColors = new()
    {
        { LevelData.LevelType.Classic,     new Color(0.4f,  0.4f,  0.4f)  }, // Koyu Gri
        { LevelData.LevelType.QuarterFill, new Color(0.8f,  0.4f,  0.1f)  }, // Turuncu/Kahve
        { LevelData.LevelType.ColorMix,    new Color(0.2f,  0.5f,  0.8f)  }, // Mavi
        { LevelData.LevelType.Shadow,      new Color(0.6f,  0.3f,  0.8f)  }, // Mor
        { LevelData.LevelType.Rotation,    new Color(0.2f,  0.7f,  0.4f)  }, // Yeşil
    };

    private Color GetTypeColor(LevelData.LevelType type)
    {
        // İlk bulunan aktif flag'in rengini döndür
        foreach (var kvp in typeColors)
        {
            if (type.HasFlag(kvp.Key)) return kvp.Value;
        }
        return new Color(0.4f, 0.4f, 0.4f);
    }

    [MenuItem("Symmetric3D/Level Akış Yöneticisi")]
    public static void ShowWindow()
    {
        GetWindow<LevelFlowWindow>("Level Akışı");
    }

    void OnGUI()
    {
        // ── Başlık ──────────────────────────────────────────────
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        GUILayout.Label("🔀 Symmetric3D — Level Akış Yöneticisi", title);
        GUILayout.Space(4);

        // ── Sequence Seç ─────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        sequence = (LevelSequenceData)EditorGUILayout.ObjectField(
            "Level Sequence Asset", sequence, typeof(LevelSequenceData), false);
        EditorGUI.EndChangeCheck();

        if (sequence == null)
        {
            GUILayout.Space(6);
            EditorGUILayout.HelpBox("Bir LevelSequenceData asset'i seçin veya yeni oluşturun.", MessageType.Info);
            if (GUILayout.Button("Yeni Sequence Asset Oluştur", GUILayout.Height(36)))
                CreateNewSequence();
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // ── 1. BÖLÜM: Level Türü Yapılandırması ─────────────────
        DrawSectionHeader("⚙️  Level Türü Yapılandırması");
        GUILayout.Space(2);
        DrawTypeConfigs();

        GUILayout.Space(8);

        // ── 2. BÖLÜM: Level Sırası ───────────────────────────────
        DrawSectionHeader("📋  Level Sırası");
        GUILayout.Space(2);
        DrawLevelList();

        GUILayout.Space(8);

        // ── Level Ekle ───────────────────────────────────────────
        DrawSectionHeader("➕  Level Ekle");
        GUILayout.Space(2);
        DrawAddLevelArea();

        GUILayout.Space(10);

        // ── Kaydet ──────────────────────────────────────────────
        if (GUILayout.Button("💾  Kaydet", GUILayout.Height(32)))
        {
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelFlow] Sequence kaydedildi.");
        }

        EditorGUILayout.EndScrollView();

        // Drag-drop esnasında sürekli repaint (kasmasını önler)
        if (draggingIndex >= 0 || draggingTypeIndex >= 0)
        {
            Repaint();
        }
    }

    // ── Bölüm 1: Tür Konfigürasyonu ──────────────────────────────

    void DrawTypeConfigs()
    {
        if (sequence.typeConfigs != null)
        {
            bool synced = false;
            // 1. Yeni eklenenleri senkronize et
            foreach (LevelData.LevelType lt in System.Enum.GetValues(typeof(LevelData.LevelType)))
            {
                bool exists = sequence.typeConfigs.Exists(c => c.levelType == lt);
                if (!exists)
                {
                    Undo.RecordObject(sequence, "LevelType Sync");
                    sequence.typeConfigs.Add(new LevelSequenceData.LevelTypeConfig { levelType = lt, unlockAtProgress = 100 });
                    synced = true;
                }
            }

            // 2. Enum'da olmayan (obsolete) türleri temizle
            var allTypes = new HashSet<LevelData.LevelType>((LevelData.LevelType[])System.Enum.GetValues(typeof(LevelData.LevelType)));
            int removedCount = sequence.typeConfigs.RemoveAll(c => !allTypes.Contains(c.levelType));
            if (removedCount > 0) synced = true;

            if (synced) EditorUtility.SetDirty(sequence);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("≡", EditorStyles.boldLabel, GUILayout.Width(20));
        GUILayout.Label("Tür", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.Label("Açılma (%)", EditorStyles.boldLabel, GUILayout.Width(90));
        GUILayout.Label("Durum", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        DrawThinLine();

        if (sequence.typeConfigs == null) return;

        int lifetimeProgress = GameManager.Instance != null ? GameManager.Instance.lifetimeProgress : 0;

        bool dirty = false;
        Event evt = Event.current;
        for (int i = 0; i < sequence.typeConfigs.Count; i++)
        {
            var cfg = sequence.typeConfigs[i];
            
            GUIStyle rowStyle = new GUIStyle(GUI.skin.box);
            rowStyle.margin = new RectOffset(0, 0, 0, 0);
            rowStyle.padding = new RectOffset(2, 2, 4, 4);

            if (draggingTypeIndex == i)
                GUI.backgroundColor = new Color(0.1f, 0.5f, 0.8f, 0.8f);
            else if (hoverTypeIndex == i && draggingTypeIndex >= 0)
                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            else
                GUI.backgroundColor = Color.clear;

            Rect rowRect = EditorGUILayout.BeginHorizontal(rowStyle);
            GUI.backgroundColor = Color.white;

            // Handle
            Rect handleRect = GUILayoutUtility.GetRect(new GUIContent("≡"), EditorStyles.label, GUILayout.Width(20));
            GUI.Label(handleRect, "≡");
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition) && evt.button == 0)
            {
                draggingTypeIndex = i;
                hoverTypeIndex = i;
                evt.Use();
            }

            if (draggingTypeIndex >= 0 && evt.type == EventType.MouseDrag && rowRect.Contains(evt.mousePosition))
            {
                hoverTypeIndex = i;
            }

            Color tc = GetTypeColor(cfg.levelType);
            Color prevCell = GUI.backgroundColor;
            GUI.backgroundColor = tc;
            GUILayout.Label(cfg.levelType.ToString(), EditorStyles.miniButtonMid, GUILayout.Width(120));
            GUI.backgroundColor = prevCell;

            int newVal = EditorGUILayout.IntField(cfg.unlockAtProgress, GUILayout.Width(50));
            newVal = Mathf.Max(newVal, 0);
            GUILayout.Label("%", GUILayout.Width(16));
            if (newVal != cfg.unlockAtProgress)
            {
                Undo.RecordObject(sequence, "Unlock % Değiştir");
                cfg.unlockAtProgress = newVal;
                dirty = true;
            }

            bool unlocked = lifetimeProgress >= cfg.unlockAtProgress;
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = unlocked ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.1f) }
            };
            GUILayout.Label(cfg.unlockAtProgress == 0 ? "✅ Her zaman açık"
                : unlocked ? "✅ Açık" : "🔒 Kilitli", statusStyle);

            EditorGUILayout.EndHorizontal();

            // Araya giren çizgi
            if (draggingTypeIndex >= 0 && draggingTypeIndex != i && hoverTypeIndex == i)
            {
                float lineY = (draggingTypeIndex < i) ? rowRect.yMax : rowRect.yMin - 2;
                Rect lineRect = new Rect(rowRect.x, lineY, rowRect.width, 3);
                EditorGUI.DrawRect(lineRect, Color.cyan);
            }
        }

        // Bırakma Olayı
        if (evt.type == EventType.MouseUp)
        {
            if (draggingTypeIndex >= 0)
            {
                if (hoverTypeIndex >= 0 && hoverTypeIndex != draggingTypeIndex)
                {
                    Undo.RecordObject(sequence, "Type Config Sırası Değiştir");
                    var item = sequence.typeConfigs[draggingTypeIndex];
                    sequence.typeConfigs.RemoveAt(draggingTypeIndex);
                    sequence.typeConfigs.Insert(hoverTypeIndex, item);
                    EditorUtility.SetDirty(sequence);
                    dirty = true;
                }
                draggingTypeIndex = -1;
                hoverTypeIndex = -1;
                evt.Use();
            }
        }

        if (draggingTypeIndex >= 0 && evt.type == EventType.MouseDrag)
        {
            evt.Use();
        }

        if (dirty) EditorUtility.SetDirty(sequence);
    }

    // ── Bölüm 2: Level Sırası ────────────────────────────────────

    void DrawLevelList()
    {
        if (sequence.levels == null || sequence.levels.Count == 0)
        {
            EditorGUILayout.HelpBox("Henüz level eklenmedi. Aşağıdan ekleyin.", MessageType.None);
            return;
        }

        int lifetimeProgress = GameManager.Instance != null ? GameManager.Instance.lifetimeProgress : 0;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("≡",       EditorStyles.boldLabel, GUILayout.Width(20)); // Tutamak başlığı
        GUILayout.Label("#",       EditorStyles.boldLabel, GUILayout.Width(28));
        GUILayout.Label("Ad",      EditorStyles.boldLabel, GUILayout.Width(130));
        GUILayout.Label("Tür",     EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("Açılma",  EditorStyles.boldLabel, GUILayout.Width(70));
        GUILayout.Label("",                                GUILayout.Width(30)); 
        EditorGUILayout.EndHorizontal();
        DrawThinLine();

        int removeIndex = -1;
        Event evt = Event.current;

        for (int i = 0; i < sequence.levels.Count; i++)
        {
            LevelData level = sequence.levels[i];

            // Arka plan kutusu
            GUIStyle rowStyle = new GUIStyle(GUI.skin.box);
            rowStyle.margin = new RectOffset(0, 0, 0, 0);
            rowStyle.padding = new RectOffset(2, 2, 4, 4);

            if (draggingIndex == i)
                GUI.backgroundColor = new Color(0.1f, 0.5f, 0.8f, 0.8f); 
            else if (hoverIndex == i && draggingIndex >= 0)
                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f); 
            else
                GUI.backgroundColor = Color.clear; // Orijinal transparan görünüm
                
            Rect rowRect = EditorGUILayout.BeginHorizontal(rowStyle);
            GUI.backgroundColor = Color.white; 

            // Handle (Tutamak) ve satıra tıklandığını algılama
            Rect handleRect = GUILayoutUtility.GetRect(new GUIContent("≡"), EditorStyles.label, GUILayout.Width(20));
            GUI.Label(handleRect, "≡");
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

            // Sadece handle (≡) üzerine basılırsa sürükleme başlasın
            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition) && evt.button == 0)
            {
                draggingIndex = i;
                hoverIndex = i;
                evt.Use();
            }

            if (draggingIndex >= 0 && evt.type == EventType.MouseDrag)
            {
                if (rowRect.Contains(evt.mousePosition))
                {
                    hoverIndex = i;
                }
            }

            // Index
            GUILayout.Label((i + 1).ToString(), GUILayout.Width(28));

            if (level == null)
            {
                GUILayout.Label("⚠️ null", GUILayout.Width(130));
                GUILayout.Label("—", GUILayout.Width(100));
                GUILayout.Label("—", GUILayout.Width(70));
            }
            else
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = GetTypeColor(level.levelType);

                // Orijinaldeki buton
                if (GUILayout.Button(level.levelDisplayName, EditorStyles.miniButtonMid, GUILayout.Width(130)))
                    EditorGUIUtility.PingObject(level);

                GUI.backgroundColor = prev;

                GUILayout.Label(level.levelType.ToString(), GUILayout.Width(100));

                int unlockPct = sequence.GetUnlockProgress(level.levelType);
                bool unlocked = lifetimeProgress >= unlockPct;
                GUIStyle s = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = unlocked ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.1f) }
                };
                GUILayout.Label(unlockPct == 0 ? "✅ Açık" : unlocked ? $"✅ %{unlockPct}" : $"🔒 %{unlockPct}", s, GUILayout.Width(70));
            }

            // ✕ butonu (Kaldırma)
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIndex = i;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Araya giren cizgi
            if (draggingIndex >= 0 && draggingIndex != i && hoverIndex == i)
            {
                float lineY = (draggingIndex < i) ? rowRect.yMax : rowRect.yMin - 2;
                Rect lineRect = new Rect(rowRect.x, lineY, rowRect.width, 3);
                EditorGUI.DrawRect(lineRect, Color.cyan);
            }
        }

        // Bırakma Olayı
        if (evt.type == EventType.MouseUp)
        {
            if (draggingIndex >= 0)
            {
                if (hoverIndex >= 0 && hoverIndex != draggingIndex)
                {
                    Undo.RecordObject(sequence, "Level Sırası Değiştir");
                    LevelData item = sequence.levels[draggingIndex];
                    sequence.levels.RemoveAt(draggingIndex);
                    sequence.levels.Insert(hoverIndex, item);
                    EditorUtility.SetDirty(sequence);
                }
                draggingIndex = -1;
                hoverIndex = -1;
                evt.Use();
            }
        }

        // Event emme
        if (draggingIndex >= 0 && evt.type == EventType.MouseDrag)
        {
            evt.Use();
        }

        if (removeIndex >= 0)
        {
            Undo.RecordObject(sequence, "Level Çıkar");
            sequence.levels.RemoveAt(removeIndex);
            EditorUtility.SetDirty(sequence);
            Repaint();
        }
    }

    // ── Bölüm 3: Level Ekle ──────────────────────────────────────

    void DrawAddLevelArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "📂  LevelData asset'ini buraya sürükle veya seç");

        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is LevelData ld)
                    {
                        Undo.RecordObject(sequence, "Level Ekle");
                        sequence.levels.Add(ld);
                        EditorUtility.SetDirty(sequence);
                    }
                }
            }
            evt.Use();
        }

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Seçerek Ekle", GUILayout.Width(140), GUILayout.Height(26)))
        {
            string path = EditorUtility.OpenFilePanel("LevelData Seç", "Assets", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
                LevelData picked = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (picked != null)
                {
                    Undo.RecordObject(sequence, "Level Ekle");
                    sequence.levels.Add(picked);
                    EditorUtility.SetDirty(sequence);
                }
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ── Yardımcılar ──────────────────────────────────────────────

    void DrawSectionHeader(string label)
    {
        GUILayout.Space(2);
        Rect r = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f));
        GUILayout.Space(2);
        GUILayout.Label(label, EditorStyles.boldLabel);
    }

    void DrawThinLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
        GUILayout.Space(2);
    }

    void CreateNewSequence()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Yeni Sequence Kaydet", "LevelSequence", "asset", "Nereye kaydedilsin?");
        if (!string.IsNullOrEmpty(path))
        {
            var s = ScriptableObject.CreateInstance<LevelSequenceData>();
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            sequence = s;
            EditorGUIUtility.PingObject(s);
        }
    }
}
