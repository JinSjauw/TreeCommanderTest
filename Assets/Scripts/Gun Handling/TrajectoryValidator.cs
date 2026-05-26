using UnityEngine;

public class TrajectoryValidator
{
    private int segmentCount = 8;
    private LayerMask obstacleMask;
    private float positionAlpha = 0.5f;
    public TrajectoryValidator(int segmentCount, LayerMask obstacleMask, float positionAlpha)
    {
        this.segmentCount = segmentCount;
        this.obstacleMask = obstacleMask;
        this.positionAlpha = positionAlpha;
    }

    public bool Validate(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        if (IsControlPointBelowLine(start, end, controlPoint))
        {
            return false;
        }

        Vector3 oldPosition = start;

        for (int i = 0; i < segmentCount; i++)
        {
            float segmentIndex = (float)i / segmentCount;
            Vector3 nextPosition = QuadraticCurve.EvaluateCurve(start, end, controlPoint, segmentIndex);

            Debug.DrawLine(oldPosition, nextPosition, Color.red, 2f);

            if (Physics.Linecast(oldPosition, nextPosition, obstacleMask))
            {
                return false;
            }

            oldPosition = nextPosition;
        }

        return true;
    }

    //Calculate the projected line y position of the control point and reject if valley
    private bool IsControlPointBelowLine(Vector3 start, Vector3 end, Vector3 controlPoint)
    {
        float minHeight = Mathf.Lerp(start.y, end.y, positionAlpha);
        return controlPoint.y <= minHeight; 
    }
}
