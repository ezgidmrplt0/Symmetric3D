using UnityEngine;
using System.Collections.Generic;

public class GridSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject gridPrefab;
    public GameObject objectPrefab;

    [Header("Grid Size")]
    public int gridX = 3;
    public int gridY = 3;

    [Header("Spacing")]
    public float spacing = 0.4f;

    [Header("Object Spawn")]
    public int spawnCount = 2;
    public float objectOffset = 0.3f;

    private List<Vector3> gridPositions = new List<Vector3>();

    void Start()
    {
        SpawnGrid();
        SpawnObjects();
    }

    void SpawnGrid()
    {
        float gridSize = gridPrefab.transform.localScale.x;

        float offsetX = (gridX - 1) * (gridSize + spacing) / 2f;
        float offsetY = (gridY - 1) * (gridSize + spacing) / 2f;

        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                Vector3 pos = new Vector3(
                    x * (gridSize + spacing) - offsetX,
                    y * (gridSize + spacing) - offsetY,
                    0
                );

                Vector3 worldPos = transform.position + pos;

                Instantiate(gridPrefab, worldPos, Quaternion.identity, transform);

                gridPositions.Add(worldPos);
            }
        }
    }

    void SpawnObjects()
    {
        if (gridPositions.Count < 3) return;

        Color red = new Color(0.8f, 0.1f, 0.1f);

        // 1. Obje (Çeyrek) - Yukarı baksın (Yatay düzlem simetrisi)
        SpawnSpecificObject(red, 1, Quaternion.Euler(0,0,0));

        // 2. Obje (Çeyrek) - Aşağı baksın (Yatay düzlem simetrisi)
        SpawnSpecificObject(red, 1, Quaternion.Euler(0,0,180));

        // 3. Obje (Yarım) - Aşağı baksın
        // (Bu sayede çeyreklerden oluşan 'yukarı bakan' yarım ile tam bir uyum sağlar)
        SpawnSpecificObject(red, 2, Quaternion.Euler(0,0,180));
    }

    void SpawnSpecificObject(Color color, int slices, Quaternion rotation)
    {
        int randomIndex = Random.Range(0, gridPositions.Count);
        Vector3 spawnPos = gridPositions[randomIndex];
        spawnPos.z -= objectOffset;

        GameObject newObj = Instantiate(objectPrefab, spawnPos, rotation, transform);
        
        LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
        if (lt != null)
        {
            lt.liquidColor = color;
            lt.currentSlices = slices; // Kaç dilimle başlayacağını ayarla
        }

        gridPositions.RemoveAt(randomIndex);
    }
}