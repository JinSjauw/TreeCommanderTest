using UnityEngine;

public struct TrajectoryValidationResult
{
    public bool IsValid;
    public Vector3 HitPoint;
}

public class TrajectoryValidator : MonoBehaviour
{
    [SerializeField] private int linePositions = 8;
    [SerializeField] private LayerMask groundMask;

    private void Awake()
    {
        if (groundMask == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    public TrajectoryValidationResult Validate(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        Vector3 oldPosition = start;

        for (int i = 0; i < linePositions; i++)
        {
            float t = (float)i / linePositions;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, t);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if (Physics.Linecast(oldPosition, nextPosition, out RaycastHit hit, groundMask))
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
