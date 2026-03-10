using UnityEngine;
using UnityEditor;
using System.IO;

public class LegoTextureGenerator
{
    [MenuItem("Tools/Symmetric3D/Generate Lego Texture")]
    public static void Generate()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        // Base colors (Beyaz/Açık arkaplana uygun tertemiz ve pürüzsüz tonlar)
        Color baseGreen = new Color(0.98f, 0.98f, 0.98f, 1f); // Neredeyse tam beyaz, temiz
        Color shadow = new Color(0f, 0f, 0f, 0.15f); // Daha pastel ve hafif bir koyulukta gölge

        // Fill background
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, baseGreen);
            }
        }
        
        float cx = size / 2f;
        float cy = size / 2f;
        float radius = size * 0.38f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                
                // Very subtle Drop shadow
                float shadowDx = x - (cx - size*0.02f);
                float shadowDy = y - (cy + size*0.02f); 
                float shadowDist = Mathf.Sqrt(shadowDx*shadowDx + shadowDy*shadowDy);
                
                if (shadowDist > radius && shadowDist < radius * 1.08f)
                {
                    if (dist > radius)
                    {
                        float intensity = 1f - ((shadowDist - radius) / (radius * 0.08f));
                        intensity = Mathf.SmoothStep(0, 1, intensity) * 0.3f;
                        Color blended = Color.Lerp(baseGreen, shadow, intensity);
                        tex.SetPixel(x, y, blended);
                    }
                }
                
                if (dist <= radius)
                {
                    // Şeklin içine (tepe noktasına) hiçbir gölge, çizgi veya eğim ekleme. Tamamen düz (flat) bir renk.
                    tex.SetPixel(x, y, baseGreen);
                }
            }
        }
        
        tex.Apply();
        
        string path = Application.dataPath + "/Sprites";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        
        string fullPath = path + "/LegoTexture.png";
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        
        AssetDatabase.Refresh();
        
        TextureImporter importer = AssetImporter.GetAtPath("Assets/Sprites/LegoTexture.png") as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureType = TextureImporterType.Default;
            importer.SaveAndReimport();
        }
        
        Debug.Log("Lego texture created at Assets/Sprites/LegoTexture.png!");
    }
}
