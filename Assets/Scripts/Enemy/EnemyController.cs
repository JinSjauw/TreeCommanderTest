using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class EnemyController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string enemySignID;

    [Header("Shared Subsystems")]
    [field: SerializeField] public TurretAimingSystem Aiming { get; private set; }
    [field: SerializeField] public TrajectorySystem Trajectory { get; private set; }
    [field: SerializeField] public CurveController FireCurve { get; private set; }

    [Header("Detection")]
    [SerializeField] private float firingRadius;
    [SerializeField] private float detectionRadius;

    [SerializeField] private Transform playerTarget;

    [Header("Pathfinding")]
    [SerializeField] private float maxConeAngle;
    [SerializeField] private float minDistance;
    [SerializeField] private float maxDistance;
    [SerializeField] private float maintainDistance;
    [SerializeField] private float minimumPadding = 0.15f;

    [SerializeField] private NavMeshAgent agent;
    private NavMeshPath path;

    private Vector3 pathOrigin;
    private Vector3 pathDestination;

    public bool CanMove { get; set; }
    public bool HasPath { get; private set; }
    public bool InPosition { get; private set; } = true;

    [Header("Target Selection")]
    [SerializeField] private float randomTargetRadius;

    [Header("Firing")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Transform targetTransform;

    [Header("Projectile")]
    [SerializeField] private ProjectileVariables projectileVariables;
    [SerializeField] private Transform projectileCollection;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileDamage;
    [SerializeField] private float roundPerMinute;

    [Header("State")]
    [SerializeField] private bool isAwareOfPlayer = true;

    private float firingTimer;
    private ObjectPool pool;

    public bool IsReloading { get; private set; }

    public EventHandler<EnemyController> OnDestructionEvent;

    public void InitializeEnemy(Transform playerTransform, Vector3 spawnPosition, string ID)
    {
        playerTarget = playerTransform;
        transform.position = spawnPosition;
        enemySignID = ID;

        this.enabled = true;
    }

    void Start()
    {
        path = new NavMeshPath();
        pool = FindFirstObjectByType<ObjectPool>();
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
        IsReloading = true;
    }

    public float CalculateTravelTime()
    {
        float distance = Vector3.Distance(muzzleTransform.position, targetTransform.position);
        return projectileVariables.GetTravelTime(distance, FireCurve.DesiredCurveHeight);
    }

    public void PickRandomTargetNearPlayer()
    {
        if (playerTarget == null)
            return;

        Vector2 randomFactor = Random.insideUnitCircle * randomTargetRadius;

        Vector3 overshootDirection = playerTarget.position - agent.transform.position;

        bool directLineOfSight = !Physics.Linecast(
            muzzleTransform.position, playerTarget.position, LayerMask.GetMask("Ground"));

        float overshootFactor = directLineOfSight ? 2.2f : 0.75f;

        Vector3 randomPosition = playerTarget.position
            + (overshootDirection.normalized * overshootFactor)
            + new Vector3(randomFactor.x, 0, randomFactor.y);

        if (Physics.Raycast(randomPosition, Vector3.down, out RaycastHit hit, 100f, LayerMask.GetMask("Ground")))
        {
            randomPosition = hit.point;
        }

        Trajectory.SetTarget(randomPosition);
    }

    public float GetDistanceToPlayer()
    {
        if (playerTarget == null)
            return float.MaxValue;

        return Vector2.Distance(
            new Vector2(playerTarget.position.x, playerTarget.position.z),
            new Vector2(agent.transform.position.x, agent.transform.position.z));
    }

    public bool IsPlayerInFiringRadius()
    {
        return GetDistanceToPlayer() <= firingRadius;
    }

    public bool IsPlayerInDetectionRadius()
    {
        return GetDistanceToPlayer() <= detectionRadius;
    }

    public bool HasDirectLineOfSight()
    {
        if (playerTarget == null)
            return false;

        return !Physics.Linecast(
            muzzleTransform.position, playerTarget.position, LayerMask.GetMask("Ground"));
    }

    public bool IsAwareOfPlayer()
    {
        return isAwareOfPlayer;
    }

    public void CalculateNewPathToTarget()
    {
        InPosition = false;

        Vector3 nextPosition = CalculateTargetPosition(
            playerTarget.position, maxConeAngle, minDistance, maxDistance);

        if (agent.CalculatePath(nextPosition, path) && !HasPath)
        {
            agent.SetPath(path);
            pathOrigin = agent.transform.position;
            pathDestination = path.corners[path.corners.Length - 1];

            HasPath = true;
            CanMove = false;
        }
    }

    public Vector3 CalculateTargetPosition(Vector3 target, float maxAngle, float minDist, float maxDist)
    {
        Vector3 origin = agent.transform.position;
        Vector3 directionToTarget = (target - origin).normalized;

        float distanceToTarget = Vector3.Distance(target, origin);
        float distanceAlpha = (distanceToTarget - maintainDistance) / (detectionRadius - maintainDistance);
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
}
