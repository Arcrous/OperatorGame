using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int width = 10; //grid width
    public int height = 10; //grid height
    public float cellSize = 1f; //how big the cell is
    [Range(0.1f, 1f)]
    public float wallDensity = 0.3f; //determine how many walls will be placed

    public Cell[,] grid;
    public Cell exitCell;
    public Cell weaponCell;
    Cell exitAdj1;
    Cell exitAdj2;
    Cell exitAdj3;
    Cell exitAdj4;

    public GameObject agentObj;
    public GameObject enemyPrefab;
    public GameObject groundPrefab;

    void Start()
    {
        StartCoroutine(GenerateValidMazeCoroutine());
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
            Debug.Log("Validating maze...");
            Cell startCell = grid[0, 0];
            List<Cell> path = FindPath(exitCell, startCell); // Find path from exit to start

            if (path != null)
            {
                mazeIsValid = true;

                Debug.Log("Maze successfully validated and solved!");

                // Spawn an enemy
                SpawnEnemy();

                //Activate the agent gObj
                agentObj.SetActive(true);
            }
            else
            {
                Debug.Log("Maze was unsolvable. Regenerating...");
                foreach (Transform child in this.transform)
                {
                    yield return null;
                    Destroy(child.gameObject);
                }
            }

            yield return null; // Wait a frame before retrying
        }
    }

    IEnumerator CreateGridCoroutine() //Create a grid 
    {
        Debug.Log("Creating grid...");
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

        Debug.Log("Grid created!");
    }

    IEnumerator GenerateMazeCoroutine() //Places walls randomly and place an exit
    {
        Debug.Log("Generating maze...");
        SetRandomExit();
        SetRandomWeaponSpawn();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip start and exit cells + cells adjacent to it
                if (grid[x, y] == grid[0, 0] || grid[x, y] == exitCell || grid[x, y] == exitAdj1 || grid[x, y] == exitAdj2 || grid[x, y] == exitAdj3 || grid[x, y] == exitAdj4 || grid[x,y] == weaponCell)
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

    void SetRandomExit() //Place an exit
    {
        //Ensure the exit is at least 6 rows away from the exit
        int exitX = Random.Range(6, width-1);
        int exitY = Random.Range(6, height-1);

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
        int amountToSpawn = Random.Range(2, 5);

        int weaponX;
        int weaponY;

        int weaponCount = 0;

        do
        {
            //Ensure the exit is at least 1 rows away from the exit
            weaponX = Random.Range(1, width - 1);
            weaponY = Random.Range(1, height - 1);

            weaponCount++;
            Debug.Log("Weapon count: " + weaponCount);
        }
        while (weaponCount == 0 && (grid[weaponX, weaponY] == exitCell || grid[weaponX, weaponY] == grid[weaponX, weaponY].isWall)); //Ensure the weapon isn't on exitCell, wall Cell

        weaponCell = grid[weaponX, weaponY];

        weaponCell.SetAsWeapon();
    }

    void SpawnEnemy() //Spawn enemies
    {
        int amountToSpawn = Random.Range(2, 5);
        for (int x = 0; x < amountToSpawn; x++)
        {
            Debug.Log("Spawning enemy");
            GameObject enemy = Instantiate(enemyPrefab);
        }
        //enemy.transform.SetParent(transform);
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
