using System;
using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Hierarchy")]
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform barrelPivot;

    [Header("Targets")]
    [SerializeField] private Transform trajectoryTarget;
    [SerializeField] private Transform barrelTarget;

    [Header("Neutral Position")]
    [SerializeField] private Transform neutralTarget;
    [SerializeField] private Transform neutralBarrelTarget;

    private bool isAiming = false;

    private void Update()
    {
        if (isAiming)
        {
            RotateTurret(trajectoryTarget, barrelTarget);
        }
        else
        {
            RotateTurret(neutralTarget, neutralBarrelTarget);
        }
    }

    private void RotateTurret(Transform target, Transform barrelTarget)
    {
        turretBase.LookAt(target);
        turretBase.eulerAngles = new Vector3(0f, turretBase.eulerAngles.y, 0f);
        barrelPivot.LookAt(barrelTarget);
    }

    public void SetAiming(bool isAiming)
    {
        this.isAiming = isAiming;
    }
}
