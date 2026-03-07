using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class LevelFlowWindow : EditorWindow
{
    private LevelSequenceData sequence;
    private Vector2 scrollPos;

    // Her LevelType için renk kodu
    private static readonly Dictionary<LevelData.LevelType, Color> typeColors = new()
    {
        { LevelData.LevelType.Classic,     new Color(0.55f, 0.55f, 0.55f) },
        { LevelData.LevelType.QuarterFill, new Color(0.9f,  0.55f, 0.1f)  },
    };

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
    }

    // ── Bölüm 1: Tür Konfigürasyonu ──────────────────────────────

    void DrawTypeConfigs()
    {
        // Tablo başlığı
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Tür", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.Label("Açılma (%)", EditorStyles.boldLabel, GUILayout.Width(90));
        GUILayout.Label("Durum", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        DrawThinLine();

        if (sequence.typeConfigs == null) return;

        int progress = GameManager.Instance != null ? GameManager.Instance.totalProgress : 0;

        bool dirty = false;
        foreach (var cfg in sequence.typeConfigs)
        {
            EditorGUILayout.BeginHorizontal();

            // Tür renk etiketi
            Color prev = GUI.backgroundColor;
            if (typeColors.TryGetValue(cfg.levelType, out Color c)) GUI.backgroundColor = c;
            GUILayout.Label(cfg.levelType.ToString(), EditorStyles.miniButtonMid, GUILayout.Width(120));
            GUI.backgroundColor = prev;

            // Açılma % slider
            int newVal = EditorGUILayout.IntField(cfg.unlockAtProgress, GUILayout.Width(50));
            newVal = Mathf.Clamp(newVal, 0, 100);
            GUILayout.Label("%", GUILayout.Width(16));
            if (newVal != cfg.unlockAtProgress)
            {
                Undo.RecordObject(sequence, "Unlock % Değiştir");
                cfg.unlockAtProgress = newVal;
                dirty = true;
            }

            // Durum
            bool unlocked = progress >= cfg.unlockAtProgress;
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = unlocked ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.1f) }
            };
            GUILayout.Label(cfg.unlockAtProgress == 0 ? "✅ Her zaman açık"
                : unlocked ? "✅ Açık" : "🔒 Kilitli", statusStyle);

            EditorGUILayout.EndHorizontal();
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

        int progress = GameManager.Instance != null ? GameManager.Instance.totalProgress : 0;

        // Tablo başlığı
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("#",       EditorStyles.boldLabel, GUILayout.Width(28));
        GUILayout.Label("Ad",      EditorStyles.boldLabel, GUILayout.Width(130));
        GUILayout.Label("Tür",     EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("Açılma",  EditorStyles.boldLabel, GUILayout.Width(70));
        GUILayout.Label("",                                GUILayout.Width(90));  // Butonlar
        EditorGUILayout.EndHorizontal();
        DrawThinLine();

        int removeIndex = -1;
        int swapA = -1, swapB = -1;

        for (int i = 0; i < sequence.levels.Count; i++)
        {
            LevelData level = sequence.levels[i];
            EditorGUILayout.BeginHorizontal();

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
                // Tür rengi
                Color prev = GUI.backgroundColor;
                if (typeColors.TryGetValue(level.levelType, out Color tc)) GUI.backgroundColor = tc;

                // İsim (tıklanınca asset'e ping)
                if (GUILayout.Button(level.levelDisplayName, EditorStyles.miniButtonMid, GUILayout.Width(130)))
                    EditorGUIUtility.PingObject(level);

                GUI.backgroundColor = prev;

                GUILayout.Label(level.levelType.ToString(), GUILayout.Width(100));

                int unlockPct = sequence.GetUnlockProgress(level.levelType);
                bool unlocked = progress >= unlockPct;
                GUIStyle s = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = unlocked ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.1f) }
                };
                GUILayout.Label(unlockPct == 0 ? "✅ Açık" : unlocked ? $"✅ %{unlockPct}" : $"🔒 %{unlockPct}", s, GUILayout.Width(70));
            }

            // ▲ ▼ ✕ butonları
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(24))) { swapA = i; swapB = i - 1; }
            GUI.enabled = i < sequence.levels.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(24))) { swapA = i; swapB = i + 1; }
            GUI.enabled = true;

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIndex = i;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // Swap / Remove işlemleri
        if (swapA >= 0 && swapB >= 0)
        {
            Undo.RecordObject(sequence, "Level Sırası Değiştir");
            (sequence.levels[swapA], sequence.levels[swapB]) = (sequence.levels[swapB], sequence.levels[swapA]);
            EditorUtility.SetDirty(sequence);
        }
        if (removeIndex >= 0)
        {
            Undo.RecordObject(sequence, "Level Çıkar");
            sequence.levels.RemoveAt(removeIndex);
            EditorUtility.SetDirty(sequence);
        }
    }

    // ── Bölüm 3: Level Ekle ──────────────────────────────────────

    void DrawAddLevelArea()
    {
        // Drag-drop alanı
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

        // Manuel seçim butonu
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
