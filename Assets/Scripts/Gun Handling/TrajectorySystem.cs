using UnityEngine;

public class TrajectorySystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform trajectoryStart;
    [SerializeField] private CurveController fireCurve;
    [SerializeField] private Transform fireTarget;
    [SerializeField] private TrajectorySearchSettings directFire;
    [SerializeField] private TrajectorySearchSettings indirectFire;
    private TrajectoryValidator validator;

    [Header("Obstacle Settings")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask targetMask;

    private int attemptsTotal;

    private float curveHeight;
    private Vector3 trajectoryTargetPosition;
    private bool hasTrajectory;
    private bool hasAimTarget;
    private bool hasFailed;
    private bool hasLineOfSight;

    private TrajectorySearchSettings currentSettings;

    public bool HasTrajectory => hasTrajectory;
    public bool HasAimTarget => hasAimTarget;
    public bool HasFailed => hasFailed;
    private void Awake()
    {
        if (obstacleMask == 0)
        {
            obstacleMask = LayerMask.GetMask("Ground");
        }

        if (directFire == null)
        {
            Debug.LogError("[TrajectorySystem] SettingsWithLoS not assigned.");
            return;
        }

        validator = new TrajectoryValidator(directFire.segmentCount, obstacleMask, targetMask, fireCurve.GetPosition());
    }
    public void SetTrajectoryTarget(Vector3 position, bool losFlag = false)
    {
        if (fireTarget != null)
            fireTarget.position = position;

        trajectoryTargetPosition = position;
        hasAimTarget = true;
        hasLineOfSight = losFlag;
    }

    public TrajectorySearchState SearchTrajectory()
    {
        hasFailed = false;
        hasTrajectory = false;

        currentSettings = hasLineOfSight ? directFire : indirectFire;

        Vector3 startPosition = trajectoryStart.position;
        Vector3 controlPosition = fireCurve.transform.position;

        if (attemptsTotal == 0)
            curveHeight = currentSettings.trajectorySearchStartHeight;

        for (int i = 0; i < currentSettings.attemptsPerFrame && !hasTrajectory; i++)
        {
            controlPosition.y = curveHeight;

            bool isValid = validator.Validate(startPosition, trajectoryTargetPosition, controlPosition, hasLineOfSight, currentSettings.segmentCount);

            if (isValid)
            {
                hasTrajectory = true;
                break;
            }

            curveHeight += currentSettings.trajectoryAdjustmentStep;
        }

        attemptsTotal++;

        if (hasTrajectory)
        {
            fireTarget.position = trajectoryTargetPosition;
            fireCurve.SetHeight(curveHeight);
            attemptsTotal = 0;
            return TrajectorySearchState.Found;
        }

        if (attemptsTotal >= currentSettings.maxTotalAttempts)
        {
            attemptsTotal = 0;
            fireCurve.SetHeight(currentSettings.trajectorySearchStartHeight);
            hasAimTarget = false;
            hasLineOfSight = false;
            hasFailed = true;
            return TrajectorySearchState.Failed;
        }

        return TrajectorySearchState.Searching;
    }

    public void ResetTrajectory()
    {
        hasAimTarget = false;
        hasTrajectory = false;
        hasFailed = false;
        hasLineOfSight = false;
    }
}
