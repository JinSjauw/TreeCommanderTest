using System;
using UnityEngine;
using UnityEngine.AI;
using BehaviourTree.Runtime;
using Random = UnityEngine.Random;

public class EnemyController : MonoBehaviour
{

    [Header("Systems")]
    [field: SerializeField] public EnemyDetectionSystem Detection { get; private set; }
    [field: SerializeField] public GunHandling GunHandling { get; private set; }

    [Header("Targeting")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Pathfinding")]
    [SerializeField] private float maxConeHalfAngle;
    [SerializeField] private float minDistance;
    [SerializeField] private float maxDistance;
    [SerializeField] private float maintainDistance;
    [SerializeField] private float minimumPadding = 0.15f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform visualBody;

    public bool CanMove { get; set; }
    public bool HasPath { get; private set; }
    public bool InPosition { get; private set; } = true;

    public NavMeshAgent Agent => agent;
    public EventHandler<EnemyController> OnDestructionEvent;

    private Transform selectedTarget;
    private bool masksConfigured;
    private int lastPatrolPointIndex = 0;

    private void OnEnable()
    {
        if (!masksConfigured)
        {
            if (Detection != null)
                Detection.TargetLayers = targetLayers;

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

        if (Detection != null)
            Detection.TargetLayers = targetLayers;

        if (GunHandling != null)
        {
            GunHandling.ObstructionLayers = obstacleLayers;

            TrajectorySystem trajectory = GunHandling.Trajectory;
            if (trajectory != null)
                trajectory.UpdateMasks(obstacleLayers, targetLayers);
        }
    }

    public Transform SelectTarget(SelectionStrategy strategy)
    {
        if (!Detection.DetectTargets())
            return null;

        selectedTarget = Detection.GetTarget(strategy);
        return selectedTarget;
    }

    public bool SelectAimTarget()
    {
        if (selectedTarget == null) return false;
        return GunHandling.SelectAimTarget(selectedTarget);
    }

    public TrajectorySearchState SearchTrajectory()
    {
        return GunHandling.SearchTrajectory();
    }

    public void Fire()
    {
        GunHandling.Fire();
    }

    public Vector3 CalculateNewPathToTarget()
    {
        if (selectedTarget == null)
            return Vector3.zero;

        Vector3 nextPosition = CalculateTargetPosition(selectedTarget.position, maxConeHalfAngle, minDistance, maxDistance);
        agent.SetDestination(nextPosition);
        return nextPosition;
    }

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
        agent.isStopped = true;
        OnDestructionEvent?.Invoke(this, this);
        this.enabled = false;
    }

    public Vector3 SetRandomPatrolPoint(Transform patrolPointsParent)
    {
        int randomIndex = Random.Range(0, patrolPointsParent.childCount);
        lastPatrolPointIndex = randomIndex;

        return patrolPointsParent.GetChild(randomIndex).position;
    }

    public Vector3 SetNextPatrolPoint(Transform patrolPointsParent)
    {
        lastPatrolPointIndex = (lastPatrolPointIndex + 1) % patrolPointsParent.childCount;

        Debug.Log($"[EnemyController] Setting next patrol point to index {lastPatrolPointIndex} : {patrolPointsParent.childCount}");

        if (lastPatrolPointIndex < 0)
        {
            Debug.LogError($"[EnemyController] Last patrol point index is negative. Cannot set next patrol point.");
            return Vector3.zero;
        }

        return patrolPointsParent.GetChild(lastPatrolPointIndex).position;
    }

    public bool HasArrivedAtDestination()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    public void StopMoving()
    {
        agent.isStopped = true;
    }

    public bool TargetInFiringRange(Transform target)
    {
        return Detection.TargetInFiringRange(target);
    }

    public bool HasLineOfSightToTarget(Transform target)
    {
        return Detection.HasLineOfSightToTarget(target);
    }
}
