using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
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
                cellObj.transform.parent = transform; //set grid manager as parent for organization.
                Cell cell = cellObj.AddComponent<Cell>();
                cell.Initialize(x, y);
                grid[x, y] = cell;
            }
        }
    }

    void SetRandomExit()
    {
        /*int exitX, exitY;
        do
        {
            exitX = Random.Range(1, width);  // Avoid (0,0)
            exitY = Random.Range(1, height); // Avoid (0,0)
        } while (*//*grid[exitX, exitY].isWall &&*//* exitX < 6 && exitY < 6); // Ensure the exit is not a wall*/

        int exitX = Random.Range(3, width);
        int exitY = Random.Range(3, height);

        exitCell = grid[exitX, exitY];
        exitCell.SetAsExit();
    }

    void GenerateMaze()
    {
        //Set an exit
        SetRandomExit();

        //Use A* to generate a guaranteed path from start to exit
        Cell startCell = grid[0, 0];
        List<Cell> path = FindPath(startCell, exitCell);

        if (path == null)
        {
            Debug.LogError("Failed to generate a valid path.");
            return;
        }

        // Mark all cells in the path
        foreach (Cell cell in path)
        {
            cell.isWall = false; // Ensure path cells are not walls
            cell.SetEvent("Path");
        }

        // Step 3: Add walls randomly while avoiding the path
        AddRandomWalls(path);
    }

    void AddRandomWalls(List<Cell> path)
    {
        foreach (Cell cell in grid)
        {
            // Skip cells in the path or the start/exit cells
            if (path.Contains(cell) || cell == grid[0, 0] || cell == exitCell)
                continue;

            // Randomly decide if the cell should be a wall
            if (Random.value < wallDensity) // Adjust probability for wall density
            {
                cell.SetAsWall();
            }
        }
    }

    /////////////////////////////////
    //Old maze gen system - doesn't work
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

    void GenerateMazeOld()
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
    /////////////////////////////////



    ///////////////////////
    /// A* pathfinding
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
                {
                    continue;
                }

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

        return null;
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
                    Gizmos.color = Color.yellow; // Mark path cells for easier visualization
                    Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                }
            }
        }
    }
}
