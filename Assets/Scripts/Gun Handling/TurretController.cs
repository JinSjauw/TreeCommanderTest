using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Transforms")]
    [SerializeField] private Transform turretOrigin;
    [SerializeField] private Transform turretPivot;
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
        Transform baseTarget = isAiming ? barrelTarget : neutralTarget;
        Transform barrelLookTarget = isAiming ? barrelTarget : neutralBarrelTarget;

        targetBaseRot = RotateTurretBase(baseTarget);
        targetBarrelRot = RotateBarrelPivot(barrelLookTarget);
    }

    private Quaternion RotateTurretBase(Transform target)
    {
        Vector3 worldDir = (target.position - turretPivot.position).normalized;
        Vector3 localTarget = turretOrigin.InverseTransformDirection(worldDir);
        float azimuth = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        Quaternion targetRot = Quaternion.Euler(0f, azimuth, 0f);
        turretPivot.localRotation = Quaternion.RotateTowards(turretPivot.localRotation, targetRot, turretTraverseSpeed * Time.deltaTime);

        // Debug
        Debug.DrawLine(turretPivot.position, target.position, Color.green);
        Debug.DrawRay(turretPivot.position, turretPivot.forward * 5, Color.yellow);

        return targetRot;
    }

    private Quaternion RotateBarrelPivot(Transform target)
    {
        Vector3 worldDir = (target.position - barrelPivot.position).normalized;
        Vector3 localTarget = turretPivot.InverseTransformDirection(worldDir);
        float elevation = Mathf.Atan2(-localTarget.y, new Vector2(localTarget.x, localTarget.z).magnitude) * Mathf.Rad2Deg;
        Quaternion targetRot = Quaternion.Euler(elevation, 0f, 0f);
        barrelPivot.localRotation = Quaternion.RotateTowards(barrelPivot.localRotation, targetRot, barrelTraverseSpeed * Time.deltaTime);

        // Debug
        Debug.DrawRay(barrelPivot.position, barrelPivot.forward * 5, Color.blue);

        return targetRot;
    }

    public bool UpdateOnTarget()
    {
        float baseAngle = Quaternion.Angle(turretPivot.localRotation, targetBaseRot);
        float barrelAngle = Mathf.Abs(Mathf.DeltaAngle(barrelPivot.localRotation.eulerAngles.x, targetBarrelRot.eulerAngles.x));

        return baseAngle < angleThreshold && barrelAngle < angleThreshold;
    }

    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
        
        //Forcefully update rotations to update target rotations immediately
        UpdateRotations();
    }

    public Transform GetTurretBase()
    {
        return turretPivot;
    }
}
