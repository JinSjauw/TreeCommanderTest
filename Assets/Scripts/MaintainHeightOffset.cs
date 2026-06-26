using UnityEditor;
using UnityEngine;

public class MaintainHeightOffset : MonoBehaviour
{
    [SerializeField] private LayerMask obstacleMasks;
    [SerializeField] private float raycastDistance = 6f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float standardHeightOffset = 0.45f;

    void FixedUpdate()
    {
        if (transform != null)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastDistance, obstacleMasks))
            {
                Vector3 newPosition = transform.position;
                newPosition.y = hit.point.y + heightOffset;
                transform.position = newPosition;
            }
            else
            {
                Vector3 newPosition = transform.position;
                newPosition.y = standardHeightOffset;
                transform.position = newPosition;
            }
        }
    }
}
