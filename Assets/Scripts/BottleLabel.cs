using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// İksir Renk Preset'leri (Level Designer için kolay dropdown seçimi)
/// </summary>
public enum PotionColorPreset
{
    Red,        // Kırmızı İksir
    Blue,       // Mavi İksir
    Green,      // Yeşil İksir
    Purple,     // Mor İksir
    Yellow,     // Sarı İksir
    Orange,     // Turuncu İksir
    Cyan,       // Camgöbeği / Buz İksiri
    Pink,       // Pembe İksir
    White,      // Beyaz İksir
    Black,      // Siyah İksir
    Custom      // Özel Renk (RGB Seçici)
}

/// <summary>
/// Şişe gövdesine 3D kavisli olarak (Cylindrical Curve) sarılan profesyonel etiket bileşeni.
/// Düz resim kartı yerine şişenin silindirik yüzeyine %100 yapışıp sarılır.
/// </summary>
public class BottleLabel : MonoBehaviour
{
    [System.Serializable]
    public class ColorLabelEntry
    {
        public string labelName = "İksir Etiketi";
        
        [Tooltip("İksir Rengi (Dropdown Seçmeli)")]
        public PotionColorPreset colorPreset = PotionColorPreset.Red;

        [Tooltip("Eğer 'Custom' seçilirse kullanılacak özel RGB rengi")]
        public Color customColor = Color.red;

        [Tooltip("Bu iksir rengine yapışacak 2D etiket resmi")]
        public Sprite labelSprite;

        public Color GetColor()
        {
            if (colorPreset == PotionColorPreset.Custom) return customColor;
            return BottleLabel.GetPresetColor(colorPreset);
        }
    }

    [Header("Etiket Listesi (Inspector)")]
    [Tooltip("Renk bazlı etiket görselleri")]
    public List<ColorLabelEntry> colorLabels = new List<ColorLabelEntry>();

    [Tooltip("Eşleşmeyen bir renk olduğunda kullanılacak varsayılan etiket")]
    public Sprite defaultLabelSprite;

    [Header("Kavis & Boyut Ayarları")]
    [Tooltip("Şişe gövdesinin yarıçapı (Kavis yarıçapı)")]
    public float bottleRadius = 0.235f;

    [Tooltip("Etiketin şişeyi sarma açısı (Derece)")]
    public float wrapArcAngle = 65f;

    [Tooltip("Etiketin yüksekliği")]
    public float labelHeight = 0.28f;

    [Header("Animasyon")]
    [Tooltip("Etiketin belirme süresi")]
    public float popDuration = 0.38f;

    private SpriteRenderer spriteRenderer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material labelMaterial;
    private Vector3 targetScale;
    private bool hasShown = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Eğer SpriteRenderer yoksa 3D silindirik kavisli etiket için MeshFilter ve MeshRenderer sağla
        if (spriteRenderer == null)
        {
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (meshFilter != null)
        {
            meshFilter.mesh = CreateCurvedLabelMesh(bottleRadius, wrapArcAngle, labelHeight);
        }

        if (meshRenderer != null)
        {
            Shader unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");
            if (unlitShader != null)
            {
                labelMaterial = new Material(unlitShader);
                meshRenderer.material = labelMaterial;
            }
        }

        targetScale = transform.localScale;
        if (targetScale.sqrMagnitude < 0.001f) targetScale = Vector3.one;

        // Başlangıçta etiketi gizle
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Şişe tamamlandığında çağrılır. İksir rengine uygun etiketi seçip kavisli olarak gösterir.
    /// </summary>
    public void ShowLabel(Color potionColor)
    {
        if (hasShown) return;
        hasShown = true;

        Sprite chosenSprite = GetSpriteForColor(potionColor);
        if (chosenSprite == null) chosenSprite = defaultLabelSprite;

        if (chosenSprite == null)
        {
            chosenSprite = DynamicLabelGenerator.GetOrCreateFallbackSprite(potionColor);
        }

        if (chosenSprite != null)
        {
            if (labelMaterial != null)
            {
                labelMaterial.mainTexture = chosenSprite.texture;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = chosenSprite;
            }
        }

        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;

        // Tatlı Pop-In elastik belirme animasyonu
        transform.DOScale(targetScale, popDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                transform.DOPunchScale(targetScale * 0.12f, 0.2f, 4, 0.4f);
            });

        AudioManager.PlayPlace();
    }

    private Sprite GetSpriteForColor(Color color)
    {
        if (colorLabels == null || colorLabels.Count == 0) return null;

        float bestMatchDist = float.MaxValue;
        Sprite bestSprite = null;

        foreach (var entry in colorLabels)
        {
            if (entry.labelSprite == null) continue;

            Color targetC = entry.GetColor();
            float dist = Mathf.Abs(targetC.r - color.r) +
                         Mathf.Abs(targetC.g - color.g) +
                         Mathf.Abs(targetC.b - color.b);

            if (dist < 0.45f && dist < bestMatchDist)
            {
                bestMatchDist = dist;
                bestSprite = entry.labelSprite;
            }
        }

        return bestSprite;
    }

