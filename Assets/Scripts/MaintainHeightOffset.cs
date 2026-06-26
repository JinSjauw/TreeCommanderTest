using UnityEditor;
using UnityEngine;

public class MaintainHeightOffset : MonoBehaviour
{
    [SerializeField] private LayerMask obstacleMasks;
    [SerializeField] private float raycastDistance = 6f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float standardHeightOffset = 0.45f;

    private Transform targetTransform;
    private Vector3 standardHitPosition;

    private void Awake() {
        standardHitPosition = transform.position;
        standardHitPosition.y = standardHeightOffset;
        targetTransform = transform;
    }

    void FixedUpdate()
    {
        if (targetTransform != null)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastDistance, obstacleMasks))
            {
                Vector3 hitPosition = hit.point;
                hitPosition.y += heightOffset;
                targetTransform.position = hitPosition;
            }
            else
            {
                targetTransform.position = standardHitPosition;
            }
        }
    }
}
