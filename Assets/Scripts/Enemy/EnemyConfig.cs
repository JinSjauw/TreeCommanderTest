using UnityEngine;

/// <summary>
/// ScriptableObject holding all tuning parameters for an enemy type.
/// Assign to a behaviour tree's Config Sources list; nodes with
/// ScriptableObjectConstant parameters can select fields from this SO.
/// Values are baked at build time — zero runtime overhead.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Config", fileName = "EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("Movement")]
    [Tooltip("NavMeshAgent speed (m/s).")]
    public float moveSpeed = 5f;

    [Tooltip("NavMeshAgent stopping distance from destination.")]
    public float stoppingDistance = 1f;

    [Tooltip("Ideal distance to maintain from the target (for ranged enemies).")]
    public float maintainDistance = 10f;

    [Tooltip("Minimum random path offset distance from target.")]
    public float minPathDistance = 3f;

    [Tooltip("Maximum random path offset distance from target.")]
    public float maxPathDistance = 8f;

    [Tooltip("Half-angle (degrees) for random cone pathfinding spread.")]
    public float maxConeHalfAngle = 30f;

    [Header("Detection")]
    [Tooltip("Radius of the detection sphere (enemy notices targets within this range).")]
    public float detectionRadius = 30f;

    [Tooltip("Radius within which the enemy is allowed to fire.")]
    public float firingRadius = 15f;

    [Tooltip("Whether line-of-sight is required to detect targets.")]
    public bool requireLineOfSight = true;

    [Tooltip("Layers that this enemy considers hostile.")]
    public LayerMask targetLayers = -1;

    [Tooltip("Layers that block line-of-sight.")]
    public LayerMask obstacleLayers;

    [Header("Fire Control")]
    [Tooltip("Rounds per minute.")]
    public float roundsPerMinute = 30f;

    [Tooltip("Damage per projectile.")]
    public float projectileDamage = 10f;

    [Tooltip("Random radius for aim scatter (accuracy).")]
    public float randomTargetRadius = 0.5f;

    [Header("Health")]
    [Tooltip("Maximum health points.")]
    public float maxHealth = 100f;

    [Header("Pathfinding")]
    [Tooltip("NavMeshAgent avoidance priority (lower = higher priority).")]
    public int avoidancePriority = 50;
}
