using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public class LevelInitializer
{
    static LevelInitializer()
    {
        EditorApplication.delayCall += InitializeStarterLevels;
    }

    [MenuItem("Magic Sort/Başlangıç Seviyelerini Yeniden Oluştur")]
    public static void InitializeStarterLevels()
    {
        string folder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        Debug.Log("[MagicSort] Tam çözülebilir, karışık katmanlı başlangıç seviyeleri güncelleniyor...");

        Color red = new Color(0.92f, 0.22f, 0.22f);
        Color blue = new Color(0.18f, 0.52f, 0.95f);
        Color green = new Color(0.22f, 0.78f, 0.35f);
        Color yellow = new Color(0.98f, 0.82f, 0.15f);
        Color purple = new Color(0.68f, 0.26f, 0.92f);
        Color orange = new Color(0.98f, 0.55f, 0.15f);

        List<LevelData> createdLevels = new List<LevelData>();

        // ── LEVEL 1 (4 Şişe: 2 Karışık + 2 Boş) ──────────────────────
        LevelData l1 = CreateOrUpdateLevel("Level_01", 120f, new List<LevelData.PieceData>
        {
            MakeMixedPiece(0, new List<Color> { red, blue, red, blue }),
            MakeMixedPiece(1, new List<Color> { blue, red, blue, red }),
            MakeMixedPiece(2, new List<Color>()),
            MakeMixedPiece(3, new List<Color>())
        });
        createdLevels.Add(l1);

        // ── LEVEL 2 (5 Şişe: 3 Karışık + 2 Boş) ──────────────────────
        LevelData l2 = CreateOrUpdateLevel("Level_02", 150f, new List<LevelData.PieceData>
        {
            MakeMixedPiece(0, new List<Color> { yellow, red, blue, red }),
            MakeMixedPiece(1, new List<Color> { blue, yellow, red, yellow }),
            MakeMixedPiece(2, new List<Color> { red, blue, yellow, blue }),
            MakeMixedPiece(3, new List<Color>()),
            MakeMixedPiece(4, new List<Color>())
        });
        createdLevels.Add(l2);

        // ── LEVEL 3 (6 Şişe: 4 Karışık + 2 Boş) ──────────────────────
        LevelData l3 = CreateOrUpdateLevel("Level_03", 180f, new List<LevelData.PieceData>
        {
            MakeMixedPiece(0, new List<Color> { green, red, blue, yellow }),
            MakeMixedPiece(1, new List<Color> { yellow, green, red, blue }),
            MakeMixedPiece(2, new List<Color> { blue, yellow, green, red }),
            MakeMixedPiece(3, new List<Color> { red, blue, yellow, green }),
            MakeMixedPiece(4, new List<Color>()),
            MakeMixedPiece(5, new List<Color>())
        });
        createdLevels.Add(l3);

        // ── LEVEL 4 (7 Şişe: 5 Karışık + 2 Boş) ──────────────────────
        LevelData l4 = CreateOrUpdateLevel("Level_04", 200f, new List<LevelData.PieceData>
        {
            MakeMixedPiece(0, new List<Color> { purple, green, red, blue }),
            MakeMixedPiece(1, new List<Color> { yellow, purple, green, red }),
            MakeMixedPiece(2, new List<Color> { blue, yellow, purple, green }),
            MakeMixedPiece(3, new List<Color> { red, blue, yellow, purple }),
            MakeMixedPiece(4, new List<Color> { green, red, blue, yellow }),
            MakeMixedPiece(5, new List<Color>()),
            MakeMixedPiece(6, new List<Color>())
        });
        createdLevels.Add(l4);

        // ── LEVEL 5 (8 Şişe: 6 Karışık [1 Donuk] + 2 Boş) ─────────────
        LevelData l5 = CreateOrUpdateLevel("Level_05", 220f, new List<LevelData.PieceData>
        {
            MakeMixedPiece(0, new List<Color> { orange, purple, green, red }),
            MakeMixedPiece(1, new List<Color> { yellow, orange, purple, green }),
            MakeMixedPiece(2, new List<Color> { blue, yellow, orange, purple }),
            MakeMixedPiece(3, new List<Color> { red, blue, yellow, orange }),
            MakeMixedPiece(4, new List<Color> { green, red, blue, yellow }),
            MakeMixedPiece(5, new List<Color> { purple, green, red, blue }, isFrozen: true, requiredMatches: 1),
            MakeMixedPiece(6, new List<Color>()),
            MakeMixedPiece(7, new List<Color>())
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
        Debug.Log("[MagicSort] 5 adet yeni çözülebilir ve karışık renkli seviye başarıyla oluşturuldu/güncellendi!");
    }

    private static LevelData.PieceData MakeMixedPiece(int index, List<Color> colors, bool isFrozen = false, int requiredMatches = 1)
    {
        List<Color> sliceList = colors != null ? new List<Color>(colors) : new List<Color>();
        Color topColor = sliceList.Count > 0 ? sliceList[sliceList.Count - 1] : Color.white;

        return new LevelData.PieceData
        {
            gridPosition = new Vector2Int(index, 0),
            liquidColor = topColor,
            currentSlices = sliceList.Count,
            sliceColors = sliceList,
            rotationZ = 0f,
            linkId = 0,
            isFrozen = isFrozen,
            requiredMatches = requiredMatches,
            canRotate = false
        };
    }

    private static LevelData CreateOrUpdateLevel(string name, float timeLimit, List<LevelData.PieceData> pieces)
    {
        string path = $"Assets/Levels/{name}.asset";
        LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (level == null)
        {
            level = ScriptableObject.CreateInstance<LevelData>();
            level.name = name;
            level.levelDisplayName = name;
            level.timeLimit = timeLimit;
            level.boardMode = LevelData.BoardMode.Flat2D;
            level.pieces = pieces;
            AssetDatabase.CreateAsset(level, path);
        }
        else
        {
            level.levelDisplayName = name;
            level.timeLimit = timeLimit;
            level.boardMode = LevelData.BoardMode.Flat2D;
            level.pieces = pieces;
            EditorUtility.SetDirty(level);
        }
        return level;
    }
}
