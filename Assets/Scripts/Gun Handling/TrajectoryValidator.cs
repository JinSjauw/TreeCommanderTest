using UnityEngine;

public class TrajectoryValidator
{
    private int segmentCount = 8;
    private LayerMask obstacleMask;
    private LayerMask targetMask;
    private float positionAlpha = 0.5f;
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

    public bool Validate(Vector3 start, Vector3 end, Vector3 controlPoint, bool hasLineOfSight, int segmentCountOverride = -1)
    {
        int segments = segmentCountOverride > 0 ? segmentCountOverride : segmentCount;
        
        if (IsControlPointBelowLine(start, end, controlPoint))
        {
            return false;
        }

        Vector3 oldPosition = start;
        bool hasDirectHit = false;
        bool hasObstacleHit = false;

        for (int i = 0; i < segments; i++)
        {
            float segmentIndex = (float)i / segments;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, segmentIndex);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if(hasLineOfSight && Physics.Linecast(oldPosition, nextPosition, targetMask))
            {
                hasDirectHit = true;
            }

            if (Physics.Linecast(oldPosition, nextPosition, obstacleMask))
            {
                hasObstacleHit = true;
            }

            oldPosition = nextPosition;
        }

        if(hasDirectHit) return true; 
        if(hasObstacleHit) return false;

        return true;
    }

    //Calculate the projected line y position of the control point and reject if valley
    private bool IsControlPointBelowLine(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        float minHeight = Mathf.Lerp(start.y, end.y, positionAlpha);
        return controlPoint.y < minHeight; 
    }
}
