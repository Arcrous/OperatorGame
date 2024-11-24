using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 2f; // Speed of movement between cells

    private Cell startCell;
    private Cell exitCell;
    private List<Cell> path;
    private bool isMoving;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        Invoke("InitializePathfinding", 0.5f);
    }

    void InitializePathfinding()
    {
        Debug.Log("Agent AI: Initializing pathfinding");

        startCell = gridManager.grid[0, 0];
        exitCell = gridManager.exitCell;

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
}
