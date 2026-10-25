using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

public class EnemyManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private PathNode[] spawnNodes;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private HealthManager target;
    [SerializeField] private GameObject shooter;
    [SerializeField] private float defaultSpawnInterval = 10f;

    [Header("Enemy Default Stats")]
    [SerializeField] private float defaultSpeed = 3f;


    [Header("Managing spawned enemies")]
    private float nextSpawnTime;
    private Queue<Enemy> activeEnemies = new();
    private Emotion lastEmotion = Emotion.NEUTRAL;
    private float speed;
    private float spawnInterval;

    private void Start()
    {
        ResetDifficulty();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
            spawnInterval *= 0.97f;
            speed += 0.7f;
        }

        RemoveDestroyedEnemiesFromQueue();

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            ResetManager();
    }

    /// <summary>Find and shoot the first active Enemy with the requested emotion.</summary>
    public bool Shoot(Emotion emotion)
    {
        int count = activeEnemies.Count;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = activeEnemies.Dequeue();

            if (enemy == null)
                continue;

            if (enemy.emotion == emotion)
            {
                StartCoroutine(KillEnemy(enemy));
                return true;
            }

            activeEnemies.Enqueue(enemy);
        }

        return false;
    }
    
    private IEnumerator KillEnemy(Enemy enemy)
    {
        if (enemy == null || shooter == null)
            yield break;

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.position = shooter.transform.position;
        ball.transform.localScale = Vector3.one;

        Renderer ballRenderer = ball.GetComponent<Renderer>();
        if (ballRenderer != null)
        {
            ballRenderer.material.color = enemy.emotion switch
            {
                Emotion.HAPPY => new Color(255, 238, 140),
                Emotion.ANGRY => new Color(221, 160, 221),
                Emotion.SURPRISED => new Color(205, 92, 92),
                _ => Color.lightGray,
            };
        };

        Vector3 start = shooter.transform.position;

        float duration = .3f;
        float elapsed = 0f;

        while (elapsed < duration && enemy != null)
        {
            elapsed += Time.deltaTime;
            ball.transform.position = Vector3.Lerp(start, enemy.transform.position, elapsed / duration);
            yield return null;
        }
        
        // Kill only after the ball reaches the enemy
        if (enemy != null)
            enemy.Kill();

        Destroy(ball);
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || target == null) return;

        // Spawn position
        PathNode spawnNode = spawnNodes[Random.Range(0, spawnNodes.Length)];
        if (spawnNode == null)
            return;
        

        // Emotion of the spawned enemy
        Emotion randomEmotion;
        do
        {
            randomEmotion = (Emotion)Random.Range(1, 4);
        } while (randomEmotion == lastEmotion);
        lastEmotion = randomEmotion;

        // Spawning the enemy
        Enemy newEnemyObj = Instantiate(enemyPrefab, spawnNode.transform.position, Quaternion.identity);
        if (newEnemyObj.TryGetComponent<Enemy>(out var enemyScript))
        {
            enemyScript.Initialize(target, speed, randomEmotion, deathEffectPrefab);
        }

        // Adding the enemy to the queue (for management)
        activeEnemies.Enqueue(newEnemyObj);
    }
    
    private void RemoveDestroyedEnemiesFromQueue()
    {
        while (activeEnemies.Count > 0 && activeEnemies.Peek() == null)
            activeEnemies.Dequeue();
    }

    private void ResetManager()
    {
        while (activeEnemies.Count > 0)
        {
            Enemy enemyObject = activeEnemies.Dequeue();
            if (enemyObject == null)
                continue;

            if (enemyObject.TryGetComponent<Enemy>(out Enemy enemy))
                enemy.Kill();
            else
            {
                SpawnDeathEffect(enemyObject.transform.position);
                Destroy(enemyObject);
            }
        }

        ResetDifficulty();
        nextSpawnTime = Time.time + spawnInterval;

        if (target != null)
            target.Restart();

        Debug.Log("RESET");
    }

    private void ResetDifficulty()
    {
        speed = defaultSpeed;
        spawnInterval = defaultSpawnInterval;
    }

    private void SpawnDeathEffect(Vector3 position)
    {
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, position, Quaternion.identity);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        const float circleRadius = 1f;
        foreach (PathNode node in spawnNodes)
        {
            if (node != null)
                Gizmos.DrawWireSphere(node.transform.position, circleRadius);
        }
    }

    // Test if the Enemy prefab has the Script Enemy
    private void OnValidate()
    {
        if (enemyPrefab != null && !enemyPrefab.TryGetComponent<Enemy>(out _))
        {
            Debug.LogError($"The object {enemyPrefab.name} must have the script Enemy.cs!", this);
            enemyPrefab = null; 
        }
    }
}
