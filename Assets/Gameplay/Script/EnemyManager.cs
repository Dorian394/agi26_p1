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
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private HealthManager target;
    [SerializeField] private GameObject shooter;
    [SerializeField] private float defaultSpawnInterval = 10f;
    [SerializeField] private float spawnSizeX = 10f;
    [SerializeField] private float spawnSizeZ = 10f;

    [Header("Enemy Default Stats")]
    [SerializeField] private float defaultSpeed = 3f;


    [Header("Managing spawned enemies")]
    private float nextSpawnTime;
    private Queue<Enemy> activeEnemies = new();
    private Emotion lastEmotion = Emotion.NEUTRAL;
    private float speed;
    private float spawnInterval;

    void Start()
    {
        spawnInterval = defaultSpawnInterval;
        speed = defaultSpeed;
        nextSpawnTime = Time.time + spawnInterval;
    }

    void Update()
    {
        // Spawn a new enemy if possible
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
            spawnInterval *= 0.97f;
            speed += 0.7f;
        }

        // Dequeue the potentially null elements at the head of the queue
        if (activeEnemies.Count > 0)
        {
            var nearestEnemy = activeEnemies.Peek();
            while (nearestEnemy == null)
            {
                activeEnemies.Dequeue();
                if (activeEnemies.Count > 0) nearestEnemy = activeEnemies.Peek();
            }
        }
        
        // Reset
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            while(activeEnemies.Count > 0)
            {
                Enemy enemy = activeEnemies.Dequeue();
                enemy.Kill();
            }
            speed = defaultSpeed;
            spawnInterval = defaultSpawnInterval;
            target.Restart();
            Debug.Log("RESET");
        }
    }
    
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
        // Create a temporary ball
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.position = shooter.transform.position;
        ball.transform.localScale = Vector3.one * 1f;

        // Give it a colour
        var renderer = ball.GetComponent<Renderer>();
        renderer.material.color = enemy.emotion switch
        {
            Emotion.HAPPY => new Color(255, 238, 140),
            Emotion.ANGRY => new Color(221, 160, 221),
            Emotion.SURPRISED => new Color(205, 92, 92),
            _ => Color.lightGray,
        };

        Vector3 start = shooter.transform.position;

        float duration = .3f;
        float elapsed = 0f;

        while (elapsed < duration && enemy)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            ball.transform.position = Vector3.Lerp(start, enemy.transform.position, t);

            yield return null;
        }
        
        // Kill only after the ball reaches the enemy
        enemy.Kill();

        Destroy(ball);
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || target == null) return;

        // Spawn position
        Vector3 spawnPosition = transform.TransformPoint(
            (Random.value - 0.5f) * spawnSizeX,
            0,
            (Random.value - 0.5f) * spawnSizeZ
        );

        // Emotion of the spawned enemy
        Emotion randomEmotion;
        do
        {
            randomEmotion = (Emotion)Random.Range(1, 4);
        } while (randomEmotion == lastEmotion);
        lastEmotion = randomEmotion;

        // Spawning the enemy
        Enemy newEnemyObj = Instantiate(enemyPrefab, spawnPosition, Quaternion.Euler(0f, 90f, 0f));
        if (newEnemyObj.TryGetComponent<Enemy>(out var enemyScript))
        {
            enemyScript.Initialize(target, speed, randomEmotion, deathEffectPrefab);
        }

        // Adding the enemy to the queue (for management)
        activeEnemies.Enqueue(newEnemyObj);
    }

    

    // Debug: draw spawn area
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 size = new(spawnSizeX, 1f, spawnSizeZ);
        Gizmos.DrawWireCube(Vector3.zero, size);
        
        Gizmos.matrix = Matrix4x4.identity;
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
