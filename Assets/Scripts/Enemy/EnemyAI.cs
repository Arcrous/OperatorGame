using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f;
    public int patrolRange = 5; // Limits how far the enemy can move from its starting position
    public float traceDuration = 5f;

    private Cell currentCell; // Tracks the enemy's current cell
    [SerializeField]  private Cell startCell; // Tracks the enemy's starting cell
    private List<Cell> path; // Current path for patrol
    private bool isMoving;
    private bool isDead = false;
    public bool seenTrace = false;
    [SerializeField] private bool isChasing = false;
    [SerializeField] bool foundReturn;
    [SerializeField] bool returned;

    private Pathfinding pathfinding;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();

        pathfinding = new Pathfinding(gridManager);
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
            //Debug.LogError("EnemyAI: Failed to find a valid starting cell.");
        }
    }

    void GenerateNewPatrolPath()
    {
        //get a random cell with range to set as target
        Cell targetCell = GetRandomCellWithinRange(currentCell, patrolRange);
        if (targetCell != null)
        {
            path = pathfinding.FindPath(currentCell, targetCell);
        }
    }

    IEnumerator FollowPath()
    {
        while (!isDead && !isChasing)
        {
            if (path != null && path.Count > 0)
            {
                for (int i = 0; i < path.Count; i++)
                    yield return MoveToCell(path[i]);

                for (int i = path.Count - 1; i >= 0; i--)
                    yield return MoveToCell(path[i]);

                // Generate a new patrol path after completing the loop
                GenerateNewPatrolPath();
            }
            else
            {
                //Debug.LogWarning("EnemyAI: No valid path found. Regenerating...");
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
                if (!isChasing)
                {
                    Debug.Log("Enemy detected Agent's trace! Chasing...");
                    isChasing = true;
                    StopCoroutine(FollowPath());
                    StartCoroutine(ChaseAgent());
                }
            }
            else if (isChasing)
            {
                yield return new WaitForSeconds(5f); // Add delay before returning to patrol
                Debug.Log("Lost Agent's trace. Waiting before returning to patrol.");

                isChasing = false;
                StartCoroutine(ReturnToPatrol());
                //Debug.Log("Lost Agent's trace");
            }
            yield return new WaitForSeconds(1f);
        }
    }

    bool DetectAgentTrace()
    {
        foreach (Cell neighbor in pathfinding.GetNeighbors(currentCell))
        {
            if (neighbor.cellEvent == "AgentTrace")
            {
                //Debug.Log("Detected Agent trace");
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
                path = pathfinding.FindPath(currentCell, targetCell);
                if (path != null && path.Count > 0)
                {
                    foreach (Cell cell in path)
                    {
                        yield return MoveToCell(cell); // Ensure step-by-step movement
                    }
                }
            }
            yield return new WaitForSeconds(0.2f); // Prevent infinite loops
        }
    }

    Cell FindNearestAgentTrace()
    {
        //Debug.Log("Finding nearest Agent trace");
        Cell closestTrace = null;
        int minDistance = 1; // Limit to patrol range

        foreach (Cell cell in gridManager.grid)
        {
            int distanceFromStart = pathfinding.CalculateHeuristic(startCell, cell);
            if (cell.cellEvent == "AgentTrace" && distanceFromStart <= patrolRange)
            {
                int distance = pathfinding.CalculateHeuristic(currentCell, cell);
                if (distance <= minDistance)
                {
                    minDistance = distance;
                    closestTrace = cell;
                }
            }
        }

        return closestTrace;
    }

    IEnumerator ReturnToPatrol()
    {
        yield return new WaitForSeconds(2f); // Delay before returning to patrol
        Debug.Log("Returning to patrol");
        GoBackToSpawn();
        isChasing = false;
    }

    void GoBackToSpawn()
    {
        //get a random cell with range to set as target
        Cell targetCell = startCell;
        if (targetCell != null)
        {
            path = pathfinding.FindPath(currentCell, targetCell);
            if (path != null && path.Count > 0)
            {
                foundReturn = true;
            }
        }

        if(foundReturn)
        {
           StartCoroutine(ReturnToSpawn());
        }            
    }

    IEnumerator ReturnToSpawn()
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("No return path. Reinitializing patrol.");
            GenerateNewPatrolPath();
            StartCoroutine(FollowPath());
            yield break;
        }

        foreach (Cell cell in path)
        {
            yield return MoveToCell(cell);
        }

        returned = true;
        StartCoroutine(FollowPath());
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
        if (isMoving) yield break;

        isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = targetCell.transform.position;
        float journeyTime = 1f / moveSpeed;
        float elapsedTime = 0f;

        LeaveTrace(currentCell, "EnemyTrace");

        while (elapsedTime < journeyTime)
        {
            elapsedTime += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / journeyTime);
            yield return null;
        }

        transform.position = endPos;
        currentCell = targetCell;
        isMoving = false;
    }

    void LeaveTrace(Cell cell, string traceType)
    {
        //set cell event as trace for Agent to pick up.
        if (cell != null)
        {
            //Debug.Log("Leaving trace - Enemy");
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
