using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    [Range(0.1f, 0.5f)]
    public float wallDensity = 0.3f;  // Percentage of cells to be walls

    private Cell[,] grid;

    void Start()
    {
        CreateGrid();
        GenerateMaze();
    }

    void CreateGrid()
    {
        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * cellSize, y * cellSize, 0);
                GameObject cellObj = new GameObject($"Cell_{x}_{y}");
                cellObj.transform.position = position;
                cellObj.transform.parent = transform;
                Cell cell = cellObj.AddComponent<Cell>();
                cell.Initialize(x, y);
                grid[x, y] = cell;
            }
        }
    }

    void GenerateMaze()
    {
        // Randomly assign cells as walls based on wallDensity
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip the starting cell (0,0)
                if (x == 0 && y == 0) continue;

                // Randomly decide if this cell should be a wall
                if (Random.value < wallDensity)
                {
                    grid[x, y].isWall = true;
                }
            }
        }

        // Choose an exit cell that is not (0,0) and is not a wall
        SetRandomExit();
    }

    void SetRandomExit()
    {
        int exitX, exitY;
        do
        {
            exitX = Random.Range(1, width);  // Avoid (0,0)
            exitY = Random.Range(1, height); // Avoid (0,0)
        } while (grid[exitX, exitY].isWall); // Ensure the exit is not a wall

        grid[exitX, exitY].isExit = true;
    }

    // Draw grid in the Scene view
    void OnDrawGizmos()
    {
        if (grid == null)
            return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * cellSize, y * cellSize, 0);
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(position, Vector3.one * cellSize);

                // Wall visualization
                if (grid[x, y] != null && grid[x, y].isWall)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
                // Exit visualization
                else if (grid[x, y] != null && grid[x, y].isExit)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
                else if (grid[x, y] != null && grid[x, y].isOccupied)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
                else if (grid[x, y] != null && grid[x, y].cellEvent != "None")
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
            }
        }
    }
}
