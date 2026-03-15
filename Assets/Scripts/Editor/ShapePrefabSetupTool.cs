using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ShapePrefabSetupTool : EditorWindow
{
    [MenuItem("Symmetric3D/Setup Shape Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<ShapePrefabSetupTool>("Shape Setup");
    }

    private GameObject cubePrefab;
    private GameObject prismPrefab;

    private void OnEnable()
    {
        if (cubePrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("Cube t:Prefab");
            if (guids.Length > 0) cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[guids.Length - 1]));
        }
        if (prismPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("Prism t:Prefab");
            if (guids.Length > 0) prismPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[guids.Length - 1]));
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Shape Setup Tool (PRO Mesh Bounds)", EditorStyles.boldLabel);
        
        cubePrefab = (GameObject)EditorGUILayout.ObjectField("Cube Prefab", cubePrefab, typeof(GameObject), false);
        prismPrefab = (GameObject)EditorGUILayout.ObjectField("Prism Prefab", prismPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        if (GUILayout.Button("Setup Cube Prefab (Smart Bounds)", GUILayout.Height(30))) SetupCube(cubePrefab);
        if (GUILayout.Button("Setup Prism Prefab (Smart Bounds)", GUILayout.Height(30))) SetupPrism(prismPrefab);
    }

    /// <summary>
    /// Mesh'in sharedMesh.bounds'unu (mesh-local) root objesinin local uzayına dönüştürür.
    /// Mesh child'ı farklı scale/offset'e sahip olsa bile marker'lar doğru konuma gelir.
    /// </summary>
    private Bounds GetRootLocalBounds(MeshFilter mf, Transform root)
    {
        Bounds meshB = mf.sharedMesh.bounds;
        // 8 köşeyi mesh-local → dünya → root-local'a çevir, AABB al
        Vector3 mn = Vector3.positiveInfinity;
        Vector3 mx = Vector3.negativeInfinity;
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(meshB.min.x, meshB.min.y, meshB.min.z),
            new Vector3(meshB.max.x, meshB.min.y, meshB.min.z),
            new Vector3(meshB.min.x, meshB.max.y, meshB.min.z),
            new Vector3(meshB.max.x, meshB.max.y, meshB.min.z),
            new Vector3(meshB.min.x, meshB.min.y, meshB.max.z),
            new Vector3(meshB.max.x, meshB.min.y, meshB.max.z),
            new Vector3(meshB.min.x, meshB.max.y, meshB.max.z),
            new Vector3(meshB.max.x, meshB.max.y, meshB.max.z),
        };
        foreach (var c in corners)
        {
            Vector3 rootLocal = root.InverseTransformPoint(mf.transform.TransformPoint(c));
            mn = Vector3.Min(mn, rootLocal);
            mx = Vector3.Max(mx, rootLocal);
        }
        Bounds b = new Bounds();
        b.SetMinMax(mn, mx);
        return b;
    }

    private void SetupCube(GameObject prefab)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
        if (mf == null) { Debug.LogError("Mesh bulunamadı!"); PrefabUtility.UnloadPrefabContents(root); return; }

        Bounds b = GetRootLocalBounds(mf, root.transform);
        ShapeDefinition def = root.GetComponent<ShapeDefinition>() ?? root.AddComponent<ShapeDefinition>();
        ClearOldMarkers(root);

        // 6 Side Setup — tüm koordinatlar root-local uzayında
        CreateMarker(root.transform, "Marker_Front",  new Vector3(b.center.x, b.center.y, b.min.z), Quaternion.Euler(0, 0, 0),    new Vector2(b.size.x, b.size.y));
        CreateMarker(root.transform, "Marker_Back",   new Vector3(b.center.x, b.center.y, b.max.z), Quaternion.Euler(0, 180, 0),  new Vector2(b.size.x, b.size.y));
        CreateMarker(root.transform, "Marker_Right",  new Vector3(b.max.x, b.center.y, b.center.z), Quaternion.Euler(0, -90, 0),  new Vector2(b.size.z, b.size.y));
        CreateMarker(root.transform, "Marker_Left",   new Vector3(b.min.x, b.center.y, b.center.z), Quaternion.Euler(0, 90, 0),   new Vector2(b.size.z, b.size.y));
        CreateMarker(root.transform, "Marker_Top",    new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.Euler(90, 0, 0),   new Vector2(b.size.x, b.size.z));
        CreateMarker(root.transform, "Marker_Bottom", new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.Euler(-90, 0, 0),  new Vector2(b.size.x, b.size.z));

        def.RefreshFaces();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Cube Prefab Hazır!");
    }

    private void SetupPrism(GameObject prefab)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
        if (mf == null) { Debug.LogError("Mesh bulunamadı!"); PrefabUtility.UnloadPrefabContents(root); return; }

        Bounds b = GetRootLocalBounds(mf, root.transform);
        ShapeDefinition def = root.GetComponent<ShapeDefinition>() ?? root.AddComponent<ShapeDefinition>();
        ClearOldMarkers(root);

        var mFront = CreateMarker(root.transform, "Marker_FrontTriangle", new Vector3(b.center.x, b.center.y, b.min.z), Quaternion.Euler(0, 0, 0), new Vector2(b.size.x, b.size.y));
        mFront.surfaceType = ShapeFaceMarker.FaceSurfaceType.Triangle;

        var mBack = CreateMarker(root.transform, "Marker_BackTriangle", new Vector3(b.center.x, b.center.y, b.max.z), Quaternion.Euler(0, 180, 0), new Vector2(b.size.x, b.size.y));
        mBack.surfaceType = ShapeFaceMarker.FaceSurfaceType.Triangle;

        CreateMarker(root.transform, "Marker_BottomRect", new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.Euler(-90, 0, 0), new Vector2(b.size.x, b.size.z));

        // Yan eğimli yüzeyler — LookRotation ile doğru normal + root-local bounds'a göre gerçek merkez
        float sideWidth = Mathf.Sqrt(Mathf.Pow(b.size.x * 0.5f, 2) + Mathf.Pow(b.size.y, 2));

        // Sol yüz: b.min.x'ten b.center.x'e yukarı çıkan eğim
        Vector3 leftSlantDir  = new Vector3(b.center.x - b.min.x, b.size.y, 0f).normalized;
        Vector3 leftOutward   = Vector3.Cross(Vector3.forward, leftSlantDir).normalized;
        Vector3 leftCenter    = new Vector3((b.min.x + b.center.x) * 0.5f, b.center.y, b.center.z);
        Quaternion leftRot    = Quaternion.LookRotation(-leftOutward, leftSlantDir);
        CreateMarker(root.transform, "Marker_LeftRect", leftCenter, leftRot, new Vector2(b.size.z, sideWidth));

        // Sağ yüz: b.max.x'ten b.center.x'e yukarı çıkan eğim
        Vector3 rightSlantDir = new Vector3(b.center.x - b.max.x, b.size.y, 0f).normalized;
        Vector3 rightOutward  = Vector3.Cross(rightSlantDir, Vector3.forward).normalized;
        Vector3 rightCenter   = new Vector3((b.max.x + b.center.x) * 0.5f, b.center.y, b.center.z);
        Quaternion rightRot   = Quaternion.LookRotation(-rightOutward, rightSlantDir);
        CreateMarker(root.transform, "Marker_RightRect", rightCenter, rightRot, new Vector2(b.size.z, sideWidth));

        def.RefreshFaces();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Prism Prefab Hazır!");
    }

    private void ClearOldMarkers(GameObject root)
    {
        List<GameObject> toLog = new List<GameObject>();
        foreach (Transform child in root.transform) if (child.name.StartsWith("Marker_")) toLog.Add(child.gameObject);
        foreach (GameObject g in toLog) DestroyImmediate(g);
    }

    private ShapeFaceMarker CreateMarker(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector2 scale)
    {
        GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObj.name = name;
        markerObj.transform.SetParent(parent);
        markerObj.transform.localPosition = localPos;
        markerObj.transform.localRotation = localRot;
        markerObj.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        // Gereksiz collider'ı temizle
        DestroyImmediate(markerObj.GetComponent<MeshCollider>());

        // Oyun içinde görünmesin
        MeshRenderer mr = markerObj.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        ShapeFaceMarker marker = markerObj.AddComponent<ShapeFaceMarker>();
        marker.faceId = name.Replace("Marker_", "");
        return marker;
    }
}
