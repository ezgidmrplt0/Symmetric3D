using UnityEngine;
using TMPro;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Projedeki tüm TextMeshProUGUI öğelerine seçilen fontu topluca uygulama aracı.
/// </summary>
[ExecuteInEditMode]
public class GlobalFontManager : MonoBehaviour
{
    public TMP_FontAsset targetFont;

    [Header("Ayarlar")]
    public bool applyToAllOnStart = true;
    public bool includeInactive = true;

    void Start()
    {
        if (applyToAllOnStart && targetFont != null && Application.isPlaying)
        {
            ApplyToAllInScene();
        }
    }

    /// <summary>
    /// Sahnede bulunan tüm aktif/pasif TMP textlerini bulur ve seçili fontu basar.
    /// </summary>
    [ContextMenu("Sahnede'ki Tüm Textlere Uygula (Force Apply)")]
    public void ApplyToAllInScene()
    {
        if (targetFont == null)
        {
            Debug.LogError("[GlobalFontManager] Lütfen önce bir 'Target Font' (SDF Asset) atayın!");
            return;
        }

        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int count = 0;

        foreach (var text in allTexts)
        {
            // Prefab editor sahnelerini veya sahnede olmayan assetleri ele
            if (!IsPrefabInstance(text.gameObject) && text.gameObject.scene.name != null)
            {
                if (!includeInactive && !text.gameObject.activeInHierarchy) continue;

                #if UNITY_EDITOR
                Undo.RecordObject(text, "Global Font Change");
                #endif

                text.font = targetFont;
                
                #if UNITY_EDITOR
                EditorUtility.SetDirty(text);
                #endif
                
                count++;
            }
        }

        Debug.Log($"<color=green>[GlobalFontManager]</color> Toplam {count} text öğesine '{targetFont.name}' fontu uygulandı.");
    }

    private bool IsPrefabInstance(GameObject go)
    {
        #if UNITY_EDITOR
        return PrefabUtility.IsPartOfPrefabAsset(go);
        #else
        return false;
        #endif
    }
}
