using UnityEngine;

/// <summary>
/// Reads a SpringComponent's float Value and applies it as a localPosition offset
/// on a chosen axis. Also relays impulses to the SpringComponent.
///
/// Typical use: body bob on Y, lateral sway on X, or any spring-driven offset.
///
/// Usage:
///   offset.ApplyImpulse(1f);  // kick the spring
///   // The component automatically reads SpringComponent.Value each frame
///   // and applies it as: localPosition[axis] = initialOffset + Value * multiplier
/// </summary>
public class SpringOffset : MonoBehaviour
{
    [SerializeField] private SpringComponent springSource;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Axis axis = Axis.Y;
    [SerializeField] private float multiplier = 1f;
    [SerializeField] private float initialOffset;
    [SerializeField] private float impulseValue = 1f;
    [SerializeField] private bool logValues;

    private Vector3 initialLocalPosition;
    private bool initialized;
    private int logFrameSkip;

    public enum Axis { X, Y, Z }

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
        if (targetTransform == null)
            targetTransform = transform;

        initialLocalPosition = targetTransform.localPosition;
        initialized = true;
    }

    /// <summary>LateUpdate runs after SpringComponent.LateUpdate, so Value is current.</summary>
    private void LateUpdate()
    {
        if (!initialized || springSource == null || targetTransform == null)
            return;

        float springValue = springSource.Value;
        float offset = initialOffset + springValue * multiplier;
        Vector3 pos = initialLocalPosition;

        switch (axis)
        {
            case Axis.X: pos.x += offset; break;
            case Axis.Y: pos.y += offset; break;
            case Axis.Z: pos.z += offset; break;
        }

        targetTransform.localPosition = pos;

        if (logValues && ++logFrameSkip % 30 == 0)
        {
            Debug.Log($"[SpringOffset:{name}] springSrc={springSource.name}  "
                + $"springValue={springValue:F4}  calcOffset={offset:F4}  "
                + $"localPos={targetTransform.localPosition}  axis={axis}");
        }
    }

    public void ApplyImpulse()
    {
        ApplyImpulse(impulseValue);
    }

    /// <summary>Apply an impulse to the source spring.</summary>
    public void ApplyImpulse(float velocityDelta)
    {
        if (springSource != null)
            springSource.AddImpulse(velocityDelta);
    }

    /// <summary>Snap the source spring to a value (zero velocity).</summary>
    public void SnapToTarget(float target)
    {
        if (springSource != null)
            springSource.SnapTo(target);
    }

    /// <summary>Set the source spring's equilibrium target.</summary>
    public void SetTarget(float target)
    {
        if (springSource != null)
            springSource.SetTarget(target);
    }
}
