using UnityEngine;

/// <summary>
/// Temel renk tanımları ve toleranslı renk karşılaştırması.
/// Not: Magic Sort mekaniğinde renkler karışmaz, şişe içinde katman katman durur.
/// </summary>
public static class ColorMixData
{
    // ── Temel Renkler ────
    public static readonly Color Mavi      = new Color(0f,    0f,    1f,    1f);
    public static readonly Color Kirmizi   = new Color(1f,    0f,    0f,    1f);
    public static readonly Color Sari      = new Color(1f,    1f,    0f,    1f);
    public static readonly Color Mor       = new Color(0.5f,  0f,    0.5f,  1f);
    public static readonly Color Turuncu   = new Color(1f,    0.5f,  0f,    1f);
    public static readonly Color Yesil     = new Color(0f,    1f,    0f,    1f);

    // ── Ek Renkler ────
    public static readonly Color AcikMavi  = new Color(0f,    0.935f, 1f,   1f);
    public static readonly Color Pembe     = new Color(1f,    0f,    0.84f, 1f);
    public static readonly Color Siyah     = new Color(0.08f, 0.08f, 0.08f, 1f);
    public static readonly Color KoyuKirm  = new Color(0.8f,  0.1f,  0.1f,  1f);
    public static readonly Color KoyuYesil = new Color(0f,    0.6f,  0f,    1f);
    public static readonly Color KoyuMor   = new Color(0.5f,  0f,    0.6f,  1f);

    // Float karşılaştırması için toleranslı eşitlik
    public static bool ColorsMatch(Color a, Color b, float tolerance = 0.2f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}
