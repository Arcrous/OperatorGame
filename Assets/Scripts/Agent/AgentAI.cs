using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AgentAI : MonoBehaviour
{
    public GridManager gridManager;
    private Pathfinding pathfinding;

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

    #region Setup
    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();

        pathfinding = new Pathfinding(gridManager);
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

        List<Cell> pathToExit = pathfinding.FindPath(startCell, exitCell);
        List<Cell> pathToWeapon = pathfinding.FindPath(startCell, weaponCell);

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

            path.Clear();
            StartCoroutine(RipAndTear());
            yield break;
        }

        if (!returnedToSpawn && seenTrace)
        {
            returnedToSpawn = true;
            moveSpeed = 1f;
            StartCoroutine(SearchUntilFoundFromSpawn());
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
        List<Cell> pathToExit = pathfinding.FindPath(startCell, exitCell);
        List<Cell> pathToWeapon = pathfinding.FindPath(startCell, weaponCell);

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
            //Debug.LogError("Agent AI: Unable to find a new path!");
            StartCoroutine(SearchUntilFoundFromSpawn());
        }
        else
        {
            //Debug.Log("Found path");
            seenTrace = false;
            StartCoroutine(FollowPath());
        }
    }

    //calculate path from current cell to start/weapon, then compare it
    private List<Cell> SetShortestPathToStartOrWeapon()
    {
        List<Cell> pathToStart = pathfinding.FindPath(currentCell, startCell);
        List<Cell> pathToWeapon = pathfinding.FindPath(currentCell, weaponCell);

        if (pathToWeapon != null && pathToStart != null)
        {
            if (pathToWeapon.Count < pathToStart.Count)
            {
                path = pathToWeapon;
                Debug.Log("Agent AI: Path to weapon chosen.");
            }
            else
            {
                path = pathToStart;
                Debug.Log("Agent AI: Path to start chosen.");
            }
            return pathToWeapon.Count < pathToStart.Count ? pathToWeapon : pathToStart;
        }
        else if (pathToWeapon != null)
        {
            return pathToWeapon;
        }
        else if (pathToStart != null)
        {
            return pathToStart;
        }

        return null; // No valid path found
    }

    //When a weapon is picked up, calculate path to exit from current cell
    IEnumerator RipAndTear()
    {
        yield return new WaitForSeconds(0.3f);
        path = pathfinding.FindPath(currentCell, exitCell);
        //Debug.Log("Until it is done");
        moveSpeed = 1.5f;
        StartCoroutine(FollowPath());
    }

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
        foreach (Cell neighbor in pathfinding.GetNeighbors(cell))
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
        path = SetShortestPathToStartOrWeapon();
        moveSpeed = 2f;
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

}
