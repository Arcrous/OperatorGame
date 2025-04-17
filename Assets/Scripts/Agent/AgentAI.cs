using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AgentAI : MonoBehaviour
{
    public GridManager gridManager;

    public float moveSpeed = 1f; // Speed of movement between cells
    public float traceDuration = 5f; // Duration for traces to persist
    public int lookAheadCells = 3; // Number of cells to look ahead

    private Cell startCell;
    private Cell exitCell;
    private Cell currentCell;
    private Cell weaponCell;

    int retries = 0;

    private List<Cell> path;
    private bool isMoving;
    [SerializeField] private bool hasWeapon;

    bool isDead = false;
    public bool seenTrace = false;
    public bool foundPath = false;
    public bool returnedToSpawn = false;

    [SerializeField] Sprite hasWeaponSprite;

    #region Setup
    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        //Delay agent pathfinding a bit.
        Invoke("InitializePathfinding", 1.5f);
    }

    void InitializePathfinding()
    {
        //Debug.Log("Agent AI: Initializing pathfinding");

        startCell = gridManager.grid[0, 0];
        exitCell = gridManager.exitCell;
        weaponCell = gridManager.weaponCell;

        //set current cell
        currentCell = startCell;
        LeaveTrace(currentCell);

        List<Cell> pathToExit = FindPath(startCell, exitCell);
        List<Cell> pathToWeapon = FindPath(startCell, weaponCell);

        if (pathToWeapon != null && pathToExit != null && pathToWeapon.Count < pathToExit.Count)
        {
            path = pathToWeapon;
        }
        else
        {
            path = pathToExit;
        }

        //move along the path
        if (path != null && path.Count > 0)
        {
            StartCoroutine(FollowPath());
        }
    }
    #endregion

    #region Main logic loop
    IEnumerator FollowPath()
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (!hasWeapon)
            {
                if (!seenTrace)
                {
                    Cell currentCell = path[i]; //keep track of current cell to recalc from there

                    if (ShouldRecalculatePath(currentCell, i)) //recalculate from current pose if picked up on EnemyTrace
                    {
                        path.Clear();

                        path = ComparePathsFromCurrentCell();

                        if (path != null && path.Count > 0)
                        {
                            moveSpeed = 2f;
                            i = -1; // Reset the index to start following the new path
                            continue;
                        }
                        else
                        {
                            // Fallback to returning to spawn if no path found
                            if (!returnedToSpawn)
                            {
                                StartCoroutine(ReturnToSpawn());
                            }
                            yield break;
                        }
                    }
                    else
                        yield return MoveToCell(currentCell);
                }
                else if (!returnedToSpawn && seenTrace)
                {
                    Cell currentCell = path[i];
                    yield return MoveToCell(currentCell);
                }
            }
            else
            {
                Cell currentCell = path[i];
                yield return MoveToCell(currentCell);
            }
        }

        if (currentCell == weaponCell)
        {
            hasWeapon = true;
            SpriteRenderer spriteRend = this.gameObject.GetComponent<SpriteRenderer>();
            spriteRend.sprite = hasWeaponSprite;

            path.Clear();
            StartCoroutine(RipAndTear());
            yield break;
        }

        if (currentCell == startCell)
        {
            Debug.Log("Here we go again");
            returnedToSpawn = true;
            moveSpeed = 1f;
            path.Clear();
            StartCoroutine(SearchUntilFoundFromSpawn());
            yield break;
        }

        if (currentCell == exitCell)
        {
            Invoke("ReloadScene", 5f);
            Debug.Log("Agent AI: Reached the exit!");
        }
    }

    //Once back at spawn, search for a new path to exit or weapon
    IEnumerator SearchUntilFoundFromSpawn()
    {
        returnedToSpawn = false;
        yield return new WaitForSeconds(1f);
        List<Cell> pathToExit = FindPath(startCell, exitCell);
        List<Cell> pathToWeapon = FindPath(startCell, weaponCell);

        if (pathToWeapon != null && pathToExit != null)
        {
            if (pathToWeapon.Count < pathToExit.Count)
            {
                path = pathToWeapon;
            }
            else
            {
                path = pathToExit;
            }

        }

        if (path == null || path.Count == 0)
        {
            retries++;
            Debug.Log("retrying... " + retries);
            if (retries >= 10)
            {
                Debug.LogError("Agent AI: Unable to find a new path after 10 retries!");
                Invoke("ReloadScene", 2f);
                yield break;
            }

            Debug.LogError("Agent AI: Unable to find a new path!");
            StartCoroutine(SearchUntilFoundFromSpawn());
        }
        else
        {
            retries = 0;
            Debug.Log("Found path");
            seenTrace = false;
            StartCoroutine(FollowPath());
        }
    }

    //calculate path from current cell to start/weapon, then compare it
    private List<Cell> ComparePathsFromCurrentCell()
    {
        List<Cell> pathToStart = FindPath(currentCell, startCell);
        List<Cell> pathToWeapon = FindPath(currentCell, weaponCell);

        // Choose the shorter path
        if (pathToWeapon != null && pathToStart != null)
        {
            if (pathToWeapon.Count < pathToStart.Count)
            {
                Debug.Log("Agent: Weapon path is shorter, heading there");
                return pathToWeapon;
            }
            else
            {
                Debug.Log("Agent: Start path is shorter, heading there");
                return pathToStart;
            }
        }
        else if (pathToStart != null)
        {
            Debug.Log("Agent: No path to weapon, heading to start");
            return pathToStart;
        }
        else if (pathToWeapon != null)
        {
            Debug.Log("Agent: No path to start, heading to weapon");
            return pathToWeapon;
        }

        Debug.LogError("Agent: Couldn't find path to either start or weapon!");
        return null;
    }

    //When a weapon is picked up, calculate path to exit from current cell
    IEnumerator RipAndTear()
    {
        yield return new WaitForSeconds(0.3f);
        path = FindPath(currentCell, exitCell);
        //Debug.Log("Until it is done");
        moveSpeed = 1.5f;
        StartCoroutine(FollowPath());
    }
    #endregion

    #region Movement and traces logic
    //movement logic.
    IEnumerator MoveToCell(Cell targetCell)
    {
        //prevent double input
        if (isMoving)
            yield break;

        isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = targetCell.transform.position;
        float journeyTime = 1f / moveSpeed;
        float elapsedTime = 0f;

        LeaveTrace(currentCell); // Leave a trace at the current cell

        while (elapsedTime < journeyTime)
        {
            elapsedTime += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / journeyTime);
            yield return null;
        }

        transform.position = endPos; //update position
        currentCell = targetCell; //update current cell after moving
        isMoving = false;
    }

    void LeaveTrace(Cell cell)
    {
        //set cell event as trace for Enemy to pick up
        if (cell != null || cell.cellEvent == "AgentTrace")
        {
            TraceManager.Instance.LeaveTrace(cell, "AgentTrace", traceDuration);
        }
    }
    #endregion

    #region Fleeing logic
    //Check if the cells ahead for EnemyTrace
    bool ShouldRecalculatePath(Cell currentCell, int currentIndex)
    {
        int endIndex = Mathf.Min(currentIndex + lookAheadCells, path.Count);

        for (int i = currentIndex + 1; i < endIndex; i++)
        {
            Cell nextCell = path[i];
            if (nextCell.cellEvent == "EnemyTrace" || HasAdjacentEnemyTrace(nextCell))
            {
                seenTrace = true;
                return true;
            }
        }

        return false;
    }

    bool HasAdjacentEnemyTrace(Cell cell)
    {
        foreach (Cell neighbor in GetNeighbors(cell))
        {
            if (neighbor.cellEvent == "EnemyTrace")
            {
                return true;
            }
        }

        return false;
    }

    //Set a path to spawn and return there
    IEnumerator ReturnToSpawn()
    {
        yield return new WaitForSeconds(0.3f);
        path = FindPath(currentCell, startCell);
        StartCoroutine(FollowPath());
    }
    #endregion

    #region Interaction with enemies
    //Kills the Agent when touching the enemy (will expand/change in the future)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!hasWeapon)
        {
            if (collision.tag == "Enemy" && !isDead)
            {
                isDead = true;
                StopAllCoroutines();
                //Debug.Log("Agent has died, reloading in 5s");

                SpriteRenderer spriteRend = this.gameObject.GetComponent<SpriteRenderer>();
                spriteRend.color = Color.red;
                gameObject.transform.Rotate(0f, 0f, -90f, Space.Self);

                //Destroy(this.gameObject, 5f);
                Invoke("ReloadScene", 5f);
            }
        }
        else
        {
            if (collision.tag == "Enemy" && !isDead)
            {
                EnemyAI enemy = collision.gameObject.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.Die();
                }
            }
        }
    }
    #endregion

    #region Misc
    void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    //Visualisation gizmos
    private void OnDrawGizmos()
    {
        if (!isDead)
        {
            if (gridManager.grid != null && gridManager.grid != null)
            {
                foreach (Cell cell in gridManager.grid)
                {
                    if (cell.cellEvent == "AgentTrace")
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawSphere(cell.transform.position, 0.16f);
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (path != null)
        {
            Gizmos.color = Color.green;

            foreach (Cell cell in path)
            {
                Gizmos.DrawSphere(cell.transform.position, 0.15f);
            }
        }
    }
    #endregion

    #region Pathfinding
    public List<Cell> FindPath(Cell start, Cell target)
    {
        // Use our custom priority queue
        PriorityQueue<Cell> openSet = new PriorityQueue<Cell>(cell => cell.gCost + cell.hCost);
        openSet.Enqueue(start);

        HashSet<Cell> closedSet = new HashSet<Cell>();
        Dictionary<Cell, int> gCost = new Dictionary<Cell, int>();
        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

        gCost[start] = 0;
        start.gCost = 0;
        start.hCost = CalculateHeuristic(start, target);

        while (openSet.Count > 0)
        {
            Cell current = openSet.Dequeue();

            if (current == target)
            {
                return RetracePath(cameFrom, start, target);
            }

            closedSet.Add(current);

            foreach (Cell neighbor in GetNeighbors(current))
            {
                if (seenTrace && !hasWeapon)
                {
                    if (neighbor.isWall || closedSet.Contains(neighbor) || neighbor.cellEvent == "EnemyTrace")
                        continue;
                }
                else
                {
                    if (neighbor.isWall || closedSet.Contains(neighbor))
                        continue;
                }

                int tentativeGCost = gCost[current] + 1;

                if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor])
                {
                    gCost[neighbor] = tentativeGCost;
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = CalculateHeuristic(neighbor, target);
                    cameFrom[neighbor] = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor);
                }
            }
        }

        return null;
    }

    public Cell GetLowestFCostCell(List<Cell> openSet, Dictionary<Cell, int> gCost, Dictionary<Cell, int> hCost)
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

    public List<Cell> RetracePath(Dictionary<Cell, Cell> cameFrom, Cell start, Cell target)
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

    public List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new List<Cell>();

        // Check the four cardinal directions (up, down, left, right)
        int[,] directions = new int[,] { { 0, 1 }, { 0, -1 }, { 1, 0 }, { -1, 0 } };

        for (int i = 0; i < directions.GetLength(0); i++)
        {
            int nx = cell.x + directions[i, 0];
            int ny = cell.y + directions[i, 1];

            if (nx >= 0 && nx < gridManager.width && ny >= 0 && ny < gridManager.height)
            {
                Cell neighbor = gridManager.grid[nx, ny];
                if (!neighbor.isWall)
                {
                    neighbors.Add(neighbor);
                }
            }
        }

        return neighbors;
    }

    public int CalculateHeuristic(Cell a, Cell b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
    #endregion
}
