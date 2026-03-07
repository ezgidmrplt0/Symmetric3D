using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renk karıştırma tarifleri. A + B = C şeklinde tanımlanır.
/// Yeni tarif eklemek için sadece Recipes listesine satır ekle.
/// </summary>
public static class ColorMixData
{
    public struct Recipe
    {
        public Color colorA;
        public Color colorB;
        public Color result;

        public Recipe(Color a, Color b, Color res)
        {
            colorA = a;
            colorB = b;
            result = res;
        }
    }

    // ── Temel Renkler (Tarif renkleriyle tam eşleşmesi için bunları kullan) ────
    public static readonly Color Mavi     = Color.blue;
    public static readonly Color Kirmizi  = Color.red;
    public static readonly Color Sari     = Color.yellow;
    public static readonly Color Mor      = new Color(0.5f, 0f,   0.5f);
    public static readonly Color Turuncu  = new Color(1f,   0.5f, 0f);
    public static readonly Color Yesil    = Color.green;

    // ── Tarifler ─────────────────────────────────────────────
    public static readonly List<Recipe> Recipes = new List<Recipe>
    {
        new Recipe(Mavi,    Kirmizi, Mor),
        new Recipe(Kirmizi, Sari,    Turuncu),
        new Recipe(Mavi,    Sari,    Yesil),
    };

    /// <summary>
    /// İki rengin karışım sonucunu döner. Tarif yoksa false döner.
    /// </summary>
    public static bool TryGetMix(Color a, Color b, out Color result)
    {
        foreach (var r in Recipes)
        {
            if (ColorsMatch(r.colorA, a) && ColorsMatch(r.colorB, b) ||
                ColorsMatch(r.colorA, b) && ColorsMatch(r.colorB, a))
            {
                result = r.result;
                return true;
            }
        }
        result = Color.white;
        return false;
    }

    // Float karşılaştırması için toleranslı eşitlik
    public static bool ColorsMatch(Color a, Color b, float tolerance = 0.15f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}
