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

    private void SetupCube(GameObject prefab)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
        if (mf == null) { Debug.LogError("Mesh bulunamadı!"); PrefabUtility.UnloadPrefabContents(root); return; }
        
        Bounds b = mf.sharedMesh.bounds;
        ShapeDefinition def = root.GetComponent<ShapeDefinition>() ?? root.AddComponent<ShapeDefinition>();
        ClearOldMarkers(root);

        // 6 Side Setup
        CreateMarker(root.transform, "Marker_Front",  new Vector3(b.center.x, b.center.y, b.min.z), new Vector3(0, 0, 0),    new Vector2(b.size.x, b.size.y));
        CreateMarker(root.transform, "Marker_Back",   new Vector3(b.center.x, b.center.y, b.max.z), new Vector3(0, 180, 0),  new Vector2(b.size.x, b.size.y));
        CreateMarker(root.transform, "Marker_Right",  new Vector3(b.max.x, b.center.y, b.center.z), new Vector3(0, -90, 0),  new Vector2(b.size.z, b.size.y));
        CreateMarker(root.transform, "Marker_Left",   new Vector3(b.min.x, b.center.y, b.center.z), new Vector3(0, 90, 0),   new Vector2(b.size.z, b.size.y));
        CreateMarker(root.transform, "Marker_Top",    new Vector3(b.center.x, b.max.y, b.center.z), new Vector3(90, 0, 0),   new Vector2(b.size.x, b.size.z));
        CreateMarker(root.transform, "Marker_Bottom", new Vector3(b.center.x, b.min.y, b.center.z), new Vector3(-90, 0, 0),  new Vector2(b.size.x, b.size.z));

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
        
        Bounds b = mf.sharedMesh.bounds;
        ShapeDefinition def = root.GetComponent<ShapeDefinition>() ?? root.AddComponent<ShapeDefinition>();
        ClearOldMarkers(root);

        // Pro Yöntem: Bounds boyutlarını kullanarak yüzeyleri ölçekleme
        var mFront = CreateMarker(root.transform, "Marker_FrontTriangle", new Vector3(b.center.x, b.center.y, b.min.z), new Vector3(0, 0, 0), new Vector2(b.size.x, b.size.y));
        mFront.surfaceType = ShapeFaceMarker.FaceSurfaceType.Triangle;

        var mBack = CreateMarker(root.transform, "Marker_BackTriangle", new Vector3(b.center.x, b.center.y, b.max.z), new Vector3(0, 180, 0), new Vector2(b.size.x, b.size.y));
        mBack.surfaceType = ShapeFaceMarker.FaceSurfaceType.Triangle;

        CreateMarker(root.transform, "Marker_BottomRect", new Vector3(b.center.x, b.min.y, b.center.z), new Vector3(-90, 0, 0), new Vector2(b.size.x, b.size.z));

        // Yan eğimli yüzeyler (Yarım genişlik ve yükseklik kullanarak yüzeyin ORTASINA tam oturtuyoruz)
        float sideWidth = Mathf.Sqrt(Mathf.Pow(b.size.x * 0.5f, 2) + Mathf.Pow(b.size.y, 2));
        float angle = Mathf.Atan2(b.size.y, b.size.x * 0.5f) * Mathf.Rad2Deg;

        CreateMarker(root.transform, "Marker_LeftRect",  new Vector3(b.min.x * 0.5f, b.center.y, b.center.z), new Vector3(90 - angle, 90, 0),  new Vector2(b.size.z, sideWidth)); 
        CreateMarker(root.transform, "Marker_RightRect", new Vector3(b.max.x * 0.5f, b.center.y, b.center.z), new Vector3(90 - angle, -90, 0), new Vector2(b.size.z, sideWidth));

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

    private ShapeFaceMarker CreateMarker(Transform parent, string name, Vector3 localPos, Vector3 localRot, Vector2 scale)
    {
        GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObj.name = name;
        markerObj.transform.SetParent(parent);
        markerObj.transform.localPosition = localPos;
        markerObj.transform.localRotation = Quaternion.Euler(localRot);
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
