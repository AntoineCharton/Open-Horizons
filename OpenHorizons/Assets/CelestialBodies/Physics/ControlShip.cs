using System;
using CelestialBodies.PhysicsBodies;
using UnityEngine;

public class ControlShip : MonoBehaviour
{
    [SerializeField] private Transform cameraPosition;
    [SerializeField] private Transform cameraAnchor;
    [SerializeField] private PhysicsShip ship;
    private bool isControlling;
    [SerializeField] private GameObject speedUI;
    [SerializeField] private GameObject altitudeUI;
    private Transform previousTransformParent;
    
    internal bool TakeControl(PhysicsBody controller, Transform camera)
    {
        if(isControlling == true)
            Debug.LogWarning("The ship was controlled");
        ship.StartEngine();
        speedUI.SetActive(true);
        altitudeUI.SetActive(true);
        previousTransformParent = camera.transform.parent;
        camera.SetParent(cameraAnchor);
        camera.transform.position = cameraPosition.position;
        camera.transform.localRotation = cameraPosition.localRotation;
        isControlling = true;
        return true;
    }

    internal bool ReleaseControl(Transform camera)
    {
        if(isControlling == false)
            Debug.LogWarning("The ship wasn't controlled");
        ship.StopEngine();
        speedUI.SetActive(true);
        altitudeUI.SetActive(true);
        camera.SetParent(previousTransformParent);
        camera.transform.localPosition = Vector3.zero;
        camera.transform.localRotation = Quaternion.identity;
        isControlling = false;
        return true;
    }

    internal void Roll(float value)
    {
        ship.Roll(value);
    }

    internal void VerticalThrust(float value)
    {
        ship.VerticalThrust(value);
    }

    internal void ForwardThust(float value)
    {
        ship.ForwardThrust(value);
    }
    
    internal void LateralThrust(float value)
    {
        ship.LateralThrust(value);
    }

    internal void Pitch(float value)
    {
        ship.Pitch(value);
    }
    
    internal void Yaw(float value)
    {
        ship.Yaw(value);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if(isControlling)
            return;
        var body = other.GetComponent<PhysicsBody>();
        if (body != null)
        {
            body.SetInteractable(this);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if(isControlling)
            return;
        var body = other.GetComponent<PhysicsBody>();
        if (body != null)
        {
            body.UnsetInteractable(this);
        }
    }
}
