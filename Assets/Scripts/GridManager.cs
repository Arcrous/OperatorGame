using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    [Range(0.1f, 0.5f)]
    public float wallDensity = 0.3f;

    private Cell[,] grid;
    private Cell exitCell;

    void Start()
    {
        CreateGrid();
        SetRandomExit();
        GeneratePathToExit();
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

    void SetRandomExit()
    {
        int exitX, exitY;
        do
        {
            exitX = Random.Range(1, width);  // Avoid (0,0)
            exitY = Random.Range(1, height); // Avoid (0,0)
        } while (grid[exitX, exitY].isWall); // Ensure the exit is not a wall

        exitCell = grid[exitX, exitY];
        exitCell.SetAsExit();
    }

    void GeneratePathToExit()
    {
        // Use Depth-First Search (DFS) or another pathfinding algorithm to create a path from (0,0) to the exit
        Stack<Cell> stack = new Stack<Cell>();
        Cell startCell = grid[0, 0];
        startCell.isPath = true;
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            Cell current = stack.Peek();

            // If we've reached the exit, break out
            if (current == exitCell) break;

            // Get unvisited neighbors
            List<Cell> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                Cell nextCell = neighbors[Random.Range(0, neighbors.Count)];
                nextCell.isPath = true;
                stack.Push(nextCell);
            }
            else
            {
                // Backtrack if no unvisited neighbors
                stack.Pop();
            }
        }
    }

    List<Cell> GetUnvisitedNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();

        // Check all four directions and add unvisited neighbors
        if (cell.x > 0 && !grid[cell.x - 1, cell.y].isPath) neighbors.Add(grid[cell.x - 1, cell.y]);
        if (cell.x < width - 1 && !grid[cell.x + 1, cell.y].isPath) neighbors.Add(grid[cell.x + 1, cell.y]);
        if (cell.y > 0 && !grid[cell.x, cell.y - 1].isPath) neighbors.Add(grid[cell.x, cell.y - 1]);
        if (cell.y < height - 1 && !grid[cell.x, cell.y + 1].isPath) neighbors.Add(grid[cell.x, cell.y + 1]);

        return neighbors;
    }

    void GenerateMaze()
    {
        // After path is created, assign walls to the rest of the cells based on wallDensity
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y].isPath && !grid[x, y].isExit && Random.value < wallDensity)
                {
                    grid[x, y].SetAsWall();
                }
            }
        }
    }

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

                if (grid[x, y] != null && grid[x, y].isWall)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
                else if (grid[x, y] != null && grid[x, y].isExit)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
                else if (grid[x, y] != null && grid[x, y].isPath)
                {
                    Gizmos.color = Color.yellow; // Mark path cells for easier visualization
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
            }
        }
    }
}
