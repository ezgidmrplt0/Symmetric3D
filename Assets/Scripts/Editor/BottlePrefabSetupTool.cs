using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Unity Editor aracı: MainObject.prefab objesini tertemiz fırınlanmış Assets/Prefabs/Bottle.fbx
/// mesh'leri ile (Glass, Liquid, Cork) mükemmel hizada, dik açıda ve ideal boyutta (Scale ~0.75) bağlar.
/// </summary>
[InitializeOnLoad]
public class BottlePrefabSetupTool
{
    private const string PREFAB_PATH = "Assets/Prefabs/MainObject.prefab";
    private const string FBX_PATH = "Assets/Prefabs/Bottle.fbx";

    static BottlePrefabSetupTool()
    {
        EditorApplication.delayCall += () =>
        {
            SetupBottlePrefab();
        };
    }

    [MenuItem("Symmetric3D/Setup Bottle Prefab (FBX Mesh)")]
    public static void ManualSetup()
    {
        SetupBottlePrefab();
        EditorUtility.DisplayDialog("Bottle Prefab Setup", "MainObject.prefab ideal boyutta (Scale 1.35) başarıyla güncellendi!", "Tamam");
    }

    public static void SetupBottlePrefab()
    {
        // 1. FBX ModelImporter Ayarları (Tekrarlı dönme/büyüme hatalarını engeller)
        ModelImporter importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
        if (importer != null)
        {
            bool reimportNeeded = false;
            // Çift dönmeyi engellemek için bakeAxisConversion kapalı (FBX Blender tarafında zaten Y-up fırınlandı)
            if (importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = false;
                reimportNeeded = true;
            }
            if (importer.useFileScale)
            {
                importer.useFileScale = false;
                reimportNeeded = true;
            }
            if (Mathf.Abs(importer.globalScale - 1.0f) > 0.001f)
            {
                importer.globalScale = 1.0f;
                reimportNeeded = true;
            }
            if (reimportNeeded)
            {
                importer.SaveAndReimport();
            }
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        if (prefabRoot == null)
        {
            Debug.LogError($"[BottlePrefabSetupTool] {PREFAB_PATH} yüklenemedi!");
            return;
        }

        try
        {
            Object[] allFBXAssets = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
            if (allFBXAssets == null || allFBXAssets.Length == 0)
            {
                Debug.LogError($"[BottlePrefabSetupTool] {FBX_PATH} varlıkları yüklenemedi!");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            Mesh glassMesh = allFBXAssets.OfType<Mesh>().FirstOrDefault(m => m.name == "Bottle_Glass" || m.name.Contains("Bottle_Glass"));
            Mesh liquidMesh = allFBXAssets.OfType<Mesh>().FirstOrDefault(m => m.name == "Bottle_Liquid" || m.name.Contains("Bottle_Liquid"));
            Mesh corkMesh = allFBXAssets.OfType<Mesh>().FirstOrDefault(m => m.name == "Bottle_Cork" || m.name.Contains("Bottle_Cork"));
            Material fbxCorkMat = allFBXAssets.OfType<Material>().FirstOrDefault(m => m.name.Contains("Cork") || m.name.Contains("M_Cork"));

            if (glassMesh == null || liquidMesh == null || corkMesh == null)
            {
                Debug.LogWarning($"[BottlePrefabSetupTool] FBX mesh'leri tam bulunamadı. Bulunanlar: Glass={(glassMesh != null)}, Liquid={(liquidMesh != null)}, Cork={(corkMesh != null)}");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            // 2. Root Mesh (Bottle_Glass) - Dik Duruş ve İdeal Boyut (0.75)
            prefabRoot.transform.localPosition = Vector3.zero;
            prefabRoot.transform.localRotation = Quaternion.identity;
            prefabRoot.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);

            MeshFilter rootMf = prefabRoot.GetComponent<MeshFilter>();
            if (rootMf == null) rootMf = prefabRoot.AddComponent<MeshFilter>();
            rootMf.sharedMesh = glassMesh;

            MeshRenderer rootMr = prefabRoot.GetComponent<MeshRenderer>();
            if (rootMr == null) rootMr = prefabRoot.AddComponent<MeshRenderer>();

            // 3. Liquid Child Mesh (Bottle_Liquid) - 1:1 Tam Çakışma
            LiquidTransfer liquidTransfer = prefabRoot.GetComponentInChildren<LiquidTransfer>(true);
            Transform liquidTransform = liquidTransfer != null ? liquidTransfer.transform : null;
            if (liquidTransform == null && prefabRoot.transform.childCount > 0)
            {
                liquidTransform = prefabRoot.transform.GetChild(0);
            }

            if (liquidTransform != null)
            {
                // FBX tarafında mesh vertex'leri fırınlandığı için localPosition=(0,0,0) tam çakışır
                liquidTransform.localPosition = Vector3.zero;
                liquidTransform.localRotation = Quaternion.identity;
                liquidTransform.localScale = Vector3.one;

                MeshFilter liquidMf = liquidTransform.GetComponent<MeshFilter>();
                if (liquidMf == null) liquidMf = liquidTransform.gameObject.AddComponent<MeshFilter>();
                liquidMf.sharedMesh = liquidMesh;
            }

            // 4. Cork Child Mesh (Bottle_Cork) & Component (BottleCork) - 1:1 Tam Çakışma
            Transform corkTransform = prefabRoot.transform.Find("Cork");
            GameObject corkGo;
            if (corkTransform == null)
            {
                corkGo = new GameObject("Cork");
                corkGo.transform.SetParent(prefabRoot.transform, false);
            }
            else
            {
                corkGo = corkTransform.gameObject;
            }

            corkGo.transform.localPosition = Vector3.zero;
            corkGo.transform.localRotation = Quaternion.identity;
            corkGo.transform.localScale = Vector3.one;

            MeshFilter corkMf = corkGo.GetComponent<MeshFilter>();
            if (corkMf == null) corkMf = corkGo.AddComponent<MeshFilter>();
            corkMf.sharedMesh = corkMesh;

            MeshRenderer corkMr = corkGo.GetComponent<MeshRenderer>();
            if (corkMr == null) corkMr = corkGo.AddComponent<MeshRenderer>();

            if (corkMr.sharedMaterial == null || corkMr.sharedMaterial == rootMr.sharedMaterial)
            {
                if (fbxCorkMat != null)
                {
                    corkMr.sharedMaterial = fbxCorkMat;
                }
                else
                {
                    corkMr.sharedMaterial = rootMr.sharedMaterial;
                }
            }

            BottleCork bottleCork = corkGo.GetComponent<BottleCork>();
            if (bottleCork == null) bottleCork = corkGo.AddComponent<BottleCork>();

            if (liquidTransfer != null)
            {
                liquidTransfer.cork = bottleCork;
            }

            // 5. Label Child Object & Component (BottleLabel)
            Transform labelTransform = prefabRoot.transform.Find("Label");
            GameObject labelGo;
            if (labelTransform == null)
            {
                labelGo = new GameObject("Label");
                labelGo.transform.SetParent(prefabRoot.transform, false);
            }
            else
            {
                labelGo = labelTransform.gameObject;
            }

            // Şişe karın kısmının merkezine yerleştir (3D kavisli mesh kendi yarıçapıyla sarılır)
            labelGo.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.transform.localScale = Vector3.one;

            // Eski çakışan SpriteRenderer varsa kaldır (MeshFilter/MeshRenderer ile 3D silindirik kavisli etiket kullanılır)
            SpriteRenderer labelSr = labelGo.GetComponent<SpriteRenderer>();
            if (labelSr != null)
            {
                Object.DestroyImmediate(labelSr, true);
            }

            MeshFilter labelMf = labelGo.GetComponent<MeshFilter>();
            if (labelMf == null) labelMf = labelGo.AddComponent<MeshFilter>();

            MeshRenderer labelMr = labelGo.GetComponent<MeshRenderer>();
            if (labelMr == null) labelMr = labelGo.AddComponent<MeshRenderer>();

            BottleLabel bottleLabel = labelGo.GetComponent<BottleLabel>();
            if (bottleLabel == null) bottleLabel = labelGo.AddComponent<BottleLabel>();

            if (bottleLabel.colorLabels == null || bottleLabel.colorLabels.Count == 0)
            {
                bottleLabel.colorLabels = new List<BottleLabel.ColorLabelEntry>
                {
                    new BottleLabel.ColorLabelEntry { labelName = "Red Potion", colorPreset = PotionColorPreset.Red },
                    new BottleLabel.ColorLabelEntry { labelName = "Blue Potion", colorPreset = PotionColorPreset.Blue },
                    new BottleLabel.ColorLabelEntry { labelName = "Green Potion", colorPreset = PotionColorPreset.Green },
                    new BottleLabel.ColorLabelEntry { labelName = "Purple Potion", colorPreset = PotionColorPreset.Purple },
                    new BottleLabel.ColorLabelEntry { labelName = "Yellow Potion", colorPreset = PotionColorPreset.Yellow },
                    new BottleLabel.ColorLabelEntry { labelName = "Orange Potion", colorPreset = PotionColorPreset.Orange },
                    new BottleLabel.ColorLabelEntry { labelName = "Cyan Potion", colorPreset = PotionColorPreset.Cyan },
                    new BottleLabel.ColorLabelEntry { labelName = "Pink Potion", colorPreset = PotionColorPreset.Pink }
                };
            }

            if (liquidTransfer != null)
            {
                liquidTransfer.label = bottleLabel;
            }

            // 6. Collider Ayarı: Şişenin tabanından tıpasına kadar tüm gövdeyi kapsayan CapsuleCollider
            SphereCollider[] oldSpheres = prefabRoot.GetComponentsInChildren<SphereCollider>(true);
            foreach (var s in oldSpheres) Object.DestroyImmediate(s, true);

            CapsuleCollider capsuleCol = prefabRoot.GetComponent<CapsuleCollider>();
            if (capsuleCol == null) capsuleCol = prefabRoot.AddComponent<CapsuleCollider>();

            capsuleCol.center = new Vector3(0f, 0.55f, 0f);
            capsuleCol.height = 1.15f;
            capsuleCol.radius = 0.32f;
            capsuleCol.direction = 1; // Y-axis
            capsuleCol.isTrigger = false;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
            Debug.Log($"[BottlePrefabSetupTool] MainObject.prefab tüm gövdeyi kapsayan CapsuleCollider (Center Y=0.55, Height=1.15) ile güncellendi!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
