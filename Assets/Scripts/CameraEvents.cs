using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CameraEvents", menuName = "Scriptable Objects/CameraEvents")]
public class CameraEvents : ScriptableObject
{
    public EventHandler<float> cameraZoomChangedEvent;

    public void OnCameraZoomChanged(float zoom) 
    {
        cameraZoomChangedEvent?.Invoke(this, zoom);
    }
}
