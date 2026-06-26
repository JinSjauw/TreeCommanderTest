using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;
using UnityEngine.AI;

public class EnemyManager : MonoBehaviour
{
    [Header("Pooling")]
    [SerializeField] private ObjectPool objectPool;

    [Header("Enemy")]
    [SerializeField, Tooltip("Enemy prefab to spawn.")]
    private GameObject enemyPrefab;

    [SerializeField, Tooltip("Patrol points for individual enemies. Children are used as spawn positions and patrol waypoints. Falls back to squadPatrolPoints.")]
    private Transform enemyPatrolPoints;

    [SerializeField, Tooltip("Spawn an enemy on Start.")]
    private bool spawnEnemyAtStart;

    [SerializeField, Tooltip("Spawn enemies on a repeating timer.")]
    private bool spawnEnemyOnTimer;

    [SerializeField, Tooltip("Interval in seconds between enemy spawns. Only used when spawnEnemyOnTimer is enabled.")]
    private float enemySpawnTimer = 10f;

    [Header("Squad")]
    [SerializeField, Tooltip("Prefab with SquadManager component. One instance is spawned per squad via the object pool.")]
    private GameObject squadManagerPrefab;

    [SerializeField, Tooltip("Patrol points for squads. Children are used as spawn positions and patrol waypoints.")]
    private Transform squadPatrolPoints;

    [SerializeField, Tooltip("Spawn a squad on Start.")]
    private bool spawnSquadAtStart;

    [SerializeField, Tooltip("Spawn squads on a repeating timer.")]
    private bool spawnSquadOnTimer;

    [SerializeField, Tooltip("Interval in seconds between squad spawns. Only used when spawnSquadOnTimer is enabled.")]
    private float squadSpawnTimer = 30f;

    [SerializeField, Tooltip("Number of agents per auto-spawned squad.")]
    private int squadAgentCount = 3;

    [SerializeField, Tooltip("Circle formation radius for auto-spawned squads.")]
    private float squadRadius = 5f;

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private int currentSpawnIndex;
    private float enemyTimerAccumulator;
    private float squadTimerAccumulator;

    private void Start()
    {
        if (spawnEnemyAtStart)
            SpawnEnemy();

        if (spawnSquadAtStart)
            SpawnSquadInFormation(squadAgentCount, squadRadius);
    }

    private void Update()
    {
        // Clean up null entries
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }

        // Timer-based enemy spawning
        if (spawnEnemyOnTimer)
        {
            enemyTimerAccumulator += Time.deltaTime;
            if (enemyTimerAccumulator >= enemySpawnTimer)
            {
                enemyTimerAccumulator = 0;
                SpawnEnemy();
            }
        }

