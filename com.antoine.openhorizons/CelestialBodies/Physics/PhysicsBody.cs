using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CelestialBodies.PhysicsBodies
{
    public class PhysicsBody : MonoBehaviour
    {
        [SerializeField] private Transform gravityTarget;
        private float forwardSpeed;
        const string xAxis = "Mouse X"; //Strings in direct code generate garbage, storing and re-using them creates no garbage
        const string yAxis = "Mouse Y";

        [SerializeField] private CelestialCharacter celestialCharacter;
        [SerializeField] private CelestialInputs celestialInputs = CelestialInputs.Default();
        private ControlShip _physicsShip;
        [SerializeField] private GameObject interactionAvailable;
        private bool isControllingOther;
        [FormerlySerializedAs("camera")] [SerializeField] private Transform target;
        [SerializeField] private Vector3 offsetFromParent;
        [SerializeField] private bool jump;
        [SerializeField] private bool wasJumpTriggered;
        private int previousLayer;
        
        internal void SetGravityTarget(Transform newGravityTarget)
        {
            gravityTarget = newGravityTarget;
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
                celestialInputs.UpdateRotation(gravityTarget, Input.GetAxis(xAxis), Input.GetAxis(yAxis));
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
                celestialInputs.UpdateRotation(gravityTarget, 0,  0);
                transform.localPosition = offsetFromParent;
            }
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (isControllingOther)
            {
                celestialInputs.UpdateRotation(gravityTarget, 0,  0);
                celestialCharacter.Rigidbody.linearVelocity = Vector3.zero;
                celestialCharacter.Rigidbody.angularVelocity = Vector3.zero;
                transform.localPosition = offsetFromParent;
            }

            if (!isControllingOther)
            {
                celestialCharacter.UpdateGravity(forwardSpeed, jump);
                wasJumpTriggered = true;
            }
        }
    }
}