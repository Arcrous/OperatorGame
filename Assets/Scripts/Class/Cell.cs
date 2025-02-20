using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int x;
    public int y;
    public bool isExit;
    public bool isWall;
    public string cellEvent;

    //A* pathfinding heuristic cost
    public int gCost; //Cost from start node
    public int hCost; //Heuristic cost to exit
    public int fCost => gCost + hCost; //Total cost

    public Cell parent; //to track the path

    //store sprites
    [SerializeField] Sprite groundSprite;
    [SerializeField] Sprite groundSprite2;
    [SerializeField] Sprite groundSprite3;
    [SerializeField] Sprite wallSprite;
    [SerializeField] Sprite exitSprite;

    [SerializeField] SpriteRenderer spriteRend;

    //Init a cell
    public void Initialize(int x, int y)
    {
        this.x = x;
        this.y = y;
        isWall = false;
        isExit = false;
        cellEvent = "None";
        gCost = 0;
        hCost = 0;
        parent = null;

        int randomIndex = Random.Range(0, 2);
        switch (randomIndex)
        {
            case 0:
                spriteRend.sprite = groundSprite;
                break;
            case 1:
                spriteRend.sprite = groundSprite2;
                break;
            case 2:
                spriteRend.sprite = groundSprite3;
                break;
        }
    }

    //Set cell event
    public void SetEvent(string eventDescription)
    {
        cellEvent = eventDescription;
    }

    //Set as wall
    public void SetAsWall()
    {
        isWall = true;
        cellEvent = "Wall";
        spriteRend.sprite = wallSprite;
    }

    //Set as exit
    public void SetAsExit()
    {
        isExit = true;
        cellEvent = "Exit";
        spriteRend .sprite = exitSprite;
    }

    //Calc the heuristic cost
    public void CalculateHeuristic(Cell target)
    {
        hCost = Mathf.Abs(target.x - x) + Mathf.Abs(target.y - y);
    }
}