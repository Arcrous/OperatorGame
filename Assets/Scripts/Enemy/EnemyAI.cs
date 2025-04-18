using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    Chaser,   // Current implementation - follows traces directly
    Ambusher, // Tries to predict and get ahead of the player
    Patroller, // More erratic movement, patrols specific patterns
    Wanderer  // Sometimes chases, sometimes runs away
}
public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager;
    public float moveSpeed = 1f;
    public EnemyType enemyType = EnemyType.Chaser; // Default to chaser
    public SpriteRenderer typeDiamond; //Color coded diamond for enemy type

    [Range(1, 10)] // Limits how far the enemy can move from its starting position
    public int patrolRange = 3;

    public float traceDuration = 5f;

    // Behavior-specific settings
    [Header("Behavior Settings")]
    [Tooltip("How far ahead the Ambusher tries to get")]
    public int ambushLookAhead = 4;

    [Tooltip("How many cells the Patroller considers for its patterns")]
    public int patrollerPatternSize = 8;

    [Tooltip("Chance the Wanderer will chase (0-1)")]
    [Range(0, 1)]
    public float wandererChaseChance = 0.5f;

    [Tooltip("How far away the Wanderer tries to stay when not chasing")]
    public int wandererFleeDistance = 5;

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

    // For tracking trace direction
    private Cell lastTraceCell;
    private Vector2Int traceDirection = Vector2Int.zero;

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
        gameObject.transform.Rotate(0f, 0f, -90f, Space.Self);

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
                            StartCoroutine(ChaseBasedOnEnemyType());
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

    #region Behavior Patterns logic
    IEnumerator ChaseBasedOnEnemyType()
    {
        switch (enemyType)
        {
            case EnemyType.Chaser:
                yield return StartCoroutine(ChaserBehavior());
                break;
            case EnemyType.Ambusher:
                yield return StartCoroutine(AmbusherBehavior());
                break;
            case EnemyType.Patroller:
                yield return StartCoroutine(PatrollerBehavior());
                break;
            case EnemyType.Wanderer:
                yield return StartCoroutine(WandererBehavior());
                break;
        }
    }

    // Original chase behavior (Chaser type)
    IEnumerator ChaserBehavior()
    {
        Debug.Log("Chaser behavior initiated.");
        while (isChasing)
        {
            // First, check if any adjacent cell contains a trace.
            Cell adjacentTrace = FindAdjacentTrace();
            if (adjacentTrace != null)
            {
                Debug.Log("Chasing adjacent trace.");
                yield return MoveToCell(adjacentTrace);

                // Update trace direction
                UpdateTraceDirection(adjacentTrace);
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

                            // If we encounter a trace, update the direction
                            if (cell.cellEvent == "AgentTrace")
                            {
                                UpdateTraceDirection(cell);
                            }
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
        ResumePatrol();
    }

    // Ambusher behavior - tries to get ahead of the player by predicting movement
    IEnumerator AmbusherBehavior()
    {
        Debug.Log("Ambusher behavior initiated.");
        while (isChasing)
        {
            // Determine the trace direction if we can
            DetermineTraceDirection();

            if (traceDirection != Vector2Int.zero)
            {
                // Try to get ahead of the player by predicting their movement
                Cell targetCell = PredictPlayerPosition();

                if (targetCell != null)
                {
                    List<Cell> ambushPath = pathfinding.FindPath(currentCell, targetCell);
                    if (ambushPath != null && ambushPath.Count > 0)
                    {
                        foreach (Cell cell in ambushPath)
                        {
                            yield return MoveToCell(cell);

                            // If we encounter a trace, update our direction understanding
                            if (cell.cellEvent == "AgentTrace")
                            {
                                UpdateTraceDirection(cell);
                            }
                        }
                    }
                }
                else
                {
                    // Fall back to chaser behavior if prediction fails
                    yield return ChaserBehaviorOneStep();
                }
            }
            else
            {
                // If we can't determine direction, use standard chaser behavior until we can
                yield return ChaserBehaviorOneStep();
            }

            yield return new WaitForSeconds(0.1f);

            // Check if we've lost the trail
            if (!AnyTracesRemaining())
            {
                Debug.Log("Ambusher lost the trail, returning to patrol.");
                isChasing = false;
                break;
            }
        }
        ResumePatrol();
    }

    // Patroller behavior - moves in specific patterns around traces
    IEnumerator PatrollerBehavior()
    {
        Debug.Log("Patroller behavior initiated.");

        while (isChasing)
        {
            Cell nearestTrace = FindNearestTraceCell();
            if (nearestTrace != null)
            {
                // Create a constrained pattern around the trace
                List<Cell> patternCells = CreateConstrainedPatternAroundTrace(nearestTrace);

                if (patternCells != null && patternCells.Count > 0)
                {
                    // Follow the pattern, but only for a limited number of steps before reassessing
                    int stepsToFollow = Mathf.Min(3, patternCells.Count);
                    for (int i = 0; i < stepsToFollow; i++)
                    {
                        if (i < patternCells.Count)
                        {
                            yield return MoveToCell(patternCells[i]);

                            // Check if we've moved to a trace
                            if (patternCells[i].cellEvent == "AgentTrace")
                            {
                                break; // Found a trace, recalculate pattern
                            }
                        }
                    }
                }
                else
                {
                    // If pattern creation fails, use a simpler approach
                    Cell nextTrace = FindNearestTraceCell();
                    if (nextTrace != null)
                    {
                        List<Cell> directPath = pathfinding.FindPath(currentCell, nextTrace);
                        if (directPath != null && directPath.Count > 0)
                        {
                            yield return MoveToCell(directPath[0]);
                        }
                    }
                    else
                    {
                        isChasing = false;
                        break;
                    }
                }
            }
            else
            {
                Debug.Log("Patroller lost the trail, returning to patrol.");
                isChasing = false;
                break;
            }

            yield return new WaitForSeconds(0.15f);
        }
        ResumePatrol();
    }

    // Wanderer behavior - sometimes chases, sometimes runs away
    IEnumerator WandererBehavior()
    {
        Debug.Log("Wanderer behavior initiated.");
        bool isCurrentlyChasing = Random.value < wandererChaseChance;
        float behaviorDuration = Random.Range(3f, 7f);
        float behaviorTimer = 0f;

        while (isChasing)
        {
            behaviorTimer += 0.1f;

            // Change behavior periodically
            if (behaviorTimer > behaviorDuration)
            {
                isCurrentlyChasing = Random.value < wandererChaseChance;
                behaviorDuration = Random.Range(3f, 7f);
                behaviorTimer = 0f;
                Debug.Log("Wanderer now " + (isCurrentlyChasing ? "chasing" : "fleeing"));
            }

            if (isCurrentlyChasing)
            {
                // Use chaser behavior when in chase mode
                yield return ChaserBehaviorOneStep();
            }
            else
            {
                // Run away from traces when in flee mode
                Cell fleeFrom = FindNearestTraceCell();
                if (fleeFrom != null)
                {
                    Cell fleeTarget = FindFleeTarget(fleeFrom);
                    if (fleeTarget != null)
                    {
                        List<Cell> fleePath = pathfinding.FindPath(currentCell, fleeTarget);
                        if (fleePath != null && fleePath.Count > 0)
                        {
                            yield return MoveToCell(fleePath[0]);
                        }
                    }
                }
                else
                {
                    // If no trace to flee from, go back to patrol
                    isChasing = false;
                    break;
                }
            }

            yield return new WaitForSeconds(0.1f);

            // Check occasionally if we should keep chasing
            if (Random.value < 0.05f && !AnyTracesRemaining())
            {
                Debug.Log("Wanderer got bored, returning to patrol.");
                isChasing = false;
                break;
            }
        }
        ResumePatrol();
    }

    // Helper for the chaser behavior to just take one step
    IEnumerator ChaserBehaviorOneStep()
    {
        Cell targetTrace = FindNearestTraceCell();
        if (targetTrace != null)
        {
            List<Cell> chasePath = pathfinding.FindPath(currentCell, targetTrace);
            if (chasePath != null && chasePath.Count > 0)
            {
                yield return MoveToCell(chasePath[0]);

                // If we just moved to a trace, update direction
                if (chasePath[0].cellEvent == "AgentTrace")
                {
                    UpdateTraceDirection(chasePath[0]);
                }
            }
        }
    }

    // Resume patrol after chasing
    void ResumePatrol()
    {
        path.Clear(); // Clear the path to avoid confusion
        GenerateNewPatrolPath(); // Generate a new patrol path
        StartCoroutine(FollowPath());
    }

    //Check if the cells ahead for AgentTrace, use a bool function to return true or false
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
    #endregion

    #region Behavior Helper Methods
    // Update our understanding of trace direction based on seeing new traces
    void UpdateTraceDirection(Cell traceCell)
    {
        if (lastTraceCell != null)
        {
            traceDirection = new Vector2Int(
                traceCell.x - lastTraceCell.x,
                traceCell.y - lastTraceCell.y
            );

            // Normalize to -1, 0, or 1
            if (traceDirection.x != 0) traceDirection.x = traceDirection.x / Mathf.Abs(traceDirection.x);
            if (traceDirection.y != 0) traceDirection.y = traceDirection.y / Mathf.Abs(traceDirection.y);
        }

        lastTraceCell = traceCell;
    }

    // Determine player direction by looking at multiple traces
    void DetermineTraceDirection()
    {
        if (traceDirection != Vector2Int.zero) return; // We already know the direction

        List<Cell> traceCells = FindAllTraces();
        if (traceCells.Count < 2) return;

        // Sort traces by distance to find the freshest ones
        traceCells.Sort((a, b) =>
        {
            int distA = Mathf.Abs(a.x - currentCell.x) + Mathf.Abs(a.y - currentCell.y);
            int distB = Mathf.Abs(b.x - currentCell.x) + Mathf.Abs(b.y - currentCell.y);
            return distA.CompareTo(distB);
        });

        // Try to determine direction from the two closest traces
        if (traceCells.Count >= 2)
        {
            Cell trace1 = traceCells[0];
            Cell trace2 = traceCells[1];

            Vector2Int direction = new Vector2Int(
                trace1.x - trace2.x,
                trace1.y - trace2.y
            );

            // Normalize
            if (direction.x != 0) direction.x = direction.x / Mathf.Abs(direction.x);
            if (direction.y != 0) direction.y = direction.y / Mathf.Abs(direction.y);

            traceDirection = direction;
        }
    }

    // For the Ambusher: predict where the player might be going
    Cell PredictPlayerPosition()
    {
        if (traceDirection == Vector2Int.zero) return null;

        Cell newestTrace = FindNearestTraceCell();
        if (newestTrace == null) return null;

        // Look ahead in the direction of movement
        for (int i = 1; i <= ambushLookAhead; i++)
        {
            int predictedX = newestTrace.x + (traceDirection.x * i);
            int predictedY = newestTrace.y + (traceDirection.y * i);

            // Check if valid cell
            if (predictedX >= 0 && predictedX < gridManager.width &&
                predictedY >= 0 && predictedY < gridManager.height)
            {
                Cell predictedCell = gridManager.grid[predictedX, predictedY];
                if (!predictedCell.isWall)
                {
                    return predictedCell;
                }
            }
        }

        return null;
    }

    // For the Patroller: create a pattern around a trace
    // Create a pattern around a trace that stays within movement constraints
    List<Cell> CreateConstrainedPatternAroundTrace(Cell centerCell)
    {
        List<Cell> patternCells = new List<Cell>();

        // Define a smaller area to patrol around the trace
        int maxPatternDistance = Mathf.Min(patrollerPatternSize, patrolRange);

        // Start position is where the enemy first spotted the trace
        Cell startCell = currentCell;

        // Create a constrained pattern
        for (int dy = -maxPatternDistance; dy <= maxPatternDistance; dy++)
        {
            for (int dx = -maxPatternDistance; dx <= maxPatternDistance; dx++)
            {
                // Skip cells that are too far from the center
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > maxPatternDistance) continue;

                // Calculate position
                int nx = centerCell.x + dx;
                int ny = centerCell.y + dy;

                // Ensure position is within grid bounds
                if (nx >= 0 && nx < gridManager.width && ny >= 0 && ny < gridManager.height)
                {
                    Cell candidate = gridManager.grid[nx, ny];

                    // Check if it's walkable
                    if (!candidate.isWall)
                    {
                        // Check if it's within range of our patrol area
                        int distanceFromStart = Mathf.Abs(candidate.x - startCell.x) + Mathf.Abs(candidate.y - startCell.y);
                        if (distanceFromStart <= patrolRange)
                        {
                            patternCells.Add(candidate);
                        }
                    }
                }
            }
        }

        // If we found valid cells, sort them by proximity to the trace
        if (patternCells.Count > 0)
        {
            // Sort by distance to trace (closest first)
            patternCells.Sort((a, b) =>
            {
                int distA = Mathf.Abs(a.x - centerCell.x) + Mathf.Abs(a.y - centerCell.y);
                int distB = Mathf.Abs(b.x - centerCell.x) + Mathf.Abs(b.y - centerCell.y);
                return distA.CompareTo(distB);
            });

            // Add some randomness by shuffling cells at similar distances
            for (int i = 0; i < patternCells.Count - 1; i++)
            {
                Cell a = patternCells[i];
                Cell b = patternCells[i + 1];

                int distA = Mathf.Abs(a.x - centerCell.x) + Mathf.Abs(a.y - centerCell.y);
                int distB = Mathf.Abs(b.x - centerCell.x) + Mathf.Abs(b.y - centerCell.y);

                if (distA == distB && Random.value > 0.5f)
                {
                    patternCells[i] = b;
                    patternCells[i + 1] = a;
                }
            }
        }

        return patternCells;
    }

    // For the Wanderer: find a cell to flee to
    Cell FindFleeTarget(Cell fleeFrom)
    {
        // Get direction away from trace
        Vector2Int fleeDirection = new Vector2Int(
            currentCell.x - fleeFrom.x,
            currentCell.y - fleeFrom.y
        );

        // Normalize
        if (fleeDirection.x != 0) fleeDirection.x = fleeDirection.x / Mathf.Abs(fleeDirection.x);
        if (fleeDirection.y != 0) fleeDirection.y = fleeDirection.y / Mathf.Abs(fleeDirection.y);

        // Try to find a cell in that direction
        for (int distance = 1; distance <= wandererFleeDistance; distance++)
        {
            int targetX = currentCell.x + (fleeDirection.x * distance);
            int targetY = currentCell.y + (fleeDirection.y * distance);

            if (targetX >= 0 && targetX < gridManager.width &&
                targetY >= 0 && targetY < gridManager.height)
            {
                Cell fleeCell = gridManager.grid[targetX, targetY];
                if (!fleeCell.isWall)
                {
                    return fleeCell;
                }
            }
        }

        // If we can't flee in that direction, pick a random walkable cell
        return GetRandomCellWithinRange(currentCell, wandererFleeDistance);
    }

    // Get all traces in the grid
    List<Cell> FindAllTraces()
    {
        List<Cell> traces = new List<Cell>();

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                Cell cell = gridManager.grid[x, y];
                if (cell.cellEvent == "AgentTrace")
                {
                    traces.Add(cell);
                }
            }
        }

        return traces;
    }

    // Check if there are any traces left to follow
    bool AnyTracesRemaining()
    {
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                if (gridManager.grid[x, y].cellEvent == "AgentTrace")
                {
                    return true;
                }
            }
        }
        return false;
    }
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
            TraceManager.Instance.LeaveTrace(cell, traceType, traceDuration);
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

            // Draw behavior-specific visualization
            if (isChasing)
            {
                switch (enemyType)
                {
                    case EnemyType.Ambusher:
                        // Show prediction
                        if (traceDirection != Vector2Int.zero)
                        {
                            Cell predictedCell = PredictPlayerPosition();
                            if (predictedCell != null)
                            {
                                Gizmos.color = Color.cyan;
                                Gizmos.DrawSphere(predictedCell.transform.position, 0.2f);
                            }
                        }
                        break;
                    case EnemyType.Patroller:
                        // Show constrained patrol pattern
                        Cell nearestTrace = FindNearestTraceCell();
                        if (nearestTrace != null)
                        {
                            List<Cell> pattern = CreateConstrainedPatternAroundTrace(nearestTrace);
                            if (pattern != null && pattern.Count > 0)
                            {
                                Gizmos.color = Color.green;

                                // Draw patrol area boundary
                                Gizmos.DrawWireSphere(currentCell.transform.position, patrolRange * 0.75f);

                                // Draw potential movement cells
                                foreach (Cell patternCell in pattern.GetRange(0, Mathf.Min(5, pattern.Count)))
                                {
                                    Gizmos.DrawWireSphere(patternCell.transform.position, 0.2f);
                                }
                            }
                        }
                        break;
                    case EnemyType.Wanderer:
                        // Show flee target if not chasing
                        Cell fleeFrom = FindNearestTraceCell();
                        if (fleeFrom != null)
                        {
                            Cell fleeTarget = FindFleeTarget(fleeFrom);
                            if (fleeTarget != null)
                            {
                                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                                Gizmos.DrawWireCube(fleeTarget.transform.position, new Vector3(0.4f, 0.4f, 0.4f));
                            }
                        }
                        break;
                }
            }
        }
    }
    #endregion
}
