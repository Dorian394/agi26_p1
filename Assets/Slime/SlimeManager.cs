using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SlimeManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] enemyPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float defaultSpawnInterval = 1f;
    [SerializeField] private PathNode[] spawnNodes;

    [Header("Enemy Default Stats")]
    [SerializeField] private float defaultSpeed = 3f;
    [SerializeField] private float enemyDamages = 10.1f;


    [Header("Managing spawned enemies")]
    private float nextSpawnTime;
    private Queue<GameObject> activeEnemies = new();
    private Emotion lastEmotion = Emotion.NEUTRAL;
    private float speed;
    private int spawned = 0;
    private PathNode spawnNode;

    [Header("Health / Gameplay")]
    [SerializeField] private HealthManager healthManager;

    void Start()
    {
        // Spawn node
        spawnNode = spawnNodes[Random.Range(0, spawnNodes.Length)];
        speed = defaultSpeed;
        nextSpawnTime = Time.time + defaultSpawnInterval;
    }

    void Update()
    {
        // Spawn a new enemy if possible
        if (spawned < 4 && Time.time > nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + defaultSpawnInterval;
            spawned += 1;
        }

        // Reset
        if(spawned == 4 && activeEnemies.Count == 0)
        {
            spawned = 0;
            spawnNode = spawnNodes[Random.Range(0, spawnNodes.Length)];
            speed += 0.7f;
        }

        // Destroy the nearest enemy if the corresponding emotion is shown
        if(activeEnemies.Count > 0) {
            var nearestEnemy = activeEnemies.Peek();
            if (nearestEnemy.TryGetComponent<Slime>(out var enemyScript))
            {
                Emotion enemyEmotion = enemyScript.emotion;
                if (EmotionBridge.GetEmotion() == enemyEmotion)
                {
                    DeleteClosestEnemy();
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
            spawned = 0;
            Debug.Log("RESET");
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null) return;

        // Emotion of the spawned enemy
        Emotion randomEmotion;
        do
        {
            randomEmotion = (Emotion)Random.Range(1, 4); // The 1-3 range is hardcoded here, bad form but it works for now
        } while (randomEmotion == lastEmotion);
        lastEmotion = randomEmotion;

        // Spawning the enemy
        GameObject newEnemyObj = Instantiate(enemyPrefab[(int)randomEmotion - 1], spawnNode.transform.position, Quaternion.identity);
        if (newEnemyObj.TryGetComponent<Slime>(out var slimeScript))
        {
            slimeScript.Initialize(spawnNode, speed, randomEmotion, this);
        }

        // Adding the enemy to the queue (for management)
        activeEnemies.Enqueue(newEnemyObj);
    }

    public void DeleteClosestEnemy()
    {
        var nearestEnemy = activeEnemies.Dequeue();
        SpawnDeathEffect(nearestEnemy.transform.position);
        Destroy(nearestEnemy);
    }

    private void SpawnDeathEffect(Vector3 position)
    {
        if (deathEffectPrefab == null) return;

        GameObject newEffectObj = Instantiate(deathEffectPrefab, position, Quaternion.identity);
    }

    public void InflictDamages()
    {
        healthManager.inflictDamages(enemyDamages);
    }

    // Debug: draw spawn area
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        const float circleRadius = 1.0f;
        for (int i = 0; i < spawnNodes.Length; i++)
        {
            Gizmos.DrawWireSphere(spawnNodes[i].transform.position, circleRadius);
        }
        
    }
}
