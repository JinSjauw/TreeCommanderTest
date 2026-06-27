using UnityEngine;

/// <summary>
/// Drives a Transform's localPosition as basePosition + spring-driven offset on each axis.
/// Designed for a child GameObject (e.g. TankMesh) so the root can follow
/// a NavMeshAgent (setting the base) while the visual body springs around it.
///
/// Effectively integrates 3 SpringOffsets (X, Y, Z) into one component.
///
/// Uses LateUpdate so that external systems can set targets during Update.
///
/// External systems interact through these methods:
///   SetBasePosition(Vector3)             — set the base (NavMeshAgent-driven position)
///   SetTargetOffsetX/Y/Z(float)          — set desired spring offset from base (e.g. LegManager sets Y)
///   SetRotationEquilibrium(Quaternion)   — set the target local rotation
///   AddImpulseX/Y/Z(float)              — velocity kick on offset
///   SnapToEquilibrium()                  — teleport offsets to zero
/// </summary>
public class Spring3D : MonoBehaviour
{
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Vector3 impulsePower = Vector3.one;
    [SerializeField] private float angularFrequency = 10f;
    [SerializeField] private float dampingRatio = 0.5f;

    private FloatSpring _springX, _springY, _springZ;
    private Vector3 _baseLocalPosition;
    private bool _initialized;

    /// <summary>Current spring offset from base (local space). Equilibrium = 0 at rest.</summary>
    public Vector3 CurrentOffset => new Vector3(_springX.position, _springY.position, _springZ.position);

    /// <summary>Current spring offset velocity.</summary>
    public Vector3 CurrentVelocity => new Vector3(_springX.velocity, _springY.velocity, _springZ.velocity);

    /// <summary>Target offset the springs are chasing.</summary>
    public Vector3 TargetOffset => new Vector3(_springX.equilibrium, _springY.equilibrium, _springZ.equilibrium);

    /// <summary>Base local position (set by external system, e.g. NavMeshAgent).</summary>
    public Vector3 BaseLocalPosition => _baseLocalPosition;

    public float AngularFrequency
    {
        get => angularFrequency;
        set => angularFrequency = value;
    }

    public float DampingRatio
    {
        get => dampingRatio;
        set => dampingRatio = value;
    }

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (bodyTransform == null)
            bodyTransform = transform;

        _baseLocalPosition = Vector3.zero;
        _springX = new FloatSpring(0f);
        _springY = new FloatSpring(0f);
        _springZ = new FloatSpring(0f);
        _initialized = true;
    }

    /// <summary>LateUpdate ensures all target-setting (Update) completes before springs apply.</summary>
    private void LateUpdate()
    {
        if (!_initialized || bodyTransform == null)
            return;

        float dt = Time.deltaTime;
        _springX.Update(dt, angularFrequency, dampingRatio);
        _springY.Update(dt, angularFrequency, dampingRatio);
        _springZ.Update(dt, angularFrequency, dampingRatio);

        bodyTransform.localPosition = _baseLocalPosition + CurrentOffset;
    }

    // ── Base position (NavMeshAgent drives this) ──

    /// <summary>Set the base local position. Call from NavMeshAgent movement.</summary>
    public void SetBasePosition(Vector3 localPos)
    {
        _baseLocalPosition = localPos;
    }

    // ── Per-axis offset targets (LegManager sets Y, etc.) ──

    /// <summary>Set the target offset for X axis (0 = no offset from base).</summary>
    public void SetTargetOffsetX(float offset) { _springX.equilibrium = offset; }

    /// <summary>Set the target offset for Y axis (0 = no offset from base). LegManager calls this.</summary>
    public void SetTargetOffsetY(float offset) 
    {
        _springY.equilibrium = offset; 
    }

    /// <summary>Set the target offset for Z axis (0 = no offset from base).</summary>
    public void SetTargetOffsetZ(float offset) { _springZ.equilibrium = offset; }

    // ── Impulses (velocity kicks on offset) ──

    public void AddImpulseDirection(Vector3 direction)
    {
        Vector3 impulse = Vector3.Scale(direction, impulsePower);
        AddImpulse(impulse);
    }

    /// <summary>Add an instantaneous velocity impulse to the offset on all axes.</summary>
    public void AddImpulse(Vector3 delta)
    {
        _springX.AddImpulse(delta.x);
        _springY.AddImpulse(delta.y);
        _springZ.AddImpulse(delta.z);
    }

    public void AddImpulseX(float delta) { _springX.AddImpulse(delta); }
    public void AddImpulseY(float delta) { _springY.AddImpulse(delta); }
    public void AddImpulseZ(float delta) { _springZ.AddImpulse(delta); }

    // ── Snap / Reset ──

    /// <summary>Snap a single axis offset to a value with zero velocity.</summary>
    public void SnapXTo(float offset) { _springX.SnapTo(offset); }
    public void SnapYTo(float offset) { _springY.SnapTo(offset); }
    public void SnapZTo(float offset) { _springZ.SnapTo(offset); }

    /// <summary>Set target offset for X/Y/Z to zero (spring returns smoothly).</summary>
    public void SetTargetOffsetXZero() { _springX.equilibrium = 0f; }
    public void SetTargetOffsetYZero() { _springY.equilibrium = 0f; }
    public void SetTargetOffsetZZero() { _springZ.equilibrium = 0f; }

    /// <summary>Teleport all offsets to zero with zero velocity.</summary>
    public void SnapToEquilibrium()
    {
        _springX.Snap();
        _springY.Snap();
        _springZ.Snap();
    }
}
