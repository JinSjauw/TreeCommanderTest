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

    private int attemptsTotal;

    private float curveHeight;
    private Vector3 trajectoryTargetPosition;
    private bool hasTrajectory;
    private bool hasAimTarget;
    private bool hasFailed;
    private bool hasLineOfSight;

    private TrajectorySearchSettings currentSettings;
    
    public LayerMask ObstructionMask { get; set; }
    public LayerMask targetMask { get; set; }
    public bool HasTrajectory => hasTrajectory;
    public bool HasAimTarget => hasAimTarget;
    public bool HasFailed => hasFailed;
    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (ObstructionMask == 0)
        {
            ObstructionMask = LayerMask.GetMask("Ground");
        }

        if (directFire == null)
        {
            Debug.LogError("[TrajectorySystem] SettingsWithLoS not assigned.");
            return;
        }

        validator = new TrajectoryValidator(directFire.segmentCount, ObstructionMask, targetMask, fireCurve.GetPosition());
    }

    public void SetTrajectoryTarget(Vector3 position, bool losFlag = false)
    {
        if (fireTarget != null)
            fireTarget.position = position;

        trajectoryTargetPosition = position;
        hasAimTarget = true;
        hasLineOfSight = losFlag;
    }

    public TrajectorySearchState SearchTrajectory(float trajectoryStartingHeight = -1)
    {
        hasFailed = false;
        hasTrajectory = false;

        currentSettings = hasLineOfSight ? directFire : indirectFire;

        Vector3 startPosition = trajectoryStart.position;
        Vector3 controlPosition = fireCurve.transform.position;

        if (attemptsTotal == 0)
        {
            float heightOffset = 0;

            if(hasLineOfSight)
            {
                float heightDifference = startPosition.y - trajectoryTargetPosition.y;
                if(heightDifference > 0)
                {
                    heightOffset = heightDifference;
                }
            }

            curveHeight = trajectoryStartingHeight > 0 ? trajectoryStartingHeight : 
            currentSettings.trajectorySearchStartHeight - heightOffset;
        }

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

    public void UpdateMasks(LayerMask obstructionMask, LayerMask targetMask)
    {
        ObstructionMask = obstructionMask;
        this.targetMask = targetMask;

        if(validator == null) Init(); 

        validator.SetMasks(ObstructionMask, targetMask);
    }

    public void ResetTrajectory()
    {
        hasAimTarget = false;
        hasTrajectory = false;
        hasFailed = false;
        hasLineOfSight = false;
    }
}
