using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ApplyCrispGlass
{
    static ApplyCrispGlass()
    {
        EditorApplication.delayCall += AssignShader;
    }

    static void AssignShader()
    {
        Shader glassShader = Shader.Find("Custom/HypercasualCrispGlass");
        if (glassShader != null)
        {
            string matPath = "Assets/Materials/Glass.mat";
            Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            
            if (glassMat != null && glassMat.shader != glassShader)
            {
                glassMat.shader = glassShader;
                
                // Parıltıyı maksimize eden Hypercasual değerleri
                glassMat.SetColor("_Color", new Color(0.8f, 0.9f, 1.0f, 0.05f));   // En alt baz rengi (hafif mavi)
                glassMat.SetColor("_RimColor", new Color(1.0f, 1.0f, 1.0f, 0.9f)); // Kenarlar çok keskin beyaz parlayacak
                glassMat.SetFloat("_RimPower", 1.8f);                              // Kenar parlaklık yayılımı (düşük = daha geniş cam etkisi)
                glassMat.SetColor("_SpecColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));// Işık vuran tam tepe noktası
                glassMat.SetFloat("_Shininess", 0.65f);                            // Parlama noktasının netliği
                
                EditorUtility.SetDirty(glassMat);
                AssetDatabase.SaveAssets();
                Debug.Log("Hypercasual Crisp Glass successfully applied!");
            }
        }
    }
}
