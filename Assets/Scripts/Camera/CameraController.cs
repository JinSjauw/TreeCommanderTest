using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [SerializeField] private AnimationCurve moveSpeedZoomCurve;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float deceleration;

    Vector3 currentVelocity = Vector3.zero;

    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomSmoothing;

    private float currentZoomSpeed;

    public float ZoomLevel 
    {
        get 
        {
            InputAxis axis = orbitalFollow.RadialAxis;

            return Mathf.InverseLerp(axis.Range.x, axis.Range.y, axis.Value);
        }
    }


    [SerializeField] private float orbitSensitivity;
    [SerializeField] private float orbitSmoothing;

    #region Input Handling

    private Vector2 moveInput;
    private Vector2 scrollInput;
    private Vector2 lookInput;
    private bool orbitCamera = false;
    private bool leftShiftDown = false;

    private void OnCameraZoom(Vector2 value)
    {
        scrollInput = value;
    }

    private void OnScrollWheel(InputValue value) 
    {
        scrollInput = value.Get<Vector2>();
        if (leftShiftDown) { scrollInput = Vector2.zero; }
    }

    private void OnLeftShift(InputValue value) 
    {
        leftShiftDown = value.isPressed;
    }

    #endregion

    #region Unity Functions

    private void Start()
    {
        Services.Input.OnZoomCameraEvent += OnCameraZoom;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        moveInput = Services.Input.Move;
        lookInput = Services.Input.Look;
        orbitCamera = Services.Input.OrbitCamera;

        UpdateMovement(deltaTime);
        UpdateOrbit(deltaTime);
        UpdateZoom(deltaTime);
    }

    #endregion

    #region Functions

    private void UpdateMovement(float deltaTime)
    {
        Vector3 forward = Camera.main.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Camera.main.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y) * (moveSpeed * moveSpeedZoomCurve.Evaluate(ZoomLevel));
        
        if(moveInput.sqrMagnitude > 0.01f) 
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * deltaTime);
        }
        else 
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * deltaTime);
        }


        Vector3 motion = currentVelocity * deltaTime;
        cameraTarget.position += forward * motion.z + right * motion.x;
    }

    private void UpdateOrbit(float deltaTime) 
    {
        Vector2 orbitInput = lookInput * (orbitCamera ? 1f : 0f);

        orbitInput *= orbitSensitivity;

        InputAxis horizontalAxis = orbitalFollow.HorizontalAxis;
        InputAxis verticalAxis = orbitalFollow.VerticalAxis;

        horizontalAxis.Value += orbitInput.x;
        verticalAxis.Value -= orbitInput.y;

        horizontalAxis.Value = Mathf.Lerp(horizontalAxis.Value, horizontalAxis.Value + orbitInput.x, orbitSmoothing * deltaTime);
        verticalAxis.Value = Mathf.Lerp(verticalAxis.Value, verticalAxis.Value - orbitInput.y, orbitSmoothing * deltaTime);

        verticalAxis.Value = Mathf.Clamp(verticalAxis.Value, verticalAxis.Range.x, verticalAxis.Range.y);

        horizontalAxis.Value = Mathf.Repeat(horizontalAxis.Value, 360f);

        orbitalFollow.HorizontalAxis = horizontalAxis;
        orbitalFollow.VerticalAxis = verticalAxis;
    }

    private void UpdateZoom(float deltaTime) 
    {
        InputAxis axis = orbitalFollow.RadialAxis;

        float targetZoomSpeed = 0f;

        if(Mathf.Abs(scrollInput.y) >= 0.01f) 
        {
            targetZoomSpeed = zoomSpeed * scrollInput.y;
        }

        currentZoomSpeed = Mathf.Lerp(currentZoomSpeed, targetZoomSpeed, zoomSmoothing * deltaTime);

        axis.Value -= currentZoomSpeed;
        axis.Value = Mathf.Clamp(axis.Value, axis.Range.x, axis.Range.y);

        orbitalFollow.RadialAxis = axis;

        float zoomAlpha = (axis.Value - axis.Range.x) / (axis.Range.y - axis.Range.x);
    }

    #endregion
}
