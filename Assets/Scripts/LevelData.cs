using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Symmetric3D/Level Data")]
public class LevelData : ScriptableObject
{
    public enum LevelType
    {
        Classic,
        QuarterFill,
        ColorMix,
        Shadow,
        Rotation,
        Linked
    }

    public enum BoardMode
    {
        Flat2D,
        Shape3D
    }

    [System.Serializable]
    public class FaceLayoutData
    {
        public string faceId = "Face";
        public ShapeFaceMarker.FaceSurfaceType surfaceType = ShapeFaceMarker.FaceSurfaceType.Rectangle;
        public bool isActive = true;
        public int gridX = 3;
        public int gridY = 3;
        public List<Vector2Int> customGridPositions = new List<Vector2Int>();
    }

    [System.Serializable]
    public class PieceData
    {
        public Vector2Int gridPosition;
        public int faceIndex = 0;
        public Color liquidColor = Color.white;
        public int currentSlices = 1;
        public float rotationZ = 0f;
        public bool isShadowTrigger = false;
        public int linkId = 0;
    }

    [Header("Level Bilgileri")]
    public string levelDisplayName = "Yeni Level";
    public LevelType levelType = LevelType.Classic;

    [Header("Board Mode")]
    public BoardMode boardMode = BoardMode.Flat2D;

    [Header("Flat Grid")]
    public int gridX = 3;
    public int gridY = 3;
    public List<Vector2Int> customGridPositions = new List<Vector2Int>();

    [Header("3D Shape")]
    public GameObject shapePrefab;
    public List<FaceLayoutData> shapeFaces = new List<FaceLayoutData>();

    [Header("Parçalar")]
    public List<PieceData> pieces = new List<PieceData>();

    public bool IsUnlocked(LevelSequenceData sequence, int currentProgress)
    {
        if (sequence == null) return true;
        return currentProgress >= sequence.GetUnlockProgress(levelType);
    }

#if UNITY_EDITOR
    [ContextMenu("Sync Shape Faces From Prefab")]
    public void SyncShapeFacesFromPrefab()
    {
        if (shapePrefab == null) return;

        ShapeDefinition def = shapePrefab.GetComponent<ShapeDefinition>();
        if (def == null)
        {
            Debug.LogWarning($"'{shapePrefab.name}' prefabında ShapeDefinition yok.");
            return;
        }

        def.RefreshFaces();

        var newList = new List<FaceLayoutData>();

        for (int i = 0; i < def.FaceCount; i++)
        {
            var marker = def.GetFace(i);
            if (marker == null) continue;

            FaceLayoutData existing = shapeFaces.Find(f => f.faceId == marker.faceId);

            if (existing != null)
            {
                newList.Add(existing);
            }
            else
            {
                newList.Add(new FaceLayoutData
                {
                    faceId = marker.faceId,
                    surfaceType = marker.surfaceType, // Marker'dan oku
                    isActive = true,
                    gridX = marker.defaultGridX,
                    gridY = marker.defaultGridY,
                    customGridPositions = new List<Vector2Int>()
                });
                
                // İsim bazlı koruma (Eğer marker'da yanlış ayarlanmışsa)
                if (marker.faceId.ToLower().Contains("triangle"))
                    newList[newList.Count - 1].surfaceType = ShapeFaceMarker.FaceSurfaceType.Triangle;
            }
        }

        shapeFaces = newList;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
