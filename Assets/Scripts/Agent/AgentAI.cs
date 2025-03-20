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

    private List<Cell> path;
    private bool isMoving;
    [SerializeField] private bool hasWeapon;

    bool isDead = false;
    public bool seenTrace = false;
    public bool foundPath = false;
    public bool returnedToSpawn = false;

    [SerializeField] Sprite hasWeaponSprite;

    [Range(0, 10)]
    public float gameSpeed;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        //Delay agent pathfinding a bit.
        Invoke("InitializePathfinding", 1.5f);
    }

    private void Update()
    {

        Time.timeScale = gameSpeed;
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

        //get the path from start to exit.
        //path = FindPath(startCell, exitCell);

        //move along the path
        if (path != null && path.Count > 0)
        {
            StartCoroutine(FollowPath());
        }
        else
        {
            //Debug.LogError("Agent AI: No path found to the exit!");
        }
    }

    IEnumerator FollowPath()
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (!hasWeapon)
            {
                if (!seenTrace)
                {
                    Cell currentCell = path[i]; //keep track of current cell to recalc from there

                    if (ShouldRecalculatePath(currentCell, i)) //recalc from current pose if picked up on EnemyTrace
                    {
                        path.Clear();
                        if (!returnedToSpawn)
                        {
                            StartCoroutine(ReturnToSpawn());
                        }

                        yield break;
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
            //Debug.Log("Agent AI: Picked up weapon, going to exit");
            path.Clear();
            StartCoroutine(RipAndTear());
            yield break;
        }

        if (!returnedToSpawn && seenTrace)
        {
            returnedToSpawn = true;
            moveSpeed = 1f;
            StartCoroutine(SearchUntilFound());
        }
        //Debug.Log("Agent AI: Reached the exit!");
    }

    //loop until a path is found
    IEnumerator SearchUntilFound()
    {
        returnedToSpawn = false;
        yield return new WaitForSeconds(1f);
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
        //Debug.Log("finding path");

        if(path == null || path.Count == 0)
        {
            //Debug.LogError("Agent AI: Unable to find a new path!");
            StartCoroutine(SearchUntilFound());
        }
        else
        {
            //Debug.Log("Found path");
            seenTrace = false;
            StartCoroutine(FollowPath());
        }
    }

    //calc path from current cell to start/weapon, then compare it

    //calc path to current cell to start
    IEnumerator ReturnToSpawn()
    {
        yield return new WaitForSeconds(0.3f);
        path = FindPath(currentCell, startCell);
        //Debug.Log("Running to start");
        moveSpeed = 2f;
        StartCoroutine(FollowPath());
    }

    //calc path to exit from current cell
    IEnumerator RipAndTear()
    {
        yield return new WaitForSeconds(0.3f);
        path = FindPath(currentCell, exitCell);
        //Debug.Log("Until it is done");
        moveSpeed = 1.5f;
        StartCoroutine(FollowPath());
    }

    //movement logic.
    IEnumerator MoveToCell(Cell targetCell)
    {
        //prevent double input
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

        transform.position = endPos; //update position
        currentCell = targetCell; //update current cell after moving
        isMoving = false;
    }

    void LeaveTrace(Cell cell)
    {
        //set cell event as trace for Enemy to pick up
        if (cell != null)
        {
            //Debug.Log("Leaving trace - Agent");
            cell.cellEvent = "AgentTrace";
            StartCoroutine(ClearTraceAfterDelay(cell));
        }
    }

    //Clear the trace after a delay
    IEnumerator ClearTraceAfterDelay(Cell cell)
    {
        yield return new WaitForSeconds(traceDuration);
        if (cell != null && cell.cellEvent == "AgentTrace")
        {
            cell.cellEvent = "None";
        }
    }

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
                gameObject.transform.Rotate(0f, 0f, 90f, Space.Self);

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

    void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /////////////
    /// A* pathfinding logic (made by chatgpt - to be honest, i only get the theory behind it but not the intricacy of the code itself) <summary>
    /////////////
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
                if (seenTrace)
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

    //Visualisation gizmos
    private void OnDrawGizmosSelected()
    {
        if (!isDead)
        {
            if (path != null)
            {
                Gizmos.color = Color.green;

                foreach (Cell cell in path)
                {
                    Gizmos.DrawSphere(cell.transform.position, 0.15f);
                }
            }

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
}
