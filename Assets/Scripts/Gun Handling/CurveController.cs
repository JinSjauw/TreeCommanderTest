using UnityEngine;

public class CurveController : MonoBehaviour
{
    [SerializeField][Range(0, 1)] private float positionAlpha;
    [SerializeField] private Transform startCurve, endCurve, control;

    public Vector3 ControlPosition => control.position;
    public float CurrentCurveHeight => control.position.y;

    private float currentHeight;

    void Update()
    {
        Vector3 inlinePositon = Vector3.Lerp(startCurve.position, endCurve.position, positionAlpha);
        control.position = new Vector3(inlinePositon.x, currentHeight, inlinePositon.z);
    }

    public void SetHeight(float curveHeight)
    {
        float minHeight = Mathf.Lerp(startCurve.position.y, endCurve.position.y, positionAlpha);
        currentHeight = Mathf.Max(curveHeight, minHeight);
    }

    public float GetPosition()
    {
        return positionAlpha;
    }
}
