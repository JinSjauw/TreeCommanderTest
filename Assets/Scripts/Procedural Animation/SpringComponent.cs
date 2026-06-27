using UnityEngine;

/// <summary>
/// Wraps a single FloatSpring as a MonoBehaviour component. Outputs a single float value
/// that smoothly follows a target. Drop anywhere and read .Value or hook up via script.
///
/// Uses LateUpdate so that external systems can set targets during Update.
///
/// Usage:
///   spring.SetTarget(1f);           // set equilibrium target each frame
///   float current = spring.Value;   // read the smoothed output
///   spring.AddImpulse(0.5f);        // velocity kick
///   spring.SnapTo(0f);              // instant reset
/// </summary>
public class SpringComponent : MonoBehaviour
{
    [SerializeField] private float angularFrequency = 10f;
    [SerializeField] private float dampingRatio = 0.5f;
    [SerializeField] private bool logValues;

    private FloatSpring _spring;
    private bool _initialized;
    private int _logFrameSkip;

    /// <summary>Current spring position (smoothed output).</summary>
    public float Value => _spring.position;

    /// <summary>Current spring velocity.</summary>
    public float Velocity => _spring.velocity;

    /// <summary>Target equilibrium the spring is chasing.</summary>
    public float Target => _spring.equilibrium;

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
        _spring = new FloatSpring(0f);
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        _spring.Update(Time.deltaTime, angularFrequency, dampingRatio);

        if (logValues && ++_logFrameSkip % 30 == 0)
        {
            Debug.Log($"[SpringComponent:{name}] Value={_spring.position:F4}  "
                + $"Vel={_spring.velocity:F4}  Target={_spring.equilibrium:F4}  "
                + $"ω={angularFrequency} ζ={dampingRatio}");
        }
    }

    /// <summary>Set the equilibrium (target) value. Call each frame to keep the spring chasing.</summary>
    public void SetTarget(float target)
    {
        _spring.equilibrium = target;
    }

    /// <summary>Add an instantaneous velocity impulse.</summary>
    public void AddImpulse(float velocityDelta)
    {
        _spring.AddImpulse(velocityDelta);
    }

    /// <summary>Instantly snap value to equilibrium with zero velocity.</summary>
    public void Snap()
    {
        _spring.Snap();
    }

    /// <summary>Instantly snap to a given target and set it as equilibrium.</summary>
    public void SnapTo(float target)
    {
        _spring.SnapTo(target);
    }
}
