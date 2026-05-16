using UnityEngine;

public class SmoothTransformFollower : MonoBehaviour
{
    [SerializeField] private Transform follower;
    [SerializeField] private float changeRate = 5f;

    private Vector3 targetPosition;
    private bool onTarget;

    public bool OnTarget => onTarget;

    private void Awake()
    {
        targetPosition = follower.position;
    }

    private void Update()
    {
        follower.position = Vector3.MoveTowards(
            follower.position, targetPosition, changeRate * Time.deltaTime);

        if (Vector3.Distance(follower.position, targetPosition) < 0.01f)
        {
            onTarget = true;
        }
    }

    public void SetTarget(Vector3 position)
    {
        targetPosition = position;
        onTarget = false;
    }
}