        // Timer-based squad spawning
        if (spawnSquadOnTimer)
        {
            squadTimerAccumulator += Time.deltaTime;
            if (squadTimerAccumulator >= squadSpawnTimer)
            {
                squadTimerAccumulator = 0;
                SpawnSquadInFormation(squadAgentCount, squadRadius);
            }
        }
    }

    /// <summary>
    /// Spawns a single enemy at a random patrol point.
    /// Returns the spawned EnemyController, or null if spawning failed.
    /// </summary>
    public EnemyController SpawnEnemy()
    {
        if (objectPool == null || enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemyManager] ObjectPool or enemy prefab not assigned on {name}");
            return null;
        }

        Transform patrolPointsSource = enemyPatrolPoints != null ? enemyPatrolPoints : squadPatrolPoints;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (!TryGetRandomPatrolPoint(patrolPointsSource, out spawnPosition, out spawnRotation))
        {
            Debug.LogWarning($"[EnemyManager] No patrol points assigned on {name}");
            return null;
        }

        int agentPriority = currentSpawnIndex % 100;

        GameObject spawnedObj = objectPool.GetObject(enemyPrefab, false);

        float navMeshHeight = spawnPosition.y;
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit navMeshHit, 10f, NavMesh.AllAreas))
        {
            navMeshHeight = navMeshHit.position.y;
        }

        Vector3 finalSpawnPos = new Vector3(spawnPosition.x, navMeshHeight + 0.5f, spawnPosition.z);
        spawnedObj.transform.position = finalSpawnPos;
        spawnedObj.transform.rotation = spawnRotation;

        EnemyController controller = spawnedObj.GetComponent<EnemyController>();
        if (controller != null)
        {
            activeEnemies.Add(controller);
            controller.enabled = true;
            if (controller.Agent != null)
            {
                controller.Agent.transform.localPosition = Vector3.zero;
                controller.Agent.avoidancePriority = agentPriority;
            }
        }
        else
        {
            Debug.LogError($"[EnemyManager] EnemyController not found on spawned {name}");
            return null;
        }

        AgentTreeRunner runner = spawnedObj.GetComponent<AgentTreeRunner>();
        if (runner != null)
        {
            runner.Initialize();
            BlackBoard bb = spawnedObj.GetComponent<BlackBoard>();
            if (bb != null)
                bb.Set("PatrolPoints", patrolPointsSource);
        }

        DeathHandler deathHandler = spawnedObj.GetComponentInChildren<DeathHandler>();
        if (deathHandler != null)
        {
            deathHandler.SetPool(objectPool);
        }

        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit agentHit, 10f, NavMesh.AllAreas))
        {
            if (controller.Agent != null)
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

    /// <summary>
    /// Spawns a squad by instantiating a new SquadManager via the object pool
    /// at a random patrol point. SquadMovePosition is set to the spawn point.
    /// </summary>
    public void SpawnSquad()
    {
        SquadManager sm = CreateSquadManager(out Vector3 spawnPos);
        if (sm == null) return;

        sm.gameObject.SetActive(true);
        sm.Spawn(patrolPointsOverride: squadPatrolPoints, squadMovePosition: spawnPos);
    }

    /// <summary>
    /// Spawns a squad by instantiating a new SquadManager via the object pool,
    /// positioning agents in a circle formation centered on the given position.
    /// Mirrors the CalculateFormation node logic.
    /// </summary>
    public void SpawnSquadInFormation(Vector3 center, int count, float radius)
    {
        SquadManager sm = CreateSquadManager(out _);
        if (sm == null) return;

        sm.gameObject.SetActive(true);
        sm.SpawnInFormation(center, count, radius, patrolPointsOverride: squadPatrolPoints, squadMovePosition: center);
    }

    /// <summary>
    /// Spawns a squad in a circle formation centered on a random patrol point.
    /// </summary>
    public void SpawnSquadInFormation(int count, float radius)
    {
        SpawnSquadInFormation(GetRandomPatrolPointPosition(squadPatrolPoints), count, radius);
    }

    /// <summary>
    /// Instantiates a SquadManager from the object pool and positions it at a random patrol point.
    /// Returns the SquadManager component, or null on failure.
    /// </summary>
    private SquadManager CreateSquadManager(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;

        if (objectPool == null || squadManagerPrefab == null)
        {
            Debug.LogWarning($"[EnemyManager] ObjectPool or SquadManager prefab not assigned on {name}.");
            return null;
        }

        if (!TryGetRandomPatrolPoint(squadPatrolPoints, out spawnPosition, out _))
        {
            Debug.LogWarning($"[EnemyManager] No patrol points for squad on {name}.");
            return null;
        }

        GameObject squadGo = objectPool.GetObject(squadManagerPrefab, false);
        squadGo.transform.SetParent(transform);
        squadGo.transform.position = spawnPosition;

        SquadManager sm = squadGo.GetComponent<SquadManager>();
        if (sm == null)
        {
            Debug.LogError($"[EnemyManager] SquadManager prefab has no SquadManager component.");
            return null;
        }

        sm.DisableAutoSpawn();
        return sm;
    }

    /// <summary>
    /// Picks a random child transform from the given parent. Increments currentSpawnIndex.
    /// Returns true and the position+rotation if the parent has children, false otherwise.
    /// </summary>
    private bool TryGetRandomPatrolPoint(Transform parent, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (parent == null || parent.childCount == 0)
            return false;

        int index = currentSpawnIndex % parent.childCount;
        currentSpawnIndex++;
        Transform child = parent.GetChild(index);
        position = child.position;
        rotation = child.rotation;
        return true;
    }

    /// <summary>
    /// Returns the position of a random child from the given patrol points parent.
    /// Returns Vector3.zero if the parent is null or has no children.
    /// </summary>
    private Vector3 GetRandomPatrolPointPosition(Transform parent)
    {
        TryGetRandomPatrolPoint(parent, out Vector3 pos, out _);
        return pos;
    }

    public void ReturnEnemy(object sender, EnemyController enemy)
    {
        if (enemy == null) return;
        activeEnemies.Remove(enemy);
        enemy.OnDestructionEvent = null;
    }
}
