using UnityEngine;
using Random = UnityEngine.Random;

public enum TrajectorySearchState { Searching, Found, Failed }

public class GunHandling : MonoBehaviour
{
    [Header("Turret")]
    [SerializeField] private TurretController turretController;

    [Header("Trajectory")]
    [SerializeField] private TrajectorySystem trajectory;

    [Header("Firing")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Transform projectileTargetTransform;
    [SerializeField] private CurveController fireCurve;
    [SerializeField] private ProjectileVariables projectileVariables;
    [SerializeField] private Transform projectileCollection;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileDamage;
    [SerializeField] private float roundPerMinute;

    [Header("Aim Targeting")]
    [SerializeField] private float randomTargetRadius = 0.5f;
    private Transform aimOrigin;

    private float reloadDuration;
    private float reloadTimer;
    private bool isReloading;

    private float fireDelay;
    private float firingDelayTimer;

    private ObjectPool pool;

    public LayerMask ObstructionLayers { get; set; }
    public bool OnTarget => turretController.UpdateOnTarget();
    public bool HasTrajectory => trajectory.HasTrajectory;
    public bool HasAimTarget => trajectory.HasAimTarget;
    public bool HasFailed => trajectory.HasFailed;
    public bool IsReloading => isReloading;
    public TrajectorySystem Trajectory => trajectory;

    private void Start()
    {
        pool = FindFirstObjectByType<ObjectPool>();
        aimOrigin = turretController.GetTurretBase();
        reloadDuration = 60f / roundPerMinute;
    }

    private void Update()
    {
        TickFiringCooldown(Time.deltaTime);
    }

    public void SetFiringCooldown(float cooldown)
    {
        reloadDuration = cooldown;
    }

    public void SetFireDelay(float delay)
    {
        fireDelay = delay;
    }

    public void SetAiming(bool aiming)
    {
        turretController.SetAiming(aiming);
    }

    public bool SelectAimTarget(Transform attackTarget)
    {
        if (attackTarget == null) return false;
        
        Vector3 originPosition = aimOrigin.position;

        Vector3 randomFactor = Random.insideUnitSphere * randomTargetRadius;
        Vector3 overshootDirection = attackTarget.position - originPosition;

        bool directLineOfSight = !Physics.Linecast( originPosition, attackTarget.position, ObstructionLayers );

        float overshootFactor = directLineOfSight ? 50f : 1.5f; //placeholder numbers
        
        Vector3 randomPosition = attackTarget.position
            + (overshootDirection.normalized * overshootFactor)
            + randomFactor;

        if (!directLineOfSight && Physics.Raycast(randomPosition, Vector3.down, out RaycastHit hit, 100f, ObstructionLayers))
        {
            randomPosition = hit.point;
        }

        trajectory.SetTrajectoryTarget(randomPosition, directLineOfSight);
        return true;
    }

    public TrajectorySearchState SearchTrajectory()
    {
        return trajectory.SearchTrajectory();
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
                projectileTargetTransform.position,
                fireCurve.transform.position,
                CalculateTravelTime(),
                projectileDamage,
                pool,
                false);
        }
        else
        {
            Debug.LogError("Couldn't get Projectile component on: " + newProjectile.name);
        }

        trajectory.ResetTrajectory();
        reloadTimer = 0f;
        isReloading = true;
    }

    public bool TickFiringCooldown(float deltaTime)
    {
        if (!isReloading)
            return false;

        reloadTimer += deltaTime;

        if (reloadTimer >= reloadDuration)
        {
            reloadTimer = 0f;
            isReloading = false;
            return true;
        }

        return false;
    }

    public bool TickFiringDelay(float deltaTime)
    {
        //if (!isTickingFireDelay) return false;

        firingDelayTimer += deltaTime;

        if (firingDelayTimer >= fireDelay)
        {
            firingDelayTimer = 0f;
            //isTickingFireDelay = false;
            return true;
        }

        return false;
    }

    private float CalculateTravelTime()
    {
        float distance = Vector3.Distance(muzzleTransform.position, projectileTargetTransform.position);
        return projectileVariables.GetTravelTime(distance, fireCurve.CurrentCurveHeight);
    }
}
