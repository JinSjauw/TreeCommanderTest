using System;
using UnityEngine;
using UnityEngine.AI;
using BehaviourTree.Runtime;
using Random = UnityEngine.Random;

public class EnemyController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string enemySignID;

    [Header("Shared Subsystems")]
    [field: SerializeField] public TurretAimingSystem Aiming { get; private set; }
    [field: SerializeField] public TrajectorySystem Trajectory { get; private set; }
    [field: SerializeField] public CurveController FireCurve { get; private set; }
    [field: SerializeField] public TurretController Turret { get; private set; }

    [Header("Detection")]
    [field: SerializeField] public EnemyDetectionSystem Detection { get; private set; }

    [Header("Pathfinding")]
    [SerializeField] private float maxConeAngle;
    [SerializeField] private float minDistance;
    [SerializeField] private float maxDistance;
    [SerializeField] private float maintainDistance;
    [SerializeField] private float minimumPadding = 0.15f;

    [SerializeField] private NavMeshAgent agent;
    public bool CanMove { get; set; }
    public bool HasPath { get; private set; }
    public bool InPosition { get; private set; } = true;

    [Header("Target Selection")]
    [SerializeField] private float randomTargetRadius;
    [SerializeField] private Transform patrolPointsParent;

    [Header("Firing")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Transform targetTransform;

    [Header("Projectile")]
    [SerializeField] private ProjectileVariables projectileVariables;
    [SerializeField] private Transform projectileCollection;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileDamage;
    [SerializeField] private float roundPerMinute;

    private float firingTimer;
    private ObjectPool pool;
    public bool IsReloading { get; private set; }
    public NavMeshAgent Agent => agent;
    public bool HasAimTarget => Trajectory.HasTarget;
    public EventHandler<EnemyController> OnDestructionEvent;

    private Transform selectedTarget;

    void Start()
    {
        pool = FindFirstObjectByType<ObjectPool>();
    }

    void Update()
    {
        //TickFiringCooldown(Time.deltaTime);
    }

    public bool TickFiringCooldown(float deltaTime)
    {
        if (!IsReloading)
            return false;

        float firingDelay = 1.0f / (roundPerMinute / 60f);
        firingTimer += deltaTime;

        if (firingTimer >= firingDelay)
        {
            firingTimer = 0f;
            IsReloading = false;
            return true;
        }

        return false;
    }

    public Transform SelectTarget(SelectionStrategy strategy)
    {
        if (!Detection.DetectTargets())
            return null;

        selectedTarget = Detection.GetTarget(strategy);

        return selectedTarget;
    }

    public void Fire()
    {
        GameObject newProjectile = pool.GetObject(projectilePrefab);
        newProjectile.transform.position = muzzleTransform.position;
        newProjectile.transform.forward = muzzleTransform.forward;
        newProjectile.transform.parent = projectileCollection;

        if (newProjectile.TryGetComponent(out Projectile projectileComponent))
        {
            projectileComponent.InitProjectile(
                muzzleTransform.position,
                targetTransform.position,
                FireCurve.transform.position,
                CalculateTravelTime(),
                projectileDamage,
                pool,
                false);
        }
        else
        {
            Debug.LogError("Couldn't get Projectile component on: " + newProjectile.name);
        }

        Trajectory.ResetTrajectory();
        firingTimer = 0f;
        IsReloading = true;
    }

    public float CalculateTravelTime()
    {
        float distance = Vector3.Distance(muzzleTransform.position, targetTransform.position);
        return projectileVariables.GetTravelTime(distance, FireCurve.DesiredCurveHeight);
    }

    public Vector3 CalculateNewPathToTarget()
    {
        if (selectedTarget == null)
            return Vector3.zero;

        Vector3 nextPosition = CalculateTargetPosition(selectedTarget.position, maxConeAngle, minDistance, maxDistance);
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

    public string GetID()
    {
        return enemySignID;
    }

    public void Die()
    {
        agent.isStopped = true;
        OnDestructionEvent?.Invoke(this, this);
        this.enabled = false;
    }

    public Vector3 SetNextPatrolPoint()
    {
        int randomIndex = Random.Range(0, patrolPointsParent.childCount);
        Vector3 point = patrolPointsParent.GetChild(randomIndex).position;
        agent.SetDestination(point);
        return point;
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

    public bool SetAimTarget()
    {
        if (selectedTarget == null) return false; 
        Vector3 trajectoryTarget = GetRandomTargetOffset(selectedTarget);
        Trajectory.SetTarget(trajectoryTarget);
        return true;
    }

    private Vector3 GetRandomTargetOffset(Transform attackTarget)
    {
        Vector2 randomFactor = Random.insideUnitCircle * randomTargetRadius;

        Vector3 overshootDirection = attackTarget.position - agent.transform.position;

        bool directLineOfSight = !Physics.Linecast(
            muzzleTransform.position, attackTarget.position, LayerMask.GetMask("Ground"));

        float overshootFactor = directLineOfSight ? 2.2f : 0.75f;

        Vector3 randomPosition = attackTarget.position
            + (overshootDirection.normalized * overshootFactor)
            + new Vector3(randomFactor.x, 0, randomFactor.y);

        if (Physics.Raycast(randomPosition, Vector3.down, out RaycastHit hit, 100f, LayerMask.GetMask("Ground")))
        {
            randomPosition = hit.point;
        }

        return randomPosition;
    }

}
