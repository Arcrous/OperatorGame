using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManagerTest : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    [Range(0.1f, 1f)]
    public float wallDensity = 0.3f;

    public Cell[,] grid;
    public Cell exitCell;

    void Start()
    {
        GenerateValidMaze();
    }

    void GenerateValidMaze()
    {
        bool mazeIsValid = false;

        while (!mazeIsValid)
        {
            CreateGrid();
            GenerateMaze();

            // Attempt to solve the maze
            Cell startCell = grid[0, 0];
            List<Cell> path = FindPath(startCell, exitCell);

            if (path != null)
            {
                mazeIsValid = true;

                // Mark the valid path visually
                foreach (Cell cell in path)
                {
                    cell.isWall = false;
                    cell.SetEvent("Path");
                }
            }
            else
            {
                // If no valid path exists, clear the grid and try again
                Debug.Log("Maze was unsolvable. Regenerating...");
            }
        }

        Debug.Log("Maze successfully generated and solved!");
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
        int exitX = Random.Range(6, width);
        int exitY = Random.Range(6, height);

        exitCell = grid[exitX, exitY];
        exitCell.SetAsExit();
    }

    void GenerateMaze()
    {
        // Set the random exit cell
        SetRandomExit();

        // Add walls randomly
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip start and exit cells
                if (grid[x, y] == grid[0, 0] || grid[x, y] == exitCell)
                    continue;

                if (Random.value < wallDensity)
                {
                    grid[x, y].SetAsWall();
                }
            }
        }
    }

    ///////////////////////
    /// A* Pathfinding
    //////////////////////
    public List<Cell> FindPath(Cell start, Cell target)
    {
        List<Cell> openSet = new List<Cell> { start };
        HashSet<Cell> closedSet = new HashSet<Cell>();

        start.gCost = 0;
        start.CalculateHeuristic(target);

        while (openSet.Count > 0)
        {
            Cell current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < current.fCost || 
                    (openSet[i].fCost == current.fCost && openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                }
            }

            openSet.Remove(current);
            closedSet.Add(current);

            if (current == target)
            {
                return RetracePath(start, target);
            }

            foreach (Cell neighbor in GetNeighbors(current))
            {
                if (neighbor.isWall || closedSet.Contains(neighbor))
                    continue;

                int newMovementCostToNeighbor = current.gCost + 1;
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.CalculateHeuristic(target);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null; // No path found
    }

    List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();

        if (cell.x > 0) neighbors.Add(grid[cell.x - 1, cell.y]);
        if (cell.x < width - 1) neighbors.Add(grid[cell.x + 1, cell.y]);
        if (cell.y > 0) neighbors.Add(grid[cell.x, cell.y - 1]);
        if (cell.y < height - 1) neighbors.Add(grid[cell.x, cell.y + 1]);

        return neighbors;
    }

    List<Cell> RetracePath(Cell start, Cell end)
    {
        List<Cell> path = new List<Cell>();
        Cell current = end;

        while (current != start)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Reverse();
        return path;
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
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
            }
        }
    }
}
