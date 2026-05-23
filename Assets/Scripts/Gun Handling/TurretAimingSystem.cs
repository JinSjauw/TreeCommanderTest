using UnityEngine;

public class TurretAimingSystem : MonoBehaviour
{
    [SerializeField] private SmoothTransformFollower follower;

    public bool OnTarget => follower.OnTarget;

    public void SetTarget(Vector3 worldPosition)
    {
        Debug.Log($"Set Target: {worldPosition}");
        follower.SetTarget(worldPosition);
    }
}
