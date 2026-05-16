using UnityEngine;

public class QuadraticCurve : MonoBehaviour
{
    public static Vector3 EvaluateCurve(Vector3 a, Vector3 b, Vector3 control, float t) 
    {
        Vector3 ac = Vector3.Lerp(a, control, t);
        Vector3 cb = Vector3.Lerp(control, b, t);

        return Vector3.Lerp(ac, cb, t);
    }
}
