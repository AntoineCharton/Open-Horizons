using System;
using BigWorld;
using UnityEngine;
using UnityEngine.Serialization;

namespace CelestialBodies.PhysicsBodies
{
    public class PhysicsShip : MonoBehaviour
    {
        [SerializeField] private Transform gravityTarget;
        
        [SerializeField]
        private Transform artificialGravityTarget;

        [SerializeField] 
        private Rigidbody rigidbody;

        private bool canFly;

       [SerializeField] private RescalledTransform rescalledTransform;

       private float _roll;
       private float _pitch;
       private float _yaw;
       private float _vertical;
       private float _forward;
       private float _lateral;

        internal void StartEngine()
        {
            rigidbody.isKinematic = false;
            canFly = false;
        }
        
        internal void StopEngine()
        {
            rigidbody.isKinematic = true;
            canFly = false;
        }

        internal void Roll(float value)
        {
            _roll = value;
        }

        internal void Pitch(float value)
        {
            _pitch = value;
        }
        
        internal void Yaw(float value)
        {
            _yaw = value;
            
        }

        internal void VerticalThrust(float value)
        {
            if(rigidbody.isKinematic)
                return;
            _vertical = value;
            canFly = true;
            
        }

        internal void ForwardThrust(float value)
        {
            _forward = value;
        }

        internal void LateralThrust(float value)
        {
            _lateral = value;
        }

        private void FixedUpdate()
        {
            if(!canFly)
                return;
            if(_forward != 0)
                rigidbody.AddRelativeForce((Vector3.forward * 10000 * _forward) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_vertical != 0)
                rigidbody.AddRelativeForce((Vector3.up * 1000 * _vertical) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_lateral != 0)
                rigidbody.AddRelativeForce((Vector3.left * 1000 * _lateral) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_yaw != 0)
                rigidbody.AddRelativeTorque((Vector3.up * 100 * _yaw) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_pitch != 0)
                rigidbody.AddRelativeTorque((Vector3.left * 100 * _pitch) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_roll != 0)
                rigidbody.AddRelativeTorque((Vector3.forward * 100 * _roll) * Time.deltaTime, ForceMode.Acceleration);
            
            if (!rigidbody.isKinematic && !canFly)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            else if(rigidbody.linearVelocity.magnitude > 20)
            {
                rigidbody.angularVelocity = Vector3.Lerp(rigidbody.angularVelocity, Vector3.zero, Time.deltaTime * 1.5f);
                Vector3 forwardVelocity = transform.forward * rigidbody.linearVelocity.magnitude;
                rigidbody.linearVelocity = Vector3.Lerp(rigidbody.linearVelocity, forwardVelocity, Time.deltaTime);
            } 
            
            
            rescalledTransform.AddOffset(rigidbody.linearVelocity * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            var physicsBody = other.GetComponent<PhysicsBody>();
            if (physicsBody != null)
            {
                other.transform.parent = this.transform;
                physicsBody.SetGravityTarget(artificialGravityTarget);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var physicsBody = other.GetComponent<PhysicsBody>();
            if (physicsBody != null && !canFly)
            {
                other.transform.parent = null;
                physicsBody.SetGravityTarget(gravityTarget);
            }
        }
    }
}
