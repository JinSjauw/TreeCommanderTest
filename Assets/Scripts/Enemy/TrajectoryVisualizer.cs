using UnityEngine;

public class TrajectoryVisualizer : MonoBehaviour
{
    [SerializeField] private Transform origin;
    [SerializeField] private Transform destination;
    [SerializeField] private Transform control;

    [SerializeField] private Transform previewOrigin;
    [SerializeField] private Transform previewDestination;
    [SerializeField] private Transform previewControl;

    [SerializeField] private LineRenderer previewLineRenderer;
    [SerializeField] private LineRenderer realLineRenderer;
    [SerializeField] private LineRenderer occludedLineRenderer;

    private Vector3 offsetY = new Vector3(0, 0.001f, 0);

    //Have a transform replace both origins. 
    //When shot both lines show a cutoff for the projectile trail
    //Calculate the line positions you don't want to draw

    private void Update()
    {
        DrawPreviewTrajectory();
        DrawRealTrajectory();
    }

    private void DrawRealTrajectory() 
    {
        int linePositions = 40;
        realLineRenderer.positionCount = linePositions;
        occludedLineRenderer.positionCount = linePositions;

        for (int i = 0; i < linePositions; i++)
        {
            realLineRenderer.SetPosition(i, QuadraticCurve.EvaluateCurve(origin.position, destination.position, control.position, (float)i / (float)linePositions) + offsetY);
            occludedLineRenderer.SetPosition(i, QuadraticCurve.EvaluateCurve(origin.position, destination.position, control.position, (float)i / (float)linePositions));
        }
    }

    //Lerp real transforms to preview positions;
    private void DrawPreviewTrajectory() 
    {
        int linePositions = 40;
        previewLineRenderer.positionCount = linePositions;

        for (int i = 0; i < linePositions; i++)
        {
            previewLineRenderer.SetPosition(i, QuadraticCurve.EvaluateCurve(previewOrigin.position, previewDestination.position, previewControl.position, (float)i / (float)linePositions));
        }
    }
}
