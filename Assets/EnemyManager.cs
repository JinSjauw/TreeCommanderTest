using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("Pooling")]
    [SerializeField] private ObjectPool objectPool;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private Transform patrolPoints;

    [Header("Spawning")]
    [SerializeField] private int initialSpawnCount = 2;
    [SerializeField] private float minSpawnDelay;
    [SerializeField] private float maxSpawnDelay = 1f;

    [Header("Target Layers")]
    [SerializeField] private LayerMask targetLayerA;
    [SerializeField] private LayerMask targetLayerB;
    [SerializeField] private LayerMask obstacleLayer;

    private float nextSpawnTime;
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private bool useLayerA = true;
    private int currentSpawnIndex;

    private void Start()
    {
        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnEnemy();
        }
    }

    private void Update()
    {
        // Clean up destroyed enemies
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    /// <summary>
    /// Spawns a single enemy at a random spawn point.
    /// Returns the spawned EnemyController, or null if spawning failed.
    /// </summary>
    public EnemyController SpawnEnemy()
    {
        if (objectPool == null || enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemyManager] ObjectPool or EnemyPrefab not assigned on {name}");
            return null;
        }

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning($"[EnemyManager] No spawn points assigned on {name}");
            return null;
        }

        int randomIndex = currentSpawnIndex % spawnPoints.Count;
        Transform spawnPoint = spawnPoints[randomIndex];
        currentSpawnIndex++;

        GameObject spawnedObj = objectPool.GetObject(enemyPrefab);
        spawnedObj.transform.position = spawnPoint.position + Vector3.up * 3f;
        spawnedObj.transform.rotation = spawnPoint.rotation;

        EnemyController controller = spawnedObj.GetComponent<EnemyController>();
        if (controller != null) 
        {
            activeEnemies.Add(controller);
            controller.enabled = true;
            controller.Agent.transform.localPosition = Vector3.zero;
        }
        else
        {

            Debug.LogError($"[EnemyManager] EnemyController not found on spawned {name}");
            return null;
        }

        LayerMask targetLayer = useLayerA ? targetLayerA : targetLayerB;
        int selfLayer = GetLayerIndex(useLayerA ? targetLayerB : targetLayerA);
        controller.SetMasks(selfLayer, targetLayer, obstacleLayer);
        useLayerA = !useLayerA;

        spawnedObj.GetComponent<TreeRunner>().Initialize();
        spawnedObj.GetComponent<BlackBoard>().Set("PatrolPoints", patrolPoints);

        spawnedObj.SetActive(true);

        controller.OnDestructionEvent += ReturnEnemy;

        return controller;
    }

    public void ReturnEnemy(object sender, EnemyController enemy)
    {
        if (enemy == null) return;
        activeEnemies.Remove(enemy);
        enemy.OnDestructionEvent = null;
        objectPool?.ReturnGameObject(enemy.gameObject);
    }

    private static int GetLayerIndex(LayerMask mask)
    {
        return (int)Mathf.Log(mask.value, 2);
    }
}
