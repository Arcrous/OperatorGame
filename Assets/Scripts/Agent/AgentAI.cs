using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentAI : MonoBehaviour
{
    public GridManager gridManager;
    private Cell startCell;
    private Cell exitCell;
    private List<Cell> path;
    private int pathIndex = 0;

    private bool isMoving;
    public float moveSpeed = 2f; // Speed of movement between cells

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    private void Start()
    {
        Invoke("StartTracing", 2f);
    }

    void StartTracing()
    {
        Debug.Log("Start trace");
        //this.transform.position = gridManager.grid[0, 0].transform.position;

        startCell = gridManager.grid[0, 0];
        exitCell = gridManager.exitCell;

        path = gridManager.FindPath(startCell, exitCell);

        if (path != null && path.Count > 0)
        {
            StartCoroutine(FollowPath());
        }
        else
        {
            Debug.LogError("No path found to the exit!");
        }
    }

    IEnumerator FollowPath()
    {
        /* while (pathIndex < path.Count)
         {
             *//*Cell nextCell = path[pathIndex];
             transform.position = new Vector3(nextCell.x * gridManager.cellSize, nextCell.y * gridManager.cellSize, 0);*//*
             Vector3 goToPos = GetNextPosition();
             this.transform.position = Vector3.Slerp(this.transform.position, goToPos, 1f);
             pathIndex++;
             yield return new WaitForSeconds(2f); // Delay between steps
         }*/

        foreach (Cell cell in path)
        {
            // Wait until the agent finishes moving before proceeding
            yield return MoveToCell(cell);
        }

        Debug.Log("Agent reached the exit!");
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
            transform.position = Vector3.Slerp(startPos, endPos, elapsedTime);
            yield return null;
        }

        // Snap to the final position to avoid slight inaccuracies
        transform.position = endPos;

        isMoving = false;
    }

    private Vector3 GetNextPosition()
    {
        Vector3 nextPos;
        Cell nextCell = path[pathIndex];
        nextPos = new Vector3(nextCell.x * gridManager.cellSize, nextCell.y * gridManager.cellSize, 0);
        return nextPos;
    }
}
