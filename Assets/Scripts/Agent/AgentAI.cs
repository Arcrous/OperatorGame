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

    void Start()
    {
        startCell = gridManager.grid[0, 0];
        exitCell = gridManager.grid[gridManager.width - 1, gridManager.height - 1];

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
        while (pathIndex < path.Count)
        {
            Cell nextCell = path[pathIndex];
            transform.position = new Vector3(nextCell.x * gridManager.cellSize, nextCell.y * gridManager.cellSize, 0);
            pathIndex++;
            yield return new WaitForSeconds(0.3f); // Delay between steps
        }
    }
}
