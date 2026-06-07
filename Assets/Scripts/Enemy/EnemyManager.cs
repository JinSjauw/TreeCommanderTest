using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Target Layers")]
    [SerializeField] private LayerMask targetLayerA;
    [SerializeField] private LayerMask targetLayerB;
    [SerializeField] private LayerMask obstacleLayer;

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private bool useLayerA = true;
    private int currentSpawnIndex;
    private int maxPriorityDelta = 100;

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

        int agentPriority = currentSpawnIndex % maxPriorityDelta;
        currentSpawnIndex++;

        GameObject spawnedObj = objectPool.GetObject(enemyPrefab, false);

        // Sample navmesh to get correct spawn height instead of using arbitrary +3m offset
        Vector3 spawnPosition = spawnPoint.position;
        float navMeshHeight = spawnPoint.position.y;
        if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit navMeshHit, 10f, NavMesh.AllAreas))
        {
            navMeshHeight = navMeshHit.position.y;
        }

        Vector3 finalSpawnPos = new Vector3(spawnPosition.x, navMeshHeight + 0.5f, spawnPosition.z);
        Debug.Log($"[EnemyManager] Spawning at index={randomIndex} spawnPoint={spawnPoint.position} finalPos={finalSpawnPos} objName={spawnedObj.name} activeSelf={spawnedObj.activeSelf}");
        spawnedObj.transform.position = finalSpawnPos;
        spawnedObj.transform.rotation = spawnPoint.rotation;

        EnemyController controller = spawnedObj.GetComponent<EnemyController>();
        if (controller != null) 
        {
            activeEnemies.Add(controller);
            controller.enabled = true;
            controller.Agent.transform.localPosition = Vector3.zero;
            controller.Agent.avoidancePriority = agentPriority;
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

        DeathHandler deathHandler = spawnedObj.GetComponentInChildren<DeathHandler>();
        if (deathHandler != null)
        {
            deathHandler.SetPool(objectPool);
        }

        // Warp agent to navmesh surface and set initial destination
        if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit agentHit, 10f, NavMesh.AllAreas))
        {
            controller.Agent.Warp(agentHit.position);
        }

        spawnedObj.SetActive(true);

        LegManager legManager = spawnedObj.GetComponentInChildren<LegManager>();
        if (legManager != null)
        {
            legManager.SnapBodyHeight();
            legManager.SyncAllLegs();
        }

        controller.OnDestructionEvent += ReturnEnemy;

        return controller;
    }

    public void ReturnEnemy(object sender, EnemyController enemy)
    {
        if (enemy == null) return;
        activeEnemies.Remove(enemy);
        enemy.OnDestructionEvent = null;
        //objectPool?.ReturnGameObject(enemy.gameObject);
    }

    private static int GetLayerIndex(LayerMask mask)
    {
        return (int)Mathf.Log(mask.value, 2);
    }
}
