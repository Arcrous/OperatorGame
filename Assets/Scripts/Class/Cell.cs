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
    public bool isPath;
    public string cellEvent;

    //Init a cell
    public void Initialize(int x, int y)
    {
        this.x = x;
        this.y = y;
        isOccupied = false;
        isWall = false;
        isExit = false;
        cellEvent = "None";
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

    public void SetAsWall()
    {
        isWall = true;
        cellEvent = "Wall";
    }

    public void SetAsExit()
    {
        isExit = true;
        cellEvent = "Exit";
    }
}