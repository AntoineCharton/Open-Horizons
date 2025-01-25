using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CelestialBodies.PhysicsBodies
{
    [Serializable]
    struct CelestialCharacter
    {
        [SerializeField] private Rigidbody rigidbody;
        public Rigidbody Rigidbody => rigidbody;
        
        [SerializeField] private Transform forward;
        public Transform Forward => forward;

        [SerializeField] private Transform back;
        public Transform Back => back;

        [SerializeField] private Transform left;
        public Transform Left => left;

        [SerializeField] private Transform right;
        public Transform Right => right;
        
        [SerializeField] private LayerMask ground;
        public LayerMask Ground => ground;
        [SerializeField] 
        private GravityTarget gravityTarget;
        public GravityTarget GravityTarget => gravityTarget;
        
        internal float nextJump;
    }
    
    [Serializable]
    struct CelestialInputs
    {
        [FormerlySerializedAs("targetXRotation")] [SerializeField] private Transform targetYRotation;
        internal Transform TargetYRotation => targetYRotation;
        
        [FormerlySerializedAs("targetYRotation2")] [FormerlySerializedAs("targetYRotation")] [SerializeField] private Transform targetXRotation;
        internal Transform TargetXRotation => targetXRotation;
        
        [Range(0.1f, 9f)] [SerializeField] private float sensitivity;
        internal float Sensitivity => sensitivity;
        
        private float yAxisRotation;

        internal float YAxisRotation
        {
            get => yAxisRotation;
            set => yAxisRotation = value;
        }
        private float forwardSpeed;

        public static CelestialInputs Default()
        {
            var defaultCelestialInputs = new CelestialInputs();
            defaultCelestialInputs.sensitivity = 2f;
            return defaultCelestialInputs;
        }
    }

    static class CelestialPhysics
    {
        internal static void UpdateRotation(this ref CelestialInputs celestialInputs, Transform gravityTarget, float xAxis, float yAxis)
        {
            celestialInputs.TargetXRotation.Rotate(new Vector3(-yAxis * celestialInputs.Sensitivity, 0, 0), Space.Self);
            celestialInputs.YAxisRotation += xAxis;
            celestialInputs.TargetYRotation.LookAt(gravityTarget);
            celestialInputs.TargetYRotation.Rotate(new Vector3(-90, 0, 0), Space.Self);
            celestialInputs.TargetYRotation.Rotate(new Vector3(0, celestialInputs.YAxisRotation, 0));
        }

        internal static void UpdateGravity(this CelestialCharacter celestialCharacter, float forwardSpeed, bool jump)
        {
            var rb = celestialCharacter.Rigidbody;
            var transform = celestialCharacter.Rigidbody.transform;
            Vector3 diff = transform.position - celestialCharacter.GravityTarget.transform.position;
            var numberOfContacts = 0;
            var isForwardGrounded = Physics.CheckSphere(celestialCharacter.Forward.position, 0.2f, celestialCharacter.Ground);
            var isBackGrounded = Physics.CheckSphere(celestialCharacter.Back.position, 0.2f, celestialCharacter.Ground);
            var isLeftGrounded = Physics.CheckSphere(celestialCharacter.Left.position, 0.2f, celestialCharacter.Ground);
            var isRightGrounded = Physics.CheckSphere(celestialCharacter.Right.position, 0.2f, celestialCharacter.Ground);

            if (isForwardGrounded)
                numberOfContacts++;
            if (isBackGrounded)
                numberOfContacts++;
            if (isLeftGrounded)
                numberOfContacts++;
            if (isRightGrounded)
                numberOfContacts++;

            if (numberOfContacts > 1)
            {
                if (rb.linearVelocity.magnitude > 10 && forwardSpeed > 0 && Time.time > celestialCharacter.nextJump)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * 10, Time.deltaTime * 10);
                }
            }

            if (Time.time > celestialCharacter.nextJump && isBackGrounded && (isLeftGrounded || isRightGrounded))
            {
                if (forwardSpeed == 0 && numberOfContacts == 4)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 10);
                }
                
                if (jump)
                {
                    rb.AddRelativeForce(new Vector3(0, 1.5f, 0.5f) * 5, ForceMode.Impulse);
                    celestialCharacter.nextJump = Time.time + 0.5f;
                }
                else
                {
                    rb.AddRelativeForce(new Vector3(0, -1000f, 5000) * forwardSpeed * Time.deltaTime, ForceMode.Force);
                }
            }

            rb.AddForce(-diff.normalized * (celestialCharacter.GravityTarget.gravity * (rb.mass)));
        }
    }
}
