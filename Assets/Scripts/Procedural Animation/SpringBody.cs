using UnityEngine;

/// <summary>
/// Drives a Transform's world position using a damped spring.
/// Drop onto a GameObject, assign the body transform, tune ω and ζ in the Inspector.
///
/// External systems interact through three methods:
///   SetEquilibrium(Vector3) — set the target position (e.g. LegManager each frame)
///   AddImpulse(Vector3)     — add velocity kick (e.g. recoil on fire)
///   SnapToEquilibrium()     — teleport with zero velocity (e.g. on spawn/teleport)
/// </summary>
public class SpringBody : MonoBehaviour
{
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private float angularFrequency = 10f;
    [SerializeField] private float dampingRatio = 0.5f;

    private SpringState _positionState;
    private bool _initialized;

    public Vector3 CurrentPosition => _positionState.position;
    public Vector3 CurrentVelocity => _positionState.velocity;
    public Vector3 Equilibrium => _positionState.equilibrium;

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

        _positionState = new SpringState(bodyTransform.position);
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || bodyTransform == null)
            return;

        _positionState.Update(Time.deltaTime, angularFrequency, dampingRatio);
        bodyTransform.position = _positionState.position;
    }

    /// <summary>Set the equilibrium (target) position. Call each frame to keep the spring chasing.</summary>
    public void SetEquilibrium(Vector3 target)
    {
        _positionState.equilibrium = target;
    }

    /// <summary>Add an instantaneous velocity impulse (e.g. recoil kick on fire).</summary>
    public void AddImpulse(Vector3 velocityDelta)
    {
        _positionState.AddImpulse(velocityDelta);
    }

    /// <summary>Teleport position to equilibrium with zero velocity.</summary>
    public void SnapToEquilibrium()
    {
        _positionState.Snap();
        if (bodyTransform != null)
            bodyTransform.position = _positionState.position;
    }

    /// <summary>Teleport to a specific position and set it as the new equilibrium.</summary>
    public void SnapTo(Vector3 target)
    {
        _positionState.SnapTo(target);
        if (bodyTransform != null)
            bodyTransform.position = _positionState.position;
    }
}
