using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Hierarchy")]
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform barrelPivot;

    [Header("Targets")]
    [SerializeField] private Transform trajectoryTarget;
    [SerializeField] private Transform barrelTarget;

    private void Update()
    {
        RotateTurret();
    }

    private void RotateTurret()
    {
        turretBase.LookAt(trajectoryTarget);
        turretBase.eulerAngles = new Vector3(0f, turretBase.eulerAngles.y, 0f);
        barrelPivot.LookAt(barrelTarget);
    }
}
