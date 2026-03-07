using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Symmetric3D/Level Data")]
public class LevelData : ScriptableObject
{
    public int gridX = 3;
    public int gridY = 3;

    [System.Serializable]
    public class PieceData
    {
        public Vector2Int gridPosition;
        public Color liquidColor = Color.white;
        public int currentSlices = 1;
        public float rotationZ = 0f;
    }

    public List<PieceData> pieces = new List<PieceData>();
}
