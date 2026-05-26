using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Transforms")]
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform barrelPivot;

    [Header("Targets")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform barrelTarget;

    [Header("Neutral Position")]
    [SerializeField] private Transform neutralTarget;
    [SerializeField] private Transform neutralBarrelTarget;

    [Header("Turning")]
    [SerializeField] private float turretTraverseSpeed = 90f;
    [SerializeField] private float barrelTraverseSpeed = 90f;
    [SerializeField] private float angleThreshold = 0.5f;

    private bool isAiming;
    private Quaternion targetBaseRot;
    private Quaternion targetBarrelRot;

    void Awake()
    {
        aimTarget.position = neutralTarget.position;
        barrelTarget.position = neutralBarrelTarget.position;
    }

    void Update()
    {
        UpdateRotations();
    }

    private void UpdateRotations()
    {
        Transform baseTarget = isAiming ? aimTarget : neutralTarget;
        Transform barrelLookTarget = isAiming ? barrelTarget : neutralBarrelTarget;

        targetBaseRot = RotateTurretBase(baseTarget);
        targetBarrelRot = RotateBarrelPivot(barrelLookTarget);
    }

    private Quaternion RotateTurretBase(Transform target)
    {
        Quaternion targetRot = Quaternion.LookRotation(target.position - turretBase.position);
        targetRot = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);
        turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetRot, turretTraverseSpeed * Time.deltaTime);
        return targetRot;
    }

    private Quaternion RotateBarrelPivot(Transform target)
    {
        Quaternion targetRot = Quaternion.LookRotation(target.position - barrelPivot.position);
        targetRot = Quaternion.Euler(targetRot.eulerAngles.x, 0f, 0f);
        barrelPivot.localRotation = Quaternion.RotateTowards(barrelPivot.localRotation, targetRot, barrelTraverseSpeed * Time.deltaTime);
        return targetRot;
    }

    public bool UpdateOnTarget()
    {
        float baseAngle = Quaternion.Angle(turretBase.rotation, targetBaseRot);
        float barrelAngle = Mathf.Abs(Mathf.DeltaAngle(barrelPivot.localRotation.eulerAngles.x, targetBarrelRot.eulerAngles.x));

        return baseAngle < angleThreshold && barrelAngle < angleThreshold;
    }

    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
        
        //Forcefully update rotations to update target rotations immediately
        UpdateRotations();
    }
}
