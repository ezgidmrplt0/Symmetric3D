using UnityEngine;
using System.Collections.Generic;

public class ShapeDefinition : MonoBehaviour
{
    [SerializeField] private List<ShapeFaceMarker> faces = new List<ShapeFaceMarker>();

    public IReadOnlyList<ShapeFaceMarker> Faces => faces;

    [ContextMenu("Refresh Faces")]
    public void RefreshFaces()
    {
        faces.Clear();
        var found = GetComponentsInChildren<ShapeFaceMarker>(true);
        foreach (var f in found)
        {
            if (!faces.Contains(f))
                faces.Add(f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshFaces();
    }
#endif

    public ShapeFaceMarker GetFace(int index)
    {
        if (index < 0 || index >= faces.Count) return null;
        return faces[index];
    }

    public int FaceCount => faces != null ? faces.Count : 0;
}
