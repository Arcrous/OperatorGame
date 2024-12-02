using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f;
    public int patrolRange = 5; // Limits how far the enemy can move from its starting position

    [SerializeField] private Cell startCell;
    private List<Cell> path;
    private bool isMoving;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        Invoke("InitializePatrol", 0.5f);
    }

    void InitializePatrol()
    {
        Debug.Log("Enemy AI: Initializing patrol");

        // Get a random walkable cell avoiding the first 3 rows
        startCell = GetRandomWalkableCellAvoidingFirstRows(3);
        transform.position = startCell.transform.position;

        // Generate an initial patrol path using A*
        GenerateNewPatrolPath();
        StartCoroutine(FollowPath());
    }

    void GenerateNewPatrolPath()
    {
        Debug.Log("Generating patrol path");
        Cell targetCell = GetRandomCellWithinRange(startCell, patrolRange);
        path = FindPath(startCell, targetCell);
    }

    IEnumerator FollowPath()
    {
        while (true)
        {
            if (path != null && path.Count > 0)
            {
                foreach (Cell cell in path)
                {
                    yield return MoveToCell(cell);
                }
                path.Reverse(); // Reverse path for looping back to the start

                foreach (Cell cell in path)
                {
                    yield return MoveToCell(cell);
                }

                // Generate a new patrol path
                GenerateNewPatrolPath();
            }
            else
            {
                Debug.LogWarning("Enemy AI: No valid path found. Regenerating path.");
                GenerateNewPatrolPath();
            }
        }
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
        if (isMoving)
            yield break;

        isMoving = true;
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
        isMoving = false;
    }

    List<Cell> FindPath(Cell start, Cell target)
    {
        List<Cell> openSet = new List<Cell> { start };
        HashSet<Cell> closedSet = new HashSet<Cell>();
        Dictionary<Cell, int> gCost = new Dictionary<Cell, int>();
        Dictionary<Cell, int> hCost = new Dictionary<Cell, int>();
        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

        gCost[start] = 0;
        hCost[start] = CalculateHeuristic(start, target);

        while (openSet.Count > 0)
        {
            Cell current = GetLowestFCostCell(openSet, gCost, hCost);

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

        return null;
    }

    Cell GetLowestFCostCell(List<Cell> openSet, Dictionary<Cell, int> gCost, Dictionary<Cell, int> hCost)
    {
        Cell bestCell = openSet[0];
        int bestFCost = gCost[bestCell] + hCost[bestCell];

        foreach (Cell cell in openSet)
        {
            int fCost = gCost[cell] + hCost[cell];
            if (fCost < bestFCost)
            {
                bestCell = cell;
                bestFCost = fCost;
            }
        }

        return bestCell;
    }

    List<Cell> RetracePath(Dictionary<Cell, Cell> cameFrom, Cell start, Cell target)
    {
        List<Cell> path = new List<Cell>();
        Cell current = target;

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

    Cell GetRandomWalkableCellAvoidingFirstRows(int minRows)
    {
        List<Cell> candidates = new List<Cell>();

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = minRows; y < gridManager.height; y++)
            {
                Cell cell = gridManager.grid[x, y];
                if (!cell.isWall)
                {
                    candidates.Add(cell);
                }
            }
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    Cell GetRandomCellWithinRange(Cell start, int range)
    {
        List<Cell> candidates = new List<Cell>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int nx = start.x + dx;
                int ny = start.y + dy;

                if (nx >= 0 && nx < gridManager.width && ny >= 0 && ny < gridManager.height)
                {
                    Cell candidate = gridManager.grid[nx, ny];
                    if (!candidate.isWall)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void OnDrawGizmos()
    {
        if (path != null)
        {
            Gizmos.color = Color.red;

            foreach (Cell cell in path)
            {
                Gizmos.DrawSphere(cell.transform.position, 0.2f);
            }
        }
    }
}
