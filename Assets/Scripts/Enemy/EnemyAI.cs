using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f;
    public int patrolRange = 5; // Limits how far the enemy can move from its starting position
    public float traceDuration = 5f;
    //[SerializeField] int lookAheadCells = 1;

    private Cell currentCell; // Tracks the enemy's current cell
    [SerializeField]  private Cell startCell; // Tracks the enemy's starting cell
    private List<Cell> path; // Current path for patrol
    private bool isMoving;
    private bool isDead = false;
    public bool seenTrace = false;
    [SerializeField] private bool isChasing = false;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        InitializePatrol();
        StartCoroutine(CheckForAgentTrace());
    }

    public void Die()
    {
        isDead = true;
        StopAllCoroutines();
        Debug.Log("Enemy has died");

        SpriteRenderer spriteRend = this.gameObject.GetComponent<SpriteRenderer>();
        spriteRend.color = Color.red;
        gameObject.transform.Rotate(0f, 0f, 90f, Space.Self);

        Destroy(this.gameObject, 5f);
    }

    void InitializePatrol()
    {

        // Set the enemy's starting cell to a random walkable cell avoiding the first 3 rows
        currentCell = GetRandomWalkableCellAvoidingFirstRows(3);
        if (currentCell != null)
        {
            //update location to accomadate for relocating after spawn, so trace works properly.
            transform.position = currentCell.transform.position;
            
            startCell = currentCell; //input a value
            
            LeaveTrace(currentCell, "EnemyTrace");
            
            //gen the path and follow it.
            GenerateNewPatrolPath();
            StartCoroutine(FollowPath());
        }
        else
        {
            Debug.LogError("EnemyAI: Failed to find a valid starting cell.");
        }
    }

    void GenerateNewPatrolPath()
    {
        //get a random cell with range to set as target
        Cell targetCell = GetRandomCellWithinRange(currentCell, patrolRange);
        if (targetCell != null)
        {
            path = FindPath(currentCell, targetCell);
        }
    }

    IEnumerator FollowPath()
    {
        while (!isDead & !isChasing)
        {
            if (path != null && path.Count > 0)
            {
                foreach (Cell cell in path)
                {
                    yield return MoveToCell(cell);
                }

                path.Reverse(); // Reverse the path to return to the start
                foreach (Cell cell in path)
                {
                    yield return MoveToCell(cell);
                }

                // Generate a new patrol path after completing the loop
                GenerateNewPatrolPath();
            }
            else
            {
                Debug.LogWarning("EnemyAI: No valid path found. Regenerating...");
                GenerateNewPatrolPath();
            }
        }
    }

    //shouldchaseplayer
    IEnumerator CheckForAgentTrace()
    {
        while (!isDead)
        {
            if (DetectAgentTrace())
            {
                Debug.Log("Enemy detected Agent's trace! Chasing...");
                isChasing = true;
                StopCoroutine(FollowPath());
                StartCoroutine(ChaseAgent());
            }
            else if (isChasing)
            {
                Debug.Log("Lost Agent's trace. Returning to patrol.");
                isChasing = false;
                StartCoroutine(ReturnToPatrol());
            }
            yield return new WaitForSeconds(1f);
        }
    }

    bool DetectAgentTrace()
    {
        Debug.Log("Detected Agent trace");
        foreach (Cell neighbor in GetNeighbors(currentCell))
        {
            if (neighbor.cellEvent == "AgentTrace")
            {
                seenTrace = true;
                return true;
            }
        }
        return false;
    }
    IEnumerator ChaseAgent()
    {
        Debug.Log("Chasing Agent");
        while (isChasing && !isDead)
        {
            Cell targetCell = FindNearestAgentTrace();
            if (targetCell != null)
            {
                path = FindPath(currentCell, targetCell);
                if (path != null && path.Count > 0)
                {
                    foreach (Cell cell in path)
                    {
                        yield return MoveToCell(cell);
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    Cell FindNearestAgentTrace()
    {
        Debug.Log("Finding nearest Agent trace");
        foreach (Cell cell in gridManager.grid)
        {
            if (cell.cellEvent == "AgentTrace")
            {
                return cell;
            }
        }
        return null;
    }

    IEnumerator ReturnToPatrol()
    {
        Debug.Log("Returning to patrol");
        GoBackToSpawn();
        StartCoroutine(ReturnToSpawn());
        yield break;
    }

    void GoBackToSpawn()
    {
        //get a random cell with range to set as target
        Cell targetCell = startCell;
        if (targetCell != null)
        {
            path = FindPath(currentCell, targetCell);
        }
    }

    IEnumerator ReturnToSpawn()
    {
        Debug.Log("Returning to spawn");
        while (!isDead & !isChasing)
        {
            if (path != null && path.Count > 0)
            {
                foreach (Cell cell in path)
                {
                    yield return MoveToCell(cell);
                }
            }
            else
            {
                GenerateNewPatrolPath();
                StartCoroutine(FollowPath());
            }
        }
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
        //prevent double input.
        if (isMoving)
            yield break;

        isMoving = true;

        Vector3 startPos = transform.position;
        Vector3 endPos = targetCell.transform.position;

        // Leave a trace in the current cell before moving
        LeaveTrace(currentCell, "EnemyTrace");

        float elapsedTime = 0f;
        while (elapsedTime < 1f / moveSpeed)
        {
            elapsedTime += Time.deltaTime * moveSpeed;
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime);
            yield return null;
        }

        transform.position = endPos; //update position.
        currentCell = targetCell; // Update current cell after moving
        isMoving = false;
    }

    void LeaveTrace(Cell cell, string traceType)
    {
        //set cell event as trace for Agent to pick up.
        if (cell != null)
        {
            Debug.Log("Leaving trace - Enemy");
            cell.cellEvent = traceType;
            StartCoroutine(ClearTraceAfterDelay(cell));
        }
    }

    //Clear the trace after a delay
    IEnumerator ClearTraceAfterDelay(Cell cell)
    {
        yield return new WaitForSeconds(traceDuration);
        if (cell != null && cell.cellEvent == "EnemyTrace")
        {
            cell.cellEvent = "None";
        }
    }

    /////////////
    /// A* pathfinding logic (made by chatgpt - to be honest, i only get the theory behind it but not the intricacy of the code itself) <summary>
    /////////////
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

    //Visualisation gizmos
    private void OnDrawGizmos()
    {
        if (!isDead)
        {
            if (gridManager != null)
            {
                foreach (Cell cell in gridManager.grid)
                {
                    if (cell.cellEvent == "EnemyTrace")
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(cell.transform.position, 0.16f);
                    }
                }
            }

            if (path != null)
            {
                Gizmos.color = Color.red;

                foreach (Cell cell in path)
                {
                    Gizmos.DrawSphere(cell.transform.position, 0.15f);
                }
            }
        }
    }
}
