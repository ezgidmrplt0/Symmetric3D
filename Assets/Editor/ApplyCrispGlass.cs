using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ApplyCrispGlass
{
    static ApplyCrispGlass()
    {
        EditorApplication.delayCall += AssignShader;
    }

    [MenuItem("Symmetric3D/Apply Premium Crystal Glass Settings")]
    public static void AssignShader()
    {
        Shader glassShader = Shader.Find("Custom/HypercasualCrispGlass");
        if (glassShader != null)
        {
            string matPath = "Assets/Materials/Glass.mat";
            Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            
            if (glassMat != null)
            {
                glassMat.shader = glassShader;
                
                // Mükemmel Dengelenmiş Cam Değerleri (Görünür Hacimli & Zarif Cam)
                glassMat.SetColor("_Color", new Color(0.75f, 0.88f, 1.0f, 0.18f));   // Görünür buz mavisi-şeffaf gövde (%18 opaklık)
                glassMat.SetColor("_RimColor", new Color(0.85f, 0.95f, 1.0f, 0.65f)); // Yumuşak, doğal 3D cam kontürü
                glassMat.SetFloat("_RimPower", 2.3f);                                // Doğal cam kavis yayılımı
                glassMat.SetColor("_SpecColor", new Color(1.0f, 1.0f, 1.0f, 0.85f)); // Canlı cam parıltı noktası
                glassMat.SetFloat("_Shininess", 0.70f);                             // Net cam ışıltısı
                
                EditorUtility.SetDirty(glassMat);
                AssetDatabase.SaveAssets();
                Debug.Log("[ApplyCrispGlass] Balanced Crystal Glass settings applied!");
            }
        }
    }
}
