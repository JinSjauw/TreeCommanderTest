using UnityEngine;

public class CurveController : MonoBehaviour
{
    [SerializeField][Range(0, 1)] private float position;
    [SerializeField] private Transform startCurve, endCurve, control;
    
    public bool InterpolateTo {  get => interpolateTo; set => interpolateTo = value; }
    public bool ReachedDesiredHeight { get => reachedDesiredHeight; }
    public Vector3 ControlPosition { get => control.position; }
    public float DesiredCurveHeight {  get => desiredCurveHeight; set => desiredCurveHeight = value; }
    public float CurrentCurveHeight { get => transform.position.y; }

    [SerializeField] private float desiredCurveHeight;
    [SerializeField] private float changeRate = 5;
    [SerializeField] private bool interpolateTo;
    private bool reachedDesiredHeight;

    private float interpolatedHeight;

    void Update()
    {
        Vector3 inlinePositon = Vector3.Lerp(startCurve.position, endCurve.position, position);

        if (interpolateTo) 
        {
            interpolatedHeight = Mathf.MoveTowards(interpolatedHeight, desiredCurveHeight, changeRate * Time.deltaTime);
            control.position = new Vector3(inlinePositon.x, interpolatedHeight, inlinePositon.z);

            if(Mathf.Abs(interpolatedHeight - desiredCurveHeight) < 0.01f) 
            {
                reachedDesiredHeight = true;
            }
            else 
            {
                reachedDesiredHeight = false;
            }
        }
        else 
        {
            control.position = new Vector3(inlinePositon.x, desiredCurveHeight, inlinePositon.z);
        }
    }

    public void SetHeight(float curveHeight) 
    {
        Vector3 inlinePositon = Vector3.Lerp(startCurve.position, endCurve.position, position);
        control.position = new Vector3(inlinePositon.x, curveHeight, inlinePositon.z);

        desiredCurveHeight = curveHeight;
    }
}
