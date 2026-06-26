using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Core enemy component. Handles layer masking, holds shared system references,
/// pathfinding math, and death. Tuning values come from EnemyInitializer (on spawn)
/// and mask configuration from EnemyManager. BT nodes resolve specific systems
/// (EnemyDetectionSystem, NavMeshAgent, GunHandling) directly — no middleman.
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Systems")]
    [field: SerializeField] public EnemyDetectionSystem Detection { get; private set; }
    [field: SerializeField] public GunHandling GunHandling { get; private set; }

    [Header("Targeting")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Pathfinding")]
    [SerializeField] private float minimumPadding = 0.15f;

    private float maintainDistance;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform visualBody;

    public bool CanMove { get; set; }
    public bool HasPath { get; private set; }
    public bool InPosition { get; private set; } = true;

    public NavMeshAgent Agent => agent;
    public EventHandler<EnemyController> OnDestructionEvent;

    private bool masksConfigured;

    private void OnEnable()
    {
        if (!masksConfigured)
        {
            if (GunHandling != null)
            {
                GunHandling.ObstructionLayers = obstacleLayers;

                TrajectorySystem trajectory = GunHandling.Trajectory;
                if (trajectory != null)
                    trajectory.UpdateMasks(obstacleLayers, targetLayers);
            }
        }
    }

    private void OnDisable()
    {
        masksConfigured = false;
    }

    public void SetMasks(int selfLayer, LayerMask targetLayers, LayerMask obstacleLayers)
    {
        gameObject.layer = selfLayer;
        agent.gameObject.layer = selfLayer;
        visualBody.gameObject.layer = selfLayer;
        this.targetLayers = targetLayers;
        this.obstacleLayers = obstacleLayers;
        masksConfigured = true;

        if (GunHandling != null)
        {
            GunHandling.ObstructionLayers = obstacleLayers;

            TrajectorySystem trajectory = GunHandling.Trajectory;
            if (trajectory != null)
                trajectory.UpdateMasks(obstacleLayers, targetLayers);
        }
    }

    /// <summary>Called by EnemyInitializer to push config values.</summary>
    public void SetMovementConfig(float coneHalfAngle, float minPathDist,
        float maxPathDist, float maintainDist)
    {
        maintainDistance = maintainDist;
    }

    /// <summary>
    /// Calculates a random position around a target using a cone spread.
    /// Used by pathfinding BT nodes to generate next waypoints.
    /// </summary>
    public Vector3 CalculateTargetPosition(Vector3 target, float maxAngle, float minDist, float maxDist)
    {
        Vector3 origin = agent.transform.position;
        Vector3 directionToTarget = (target - origin).normalized;

        float distanceToTarget = Vector3.Distance(target, origin);
        float distanceAlpha = (distanceToTarget - maintainDistance) / (Detection.DetectionRadius - maintainDistance);
        distanceAlpha = distanceAlpha * 2 - 1;

        float distancePadding = distanceAlpha > 0 ? minimumPadding : -minimumPadding;
        float clampedAlpha = Mathf.Clamp01(distanceAlpha + distancePadding);

        Quaternion randomRotation = Quaternion.Euler(
            Random.Range(-maxAngle, maxAngle),
            Random.Range(-maxAngle, maxAngle),
            0);

        Vector3 randomDirection = randomRotation * directionToTarget;
        float randomDistance = Random.Range(
            minDist * clampedAlpha,
            maxDist * clampedAlpha);

        return origin + (randomDirection.normalized * randomDistance);
    }

    public void Die()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        OnDestructionEvent?.Invoke(this, this);
        this.enabled = false;
    }
}
