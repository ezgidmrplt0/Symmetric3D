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
                
                // Şeffaf, Zarif ve Parlamayan Kristal Cam Ayarları (Sıvıyı asla perdelemez veya beyazlatmaz)
                glassMat.SetColor("_Color", new Color(0.85f, 0.95f, 1.0f, 0.02f));       // Şeffaf kristal cam (%2 opaklık)
                glassMat.SetColor("_RimColor", new Color(0.75f, 0.90f, 1.0f, 0.20f));   // İnce, zarif dış cam kontürü (parlama yapmaz)
                glassMat.SetFloat("_RimPower", 4.2f);                                   // Sadece en dış siluet kenarında ince çizgi
                glassMat.SetColor("_InnerRimColor", new Color(0.3f, 0.7f, 1.0f, 0.04f)); // Sıvıyı etkilemeyen arka cam derinliği
                glassMat.SetFloat("_InnerRimPower", 4.0f);
                glassMat.SetColor("_SpecColor", new Color(1.0f, 1.0f, 1.0f, 0.35f));   // Yumuşak noktasal cam parıltısı
                glassMat.SetFloat("_Shininess", 0.88f);
                glassMat.SetFloat("_StreakIntensity", 0.0f);                            // Beyaz şeritler kapatıldı (sıvıyı örtmez)
                glassMat.SetFloat("_StreakPower", 24.0f);
                
                EditorUtility.SetDirty(glassMat);
                AssetDatabase.SaveAssets();
                Debug.Log("[ApplyCrispGlass] Crystal Clear Glass settings applied!");
            }
        }
    }
}
