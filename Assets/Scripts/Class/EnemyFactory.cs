using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyFactory : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject enemyPrefab; // Base enemy prefab

    [Header("Spawn Settings")]
    public GridManager gridManager;
    public int numberOfEnemies = 4;
    public float spawnDelay = 0.2f;
    public bool randomEnemyAmount;
    public int maxEnemies = 10;

    [Header("Enemy Type Distribution")]
    [Range(0, 1)]
    public float chaserChance = 0.25f;
    [Range(0, 1)]
    public float ambusherChance = 0.25f;
    [Range(0, 1)]
    public float patrollerChance = 0.25f;
    //wandererChance = 1 - (chaserChance + ambusherChance + patrollerChance);


    [Header("Color Coding")]
    public Color chaserColor = Color.red;
    public Color ambusherColor = Color.magenta;
    public Color patrollerColor = Color.green;
    public Color wandererColor = new Color(1f, 0.5f, 0f); // Orange

    private void Start()
    {
        if (gridManager == null)
        {
            gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
        }

        gridManager.spawnEnemy += () => StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {
        if (randomEnemyAmount)
        {
            numberOfEnemies = Random.Range(1, maxEnemies);
        }
        for (int i = 0; i < numberOfEnemies; i++)
        {
            SpawnRandomEnemy();
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    public void SpawnRandomEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab is not assigned!");
            return;
        }

        // Determine enemy type based on chances
        EnemyType enemyType = DetermineEnemyType();

        // Spawn the enemy
        GameObject enemyObj = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity);
        enemyObj.name = $"Enemy_{enemyType}";

        enemyObj.transform.SetParent(gridManager.transform);

        // Configure the enemy
        EnemyAI enemyAI = enemyObj.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.gridManager = gridManager;
            enemyAI.enemyType = enemyType;

            // Adjust parameters based on type
            ConfigureEnemyBehavior(enemyAI, enemyType);

            // Color-code the enemy for easy identification
            SpriteRenderer spriteRenderer = enemyAI.typeDiamond;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = GetColorForEnemyType(enemyType);
            }
        }
        else
        {
            Debug.LogError("Enemy prefab does not have EnemyAI component!");
        }
    }

    public void SpawnSpecificEnemy(EnemyType type)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab is not assigned!");
            return;
        }

        // Spawn the enemy
        GameObject enemyObj = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity);
        enemyObj.name = $"Enemy_{type}";

        // Configure the enemy
        EnemyAI enemyAI = enemyObj.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.gridManager = gridManager;
            enemyAI.enemyType = type;

            // Adjust parameters based on type
            ConfigureEnemyBehavior(enemyAI, type);

            // Color-code the enemy for easy identification
            SpriteRenderer spriteRenderer = enemyAI.typeDiamond;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = GetColorForEnemyType(type);
            }
        }
        else
        {
            Debug.LogError("Enemy prefab does not have EnemyAI component!");
        }
    }

    private EnemyType DetermineEnemyType()
    {
        float randomValue = Random.value;
        float cumulativeChance = 0f;

        // Chaser check
        cumulativeChance += chaserChance;
        if (randomValue <= cumulativeChance)
            return EnemyType.Chaser;

        // Ambusher check
        cumulativeChance += ambusherChance;
        if (randomValue <= cumulativeChance)
            return EnemyType.Ambusher;

        // Patroller check
        cumulativeChance += patrollerChance;
        if (randomValue <= cumulativeChance)
            return EnemyType.Patroller;

        // If none of the above, it's a wanderer
        return EnemyType.Wanderer;
    }

    private void ConfigureEnemyBehavior(EnemyAI enemyAI, EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Chaser:
                enemyAI.moveSpeed = 1.5f;
                enemyAI.patrolRange = 4;
                break;

            case EnemyType.Ambusher:
                enemyAI.moveSpeed = 1.8f;
                enemyAI.patrolRange = 5;
                enemyAI.ambushLookAhead = 5; // Look farther ahead to intercept
                break;

            case EnemyType.Patroller:
                enemyAI.moveSpeed = 1.3f;
                enemyAI.patrolRange = 6;
                enemyAI.patrollerPatternSize = 10; // More complex patterns
                break;

            case EnemyType.Wanderer:
                enemyAI.moveSpeed = 1.0f;
                enemyAI.patrolRange = 3;
                enemyAI.wandererChaseChance = 0.4f; // Less likely to chase
                enemyAI.wandererFleeDistance = 6; // Flees farther away
                break;
        }
    }

    private Color GetColorForEnemyType(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Chaser:
                return chaserColor;
            case EnemyType.Ambusher:
                return ambusherColor;
            case EnemyType.Patroller:
                return patrollerColor;
            case EnemyType.Wanderer:
                return wandererColor;
            default:
                return Color.white;
        }
    }
}