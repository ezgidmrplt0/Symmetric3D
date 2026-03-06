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
        if (gridPositions.Count < 4) return;

        // Define directions for symmetric pairs
        // Up/Down pair
        Quaternion rotUp = Quaternion.Euler(0, 0, 0);
        Quaternion rotDown = Quaternion.Euler(0, 0, 180);
        // Left/Right pair
        Quaternion rotLeft = Quaternion.Euler(0, 0, 90);
        Quaternion rotRight = Quaternion.Euler(0, 0, -90);

        List<Quaternion> rotationPairs = new List<Quaternion>();
        
        // Add two pairs (A-B and C-D)
        // Pair 1: Randomly choose between Vertical or Horizontal symmetry
        if (Random.value > 0.5f) { rotationPairs.Add(rotUp); rotationPairs.Add(rotDown); }
        else { rotationPairs.Add(rotLeft); rotationPairs.Add(rotRight); }

        // Pair 2: Randomly choose between Vertical or Horizontal symmetry
        if (Random.value > 0.5f) { rotationPairs.Add(rotUp); rotationPairs.Add(rotDown); }
        else { rotationPairs.Add(rotLeft); rotationPairs.Add(rotRight); }

        // Pair 3: Randomly choose between Vertical or Horizontal symmetry
        if (Random.value > 0.5f) { rotationPairs.Add(rotUp); rotationPairs.Add(rotDown); }
        else { rotationPairs.Add(rotLeft); rotationPairs.Add(rotRight); }

        Color red = new Color(0.8f, 0.1f, 0.1f);   // Tatlı bir kırmızı
        Color blue = new Color(0.1f, 0.4f, 0.8f);  // Tatlı bir mavi
        Color green = new Color(0.1f, 0.7f, 0.2f); // Tatlı bir yeşil

        for (int i = 0; i < 6; i++)
        {
            if (gridPositions.Count == 0) break;

            int randomIndex = Random.Range(0, gridPositions.Count);
            Vector3 spawnPos = gridPositions[randomIndex];
            spawnPos.z -= objectOffset;

            // Spawn with symmetric rotation
            GameObject newObj = Instantiate(objectPrefab, spawnPos, rotationPairs[i], transform);
            
            // Renk ata
            LiquidTransfer lt = newObj.GetComponentInChildren<LiquidTransfer>();
            if (lt != null)
            {
                if (i < 2) lt.liquidColor = red;
                else if (i < 4) lt.liquidColor = blue;
                else lt.liquidColor = green;
            }

            gridPositions.RemoveAt(randomIndex);
        }
    }
}