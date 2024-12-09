using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using UnityEngine;

public class AgentAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f; // Speed of movement between cells
    public float traceDuration = 5f; // Duration for traces to persist

    private Cell startCell;
    private Cell currentCell;
    private Cell exitCell;
    private List<Cell> path;
    private bool isMoving;

    bool gridGen = false;
    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("GridManager not found or not assigned!");
        }
        else
        {
            gridGen = true;
        }
    }

    private void Start()
    {
        Invoke("InitializePathfinding", 1.5f);
    }

    void InitializePathfinding()
    {
        Debug.Log("Agent AI: Initializing pathfinding");

        startCell = gridManager.grid[0, 0];
        exitCell = gridManager.exitCell;
        currentCell = startCell;

        LeaveTrace(currentCell);

        path = FindPath(startCell, exitCell);

        if (path != null && path.Count > 0)
        {
            StartCoroutine(FollowPath());
        }
        else
        {
            Debug.LogError("Agent AI: No path found to the exit!");
        }
    }

    IEnumerator FollowPath()
    {
        foreach (Cell cell in path)
        {
            yield return MoveToCell(cell);
        }

        Debug.Log("Agent AI: Reached the exit!");
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
        if (isMoving)
            yield break;

        isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = targetCell.transform.position;

        LeaveTrace(currentCell); // Leave a trace at the current cell

        float elapsedTime = 0f;
        while (elapsedTime < 1f / moveSpeed)
        {
            elapsedTime += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime);
            yield return null;
        }

        transform.position = endPos;
        currentCell = targetCell;
        isMoving = false;
    }

    void LeaveTrace(Cell cell)
    {
        if (cell.cellEvent != "AgentTrace")
        {
            cell.cellEvent = "AgentTrace";
            Debug.Log($"AgentAI: Trace left at ({cell.x}, {cell.y})");

            // Schedule the trace to be cleared after the specified duration
            StartCoroutine(ClearTraceAfterDelay(cell));
        }
    }

    IEnumerator ClearTraceAfterDelay(Cell cell)
    {
        yield return new WaitForSeconds(traceDuration);

        // Ensure the cell's event is still the Agent's trace before clearing
        if (cell.cellEvent == "AgentTrace")
        {
            cell.cellEvent = "None";
            Debug.Log($"AgentAI: Trace cleared at ({cell.x}, {cell.y})");
        }
    }

    public List<Cell> FindPath(Cell start, Cell target)
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
            // Find cell with lowest F cost
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

    List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();

        // Check adjacent cells in the grid
        if (cell.x > 0) neighbors.Add(gridManager.grid[cell.x - 1, cell.y]);
        if (cell.x < gridManager.width - 1) neighbors.Add(gridManager.grid[cell.x + 1, cell.y]);
        if (cell.y > 0) neighbors.Add(gridManager.grid[cell.x, cell.y - 1]);
        if (cell.y < gridManager.height - 1) neighbors.Add(gridManager.grid[cell.x, cell.y + 1]);

        return neighbors;
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

    int CalculateHeuristic(Cell a, Cell b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void OnDrawGizmos()
    {
        if (path != null)
        {
            Gizmos.color = Color.green;

            foreach (Cell cell in path)
            {
                Gizmos.DrawSphere(cell.transform.position, 0.15f);
            }
        }

        if (gridManager != null && gridGen)
        {
            foreach (Cell cell in gridManager.grid)
            {
                if (cell.cellEvent == "AgentTrace")
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(cell.transform.position, 0.3f);
                }
            }
        }
    }
}
