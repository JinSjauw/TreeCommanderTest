using UnityEngine;

/// <summary>
/// ScriptableObject holding spawn-time configuration for EnemyManager.
/// Extracted from EnemyManager so the same config can be shared across
/// multiple managers or scenes without duplicating inspector values.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Spawn Config", fileName = "EnemySpawnConfig")]
public class EnemySpawnConfig : ScriptableObject
{
    [Header("Prefab")]
    [Tooltip("The enemy prefab to spawn.")]
    public GameObject enemyPrefab;

    [Header("Spawning")]
    [Tooltip("Number of enemies spawned on Start.")]
    public int initialSpawnCount = 2;

    [Header("Layers")]
    [Tooltip("Team A target layer.")]
    public LayerMask targetLayerA;

    [Tooltip("Team B target layer.")]
    public LayerMask targetLayerB;

    [Tooltip("Layer for obstacles (blocks line of sight).")]
    public LayerMask obstacleLayer;

    [Header("Avoidance")]
    [Tooltip("NavMeshAgent avoidance priority range. Priority = spawnIndex % range.")]
    public int avoidancePriorityRange = 100;
}
