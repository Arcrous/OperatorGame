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

    public GameObject agentObj;
    public GameObject enemyPrefab; // Assign the enemy prefab in the Inspector

    void Start()
    {
        StartCoroutine(GenerateValidMazeCoroutine());
    }

    IEnumerator GenerateValidMazeCoroutine()
    {
        bool mazeIsValid = false;

        while (!mazeIsValid)
        {
            yield return StartCoroutine(CreateGridCoroutine());
            yield return StartCoroutine(GenerateMazeCoroutine());

            // Validate the maze
            Debug.Log("Validating maze...");
            Cell startCell = grid[0, 0];
            List<Cell> path = FindPath(exitCell, startCell); // Find path from exit to start

            if (path != null)
            {
                mazeIsValid = true;

                // Mark the valid path visually
                foreach (Cell cell in path)
                {
                    cell.isWall = false;
                    cell.SetEvent("Path");
                }
                Debug.Log("Maze successfully validated and solved!");

                // Spawn an enemy
                SpawnEnemy();

                //Activate the agent gObj
                //agentObj.SetActive(true);
            }
            else
            {
                Debug.Log("Maze was unsolvable. Regenerating...");
            }

            yield return null; // Wait a frame before retrying
        }
    }

    IEnumerator CreateGridCoroutine()
    {
        Debug.Log("Creating grid...");
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

                // Spread workload over frames
                if ((x * width + y) % 100 == 0) // Adjust batch size if necessary
                {
                    yield return null; // Wait a frame
                }
            }
        }

        Debug.Log("Grid created!");
    }

    IEnumerator GenerateMazeCoroutine()
    {
        Debug.Log("Generating maze...");
        SetRandomExit();

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

                // Spread workload over frames
                if ((x * width + y) % 100 == 0) // Adjust batch size if necessary
                {
                    yield return null; // Wait a frame
                }
            }
        }

        Debug.Log("Maze generated!");
    }

    void SetRandomExit()
    {
        int exitX = Random.Range(6, width);
        int exitY = Random.Range(6, height);

        exitCell = grid[exitX, exitY];
        exitCell.SetAsExit();
        //Debug.Log($"Exit set at: ({exitX}, {exitY})");
    }

    void SpawnEnemy()
    {
        int amountToSpawn = Random.Range(2, 5);
        for (int x = 0; x < amountToSpawn; x++)
        {
            Debug.Log("Spawning enemy");
        }
            GameObject enemy = Instantiate(enemyPrefab);
        //enemy.transform.SetParent(transform);
    }

    ///////////////////////
    /// A* Pathfinding
    ///////////////////////
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
                return RetracePath(target, start); // Reverse: from target (start) to start (exit)
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
        Cell current = start; // Start retracing from the start (exit cell)

        while (current != end)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Add(end); // Add the end cell (0,0)
        return path;
    }

    public Cell GetCellFromWorldPosition(Vector3 worldPosition)
    {
        // Calculate the grid cell indices based on the world position
        int x = Mathf.FloorToInt(worldPosition.x - transform.position.x);
        int y = Mathf.FloorToInt(worldPosition.z - transform.position.z); // Assuming Z is the depth in the world

        // Ensure indices are within the grid bounds
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            return grid[x, y];
        }

        // Return null if the position is outside the grid bounds
        Debug.LogWarning($"GetCellFromWorldPosition: Position {worldPosition} is outside the grid bounds.");
        return null;
    }

    public Cell GetRandomWalkableCell()
    {
        List<Cell> walkableCells = new List<Cell>();

        foreach (var cell in grid)
        {
            if (!cell.isWall) // Check if the cell is not a wall
            {
                walkableCells.Add(cell);
            }
        }

        if (walkableCells.Count > 0)
        {
            return walkableCells[Random.Range(0, walkableCells.Count)];
        }

        Debug.LogError("No walkable cells available!");
        return null;
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

                if (grid[x, y] != null)
                {
                    if (grid[x, y].isWall)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                    }
                    else if (grid[x, y].isExit)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                    }
                    else if (grid[x, y].isPath)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                    }
                }
            }
        }
    }
}
