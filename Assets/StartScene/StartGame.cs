using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A lot of code duplication from the Slime Manager script
// Not good

public class StartGame : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] enemyPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private PathNode[] spawnNodes;

    [Header("Enemy Default Stats")]
    [SerializeField] private float defaultSpeed = 0f;

    [Header("Managing spawned enemies")]
    private Queue<GameObject> activeEnemies = new();
    private float speed;

    void Start()
    {
        speed = defaultSpeed;

        // Preset start array: Happy, Surprised, Angry, Happy
        int[] emotions = {0, 2, 1, 0};
        int slimesAtStart = emotions.Length;

        // Create longer distances between the slimes
        Vector3 direction = (spawnNodes[1].transform.position - spawnNodes[0].transform.position).normalized;
        float extension = 0;
        Vector3 extendedPoint = spawnNodes[1].transform.position + (direction * extension);

        // Spawn enemies
        for(int i = 0; i < slimesAtStart; i++)
        {
            float percentDist = (float)i / (slimesAtStart - 1);
            SpawnEnemy(percentDist, extendedPoint, emotions[i]);
        }
    }

    void Update()
    {
        // Destroy the nearest enemy if the corresponding emotion is shown
        if(activeEnemies.Count > 0)
        {
            var nearestEnemy = activeEnemies.Peek();
            if (nearestEnemy.TryGetComponent<Slime>(out var enemyScript))
            {
                Emotion enemyEmotion = enemyScript.emotion;
                if (EmotionBridge.GetEmotion() == enemyEmotion)
                {
                    activeEnemies.Dequeue();
                    SpawnDeathEffect(nearestEnemy.transform.position, enemyEmotion);
                    Destroy(nearestEnemy);
                }
            }
        }

        // Reset
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("StartMenu");
        }
        
        // Force start game -> triggers next if condition
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            while(activeEnemies.Count > 0)
            {
                GameObject enemy = activeEnemies.Dequeue();
                Destroy(enemy);
            }
        }
        
        // Load game
        if (activeEnemies.Count == 0)
        {
            SceneManager.LoadScene("Forest");
        }
    }

    private void SpawnEnemy(float distance, Vector3 extension, int emotionValue)
    {
        if (enemyPrefab == null) return;

        // Emotion of the spawned enemy
        Emotion presetEmotion;
        presetEmotion = (Emotion)(emotionValue+1);

        // Assuming there are 2 spawnNodes, should do a check to be on the safe side
        Vector3 positionBetweenNodes = Vector3.Lerp(spawnNodes[0].transform.position, extension, distance);

        // Turn towards camera
        Vector3 cameraPos = Camera.main.transform.position;
        Vector3 lookDirection = cameraPos - positionBetweenNodes;
        lookDirection.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(lookDirection);

        // Spawning the enemy
        GameObject newEnemyObj = Instantiate(enemyPrefab[emotionValue], positionBetweenNodes, lookRotation);
        if (newEnemyObj.TryGetComponent<Slime>(out var slimeScript))
        {
            slimeScript.Initialize(spawnNodes[0], speed, presetEmotion, null, false);
        }

        // Adding the enemy to the queue (for management)
        activeEnemies.Enqueue(newEnemyObj);
    }

    private void SpawnDeathEffect(Vector3 position, Emotion emotion)
    {
        if (deathEffectPrefab == null) return;

        GameObject newEffectObj = Instantiate(deathEffectPrefab, position, Quaternion.identity);
        if (newEffectObj.TryGetComponent<EnemyDeathEffect>(out var deathEffectScript))
        {
            deathEffectScript.Initialize(emotion);
        }
    }
}
