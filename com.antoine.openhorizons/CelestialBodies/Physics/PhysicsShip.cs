using BigWorld;
using UnityEngine;
using UnityEngine.Serialization;

namespace CelestialBodies.PhysicsBodies
{
    public class PhysicsShip : MonoBehaviour
    {
        [SerializeField] private GravityTarget gravityTarget;
        
        [SerializeField]
        private GravityTarget artificialGravityTarget;

        [FormerlySerializedAs("rigidbody")] [SerializeField] 
        private Rigidbody shipRigidbody;

        public Rigidbody ShipRigidbody => shipRigidbody;

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
            shipRigidbody.isKinematic = false;
            canFly = false;
        }
        
        internal void StopEngine()
        {
            shipRigidbody.isKinematic = true;
            canFly = false;
        }

        internal float GetAltitude()
        {
            return Vector3.Distance(transform.position, gravityTarget.transform.position);
        }

        public void SetGravityTarget(GravityTarget gravityTarget)
        {
            this.gravityTarget = gravityTarget;
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
            if(shipRigidbody.isKinematic)
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
            var maxSpeed = 500;
            var acceleration = 7500;
            var decelerationFactor = 0.8f;

            if (GetAltitude() > 50000)
            {
                maxSpeed = 20000;
                acceleration = 200000;
            }
            else if(GetAltitude() > 37000)
            {
                maxSpeed = 5000;
                acceleration = 100000;
            }else if (GetAltitude() > 34000)
            {
                decelerationFactor = 0.9f;
                maxSpeed = 2000;
                acceleration = 10000;
            }
            else if(GetAltitude() > 32000)
            {
                maxSpeed = 1000;
                acceleration = 7500;
            }
            
            if(shipRigidbody.linearVelocity.magnitude > maxSpeed)
            {
                shipRigidbody.linearVelocity *= decelerationFactor;
            }
            
            if(_forward != 0)
                shipRigidbody.AddRelativeForce((Vector3.forward * acceleration * _forward) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_vertical != 0)
                shipRigidbody.AddRelativeForce((Vector3.up * acceleration * 0.5f * _vertical) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_lateral != 0)
                shipRigidbody.AddRelativeForce((Vector3.left * acceleration * _lateral) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_yaw != 0)
                shipRigidbody.AddRelativeTorque((Vector3.up * 100 * _yaw) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_pitch != 0)
                shipRigidbody.AddRelativeTorque((Vector3.left * 100 * _pitch) * Time.deltaTime, ForceMode.Acceleration);
            
            if(_roll != 0)
                shipRigidbody.AddRelativeTorque((Vector3.forward * 100 * _roll) * Time.deltaTime, ForceMode.Acceleration);
            
            if (!shipRigidbody.isKinematic && !canFly)
            {
                shipRigidbody.linearVelocity = Vector3.zero;
                shipRigidbody.angularVelocity = Vector3.zero;
            }
            else if(shipRigidbody.linearVelocity.magnitude > 20)
            {
                shipRigidbody.angularVelocity = Vector3.Lerp(shipRigidbody.angularVelocity, Vector3.zero, Time.deltaTime * 1.5f);
                Vector3 forwardVelocity = transform.forward * shipRigidbody.linearVelocity.magnitude;
                shipRigidbody.linearVelocity = Vector3.Lerp(shipRigidbody.linearVelocity, forwardVelocity, Time.deltaTime);
            } 
            
            
            rescalledTransform.AddOffset(shipRigidbody.linearVelocity * Time.deltaTime);
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
