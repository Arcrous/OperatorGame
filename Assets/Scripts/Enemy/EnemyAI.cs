using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f;

    [Range(1, 10)] // Limits how far the enemy can move from its starting position
    public int patrolRange = 3;

    public float traceDuration = 5f;

    private Cell currentCell; // Tracks the enemy's current cell
    private List<Cell> path; // Current path for patrol
    private List<Cell> walkableCells;
    private bool isMoving;
    [SerializeField] private bool isFollowingPath = false;
    private bool isDead = false;
    public bool seenTrace = false;
    [SerializeField] private bool isChasing = false;

    private Pathfinding pathfinding;
    [SerializeField] Sprite[] sprites;

    #region Setup region
    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
        }

        pathfinding = new Pathfinding(gridManager);

        AssignRandomSprite(); // Assign a random sprite at the start
    }

    void AssignRandomSprite() //Set a random sprite when spawn (purely cosmetic)
    {
        if (sprites != null && sprites.Length > 0)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                int randomIndex = Random.Range(0, sprites.Length);
                spriteRenderer.sprite = sprites[randomIndex];
            }
        }
        else
        {
            Debug.LogWarning("Sprites array is empty or not assigned.");
        }
    }

    private void Start()
    {
        InitializePatrol();
    }

    public void Die() //Death, simple 
    {
        isDead = true;
        StopAllCoroutines();

        SpriteRenderer spriteRend = this.gameObject.GetComponent<SpriteRenderer>();
        spriteRend.color = Color.red;
        gameObject.transform.Rotate(0f, 0f, 90f, Space.Self);

        Destroy(this.gameObject, 5f);
    }

    void InitializePatrol() //Setup the enemy when it first spawns
    {
        // Set the enemy's starting cell to a random walkable cell avoiding the first 3 rows
        currentCell = GetRandomWalkableCellAvoidingFirstRows(3);
        if (currentCell != null)
        {
            //update location to accomadate for relocating after spawn, so trace works properly.
            transform.position = currentCell.transform.position;

            LeaveTrace(currentCell, "EnemyTrace");

            //gen the path and follow it.
            GenerateNewPatrolPath();
            StartCoroutine(FollowPath());
        }
    }
    #endregion

    #region Patrol logic
    void GenerateNewPatrolPath() //Generate a new path for the enemy
    {
        //get a random cell with range to set as target
        Cell targetCell = GetRandomCellWithinRange(currentCell, patrolRange);
        if (targetCell != null)
        {
            path = pathfinding.FindPath(currentCell, targetCell);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("Generated path is null or empty.");
            }
        }
        else
        {
            Debug.LogWarning("Target cell is null. Cannot generate a patrol path.");
        }
    }

    IEnumerator FollowPath() //Follow the generated path
    {
        if (isFollowingPath) yield break; // Prevent multiple calls to FollowPath
        isFollowingPath = true;

        // Check if the path is valid before proceeding
        int maxRetries = 10;
        int retries = 0;
        while (path == null || path.Count == 0)
        {
            GenerateNewPatrolPath();
            retries++;
            if (retries >= maxRetries)
            {
                Debug.LogError("Failed to generate a valid patrol path after multiple attempts.");
                Invoke("Die", 10f);
                isFollowingPath = false;
                yield break;
            }
        }

        while (!isChasing) // Check if the enemy is chasing the player
        {
            //Check if the path is not empty and contains valid cells, if not the move along it
            if (path != null && path.Count > 0)
            {
                if (!isChasing)
                {
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (path[i] == null) continue; // Skip null cells
                        yield return MoveToCell(path[i]);

                        // Check for a trace ahead.
                        if (IsThereAgentTraceAhead(currentCell, i))
                        {
                            isChasing = true;
                            isFollowingPath = false;
                            StartCoroutine(ChaseTrace());
                            yield break;
                        }
                    }
                }

                if (!isChasing)
                {
                    for (int i = path.Count - 1; i >= 0; i--)
                    {
                        if (path[i] == null) continue; // Skip null cells
                        yield return MoveToCell(path[i]);

                        // Check for a trace ahead.
                        if (IsThereAgentTraceAhead(currentCell, i))
                        {
                            isChasing = true;
                            isFollowingPath = false;
                            StartCoroutine(ChaseTrace());
                            yield break;
                        }
                    }
                }

                yield return new WaitForSeconds(1f); // Wait for a second before starting the loop again
                //Generate a new patrol path after completing the loop
                GenerateNewPatrolPath();
            }
            else
            {
                Debug.LogWarning("EnemyAI: No valid path found. Regenerating...");
                GenerateNewPatrolPath();
            }
        }
    }
    #endregion

    #region Chase logic
    //Check if the cells ahead for AgentTrace, use a bool function to return true or false,
    bool IsThereAgentTraceAhead(Cell currentCell, int currentIndex)
    {
        int endIndex = Mathf.Min(currentIndex + 2, path.Count);

        for (int i = currentIndex + 1; i < endIndex; i++)
        {
            Cell nextCell = path[i];
            if (nextCell.cellEvent == "AgentTrace")
            {
                seenTrace = true;
                return true;
            }
        }

        return false;
    }

    // Helper method: check immediate neighbors (up, down, left, right)
    // Returns a neighbor cell containing a trace if found, null otherwise.
    Cell FindAdjacentTrace()
    {
        int[,] directions = new int[,] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        for (int i = 0; i < 4; i++)
        {
            int newX = currentCell.x + directions[i, 0];
            int newY = currentCell.y + directions[i, 1];
            if (newX >= 0 && newX < gridManager.width && newY >= 0 && newY < gridManager.height)
            {
                Cell neighbour = gridManager.grid[newX, newY];
                if (neighbour.cellEvent == "AgentTrace")
                {
                    return neighbour;
                }
            }
        }
        return null;
    }

    // Helper method: scan the grid for the nearest cell with a trace based on Manhattan distance.
    Cell FindNearestTraceCell()
    {
        Cell nearest = null;
        int minDistance = int.MaxValue;
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                Cell cell = gridManager.grid[x, y];
                if (cell.cellEvent == "AgentTrace")
                {
                    int distance = Mathf.Abs(cell.x - currentCell.x) + Mathf.Abs(cell.y - currentCell.y);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = cell;
                    }
                }
            }
        }
        return nearest;
    }

    // The chase coroutine: once triggered, the enemy will continuously follow traces.
    IEnumerator ChaseTrace()
    {
        Debug.Log("Chase initiated.");
        while (isChasing)
        {
            // First, check if any adjacent cell contains a trace.
            Cell adjacentTrace = FindAdjacentTrace();
            if (adjacentTrace != null)
            {
                Debug.Log("Chasing adjacent trace.");
                yield return MoveToCell(adjacentTrace);
            }
            else
            {
                // Otherwise, search the entire grid for the nearest trace.
                Cell targetTrace = FindNearestTraceCell();
                if (targetTrace != null)
                {
                    List<Cell> chasePath = pathfinding.FindPath(currentCell, targetTrace);
                    if (chasePath != null && chasePath.Count > 0)
                    {
                        foreach (Cell cell in chasePath)
                        {
                            yield return MoveToCell(cell);
                        }
                    }
                    else
                    {
                        Debug.Log("No valid path to trace found.");
                        isChasing = false;
                        break;
                    }
                }
                else
                {
                    // If no trace is found anywhere, stop chasing and return to patrol.
                    Debug.Log("No trace found, returning to patrol.");
                    isChasing = false;
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f); // Small delay to avoid a tight loop.
        }
        // Resume patrol behavior after chasing.
        path.Clear(); // Clear the path to avoid confusion
        GenerateNewPatrolPath(); // Generate a new patrol path
        StartCoroutine(FollowPath());
    }
    //if true, stop patrol and start chasing the player by following traces. Once reaching the targeted cell containing a trace, check if there's any other trace in the vicinity, if so, follow it. If not, return to patrol.

    //This function makes the enemy return to patrol
    #endregion

    #region Movement logic (probably don't need further editing)

    // Move the enemy to the target cell, this method handles movement
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
    #endregion

    #region Trace related stuff
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
    #endregion

    #region Setup related stuff
    void CacheWalkableCells()
    {
        walkableCells = new List<Cell>();
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                Cell cell = gridManager.grid[x, y];
                if (!cell.isWall)
                {
                    walkableCells.Add(cell);
                }
            }
        }
    }

    Cell GetRandomWalkableCellAvoidingFirstRows(int minRows)
    {
        if (walkableCells == null || walkableCells.Count == 0)
        {
            CacheWalkableCells();
        }

        List<Cell> candidates = walkableCells.FindAll(cell => cell.y >= minRows);
        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        Debug.LogError("No valid walkable cells found.");
        return null;
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

        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        Debug.LogWarning("No valid cells found within range.");
        return null;
    }
    #endregion

    #region Visualization related stuff
    //Visualisation gizmos
    private void OnDrawGizmos()
    {
        if (!isDead)
        {
            if (path != null)
            {
                Gizmos.color = Color.red;

                foreach (Cell cell in path)
                {
                    Gizmos.DrawSphere(cell.transform.position, 0.15f);
                }
            }

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
        }
    }
    #endregion
}
