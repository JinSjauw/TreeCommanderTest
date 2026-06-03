using UnityEngine;

[CreateAssetMenu(fileName = "TrajectorySearchSettings", menuName = "Gun Handling/Trajectory Search Settings")]
public class TrajectorySearchSettings : ScriptableObject
{
    public int attemptsPerFrame = 2;
    public int maxTotalAttempts = 8;
    public float trajectoryAdjustmentStep = 1f;
    public float trajectorySearchStartHeight = 2f;
    public int segmentCount = 8;
}