    /// <summary>
    /// Şişe gövdesine %100 oturan 3D kavisli silindir yüzey mesh'i üretir.
    /// </summary>
    public static Mesh CreateCurvedLabelMesh(float radius, float arcAngleDeg, float height, int segments = 16)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CurvedLabelMesh";

        int vertCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        int[] triangles = new int[segments * 6];

        float halfArc = (arcAngleDeg * 0.5f) * Mathf.Deg2Rad;
        float halfH = height * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            float angle = Mathf.Lerp(-halfArc, halfArc, u);

            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);

            // Z noktası ön tarafa doğru (-Z) kavis çizer
            Vector3 normal = new Vector3(sin, 0f, -cos);
            Vector3 posBottom = new Vector3(radius * sin, -halfH, -radius * cos);
            Vector3 posTop    = new Vector3(radius * sin,  halfH, -radius * cos);

            int idxBottom = i * 2;
            int idxTop    = i * 2 + 1;

            vertices[idxBottom] = posBottom;
            vertices[idxTop]    = posTop;

            uvs[idxBottom] = new Vector2(u, 0f);
            uvs[idxTop]    = new Vector2(u, 1f);

            normals[idxBottom] = normal;
            normals[idxTop]    = normal;
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2;
            int t0 = i * 2 + 1;
            int b1 = (i + 1) * 2;
            int t1 = (i + 1) * 2 + 1;

            // Clockwise Winding Order (Front-Facing in Unity)
            triangles[triIdx++] = b0;
            triangles[triIdx++] = b1;
            triangles[triIdx++] = t0;

            triangles[triIdx++] = b1;
            triangles[triIdx++] = t1;
            triangles[triIdx++] = t0;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    public static Color GetPresetColor(PotionColorPreset preset)
    {
        switch (preset)
        {
            case PotionColorPreset.Red:    return new Color(0.92f, 0.22f, 0.22f, 1f);
            case PotionColorPreset.Blue:   return new Color(0.20f, 0.55f, 0.95f, 1f);
            case PotionColorPreset.Green:  return new Color(0.22f, 0.85f, 0.35f, 1f);
            case PotionColorPreset.Purple: return new Color(0.68f, 0.26f, 0.92f, 1f);
            case PotionColorPreset.Yellow: return new Color(0.95f, 0.82f, 0.20f, 1f);
            case PotionColorPreset.Orange: return new Color(0.95f, 0.52f, 0.20f, 1f);
            case PotionColorPreset.Cyan:   return new Color(0.20f, 0.88f, 0.95f, 1f);
            case PotionColorPreset.Pink:   return new Color(0.95f, 0.35f, 0.72f, 1f);
            case PotionColorPreset.White:  return new Color(0.92f, 0.92f, 0.95f, 1f);
            case PotionColorPreset.Black:  return new Color(0.20f, 0.20f, 0.25f, 1f);
            default:                       return Color.white;
        }
    }

    public void ResetLabel()
    {
        hasShown = false;
        transform.DOKill();
        gameObject.SetActive(false);
        transform.localScale = targetScale;
    }

    private void OnDestroy()
    {
        if (labelMaterial != null) Destroy(labelMaterial);
    }
}

/// <summary>
/// Inspector'dan görsel atanmamışsa renklere özel prosedürel iksir etiketleri üretir.
/// </summary>
public static class DynamicLabelGenerator
{
    private static Dictionary<int, Sprite> cachedSprites = new Dictionary<int, Sprite>();

    public static Sprite GetOrCreateFallbackSprite(Color color)
    {
        int key = color.GetHashCode();
        if (cachedSprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        int width = 180;
        int height = 128;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float radiusX = width * 0.45f;
        float radiusY = height * 0.42f;

        // Yuvarlatılmış köşeli kavisli iksir etiketi (Parşömen + Altın çerçeve + İksir amblemi)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / radiusX;
                float ny = (y - center.y) / radiusY;
                float distSq = nx * nx + ny * ny;

                if (distSq <= 1.0f)
                {
                    // Dış Altın Çerçeve
                    if (distSq > 0.82f)
                    {
                        pixels[y * width + x] = new Color(0.95f, 0.82f, 0.35f, 1f);
                    }
                    // Krem / Parşömen Arkaplan
                    else if (distSq > 0.45f)
                    {
                        pixels[y * width + x] = new Color(0.96f, 0.94f, 0.88f, 0.98f);
                    }
                    // İç İksir Renk Rozeti
                    else
                    {
                        float innerNorm = Mathf.Sqrt(distSq) / 0.45f;
                        pixels[y * width + x] = Color.Lerp(color * 1.1f, color * 0.8f, innerNorm);
                    }
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedSprites[key] = sprite;
        return sprite;
    }
}
