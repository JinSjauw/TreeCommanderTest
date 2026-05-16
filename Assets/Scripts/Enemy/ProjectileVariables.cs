using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileVariables", menuName = "Scriptable Objects/ProjectileVariables")]
public class ProjectileVariables : ScriptableObject
{
    [SerializeField] private float maxRangeTime;
    [SerializeField] private float distanceTimeIncrement;
    [SerializeField] private float maxRange;
    [SerializeField] private float minRange;

    [SerializeField] private float maxCurveHeightTime;
    [SerializeField] private float heightTimeIncrement;
    [SerializeField] private float minCurveHeight;
    [SerializeField] private float maxCurveHeight;

    private float totalTravelTime;
    
    public float GetTravelTime(float distance, float curveHeight) 
    {
        //Normalize time
        //float distanceTime = (distance - minRange) / (maxRange - minRange);
        //float curveHeightTime = (curveHeight - minCurveHeight) / (maxCurveHeight - minCurveHeight);

        float distanceTime = distance * distanceTimeIncrement;
        float curveHeightTime = curveHeight * heightTimeIncrement;

        totalTravelTime = distanceTime + curveHeightTime;
        
        return totalTravelTime;
    }
}
