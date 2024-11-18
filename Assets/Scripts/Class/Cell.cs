using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int x;
    public int y;
    public bool isOccupied;
    public bool isExit;
    public bool isWall;
    public bool isPath = false;
    public string cellEvent;

    //A* pathfinding heuristic cost
    public int gCost; //Cost from start node
    public int hCost; //Heuristic cost to exit
    public int fCost => gCost + hCost; //Total cost

    public Cell parent; //to track the path

    //Init a cell
    public void Initialize(int x, int y)
    {
        this.x = x;
        this.y = y;
        isOccupied = false;
        isWall = false;
        isExit = false;
        cellEvent = "None";
        gCost = 0;
        hCost = 0;
        parent = null;
    }

    //Set cell event
    public void SetEvent(string eventDescription)
    {
        cellEvent = eventDescription;
    }

    //Set cell as occupied by entity
    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }

    //Set as wall
    public void SetAsWall()
    {
        isWall = true;
        cellEvent = "Wall";
    }

    //Set as path
    public void SetAsPath()
    {
        isPath = true;
        cellEvent = "Path";
    }

    //Set as exit
    public void SetAsExit()
    {
        isExit = true;
        cellEvent = "Exit";
    }

    //Calc the heuristic cost
    public void CalculateHeuristic(Cell target)
    {
        hCost = Mathf.Abs(target.x - x) + Mathf.Abs(target.y - y);
    }
}