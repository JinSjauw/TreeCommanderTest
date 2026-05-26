using UnityEngine;

public class TrajectorySystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform trajectoryStart;
    [SerializeField] private CurveController fireCurve;
    [SerializeField] private Transform fireTarget;
    private TrajectoryValidator validator;

    [Header("Obstacle Settings")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("Search Settings")]
    [SerializeField] private int attemptsPerFrame = 2;
    [SerializeField] private int maxTotalAttempts = 8;
    [SerializeField] private float trajectoryAdjustmentStep = 1f;
    [SerializeField] private float trajectorySearchStartHeight = 2f;
    [SerializeField] private int segmentCount = 8;

    private int attemptsTotal;
    private float curveHeight;
    private Vector3 trajectoryTargetPosition;
    private bool hasTrajectory;
    private bool hasAimTarget;
    private bool hasFailed;

    public bool HasTrajectory => hasTrajectory;
    public bool HasAimTarget => hasAimTarget;
    public bool HasFailed => hasFailed;
    private void Awake()
    {
        if (obstacleMask == 0)
        {
            obstacleMask = LayerMask.GetMask("Ground");
        }

        validator = new TrajectoryValidator(segmentCount, obstacleMask, fireCurve.GetPosition());
        
    }
    public void SetTrajectoryTarget(Vector3 position)
    {
        if (fireTarget != null)
            fireTarget.position = position;

        trajectoryTargetPosition = position;
        hasAimTarget = true;
    }

    public TrajectorySearchState SearchTrajectory()
    {
        hasFailed = false;
        hasTrajectory = false;

        Vector3 startPosition = trajectoryStart.position;
        Vector3 controlPosition = fireCurve.transform.position;

        if (attemptsTotal == 0)
            curveHeight = trajectorySearchStartHeight;

        for (int i = 0; i < attemptsPerFrame && !hasTrajectory; i++)
        {
            controlPosition.y = curveHeight;

            bool isValid = validator.Validate(startPosition, trajectoryTargetPosition, controlPosition);

            if (isValid)
            {
                hasTrajectory = true;
                break;
            }

            curveHeight += trajectoryAdjustmentStep;
        }

        attemptsTotal++;

        if (hasTrajectory)
        {
            fireTarget.position = trajectoryTargetPosition;
            fireCurve.SetHeight(curveHeight);
            attemptsTotal = 0;
            return TrajectorySearchState.Found;
        }

        if (attemptsTotal >= maxTotalAttempts)
        {
            attemptsTotal = 0;
            fireCurve.SetHeight(trajectorySearchStartHeight);
            hasAimTarget = false;
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
    }
}
