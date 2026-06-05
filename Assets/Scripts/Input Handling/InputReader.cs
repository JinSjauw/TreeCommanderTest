using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Input/InputReader")]
public class InputReader : ScriptableObject, DefaultInput.IGameplayActions, DefaultInput.IUIActions
{
    private DefaultInput defaultInput;
    private bool isInitialized = false;

    public string CurrentControlScheme { get; private set; } = "KeyboardMouse";
    public event Action<string> OnControlSchemeChanged;

    // LIFECYCLE

    public void Initialize()
    {
        if (isInitialized) return;

        defaultInput = new DefaultInput();

        // Set up callbacks for all action maps
        defaultInput.Gameplay.SetCallbacks(this);
        defaultInput.UI.SetCallbacks(this);

        // Enable gameplay by default
        EnableGameplay();

        // Listen for device changes (keyboard vs controller)
        InputSystem.onActionChange += OnActionChange;

        isInitialized = true;
    }

    public void Shutdown()
    {
        if (!isInitialized) return;

        InputSystem.onActionChange -= OnActionChange;

        defaultInput?.Gameplay.Disable();
        defaultInput?.UI.Disable();
        defaultInput = null;

        isInitialized = false;
    }

    // CONTEXT SWITCHING

    public void EnableGameplay()
    {
        defaultInput?.UI.Disable();
        defaultInput?.Gameplay.Enable();

        // PC: Lock and hide cursor
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    public void EnableUI()
    {
        defaultInput?.Gameplay.Disable();
        defaultInput?.UI.Enable();

        // PC: Free cursor for menu navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void EnableAll()
    {
        defaultInput?.Gameplay.Enable();
        defaultInput?.UI.Enable();
    }

    public void DisableAll()
    {
        defaultInput?.Gameplay.Disable();
        defaultInput?.UI.Disable();
    }

    // DEVICE DETECTION

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;

        var action = obj as InputAction;
        if (action?.activeControl?.device == null) return;

        string newScheme = GetControlSchemeForDevice(action.activeControl.device);

        if (newScheme != CurrentControlScheme)
        {
            CurrentControlScheme = newScheme;
            OnControlSchemeChanged?.Invoke(newScheme);
            Debug.Log($"Switched to {newScheme}");
        }
    }

    private string GetControlSchemeForDevice(InputDevice device)
    {
        return device switch
        {
            Keyboard or Mouse => "KeyboardMouse",
            Gamepad => "Gamepad",
            _ => "Unknown"
        };
    }

    //States

    public Vector2 Look {  get; private set; }
    public Vector2 Move { get; private set; }
    public bool OrbitCamera {  get; private set; }

    #region Events

    //Camera Events

    public event Action<Vector2> OnZoomCameraEvent;

    //Gameplay Events

    //Sonar
    public event Action OnAdjustSonarRingEvent;
    public event Action<bool> OnDragSonarRingEvent;
    
    //public event Action OnLeftShiftEvent;
    private bool leftShiftPressed;

    public event Action OnLockEvent;
    public event Action OnRadarScanEvent;

    //Vehicle
    public event Action OnFireEvent;
    public event Action OnFireInterceptEvent;
    public event Action<Vector2> OnAdjustTrajectoryEvent;
    public event Action OnPlaceDestinationEvent;
    public event Action OnPlaceTargetEvent;
    public event Action<bool> OnDragTargetEvent;
    public event Action OnStopMovingEvent;

    //UI Events
    public event Action OnOpenMenuEvent;
    public event Action OnCloseMenuEvent;

    #endregion

    #region Modifier Control Callbacks

    public void OnLeftShift(InputAction.CallbackContext context)
    {
        leftShiftPressed = context.ReadValueAsButton();
    }

    #endregion

    #region Camera Control Callbacks

    //============= Camera Control ==============//

    public void OnMoveCamera(InputAction.CallbackContext context)
    {
        Move = context.ReadValue<Vector2>();
    }

    public void OnOrbitCamera(InputAction.CallbackContext context)
    {
        OrbitCamera = context.ReadValueAsButton();
    }

    public void OnZoomCamera(InputAction.CallbackContext context)
    {
        if (context.performed && !leftShiftPressed) 
        {
            OnZoomCameraEvent?.Invoke(context.ReadValue<Vector2>());
        }
    }

    public void OnLook(InputAction.CallbackContext context) 
    {
        Look = context.ReadValue<Vector2>();
    }

    #endregion

    #region Sonar Control Callbacks

    //=============== Sonar Control ================//

    public void OnAdjustSonarRing(InputAction.CallbackContext context)
    {
        if (context.performed && leftShiftPressed) 
        {
            OnAdjustSonarRingEvent?.Invoke();
        }
    }

    public void OnDragSonarRing(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            OnDragSonarRingEvent?.Invoke(true);
        }
        else if (context.canceled) 
        {
            OnDragSonarRingEvent?.Invoke(false);
        }
    }

    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            OnLockEvent?.Invoke();
        }
    }

    public void OnRadarScan(InputAction.CallbackContext context)
    {
        OnRadarScanEvent?.Invoke();
    }

    //================ Vehicle Control =================//

    #endregion

    #region Vehicle Control Callbacks

    public void OnFire(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            OnFireEvent?.Invoke();
        }
    }

    public void OnFireIntercept(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnFireInterceptEvent?.Invoke();
        }
    }

    public void OnAdjustTrajectory(InputAction.CallbackContext context)
    {
        if(context.performed && leftShiftPressed) 
        {
            OnAdjustTrajectoryEvent?.Invoke(context.ReadValue<Vector2>());
        }
        else if(context.canceled) 
        {
            OnAdjustTrajectoryEvent?.Invoke(Vector2.zero);
        }
    }

    public void OnPlaceDestination(InputAction.CallbackContext context)
    {
        if(context.performed && !leftShiftPressed) 
        {
            OnPlaceDestinationEvent?.Invoke();
        }
    }

    public void OnPlaceTarget(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnPlaceTargetEvent?.Invoke();
        }
    }

    public void OnDragTarget(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnDragTargetEvent?.Invoke(true);
        }
        else if (context.canceled) 
        {
            OnDragTargetEvent?.Invoke(false);
        }
    }

    public void OnStopMoving(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnStopMovingEvent?.Invoke();
        }
    }

    #endregion

    #region UI Callbacks

    //================ UI ==================//

    bool menuOpened = false;
    //In gameplay map
    public void OnOpenMenu(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            OnOpenMenuEvent?.Invoke();
            menuOpened = true;
        }
        else if( context.canceled)
        {
            menuOpened = false;
        }
    }

    public void OnCloseMenu(InputAction.CallbackContext context)
    {
        if (context.performed && !menuOpened)
        {
            OnCloseMenuEvent?.Invoke();
        }
    }

    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
    public void OnCancel(InputAction.CallbackContext context) { }
    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }

    #endregion
}
