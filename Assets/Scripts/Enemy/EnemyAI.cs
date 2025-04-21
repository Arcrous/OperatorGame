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
    public int detectionRadius;
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

    // Add these fields to your EnemyAI class
    [Header("Debug Visualization")]
    public bool showDetectionRanges = true;
    public bool showDetectedTraces = true;
    private List<Cell> currentlyDetectedTraces = new List<Cell>();
    private float lastDetectionVisualizationTime = 0f;

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

    // Make sure to add an Update method to refresh the visualization regularly
    void Update()
    {
        // Refresh detected traces for visualization purposes if we're chasing
        if (isChasing && showDetectedTraces)
        {
            // Use the type-specific detection
            DetectTracesBasedOnType();
        }
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

        float periodicCheckTimer = 0f;
        float periodicCheckInterval = 0.5f; // Check every half second

        while (!isChasing) // Check if the enemy is chasing the player
        {
            // Periodic trace detection regardless of path position
            periodicCheckTimer += Time.deltaTime;
            if (periodicCheckTimer >= periodicCheckInterval)
            {
                periodicCheckTimer = 0f;
                if (PeriodicTraceCheck())
                {
                    // We detected a trace and started chasing
                    isFollowingPath = false;
                    yield break;
                }
            }

            //Check if the path is not empty and contains valid cells, if not then move along it
            if (path != null && path.Count > 0)
            {
                if (!isChasing)
                {
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (path[i] == null) continue; // Skip null cells
                        yield return MoveToCell(path[i]);

                        // Check for a trace ahead
                        if (IsThereAgentTraceAhead(currentCell, i))
                        {
                            isChasing = true;
                            isFollowingPath = false;
                            StartCoroutine(ChaseBasedOnEnemyType());
                            yield break;
                        }

                        // Periodically check again during movement
                        if (PeriodicTraceCheck())
                        {
                            isFollowingPath = false;
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

        isFollowingPath = false;
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
        float detectionTimer = 0f;
        float detectionInterval = 0.3f; // Check frequently

        while (isChasing)
        {
            // Periodically scan for new traces while chasing
            detectionTimer += 0.1f;
            if (detectionTimer >= detectionInterval)
            {
                detectionTimer = 0f;
                List<Cell> freshTraces = DetectTracesInRadius(3);

                if (freshTraces.Count > 0)
                {
                    // Sort by closest
                    freshTraces.Sort((a, b) => {
                        int distA = Mathf.Abs(a.x - currentCell.x) + Mathf.Abs(a.y - currentCell.y);
                        int distB = Mathf.Abs(b.x - currentCell.x) + Mathf.Abs(b.y - currentCell.y);
                        return distA.CompareTo(distB);
                    });

                    // If there's a trace right next to us, move to it directly
                    foreach (Cell traceCell in freshTraces)
                    {
                        int distance = Mathf.Abs(traceCell.x - currentCell.x) + Mathf.Abs(traceCell.y - currentCell.y);
                        if (distance <= 1)
                        {
                            yield return MoveToCell(traceCell);
                            UpdateTraceDirection(traceCell);
                            break;
                        }
                    }

                    // If no adjacent trace was found, pathfind to the nearest one
                    Cell targetTrace = freshTraces[0];
                    List<Cell> chasePath = pathfinding.FindPath(currentCell, targetTrace);
                    if (chasePath != null && chasePath.Count > 0)
                    {
                        yield return MoveToCell(chasePath[0]);

                        // If we moved to a trace, update direction
                        if (chasePath[0].cellEvent == "AgentTrace")
                        {
                            UpdateTraceDirection(chasePath[0]);
                        }
                    }
                }
                else
                {
                    // Use the original chase method as fallback
                    Cell adjacentTrace = FindAdjacentTrace();
                    if (adjacentTrace != null)
                    {
                        yield return MoveToCell(adjacentTrace);
                        UpdateTraceDirection(adjacentTrace);
                    }
                    else
                    {
                        Cell targetTrace = FindNearestTraceCell();
                        if (targetTrace != null)
                        {
                            List<Cell> chasePath = pathfinding.FindPath(currentCell, targetTrace);
                            if (chasePath != null && chasePath.Count > 0)
                            {
                                yield return MoveToCell(chasePath[0]);
                            }
                        }
                        else
                        {
                            // No traces found anywhere, stop chasing
                            Debug.Log("No trace found, returning to patrol.");
                            isChasing = false;
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        // Resume patrol behavior after chasing
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
                    // Instead of following the pattern directly, move one step at a time
                    // toward each pattern cell in sequence
                    for (int i = 0; i < Mathf.Min(3, patternCells.Count); i++)
                    {
                        Cell targetCell = patternCells[i];
                        // Find path to this cell
                        List<Cell> pathToTarget = pathfinding.FindPath(currentCell, targetCell);

                        if (pathToTarget != null && pathToTarget.Count > 0)
                        {
                            // Move only one step along the path
                            yield return MoveToCell(pathToTarget[0]);

                            // Check if we've moved to a trace
                            if (currentCell.cellEvent == "AgentTrace")
                            {
                                break; // Found a trace, recalculate pattern
                            }
                        }
                        else
                        {
                            // If we can't find a path, try the next cell
                            continue;
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
    #endregion

    #region Behavior Helper Methods
    // Check for traces in a wider area around the enemy
    public List<Cell> DetectTracesInRadius(int radius)
    {
        List<Cell> foundTraces = new List<Cell>();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int distance = Mathf.Abs(dx) + Mathf.Abs(dy); // Manhattan distance
                if (distance <= radius) // Only within radius
                {
                    int checkX = currentCell.x + dx;
                    int checkY = currentCell.y + dy;

                    // Check bounds
                    if (checkX >= 0 && checkX < gridManager.width &&
                        checkY >= 0 && checkY < gridManager.height)
                    {
                        Cell checkCell = gridManager.grid[checkX, checkY];
                        if (checkCell != null && checkCell.cellEvent == "AgentTrace")
                        {
                            foundTraces.Add(checkCell);
                        }
                    }
                }
            }
        }

        // Update our visualization list
        currentlyDetectedTraces = foundTraces;

        return foundTraces;
    }

    // Detect trace based on enemy type - different enemies can have different detection ranges
    public List<Cell> DetectTracesBasedOnType()
    {
        // Different enemy types have different detection capabilities
        switch (enemyType)
        {
            case EnemyType.Ambusher:
                detectionRadius = 5; // Ambushers have the longest detection range
                break;
            case EnemyType.Chaser:
                detectionRadius = 3; // Standard detection range
                break;
            case EnemyType.Patroller:
                detectionRadius = 2; // Limited detection range but systematic movement
                break;
            case EnemyType.Wanderer:
                // Wanderers have variable detection based on their current mood
                detectionRadius = Random.value < wandererChaseChance ? 4 : 2;
                break;
            default:
                detectionRadius = 3;
                break;
        }

        return DetectTracesInRadius(detectionRadius);
    }

    // Helper method for periodic trace detection during any behavior
    public bool PeriodicTraceCheck()
    {
        // Use the type-specific detection
        List<Cell> nearbyTraces = DetectTracesBasedOnType();

        if (nearbyTraces.Count > 0)
        {
            // We detected traces! Sort by closest
            nearbyTraces.Sort((a, b) => {
                int distA = Mathf.Abs(a.x - currentCell.x) + Mathf.Abs(a.y - currentCell.y);
                int distB = Mathf.Abs(b.x - currentCell.x) + Mathf.Abs(b.y - currentCell.y);
                return distA.CompareTo(distB);
            });

            // Store the closest trace for later use
            Cell closestTrace = nearbyTraces[0];
            lastTraceCell = closestTrace;

            // If we weren't already chasing, start now
            if (!isChasing)
            {
                isChasing = true;
                isFollowingPath = false;
                StartCoroutine(ChaseBasedOnEnemyType());
            }

            return true;
        }

        return false;
    }
    // Helper for the chaser behavior to just take one step
    IEnumerator ChaserBehaviorOneStep()
    {
        // First check nearby traces with improved detection
        List<Cell> nearbyTraces = DetectTracesInRadius(2);

        if (nearbyTraces.Count > 0)
        {
            // Sort by closest
            nearbyTraces.Sort((a, b) => {
                int distA = Mathf.Abs(a.x - currentCell.x) + Mathf.Abs(a.y - currentCell.y);
                int distB = Mathf.Abs(b.x - currentCell.x) + Mathf.Abs(b.y - currentCell.y);
                return distA.CompareTo(distB);
            });

            Cell closestTrace = nearbyTraces[0];
            int distance = Mathf.Abs(closestTrace.x - currentCell.x) + Mathf.Abs(closestTrace.y - currentCell.y);

            if (distance <= 1)
            {
                // If adjacent, move directly
                yield return MoveToCell(closestTrace);
                UpdateTraceDirection(closestTrace);
            }
            else
            {
                // Otherwise pathfind
                List<Cell> path = pathfinding.FindPath(currentCell, closestTrace);
                if (path != null && path.Count > 0)
                {
                    yield return MoveToCell(path[0]);

                    if (path[0].cellEvent == "AgentTrace")
                    {
                        UpdateTraceDirection(path[0]);
                    }
                }
            }
        }
        else
        {
            // Fall back to original behavior
            Cell targetTrace = FindNearestTraceCell();
            if (targetTrace != null)
            {
                List<Cell> chasePath = pathfinding.FindPath(currentCell, targetTrace);
                if (chasePath != null && chasePath.Count > 0)
                {
                    yield return MoveToCell(chasePath[0]);

                    if (chasePath[0].cellEvent == "AgentTrace")
                    {
                        UpdateTraceDirection(chasePath[0]);
                    }
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
        // First use the original method to check directly on the path
        int endIndex = Mathf.Min(currentIndex + 2, path.Count);

        for (int i = currentIndex + 1; i < endIndex; i++)
        {
            Cell nextCell = path[i];
            if (nextCell.cellEvent == "AgentTrace")
            {
                seenTrace = true;
                lastTraceCell = nextCell; // Remember this trace
                return true;
            }
        }

        // Now also check for traces in vicinity of the path
        for (int i = currentIndex + 1; i < endIndex; i++)
        {
            Cell pathCell = path[i];

            // Look at cells in small radius around this path cell
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    // Skip the center cell (already checked)
                    if (dx == 0 && dy == 0) continue;

                    int checkX = pathCell.x + dx;
                    int checkY = pathCell.y + dy;

                    // Check bounds
                    if (checkX >= 0 && checkX < gridManager.width &&
                        checkY >= 0 && checkY < gridManager.height)
                    {
                        Cell checkCell = gridManager.grid[checkX, checkY];
                        if (checkCell.cellEvent == "AgentTrace")
                        {
                            seenTrace = true;
                            lastTraceCell = checkCell; // Remember this trace
                            return true;
                        }
                    }
                }
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
        // Find cells that are reachable from our current position
        List<Cell> reachableCells = FindReachableCellsWithinSteps(currentCell, patrolRange);

        // Filter to cells that are near the trace
        List<Cell> patternCells = reachableCells.FindAll(cell =>
            Mathf.Abs(cell.x - centerCell.x) + Mathf.Abs(cell.y - centerCell.y) <= patrollerPatternSize);

        // Sort by distance to trace
        patternCells.Sort((a, b) =>
        {
            int distA = Mathf.Abs(a.x - centerCell.x) + Mathf.Abs(a.y - centerCell.y);
            int distB = Mathf.Abs(b.x - centerCell.x) + Mathf.Abs(b.y - centerCell.y);
            return distA.CompareTo(distB);
        });

        return patternCells;
    }

    // Instead of checking a square area, use BFS to find all cells within N steps
    List<Cell> FindReachableCellsWithinSteps(Cell start, int maxSteps)
    {
        List<Cell> reachableCells = new List<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();
        Queue<(Cell cell, int steps)> queue = new Queue<(Cell, int)>();

        queue.Enqueue((start, 0));
        visited.Add(start);

        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { 1, 0, -1, 0 };

        while (queue.Count > 0)
        {
            var (current, steps) = queue.Dequeue();

            if (steps <= maxSteps)
            {
                reachableCells.Add(current);

                if (steps < maxSteps)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + dx[i];
                        int ny = current.y + dy[i];

                        if (nx >= 0 && nx < gridManager.width && ny >= 0 && ny < gridManager.height)
                        {
                            Cell next = gridManager.grid[nx, ny];
                            if (!next.isWall && !visited.Contains(next))
                            {
                                queue.Enqueue((next, steps + 1));
                                visited.Add(next);
                            }
                        }
                    }
                }
            }
        }

        return reachableCells;
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
        if (!Application.isPlaying || !isDead)
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

            // Draw detection radius based on enemy type if enabled
            if (showDetectionRanges && currentCell != null)
            {
                // Different colors for different enemy types
                switch (enemyType)
                {
                    case EnemyType.Chaser:
                        Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Transparent red
                        DrawDetectionRadius(3);
                        break;
                    case EnemyType.Ambusher:
                        Gizmos.color = new Color(0f, 1f, 1f, 0.2f); // Transparent cyan
                        DrawDetectionRadius(5);
                        break;
                    case EnemyType.Patroller:
                        Gizmos.color = new Color(0f, 1f, 0f, 0.2f); // Transparent green
                        DrawDetectionRadius(2);
                        break;
                    case EnemyType.Wanderer:
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Transparent orange
                        DrawDetectionRadius(Random.value < wandererChaseChance ? 4 : 2);
                        break;
                }
            }

            // Highlight currently detected traces
            if (showDetectedTraces && currentlyDetectedTraces != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.8f); // Bright yellow
                foreach (Cell cell in currentlyDetectedTraces)
                {
                    if (cell != null)
                    {
                        Gizmos.DrawWireSphere(cell.transform.position, 0.3f);
                        Gizmos.DrawLine(transform.position, cell.transform.position);
                    }
                }
            }

            // Draw path detection visualization if we're following a path
            if (isFollowingPath && path != null && path.Count > 0 && currentCell != null)
            {
                VisualizePathDetection();
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
    // Draw the detection radius as grid cells
    void DrawDetectionRadius(int radius)
    {
        if (currentCell == null || gridManager == null) return;

        // Avoid creating too much garbage by limiting how often we draw this
        if (Time.time - lastDetectionVisualizationTime < 0.2f) return;
        lastDetectionVisualizationTime = Time.time;

        // Calculate detection area
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int distance = Mathf.Abs(dx) + Mathf.Abs(dy); // Manhattan distance
                if (distance <= radius) // Only within radius
                {
                    int checkX = currentCell.x + dx;
                    int checkY = currentCell.y + dy;

                    // Check bounds
                    if (checkX >= 0 && checkX < gridManager.width &&
                        checkY >= 0 && checkY < gridManager.height)
                    {
                        Cell cell = gridManager.grid[checkX, checkY];
                        if (cell != null)
                        {
                            Vector3 cellPos = cell.transform.position;
                            Vector3 size = new Vector3(0.9f, 0.9f, 0.1f);
                            Gizmos.DrawCube(cellPos, size);
                        }
                    }
                }
            }
        }
    }

    // Visualize path detection
    void VisualizePathDetection()
    {
        int currentIndex = 0;
        // Find our current index in the path
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == currentCell)
            {
                currentIndex = i;
                break;
            }
        }

        int endIndex = Mathf.Min(currentIndex + 2, path.Count);

        // Visualize path ahead detection area
        for (int i = currentIndex + 1; i < endIndex; i++)
        {
            if (i >= path.Count) continue;

            Cell pathCell = path[i];
            if (pathCell == null) continue;

            // Draw the path cell
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red
            Gizmos.DrawCube(pathCell.transform.position, new Vector3(0.8f, 0.8f, 0.1f));

            // Draw adjacency detection
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    // Skip the center cell (already drawn)
                    if (dx == 0 && dy == 0) continue;

                    int adjX = pathCell.x + dx;
                    int adjY = pathCell.y + dy;

                    // Check bounds
                    if (adjX >= 0 && adjX < gridManager.width &&
                        adjY >= 0 && adjY < gridManager.height)
                    {
                        Cell adjCell = gridManager.grid[adjX, adjY];
                        if (adjCell != null)
                        {
                            // Draw adjacent cells with a different color
                            Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.3f); // Light red
                            Gizmos.DrawCube(adjCell.transform.position, new Vector3(0.7f, 0.7f, 0.05f));
                        }
                    }
                }
            }
        }
    }
    #endregion
}
