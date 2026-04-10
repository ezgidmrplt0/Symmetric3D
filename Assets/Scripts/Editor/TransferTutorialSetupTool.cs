using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// TutorialManager Inspector'ına "Transfer Tutorial UI Kur" butonu ekler.
/// Eksik UI objelerini Canvas altında otomatik oluşturur ve alanlara atar.
/// </summary>
[CustomEditor(typeof(TutorialManager))]
public class TransferTutorialSetupTool : Editor
{
    // Katlanabilir bölümler için foldout durumları
    private bool _showHandSection      = true;
    private bool _showSpecialSection   = true;
    private bool _showTransferSection  = true;
    private bool _showLevelList        = false;

    public override void OnInspectorGUI()
    {
        TutorialManager tm = (TutorialManager)target;
        serializedObject.Update();

        // ── Başlık ──────────────────────────────────────────────
        EditorGUILayout.Space(4);
        GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        EditorGUILayout.LabelField("Tutorial Manager", header);
        EditorGUILayout.Space(4);

        // ── El Animasyonu ────────────────────────────────────────
        _showHandSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showHandSection, "El Animasyonu");
        if (_showHandSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("handImage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("durationPerSegment"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // ── Özel Panel ───────────────────────────────────────────
        _showSpecialSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showSpecialSection, "Özel Seviye 6 Tutorial (Eski Panel)");
        if (_showSpecialSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("specialTutorialPanel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("specialTutorialText"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // ── Transfer Yönü Tutorial ───────────────────────────────
        _showTransferSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showTransferSection, "Transfer Yönü Tutorial ★");
        if (_showTransferSection)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("transferTutorialLevelIndex"),
                new GUIContent("Level Index (0 tabanlı)", "Level 6 için → 5 gir"));

            EditorGUILayout.Space(4);

            // Her alanın durumunu renkli göster
            DrawFieldStatus("transferArrowUI",          "Ok (Arrow Image)",         tm.transferArrowUI          != null);
            DrawFieldStatus("transferTutorialOverlay",  "Overlay (CanvasGroup)",     tm.transferTutorialOverlay  != null);
            DrawFieldStatus("tapToContinueText",        "Devam Yazısı (TMP)",        tm.tapToContinueText        != null);

            EditorGUILayout.Space(6);

            bool allAssigned = tm.transferArrowUI != null
                            && tm.transferTutorialOverlay != null
                            && tm.tapToContinueText != null;

            if (!allAssigned)
            {
                // Uyarı kutusu
                Canvas canvas = FindCanvas();
                if (canvas == null)
                {
                    EditorGUILayout.HelpBox("Sahnede Canvas bulunamadı! Önce bir Canvas oluştur.", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("Eksik alanlar var. Aşağıdaki butona basarak otomatik oluştur.", MessageType.Warning);

                    GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
                    if (GUILayout.Button("⚙  Transfer Tutorial UI'ı Otomatik Kur", GUILayout.Height(34)))
                        SetupTransferTutorialUI(tm, canvas);
                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                EditorGUILayout.HelpBox("✓  Tüm alanlar atanmış. Hazır!", MessageType.None);
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Yeniden Oluştur (Üzerine Yaz)", GUILayout.Height(26)))
                {
                    Canvas canvas = FindCanvas();
                    if (canvas != null) SetupTransferTutorialUI(tm, canvas);
                }
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // ── Level Listesi ────────────────────────────────────────
        _showLevelList = EditorGUILayout.BeginFoldoutHeaderGroup(_showLevelList, "Seviye Bazlı Eğitimler");
        if (_showLevelList)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelTutorials"), true);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    // ── Yardımcılar ──────────────────────────────────────────────

    static void DrawFieldStatus(string propName, string label, bool assigned)
    {
        Color prev = GUI.color;
        GUI.color = assigned ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.4f);
        string icon = assigned ? "✓" : "✗";
        EditorGUILayout.LabelField($"{icon}  {label}", EditorStyles.miniLabel);
        GUI.color = prev;
    }

    static Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
            if (c.renderMode != RenderMode.WorldSpace) return c;
        return canvases.Length > 0 ? canvases[0] : null;
    }

    // ── UI Otomatik Kurulum ───────────────────────────────────────

    static void SetupTransferTutorialUI(TutorialManager tm, Canvas canvas)
    {
        Undo.SetCurrentGroupName("Transfer Tutorial UI Kur");
        int undoGroup = Undo.GetCurrentGroup();

        // Varsa eskiyi temizle
        Transform existing = canvas.transform.Find("TransferTutorialRoot");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // ── Kök ──────────────────────────────────────────────────
        GameObject root = new GameObject("TransferTutorialRoot");
        Undo.RegisterCreatedObjectUndo(root, "Create TransferTutorialRoot");
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        // Root aktif kalır; child'lar başlangıçta gizli (TutorialManager tek tek açacak)

        // ── Overlay (karartma) ────────────────────────────────────
        GameObject overlayGO = new GameObject("TransferOverlay");
        Undo.RegisterCreatedObjectUndo(overlayGO, "Create TransferOverlay");
        overlayGO.transform.SetParent(rootRect, false);

        RectTransform overlayRect = overlayGO.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImg = overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.55f);
        overlayImg.raycastTarget = false;

        CanvasGroup overlayCG = overlayGO.AddComponent<CanvasGroup>();
        overlayCG.interactable = false;
        overlayCG.blocksRaycasts = false;
        overlayCG.alpha = 0f;
        overlayGO.SetActive(false); // TutorialManager açacak

        // ── Ok (Arrow) ────────────────────────────────────────────
        GameObject arrowGO = new GameObject("TransferArrow");
        Undo.RegisterCreatedObjectUndo(arrowGO, "Create TransferArrow");
        arrowGO.transform.SetParent(rootRect, false);

        RectTransform arrowRect = arrowGO.AddComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(60f, 90f);
        // Pivot = alt-orta: ok döndürülünce dip noktası giver'da kalır
        arrowRect.pivot = new Vector2(0.5f, 0f);
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);

        Image arrowImg = arrowGO.AddComponent<Image>();
        // Varsayılan beyaz kare — Inspector'dan ok sprite'ı atarsın
        arrowImg.color = Color.white;
        arrowImg.raycastTarget = false;

        CanvasGroup arrowCG = arrowGO.AddComponent<CanvasGroup>();
        arrowCG.interactable = false;
        arrowCG.blocksRaycasts = false;
        arrowCG.alpha = 0f;
        arrowGO.SetActive(false); // TutorialManager açacak

        // ── "Devam için dokun" yazısı ─────────────────────────────
        GameObject textGO = new GameObject("TapToContinueText");
        Undo.RegisterCreatedObjectUndo(textGO, "Create TapToContinueText");
        textGO.transform.SetParent(rootRect, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot     = new Vector2(0.5f, 0f);
        textRect.anchoredPosition = new Vector2(0f, 60f);
        textRect.sizeDelta = new Vector2(0f, 60f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Devam için dokun";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        textGO.SetActive(false); // TutorialManager açacak

        // ── TutorialManager'a ata ─────────────────────────────────
        Undo.RecordObject(tm, "Assign Transfer Tutorial Fields");
        tm.transferArrowUI         = arrowRect;
        tm.transferTutorialOverlay = overlayCG;
        tm.tapToContinueText       = tmp;

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(tm);

        Debug.Log("[TransferTutorialSetup] UI başarıyla oluşturuldu. " +
                  "TransferArrow objesine Inspector'dan bir ok sprite'ı atamayı unutma!");
    }
}
