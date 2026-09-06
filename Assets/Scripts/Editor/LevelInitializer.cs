using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[InitializeOnLoad]
public class LevelInitializer
{
    static LevelInitializer()
    {
        EditorApplication.delayCall += InitializeStarterLevels;
    }

    [MenuItem("Magic Sort/Başlangıç Seviyelerini Oluştur")]
    public static void InitializeStarterLevels()
    {
        string folder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        string[] existingGuids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
        if (existingGuids.Length > 0)
        {
            // Zaten seviye varsa ezmeyelim
            return;
        }

        Debug.Log("[MagicSort] Yeni Magic Sort başlangıç seviyeleri oluşturuluyor...");

        Color red = new Color(0.92f, 0.22f, 0.22f);
        Color blue = new Color(0.18f, 0.52f, 0.95f);
        Color green = new Color(0.22f, 0.78f, 0.35f);
        Color yellow = new Color(0.95f, 0.82f, 0.15f);
        Color purple = new Color(0.68f, 0.26f, 0.92f);
        Color orange = new Color(0.95f, 0.55f, 0.15f);

        List<LevelData> createdLevels = new List<LevelData>();

        // ── LEVEL 1 (3 Şişe: 2 Kırmızı + 1 Boş) ─────────────────────
        LevelData l1 = CreateLevel("Level_01", 120f, new List<LevelData.PieceData>
        {
            MakePiece(0, red, 2),
            MakePiece(1, red, 2),
            MakePiece(2, Color.white, 0)
        });
        createdLevels.Add(l1);

        // ── LEVEL 2 (4 Şişe: Kırmızı + Mavi + 1 Boş) ─────────────────
        LevelData l2 = CreateLevel("Level_02", 150f, new List<LevelData.PieceData>
        {
            MakePiece(0, red, 2),
            MakePiece(1, blue, 3),
            MakePiece(2, red, 2),
            MakePiece(3, blue, 1),
            MakePiece(4, Color.white, 0)
        });
        createdLevels.Add(l2);

        // ── LEVEL 3 (5 Şişe: Kırmızı + Mavi + Yeşil + 2 Boş) ─────────
        LevelData l3 = CreateLevel("Level_03", 180f, new List<LevelData.PieceData>
        {
            MakePiece(0, red, 2),
            MakePiece(1, blue, 2),
            MakePiece(2, green, 4),
            MakePiece(3, red, 2),
            MakePiece(4, blue, 2),
            MakePiece(5, Color.white, 0),
            MakePiece(6, Color.white, 0)
        });
        createdLevels.Add(l3);

        // ── LEVEL 4 (6 Şişe — Kullanıcı Çizimindeki Tam Düzen) ───────
        // 4 Renk (Kırmızı, Mavi, Yeşil, Sarı) + 2 Boş Şişe
        LevelData l4 = CreateLevel("Level_04", 200f, new List<LevelData.PieceData>
        {
            MakePiece(0, red, 2),        // Üst-Sağ
            MakePiece(1, blue, 2),       // Orta-Sağ
            MakePiece(2, yellow, 4),     // Alt-Sağ
            MakePiece(3, Color.white, 0),// Alt-Sol (Boş)
            MakePiece(4, green, 4),      // Orta-Sol
            MakePiece(5, red, 2),        // Üst-Sol
            MakePiece(6, blue, 2),
            MakePiece(7, Color.white, 0) // Boş
        });
        createdLevels.Add(l4);

        // ── LEVEL 5 (6 Şişe — Zengin Karışım) ────────────────────────
        LevelData l5 = CreateLevel("Level_05", 220f, new List<LevelData.PieceData>
        {
            MakePiece(0, purple, 3),
            MakePiece(1, orange, 2),
            MakePiece(2, blue, 2),
            MakePiece(3, Color.white, 0),
            MakePiece(4, purple, 1),
            MakePiece(5, orange, 2),
            MakePiece(6, blue, 2),
            MakePiece(7, Color.white, 0)
        });
        createdLevels.Add(l5);

        // LevelSequence.asset'e bağla
        LevelSequenceData seq = AssetDatabase.LoadAssetAtPath<LevelSequenceData>("Assets/LevelSequence.asset");
        if (seq != null)
        {
            seq.levels = new List<LevelData>(createdLevels);
            EditorUtility.SetDirty(seq);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MagicSort] 5 adet yeni başlangıç seviyesi oluşturuldu ve LevelSequence'e bağlandı!");
    }

    private static LevelData.PieceData MakePiece(int index, Color color, int slices)
    {
        return new LevelData.PieceData
        {
            gridPosition = new Vector2Int(index, 0),
            liquidColor = color,
            currentSlices = slices,
            rotationZ = 0f,
            canRotate = false
        };
    }

    private static LevelData CreateLevel(string name, float timeLimit, List<LevelData.PieceData> pieces)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.name = name;
        level.levelDisplayName = name;
        level.timeLimit = timeLimit;
        level.boardMode = LevelData.BoardMode.Flat2D;
        level.pieces = pieces;

        string path = $"Assets/Levels/{name}.asset";
        AssetDatabase.CreateAsset(level, path);
        return level;
    }
}
