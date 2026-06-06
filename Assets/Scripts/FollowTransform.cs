using Unity.VisualScripting;
using UnityEngine;

public class FollowTransform : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool ignoreHeight = false;
    [SerializeField] private bool ignoreRotation = false;

    private void OnEnable() 
    {
        UpdateFollowPosition();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateFollowPosition();
    }

    private void UpdateFollowPosition()
    {
        if (ignoreHeight) 
        {
            Vector3 followPosition = followTarget.position;
            followPosition.y = transform.position.y;
            transform.position = followPosition;

            if (!ignoreRotation)
            {
                transform.rotation = followTarget.rotation;
            }
        }
        else 
        {
            transform.position = followTarget.position;
        }
    }
}
