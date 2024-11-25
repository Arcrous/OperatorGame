using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f; // Speed of movement between cells
    public List<Cell> patrolCells; // Cells within the patrol range
    private Queue<Cell> currentPath;
    private Cell currentCell;
    private Cell targetCell;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager is not assigned to EnemyAI!");
            return;
        }

        // Get a random, valid starting cell
        Cell randomCell = gridManager.GetRandomWalkableCell();
        if (randomCell == null)
        {
            Debug.LogError("Failed to find a valid random cell for EnemyAI.");
            return;
        }

        // Set the current cell and move the enemy to the starting position
        currentCell = randomCell;
        transform.position = randomCell.transform.position;

        // Start the patrol coroutine
        StartCoroutine(Patrol());
    }


    void InitializeEnemy()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager is not assigned to EnemyAI!");
            return;
        }

        // Get a random, valid starting cell
        Cell randomCell = gridManager.GetRandomWalkableCell();
        if (randomCell == null)
        {
            Debug.LogError("Failed to find a valid random cell for EnemyAI.");
            return;
        }

        // Set the current cell and move the enemy to the starting position
        currentCell = randomCell;
        transform.position = randomCell.transform.position;

        StartCoroutine(Patrol());
    }

    IEnumerator Patrol()
    {
        while (true)
        {
            if (patrolCells.Count == 0)
            {
                Debug.LogError("Enemy AI: Patrol cells list is empty!");
                yield break;
            }

            // Pick a random patrol target
            targetCell = patrolCells[Random.Range(0, patrolCells.Count)];
            Debug.Log($"Enemy AI: Patrolling to ({targetCell.x}, {targetCell.y})");

            List<Cell> path = FindPath(currentCell, targetCell);
            if (path == null)
            {
                Debug.LogWarning("Enemy AI: No path found to the target cell. Retrying...");
                yield return new WaitForSeconds(1f);
                continue;
            }

            currentPath = new Queue<Cell>(path);

            // Move along the path
            while (currentPath.Count > 0)
            {
                yield return MoveToCell(currentPath.Dequeue());
            }

            // Wait before choosing a new patrol target
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = targetCell.transform.position;

        float elapsedTime = 0f;
        while (elapsedTime < 1f / moveSpeed)
        {
            elapsedTime += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime);
            yield return null;
        }

        transform.position = endPos;
        currentCell = targetCell;
    }

    public List<Cell> FindPath(Cell start, Cell target)
    {
        if (start == null || target == null)
        {
            Debug.LogError("FindPath: Start or target cell is null!");
            return null;
        }

        List<Cell> openSet = new List<Cell> { start };
        HashSet<Cell> closedSet = new HashSet<Cell>();

        Dictionary<Cell, int> gCost = new Dictionary<Cell, int>();
        Dictionary<Cell, int> hCost = new Dictionary<Cell, int>();
        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

        gCost[start] = 0;
        hCost[start] = CalculateHeuristic(start, target);

        while (openSet.Count > 0)
        {
            Cell current = openSet[0];
            foreach (Cell cell in openSet)
            {
                int currentFCost = gCost[current] + hCost[current];
                int cellFCost = gCost[cell] + hCost[cell];
                if (cellFCost < currentFCost || (cellFCost == currentFCost && hCost[cell] < hCost[current]))
                {
                    current = cell;
                }
            }

            if (current == target)
            {
                return RetracePath(cameFrom, start, target);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Cell neighbor in GetNeighbors(current))
            {
                if (neighbor.isWall || closedSet.Contains(neighbor))
                    continue;

                int tentativeGCost = gCost[current] + 1;

                if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor])
                {
                    gCost[neighbor] = tentativeGCost;
                    hCost[neighbor] = CalculateHeuristic(neighbor, target);
                    cameFrom[neighbor] = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // No path found
    }

    List<Cell> RetracePath(Dictionary<Cell, Cell> cameFrom, Cell start, Cell end)
    {
        List<Cell> path = new List<Cell>();
        Cell current = end;

        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }

        path.Reverse();
        return path;
    }

    List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();

        if (cell.x > 0) neighbors.Add(gridManager.grid[cell.x - 1, cell.y]);
        if (cell.x < gridManager.width - 1) neighbors.Add(gridManager.grid[cell.x + 1, cell.y]);
        if (cell.y > 0) neighbors.Add(gridManager.grid[cell.x, cell.y - 1]);
        if (cell.y < gridManager.height - 1) neighbors.Add(gridManager.grid[cell.x, cell.y + 1]);

        return neighbors;
    }

    int CalculateHeuristic(Cell a, Cell b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    List<Cell> GetPatrolCells(Cell start, int range)
    {
        List<Cell> cells = new List<Cell>();

        Cell cell = gridManager.grid[x + 1, y];
        if (!cell.isWall)
        {
            cells.Add(cell);
        }
        Cell cell = gridManager.grid[x - 1, y];
        if (!cell.isWall)
        {
            cells.Add(cell);
        }
        Cell cell = gridManager.grid[x , y+1];
        if (!cell.isWall)
        {
            cells.Add(cell);
        }
        Cell cell = gridManager.grid[x, y-1];
        if (!cell.isWall)
        {
            cells.Add(cell);
        }

        return cells;
    }
}
