using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class LegManager : MonoBehaviour
{
    [SerializeField] private LegController[] legGroupA;
    [SerializeField] private LegController[] legGroupB;

    private List<LegController> allLegs;

    [SerializeField] private Transform bodyTransform;
    [SerializeField] private float heightOffset;

    [SerializeField] private int legsMoved = 0;
    [SerializeField] private LegGroups groupToCheck = LegGroups.LEG_A;

    [SerializeField] private float timeToUpdate;
    [SerializeField] private float updateTimer;
    [SerializeField] private float heightSpeed;
    [SerializeField] private float rotationSpeed;

    private Vector3 oldHeight;
    private Vector3 newHeight;

    private Quaternion oldTilt;
    private Quaternion newTilt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        allLegs = new List<LegController>();

        for (int i = 0; i < legGroupA.Length; i++) 
        {
            legGroupA[i].moveFinishedEvent += CheckLegs;
            allLegs.Add(legGroupA[i]);
        }

        for (int i = 0; i < legGroupB.Length; i++)
        {
            legGroupB[i].moveFinishedEvent += CheckLegs;
            allLegs.Add(legGroupB[i]);
        }

        EnableLegGroup(LegGroups.LEG_A);
    }

    // Update is called once per frame
    void Update()
    {
        //Rotate & Set body height based on average from legPositions
        UpdateRotation();

        UpdateHeight();

        UpdateTimer();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < legGroupA.Length; i++)
        {
            legGroupA[i].moveFinishedEvent -= CheckLegs;
        }

        for (int i = 0; i < legGroupB.Length; i++)
        {
            legGroupB[i].moveFinishedEvent -= CheckLegs;
        }
    }

    private void UpdateTimer() 
    {

        //bodyTransform.position = Vector3.MoveTowards(bodyTransform.position, newHeight, heightSpeed * Time.deltaTime);
        //bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, newTilt, rotationSpeed * Time.deltaTime);

        bodyTransform.rotation = Quaternion.RotateTowards(bodyTransform.rotation, newTilt, rotationSpeed * Time.deltaTime);

        //if(updateTimer < timeToUpdate) 
        //{
        //    updateTimer += Time.deltaTime;

        //    float updateAlpha = (updateTimer - 0) / (timeToUpdate - 0);

        //    bodyTransform.position = Vector3.Lerp(bodyTransform.position, newHeight, updateAlpha);
        //    bodyTransform.rotation = Quaternion.Lerp(bodyTransform.rotation, newTilt, updateAlpha);
        //}
        //else 
        //{
        //    updateTimer = 0;
        //}
    }

    private void UpdateHeight() 
    {
        //Height based on leg average -> should potentially be changed to planted height average
        //float totalHeight = 0;
        //int addedLegs = 0;

        //for (int i = 0; i < allLegs.Count; i++)
        //{
        //    LegController leg = allLegs[i];
        //    if (!leg.IsLegMoving()) 
        //    {
        //        totalHeight += allLegs[i].GetTargetPosition().y;
        //        addedLegs++;
        //    }
        //}

        //totalHeight = totalHeight / addedLegs;

        //Vector3 newBodyPosition = bodyTransform.position;
        //newBodyPosition.y = totalHeight + heightOffset;

        //oldHeight = bodyTransform.position;
        //newHeight = newBodyPosition;

        //bodyTransform.position = newBodyPosition;

        //Directly apply height 

        Vector3 heightCheckPosition = bodyTransform.position;
        heightCheckPosition.y = 20;

        if (Physics.Raycast(bodyTransform.position, Vector3.down, out RaycastHit hit, 100, LayerMask.GetMask("Ground")))
        {
            Vector3 heightApplied = bodyTransform.position;
            heightApplied.y = hit.point.y + heightOffset;

            bodyTransform.position = heightApplied;
        }
    }

    private void UpdateRotation() 
    {
        List<Vector3> localLegs = new List<Vector3>();
        Vector3 center = Vector3.zero;

        for (int i = 0; i < allLegs.Count; i++) 
        {
            Vector3 worldPosition = allLegs[i].GetTargetPosition();
            Vector3 localPosition = bodyTransform.parent.InverseTransformPoint(worldPosition);
            
            center += localPosition;

            localLegs.Add(localPosition);
        }

        center = center / localLegs.Count;

        float averageXSlope = 0;
        float averageZSlope = 0;

        int xSlopeAdded = 0;
        int zSlopeAdded = 0;

        for (int i = 0;i < allLegs.Count; i++) 
        {
            Vector3 position = localLegs[i];

            Vector3 toPoint = position - center;
            float heightDiff = position.y - center.y;

            if (toPoint.x < 0.1f) 
            {
                averageXSlope += heightDiff / toPoint.x;
                xSlopeAdded += 1;
            }

            if(toPoint.z < 0.1f) 
            {
                averageZSlope += heightDiff / toPoint.z;
                zSlopeAdded += 1;
            }
        }

        averageXSlope /= xSlopeAdded;
        averageZSlope /= zSlopeAdded;

        float rotationX = Mathf.Atan(averageZSlope) * Mathf.Rad2Deg;
        float rotationZ = Mathf.Atan(averageXSlope) * Mathf.Rad2Deg;

        //Quaternion currentRotation = bodyTransform.rotation;
        Quaternion tiltRotation = Quaternion.Euler(-rotationX, 0, rotationZ);
        oldTilt = bodyTransform.rotation;
        newTilt = Quaternion.Euler(0, bodyTransform.rotation.eulerAngles.y, 0) * tiltRotation;
        
        //bodyTransform.rotation = Quaternion.Euler(0, bodyTransform.rotation.eulerAngles.y, 0) * tiltRotation;
        
        //bodyTransform.rotation = Quaternion.Euler(0, bodyTransform.rotation.eulerAngles.y, 0) * tiltRotation;
    }

    private void CheckLegs(object sender, int legID)
    {
        if(groupToCheck == LegGroups.LEG_A) 
        {
            for (int i = 0; i < legGroupA.Length; i++)
            {
                //Debug.Log("LegID: " + legID + " " + legGroupA[i].GetID());
                if (legID == legGroupA[i].GetID())
                {
                    legsMoved++;
                }
            }

            if (legsMoved >= legGroupA.Length)
            {
                legsMoved = 0;
                groupToCheck = LegGroups.LEG_B;
                EnableLegGroup(groupToCheck);
            }
        }
        
        if(groupToCheck == LegGroups.LEG_B)
        {
            for (int i = 0; i < legGroupB.Length; i++)
            {
                if (legID == legGroupB[i].GetID())
                {
                    legsMoved++;
                }
            }

            if (legsMoved >= legGroupB.Length)
            {
                legsMoved = 0;
                groupToCheck = LegGroups.LEG_A;
                EnableLegGroup(groupToCheck);
            }
        }
    }

    private void EnableLegGroup(LegGroups groupToEnable) 
    {
        for (int i = 0; i < legGroupA.Length; i++)
        {
            legGroupA[i].SetCanUpdate(groupToEnable == LegGroups.LEG_A);
        }

        for (int i = 0; i < legGroupB.Length; i++)
        {
            legGroupB[i].SetCanUpdate(groupToEnable == LegGroups.LEG_B);
        }
    }

    public Transform GetBodyTransform()
    {
        return bodyTransform;
    }
}

public enum LegGroups 
{
    LEG_A = 0,
    LEG_B = 1,
}
