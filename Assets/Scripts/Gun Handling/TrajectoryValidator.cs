using UnityEngine;

public struct TrajectoryValidationResult
{
    public bool IsValid;
    public Vector3 HitPoint;
}

public class TrajectoryValidator : MonoBehaviour
{
    [SerializeField] private int linePositions = 8;
    [SerializeField] private LayerMask obstacleMask;

    private void Awake()
    {
        if (obstacleMask == 0)
            obstacleMask = LayerMask.GetMask("Ground");
    }

    public TrajectoryValidationResult Validate(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        Vector3 oldPosition = start;

        for (int i = 0; i < linePositions; i++)
        {
            float t = (float)i / linePositions;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, t);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if (Physics.Linecast(oldPosition, nextPosition, out RaycastHit hit, obstacleMask))
            {
                return new TrajectoryValidationResult
                {
                    IsValid = false,
                    HitPoint = hit.point
                };
            }

            oldPosition = nextPosition;
        }

        return new TrajectoryValidationResult { IsValid = true };
    }
}
