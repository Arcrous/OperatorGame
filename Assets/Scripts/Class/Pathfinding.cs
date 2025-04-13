using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
    public GridManager gridManager;

    private void Awake()
    {
        gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    public Pathfinding(GridManager gridManager)
    {
        this.gridManager = gridManager;
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
            Cell current = GetLowestFCostCell(openSet, gCost, hCost);

            if (current == target)
            {
                return RetracePath(cameFrom, start, target);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Cell neighbor in GetNeighbors(current))
            {
                if (neighbor.isWall || closedSet.Contains(neighbor))
                    continue;

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
}
