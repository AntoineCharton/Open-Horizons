using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CelestialBodies.PhysicsBodies
{
    public class PhysicsBody : MonoBehaviour
    {
        private float forwardSpeed;
        const string xAxis = "Mouse X"; //Strings in direct code generate garbage, storing and re-using them creates no garbage
        const string yAxis = "Mouse Y";
        [SerializeField] private FirstPersonControllerData firstPersonControllerData = FirstPersonControllerData.Default();
        //[SerializeField] private CelestialCharacter celestialCharacter;
        private ControlShip _physicsShip;
        [SerializeField] private GameObject interactionAvailable;
        private bool isControllingOther;
        [FormerlySerializedAs("camera")] [SerializeField] private Transform target;
        [SerializeField] private Vector3 offsetFromParent;
        [SerializeField] private bool jump;
        [SerializeField] private bool wasJumpTriggered;
        private int previousLayer;
        
        internal void SetGravityTarget(GravityTarget newGravityTarget)
        {
            firstPersonControllerData.planet = newGravityTarget;
        }

        internal void SetInteractable(ControlShip ship)
        {
            _physicsShip = ship;
            interactionAvailable.SetActive(true);
        }

        internal void UnsetInteractable(ControlShip ship)
        {
            if (ship == _physicsShip)
            {
                _physicsShip = null;
                interactionAvailable.SetActive(false);
            }
        }
        
        private void Update()
        {
            if (Input.GetKey(KeyCode.W))
            {
                forwardSpeed = 1;
            }
            else
            {
                forwardSpeed = 0;
            }

            if (Input.GetKeyDown(KeyCode.Space) && wasJumpTriggered)
            {
                jump = true;
                wasJumpTriggered = false;
            }
            else
            {
                jump = false;
            }
            
            if (!isControllingOther)
            {
                firstPersonControllerData.Update(transform);
                if (interactionAvailable != null)
                {
                    if (Input.GetKeyDown(KeyCode.F) && _physicsShip != null)
                    {
                        if (_physicsShip.TakeControl(this, target))
                        {
                            previousLayer = gameObject.layer;
                            isControllingOther = true;
                            interactionAvailable.SetActive(false);
                            offsetFromParent = transform.localPosition;
                            firstPersonControllerData.rigidbody.isKinematic = true;
                            gameObject.layer = 6;
                        }
                    }
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    _physicsShip.ReleaseControl(target);
                    interactionAvailable.SetActive(true);
                    isControllingOther = false;
                    gameObject.layer = previousLayer;
                    firstPersonControllerData.rigidbody.isKinematic = false;
                    return;
                }

                if (Input.GetKey(KeyCode.Space))
                {
                    _physicsShip.VerticalThrust(1);
                } else if (Input.GetKey(KeyCode.LeftControl))
                {
                    _physicsShip.VerticalThrust(-1);
                }else
                {
                    _physicsShip.VerticalThrust(0);
                }

                if (Input.GetKey(KeyCode.E))
                {
                    _physicsShip.Roll(-1);
                } else if (Input.GetKey(KeyCode.Q))
                {
                    _physicsShip.Roll(1);
                }
                else
                {
                    _physicsShip.Roll(0);
                }

                if (Input.GetKey(KeyCode.W))
                {
                    _physicsShip.ForwardThust(1);
                }
                else if (Input.GetKey(KeyCode.S))
                {
                    _physicsShip.ForwardThust(-1);
                }else
                {
                    _physicsShip.ForwardThust(0);
                }

                if (Input.GetKey(KeyCode.A))
                {
                    _physicsShip.LateralThrust(1);
                }else if (Input.GetKey(KeyCode.D))
                {
                    _physicsShip.LateralThrust(-1);
                }else
                {
                    _physicsShip.LateralThrust(0);
                }

                if (Input.GetAxis(yAxis) > 0)
                {
                    _physicsShip.Pitch(1);
                }
                else if (Input.GetAxis(yAxis) < 0)
                {
                    _physicsShip.Pitch(-1);
                }else
                {
                    _physicsShip.Pitch(0);
                }
                
                if (Input.GetAxis(xAxis) > 0)
                {
                    _physicsShip.Yaw(1);
                }
                else if (Input.GetAxis(xAxis) < 0)
                {
                    _physicsShip.Yaw(-1);
                }else
                {
                    _physicsShip.Yaw(0);
                }

            }
        }

        private void LateUpdate()
        {
            if (isControllingOther)
            {
                transform.localPosition = offsetFromParent;
            }
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (isControllingOther)
            {
                transform.localPosition = offsetFromParent;
            }

            if (!isControllingOther)
            {
                firstPersonControllerData.FixedUpdate(transform);
                wasJumpTriggered = true;
            }
        }
    }
    
    
    [Serializable]
    internal struct FirstPersonControllerData
    {
        [SerializeField]
        // public vars
        internal float mouseSensitivityX;

        [SerializeField] internal float mouseSensitivityY;
        [SerializeField] internal float walkSpeed;
        [SerializeField] internal float jumpForce;
        [SerializeField] internal LayerMask groundedMask;

        // System vars
        internal bool grounded;
        internal Vector3 moveAmount;
        internal Vector3 smoothMoveVelocity;
        internal float verticalLookRotation;
        [SerializeField] internal Transform cameraTransform;
        [SerializeField] internal Rigidbody rigidbody;
        [SerializeField] internal GravityTarget planet;

        public static FirstPersonControllerData Default()
        {
            var newControllerData = new FirstPersonControllerData();
            newControllerData.mouseSensitivityX = 1;
            newControllerData.mouseSensitivityY = 1;
            newControllerData.walkSpeed = 6;
            newControllerData.jumpForce = 220;
            return newControllerData;
        }
    }

    internal static class PhysicsBodyUpdater
    {
        internal static void FixedUpdate(this ref FirstPersonControllerData firstPersonControllerData,
            Transform transform)
        {
            var height = 1.9f;
            var radius = 0.4f;
            Vector3 point1 = transform.position + transform.TransformDirection(Vector3.zero) + transform.up * (height / 2 - radius);
            Vector3 point2 = transform.position + transform.TransformDirection(Vector3.zero) - transform.up * (height / 2 - radius);
            
            // Perform the overlap check
            Collider[] hitColliders = Physics.OverlapCapsule(point1, point2, radius);
            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.gameObject != transform.gameObject && hitCollider.isTrigger == false) // Don't detect collision with self
                {
                    return;
                }
            }
            firstPersonControllerData.planet.Attract(firstPersonControllerData.rigidbody);
            Vector3 localMove = transform.TransformDirection(firstPersonControllerData.moveAmount) *
                                Time.fixedDeltaTime;
            
            firstPersonControllerData.rigidbody.MovePosition(firstPersonControllerData.rigidbody.position + localMove);
        }

        internal static void Update(this ref FirstPersonControllerData firstPersonControllerData, Transform transform)
        {
            // Look rotation:
            transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * firstPersonControllerData.mouseSensitivityX);
            firstPersonControllerData.verticalLookRotation +=
                Input.GetAxis("Mouse Y") * firstPersonControllerData.mouseSensitivityY;
            firstPersonControllerData.verticalLookRotation =
                Mathf.Clamp(firstPersonControllerData.verticalLookRotation, -60, 60);
            firstPersonControllerData.cameraTransform.localEulerAngles =
                Vector3.left * firstPersonControllerData.verticalLookRotation;

            // Calculate movement:
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");

            Vector3 moveDir = new Vector3(inputX, 0, inputY).normalized;
            Vector3 targetMoveAmount = moveDir * firstPersonControllerData.walkSpeed;
            firstPersonControllerData.moveAmount = Vector3.SmoothDamp(firstPersonControllerData.moveAmount,
                targetMoveAmount, ref firstPersonControllerData.smoothMoveVelocity, .15f);

            // Jump
            if (Input.GetButtonDown("Jump"))
            {
                if (firstPersonControllerData.grounded)
                {
                    firstPersonControllerData.rigidbody.AddForce(transform.up * firstPersonControllerData.jumpForce);
                }
            }

            // Grounded check
            Ray ray = new Ray(transform.position, -transform.up);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1 + .1f, firstPersonControllerData.groundedMask))
            {
                firstPersonControllerData.grounded = true;
            }
            else
            {
                firstPersonControllerData.grounded = false;
            }
        }
    }
}