using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LegController : MonoBehaviour
{
    //Need to update the height for target for uneven terrain.
    [SerializeField] private Transform legTargetTransform;
    [SerializeField] private Transform currentLegTransform;
    [SerializeField] private Transform legTransform;
    [SerializeField] private AnimationCurve legHeightCurve;

    [SerializeField] private float legHeightOffset = 0.35f;
    [SerializeField] private float legHeightApex = 0.35f;

    [SerializeField] private float maxDistanceFromTarget = 0.14f;

    [SerializeField] private float legMoveTime;
    private float legMoveTimer;

    Vector3 oldPosition;
    private float distanceFromTarget;

    private bool legIsMoving = false;
    private bool canUpdate = false;
    private bool isGrounded = false;

    private Transform bodyTransform;

    [SerializeField] private int legID;

    public EventHandler<int> moveFinishedEvent;

    private void OnEnable()
    {
        if(bodyTransform == null)
        {
            bodyTransform = GetComponentInParent<LegManager>().GetBodyTransform();
        }

        SnapToGround();
    }

    /// <summary>
    /// Instantly snaps currentLegTransform and legTransform to the ground below legTargetTransform
    /// without any animation. Call this on spawn or pool reuse to avoid the initial leg-slide.
    /// </summary>
    public void SnapToGround()
    {
        legIsMoving = false;
        legMoveTimer = 0f;
        distanceFromTarget = 0f;

        Vector3 rayOrigin = legTargetTransform.position;
        rayOrigin.y = 20f;

        if(Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, float.MaxValue, LayerMask.GetMask("Ground")))
        {
            Vector3 groundPos = hit.point;

            currentLegTransform.position = groundPos;
            currentLegTransform.up = -hit.normal;

            legTransform.position = groundPos;
            legTransform.up = -hit.normal;
        }

        oldPosition = currentLegTransform.position;
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = CheckGrounded();

        if (!legIsMoving) 
        {
            FixLeg();
        }
        else 
        {
            AnimateLeg();
        }

        //UpdateLegTarget(legTransform.position, legTargetTransform);

        if (!canUpdate) return;

        distanceFromTarget = Vector3.Distance(legTransform.position, legTargetTransform.position);

        if(distanceFromTarget >= maxDistanceFromTarget && !legIsMoving) 
        {
            oldPosition = currentLegTransform.position;

            Vector3 heightOffset = Vector3.zero;

            heightOffset.y = 5;

            UpdateLegTarget(legTargetTransform.position + heightOffset, legTargetTransform);
            UpdateLegTarget(legTargetTransform.position + heightOffset, currentLegTransform);
            legIsMoving = true;
        }
    }

    private bool CheckGrounded() 
    {
        return Physics.Raycast(legTransform.position, legTransform.up, legTransform.position.y + legHeightOffset + 0.05f);
    }

    private void FixLeg() 
    {
        legTransform.position = currentLegTransform.position;
        legTransform.up = currentLegTransform.up;
    }

    private void AnimateLeg() 
    {
        //Lerp XZ to target position
        //Use animation curve for y height + heightOffset
        
        //Timer
        if(legMoveTimer < legMoveTime) 
        {
            legMoveTimer += Time.deltaTime;
        }
        else 
        {
            legMoveTimer = 0;
            legIsMoving = false;

            moveFinishedEvent?.Invoke(this, legID);

            oldPosition = currentLegTransform.position;
        }

        float moveAlpha = (legMoveTimer - 0) / (legMoveTime - 0);

        Vector3 newPosition = Vector3.Lerp(oldPosition, legTargetTransform.position, moveAlpha);
        
        newPosition.y += legHeightApex * legHeightCurve.Evaluate(moveAlpha);

        currentLegTransform.position = newPosition;

        FixLeg();
    }

    private void UpdateLegTarget(Vector3 updatePosition, Transform updateTransform) 
    {
        //Debug.DrawRay(updatePosition, -bodyTransform.up, Color.blue, 10);

        if(Physics.Raycast(updatePosition, Vector3.down, out RaycastHit hit, float.MaxValue, LayerMask.GetMask("Ground"))) 
        {
            updateTransform.position = hit.point + new Vector3(0, legHeightOffset, 0);
            updateTransform.up = -hit.normal;
        }
    }

    public void SetCanUpdate(bool state) 
    {
        canUpdate = state;
    }

    public int GetID() 
    {
        return legID;
    }

    public bool IsGrounded() 
    {
        return isGrounded;
    }

    public Vector3 GetFootPosition() 
    {
        return legTransform.position;
    }

    public Vector3 GetTargetPosition()
    {
        return legTargetTransform.position;
    }
}
