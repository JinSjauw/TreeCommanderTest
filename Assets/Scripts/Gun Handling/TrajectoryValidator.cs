using UnityEngine;

public class TrajectoryValidator
{
    private int segmentCount = 8;
    private LayerMask obstacleMask;
    private LayerMask targetMask;
    private float positionAlpha = 0.5f;
    private const float TargetOvershootMargin = 3f;

    public TrajectoryValidator(int segmentCount, LayerMask obstacleMask, LayerMask targetMask, float positionAlpha)
    {
        this.segmentCount = segmentCount;
        this.obstacleMask = obstacleMask;
        this.targetMask = targetMask;
        this.positionAlpha = positionAlpha;
    }

    public void SetMasks(LayerMask obstacle, LayerMask target)
    {
        obstacleMask = obstacle;
        targetMask = target;
    }

    /// <summary>
    /// Validates a direct-fire trajectory. Obstacle checks stop once past the actual target,
    /// so overshoot geometry behind the target doesn't cause false rejections.
    /// </summary>
    public bool ValidateDirect(Vector3 start, Vector3 end, Vector3 controlPoint, 
        Vector3 actualTargetPosition, int segmentCountOverride = -1)
    {
        int segments = segmentCountOverride > 0 ? segmentCountOverride : segmentCount;

        if (IsControlPointBelowLine(start, end, controlPoint))
            return false;

        float distanceToTarget = Vector3.Distance(start, actualTargetPosition) + TargetOvershootMargin;

        Vector3 oldPosition = start;
        bool hasDirectHit = false;
        bool hasObstacleHit = false;

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, t);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if (Physics.Linecast(oldPosition, nextPosition, targetMask))
            {
                hasDirectHit = true;
            }

            // Only check obstacles up to ~target distance; ignore geometry behind the target
            // hit by the overshoot, but still catch bumps between shooter and target.
            float distanceFromStart = Vector3.Distance(start, nextPosition);
            if (distanceFromStart <= distanceToTarget)
            {
                if (Physics.Linecast(oldPosition, nextPosition, obstacleMask))
                {
                    hasObstacleHit = true;
                }
            }

            oldPosition = nextPosition;
        }

        if (hasDirectHit) return true;
        if (hasObstacleHit) return false;
        return true;
    }

    /// <summary>
    /// Validates an indirect-fire trajectory. The entire curve must clear all obstacles.
    /// </summary>
    public bool ValidateIndirect(Vector3 start, Vector3 end, Vector3 controlPoint, 
        int segmentCountOverride = -1)
    {
        int segments = segmentCountOverride > 0 ? segmentCountOverride : segmentCount;

        if (IsControlPointBelowLine(start, end, controlPoint))
            return false;

        Vector3 oldPosition = start;

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, t);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if (Physics.Linecast(oldPosition, nextPosition, obstacleMask))
                return false;

            oldPosition = nextPosition;
        }

        return true;
    }

    private bool IsControlPointBelowLine(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        float minHeight = Mathf.Lerp(start.y, end.y, positionAlpha);
        return controlPoint.y < minHeight;
    }
}
