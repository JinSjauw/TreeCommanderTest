using UnityEngine;

public class SmoothTransformFollower : MonoBehaviour
{
    [SerializeField] private Transform follower;
    [SerializeField] private float changeRate = 5f;
    [SerializeField] private float threshold = 0.01f;

    private Vector3 targetPosition;
    private bool onTarget;
    private bool targetSet;

    public bool OnTarget => onTarget;

    private void Awake()
    {
        targetPosition = follower.position;
    }

    private void Update()
    {
        if (!targetSet) return;

        follower.position = Vector3.MoveTowards(
            follower.position, targetPosition, changeRate * Time.deltaTime);

        if (Vector3.Distance(follower.position, targetPosition) < threshold)
        {
            onTarget = true;
            targetSet = false;
        }
    }

    public void SetTarget(Vector3 position)
    {
        Debug.Log($"Set Target follower: {position}");
        targetPosition = position;
        onTarget = false;
        targetSet = true;
    }
}
