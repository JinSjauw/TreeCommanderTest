using UnityEngine;

public enum TrajectorySearchState { Searching, Found, Failed }

public class TrajectorySystem : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private Transform trajectoryStart;
    [SerializeField] private Transform previewTarget;
    [SerializeField] private CurveController trajectoryCurve;
    [SerializeField] private CurveController fireCurve;
    [SerializeField] private TrajectoryValidator validator;
    [SerializeField] private TurretAimingSystem aimingSystem;

    [Header("Search Settings")]
    [SerializeField] private int attemptsPerFrame = 2;
    [SerializeField] private int maxTotalAttempts = 8;
    [SerializeField] private float trajectoryAdjustmentStep = 1f;

    private int attemptsTotal;
    private float curveHeight;
    private Vector3 targetPosition;

    private bool hasTrajectory;
    private bool hasTarget;
    private bool hasFailed;

    public bool HasTrajectory => hasTrajectory;
    public bool HasFailed => hasFailed;
    public bool HasTarget => hasTarget;

    public void SetTarget(Vector3 position)
    {
        if (previewTarget != null)
            previewTarget.position = position;

        targetPosition = position;
        hasTarget = true;
    }

    public TrajectorySearchState FindTrajectory(float startingHeight)
    {
        hasFailed = false;
        hasTrajectory = false;

        Vector3 startPosition = trajectoryStart.position;

        if (attemptsTotal == 0)
        {
            curveHeight = startingHeight;
            trajectoryCurve.SetHeight(startingHeight);
        }

        for (int i = 0; i < attemptsPerFrame && !hasTrajectory; i++)
        {
            trajectoryCurve.SetHeight(curveHeight);
            Vector3 controlPosition = trajectoryCurve.transform.position;

            TrajectoryValidationResult result = validator.Validate(
                startPosition, targetPosition, controlPosition);

            if (result.IsValid)
            {
                hasTrajectory = true;
                break;
            }

            curveHeight += trajectoryAdjustmentStep;
        }

        attemptsTotal++;

        if (hasTrajectory)
        {
            if (aimingSystem != null)
                aimingSystem.SetTarget(targetPosition);

            fireCurve.InterpolateTo = true;
            fireCurve.DesiredCurveHeight = curveHeight + trajectoryAdjustmentStep;
            attemptsTotal = 0;
            return TrajectorySearchState.Found;
        }

        if (attemptsTotal >= maxTotalAttempts)
        {
            attemptsTotal = 0;
            hasTarget = false;
            hasFailed = true;
            return TrajectorySearchState.Failed;
        }

        return TrajectorySearchState.Searching;
    }

    public void ResetTries()
    {
        hasFailed = false;
    }

    public void ResetTrajectory()
    {
        hasTarget = false;
        hasTrajectory = false;
    }
}
