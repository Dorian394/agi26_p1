using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

public class EnemiesManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private HealthManager target;
    [SerializeField] private float defaultSpawnInterval = 2f;
    [SerializeField] private float spawnSizeX = 10f;
    [SerializeField] private float spawnSizeZ = 10f;

    [Header("Enemy Default Stats")]
    [SerializeField] private float defaultSpeed = 3f;


    [Header("Managing spawned enemies")]
    private float nextSpawnTime;
    private Queue<GameObject> activeEnemies = new();
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

        // Destroy the nearest enemy if the corresponding emotion is shown
        if (activeEnemies.Count > 0) 
        {
            var nearestEnemy = activeEnemies.Peek();
            
            // Dequeue the null objects at the head of the queue
            while (nearestEnemy == null)
            {
                activeEnemies.Dequeue();
                if (activeEnemies.Count > 0) nearestEnemy = activeEnemies.Peek();
            }
            
            // 
            if (nearestEnemy != null && nearestEnemy.TryGetComponent<Enemy>(out var enemyScript))
            {
                Emotion enemyEmotion = enemyScript.emotion;
                if (EmotionBridge.GetEmotion() == enemyEmotion)
                {
                    activeEnemies.Dequeue();
                    enemyScript.Kill();
                }
            }
        }

        // Reset
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            while(activeEnemies.Count > 0)
            {
                GameObject enemy = activeEnemies.Dequeue();
                Destroy(enemy);
            }
            speed = defaultSpeed;
            spawnInterval = defaultSpawnInterval;
            target.Restart();
            Debug.Log("RESET");
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || target == null) return;

        // Spawn position
        Vector3 spawnPosition = new(
            transform.position.x + (Random.value - 0.5f) * spawnSizeX,
            transform.position.y,
            transform.position.z + (Random.value - 0.5f) * spawnSizeZ
        );

        // Emotion of the spawned enemy
        Emotion randomEmotion;
        do
        {
            randomEmotion = (Emotion)Random.Range(1, 4);
        } while (randomEmotion == lastEmotion);
        lastEmotion = randomEmotion;

        // Spawning the enemy
        GameObject newEnemyObj = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
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
        Vector3 size = new(spawnSizeX, 1.0f, spawnSizeZ);
        Gizmos.DrawWireCube(this.transform.position, size);
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
