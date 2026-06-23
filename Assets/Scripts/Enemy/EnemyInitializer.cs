using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// On enable, reads every value from an EnemyConfig ScriptableObject and
/// pushes it into the correct component fields (NavMeshAgent, EnemyController,
/// EnemyDetectionSystem, GunHandling). This is the single source of truth
/// for tuning values — nothing is hardcoded on the components.
/// </summary>
public class EnemyInitializer : MonoBehaviour
{
    [SerializeField] private EnemyConfig config;

    private void OnEnable()
    {
        if (config == null)
            return;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = config.moveSpeed;
            agent.stoppingDistance = config.stoppingDistance;
            // avoidancePriority is set by EnemyManager after spawn — do not override here
        }

        EnemyController controller = GetComponent<EnemyController>();
        controller?.SetMovementConfig(config.maxConeHalfAngle, config.minPathDistance,
            config.maxPathDistance, config.maintainDistance);

        EnemyDetectionSystem detection = GetComponent<EnemyDetectionSystem>();
        detection?.SetDetectionConfig(config.detectionRadius, config.firingRadius);

        GunHandling gunHandling = GetComponent<GunHandling>();
        gunHandling?.SetFireConfig(config.projectileDamage, config.roundsPerMinute,
            config.randomTargetRadius);
    }
}
