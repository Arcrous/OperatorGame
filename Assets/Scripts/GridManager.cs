using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Events;

public class GridManager : MonoBehaviour
{
    [Header("Grid Related")]
    [SerializeField] bool destroyGrid = false;

    [Tooltip("Set this to true to generate a random grid size from 10 to 40")]
    [SerializeField] bool isRandomGrid = false; // Flag to check if the grid has been generated

    public int width = 10; //grid width
    public int height = 10; //grid height
    public float cellSize = 1f; //how big the cell is
    [Range(0.1f, 1f)]
    public float wallDensity = 0.3f; //determine how many walls will be placed

    public Cell[,] grid;
    public Cell exitCell;
    Cell exitAdj1;
    Cell exitAdj2;
    Cell exitAdj3;
    Cell exitAdj4;

    [Header("Prefab References")]
    public GameObject agentObj;
    public GameObject groundPrefab;

    [Header("Game Speed")]
    [Range(0, 30)]
    public float gameSpeed;

    public UnityAction spawnEnemy;

    [Header("Weapon Spawning")]
    public Cell weaponCell; // Keep for backward compatibility
    public List<Cell> weaponCells = new List<Cell>(); // New list to store multiple weapon cells
    [Range(1, 5)]
    [SerializeField] int weaponCount = 3; // Number of weapons to spawn
    [SerializeField] bool randomWeaponCount = false; // Whether to use random weapon count

    void Start()
    {
        if (isRandomGrid)
        {
            // Randomly generate grid size if not set
            width = Random.Range(10, 40);
            height = Random.Range(10, 40);
        }
        gameSpeed = 1f;
        StartCoroutine(GenerateValidMazeCoroutine());
        TraceManager manager = TraceManager.Instance; // Ensure TraceManager is initialized
    }

    private void Update()
    {
        Time.timeScale = gameSpeed;
    }

    //Generate a maze until it's solvable by tracing from exit to start using A*
    IEnumerator GenerateValidMazeCoroutine()
    {
        bool mazeIsValid = false;

        while (!mazeIsValid)
        {
            yield return StartCoroutine(CreateGridCoroutine()); //Make the grid first
            yield return StartCoroutine(GenerateMazeCoroutine()); //Generate a maze after

            // Validate the maze
            //Debug.Log("Validating maze...");
            Cell startCell = grid[0, 0];
            List<Cell> path = FindPath(exitCell, startCell); // Find path from exit to start

            if (path != null)
            {
                mazeIsValid = true;

                Debug.Log("Maze successfully validated and solved!");

                spawnEnemy?.Invoke(); // Invoke the event to spawn enemies

                yield return new WaitForSeconds(2f);

                //Activate the agent gObj
                agentObj.SetActive(true);
            }
            else
            {
                Debug.Log("Maze was unsolvable. Regenerating...");

                destroyGrid = true;
                // Destroy the grid before retrying
                if (destroyGrid)
                {
                    yield return new WaitForSeconds(0.01f);
                    while (transform.childCount > 0 && destroyGrid)
                    {
                        yield return new WaitForSeconds(0.01f);
                        if (transform.childCount == 0) // If all children are destroyed
                        {
                            //Debug.Log("Grid destroyed!");
                            destroyGrid = false;
                        }
                        else if (transform.childCount > 0)  // Double-check before accessing
                        {
                            Destroy(transform.GetChild(0).gameObject);
                        }
                    }
                }
            }

            yield return null; // Wait a frame before retrying
        }
    }

    IEnumerator CreateGridCoroutine() //Create a grid 
    {
        //Debug.Log("Creating grid...");
        grid = new Cell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(x * cellSize, y * cellSize, 0);
                //GameObject cellObj = new GameObject($"Cell_{x}_{y}"); //replace with cellPrefab later for sprite
                GameObject cellObj = Instantiate(groundPrefab, position, Quaternion.identity);
                cellObj.name = $"Cell_{x}_{y}";
                cellObj.transform.position = position;
                cellObj.transform.parent = transform;

                //Cell cell = cellObj.AddComponent<Cell>();
                Cell cell = cellObj.GetComponent<Cell>();
                cell.Initialize(x, y);
                grid[x, y] = cell;

                // Spread workload over frames
                if ((x * width + y) % 100 == 0) // Adjust batch size if necessary
                {
                    yield return null; // Wait a frame
                }
            }
        }

        //Debug.Log("Grid created!");
    }

    IEnumerator GenerateMazeCoroutine() //Places walls randomly and place an exit
    {
        //Debug.Log("Generating maze...");
        SetRandomExit();
        SetRandomWeaponSpawn();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip start and exit cells + cells adjacent to it
                if (grid[x, y] == grid[0, 0] || grid[x, y] == exitCell || grid[x, y] == exitAdj1 || grid[x, y] == exitAdj2 || grid[x, y] == exitAdj3 || grid[x, y] == exitAdj4 || weaponCells.Contains(grid[x, y]))
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

        //Debug.Log("Maze generated!");
    }

    void SetRandomExit() //Place an exit
    {
        //Ensure the exit is at least 6 rows away from the exit
        int exitX = Random.Range(6, width - 1);
        int exitY = Random.Range(6, height - 1);

        exitCell = grid[exitX, exitY];

        //Get the 4 cells adjacent to the exit
        exitAdj1 = grid[exitX, exitY + 1];
        exitAdj2 = grid[exitX, exitY - 1];
        exitAdj3 = grid[exitX + 1, exitY];
        exitAdj4 = grid[exitX - 1, exitY];

        exitCell.SetAsExit();
    }

    void SetRandomWeaponSpawn()
    {
        // Clear any previous weapon cells
        weaponCells.Clear();

        // Determine how many weapons to spawn
        int numWeaponsToSpawn = randomWeaponCount ? Random.Range(1, weaponCount + 1) : weaponCount;

        // Keep track of weapon spawn attempts to avoid infinite loops
        int spawnAttempts = 0;
        int maxSpawnAttempts = 50;

        for (int i = 0; i < numWeaponsToSpawn; i++)
        {
            Cell newWeaponCell = null;
            bool validCellFound = false;

            while (!validCellFound && spawnAttempts < maxSpawnAttempts)
            {
                spawnAttempts++;

                // Ensure the weapon is at least 1 row away from start
                int weaponX = Random.Range(1, width - 1);
                int weaponY = Random.Range(1, height - 1);

                Cell cellToCheck = grid[weaponX, weaponY];

                // Check if this is a valid cell (not exit, not wall, not start, not already a weapon)
                if (cellToCheck != exitCell &&
                    cellToCheck != grid[0, 0] &&
                    !cellToCheck.isWall &&
                    !weaponCells.Contains(cellToCheck))
                {
                    newWeaponCell = cellToCheck;
                    validCellFound = true;
                }
            }

            if (validCellFound && newWeaponCell != null)
            {
                weaponCells.Add(newWeaponCell);
                newWeaponCell.SetAsWeapon();

                // Set the first weapon cell as the legacy weaponCell for backward compatibility
                if (i == 0)
                {
                    weaponCell = newWeaponCell;
                }
            }
        }

    }

    ///////////////////////
    /// A* Pathfinding (made by chatgpt - I can understand the logic behind A* but not the intricacy of the code)
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

    //Visualisation
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
                        Gizmos.color = Color.white;
                        //Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                    }
                    else if (grid[x, y].isExit)
                    {
                        Gizmos.color = Color.green;
                        //Gizmos.DrawCube(position, Vector3.one * (cellSize * 0.9f));
                    }
                }
            }
        }
    }
}
