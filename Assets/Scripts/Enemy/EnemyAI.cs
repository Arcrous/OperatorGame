using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public GridManager gridManager; // Reference to the grid manager
    public int patrolRange = 3; // Max range for patrol from its spawn point
    public float moveSpeed = 1f; // Speed of movement between cells
    private Cell currentCell; // The cell where the enemy is currently located
    private Cell startCell; // Starting cell
    private Cell targetCell; // Target cell to move to
    private List<Cell> patrolCells; // List of cells within patrol range

    private bool isMoving;

    // Start is called before the first frame update
    void Start()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();

        // Find a random spawn point that is not a wall, start, or exit
        SpawnEnemy();

        // Generate patrol cells within range
        GeneratePatrolRange();

        // Start patrolling
        StartCoroutine(Patrol());
    }

    void SpawnEnemy()
    {
        while (true)
        {
            int x = Random.Range(0, gridManager.width);
            int y = Random.Range(0, gridManager.height);

            Cell potentialCell = gridManager.grid[x, y];

            if (!potentialCell.isWall && !potentialCell.isExit && (x != 0 || y != 0)) // Avoid walls, start, and exit
            {
                startCell = potentialCell;
                currentCell = startCell;
                transform.position = startCell.transform.position;
                break;
            }
        }
    }

    void GeneratePatrolRange()
    {
        patrolCells = new List<Cell>();

        int startX = Mathf.Max(0, startCell.x - patrolRange);
        int endX = Mathf.Min(gridManager.width - 1, startCell.x + patrolRange);
        int startY = Mathf.Max(0, startCell.y - patrolRange);
        int endY = Mathf.Min(gridManager.height - 1, startCell.y + patrolRange);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                Cell cell = gridManager.grid[x, y];
                if (!cell.isWall && cell != startCell) // Exclude walls and the start cell
                {
                    patrolCells.Add(cell);
                }
            }
        }
    }

    IEnumerator Patrol()
    {
        while (true)
        {
            if (isMoving)
            {
                yield return null;
                continue;
            }

            // Choose a random patrol cell within range
            targetCell = patrolCells[Random.Range(0, patrolCells.Count)];

            // Move to the target cell
            yield return MoveToCell(targetCell);

            // Wait at the target cell before moving again
            yield return new WaitForSeconds(Random.Range(1f, 3f)); // Pause between movements
        }
    }

    IEnumerator MoveToCell(Cell targetCell)
    {
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
        currentCell = targetCell;
        isMoving = false;
    }
}
