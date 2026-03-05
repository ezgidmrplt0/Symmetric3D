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
        for (int i = 0; i < spawnCount; i++)
        {
            if (gridPositions.Count == 0)
                return;

            int randomIndex = Random.Range(0, gridPositions.Count);

            Vector3 spawnPos = gridPositions[randomIndex];

            spawnPos.z -= objectOffset;

            Instantiate(objectPrefab, spawnPos, Quaternion.identity, transform);

            gridPositions.RemoveAt(randomIndex);
        }
    }
}