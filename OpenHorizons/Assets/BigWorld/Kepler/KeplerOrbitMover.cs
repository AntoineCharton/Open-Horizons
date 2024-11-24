using System;
using System.Collections;
using BigWorld.Doubles;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigWorld.Kepler
{
    /// <summary>
    /// Component for moving game object in eliptic or hyperbolic path around attractor body.
    /// </summary>
    /// <seealso cref="UnityEngine.MonoBehaviour" />
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class KeplerOrbitMover : MonoBehaviour
    {
        /// <summary>
        /// The attractor settings data.
        /// Attractor object reference must be assigned or orbit mover will not work.
        /// </summary>
        [SerializeField] AttractorData attractorSettings = new AttractorData(1000, 0.1f);

        public AttractorData AttractorSettings => attractorSettings;

        [FormerlySerializedAs("Scale")] [SerializeField]
        float scale = 100000000;

        public float Scale => scale;

        /// <summary>
        /// The velocity handle object.
        /// Assign object and use it as velocity control handle in scene view.
        /// </summary>
        [SerializeField]
        [Tooltip("The velocity handle object. Assign object and use it as velocity control handle in scene view.")]
        Transform velocityHandle;

        /// <summary>
        /// The velocity handle length scale parameter.
        /// </summary>
        [SerializeField] [Range(0f, 10f)] [Tooltip("Velocity handle scale parameter.")]
        double velocityHandleLengthScale;

        public double VelocityHandleLengthScale => velocityHandleLengthScale;

        /// <summary>
        /// The time scale multiplier.
        /// </summary>
        [SerializeField] [Tooltip("The time scale multiplier.")]
        double timeScale = 1f;

        public double TimeScale => timeScale;

        /// <summary>
        /// The orbit data.
        /// Internal state of orbit.
        /// </summary>
        [SerializeField] [Header("Orbit state details:")] [Tooltip("Internal state of orbit.")]
        KeplerOrbitData orbitData = new KeplerOrbitData();

        public KeplerOrbitData OrbitData
        {
            get => orbitData;
            set => orbitData = value;
        }

        /// <summary>
        /// Disable continious editing orbit in update loop, if you don't need it.
        /// It is also very useful in cases, when orbit is not stable due to float precision limits.
        /// </summary>
        /// <remarks>
        /// Internal orbit data uses double prevision vectors, but every update it is compared with unity scene vectors, which are float precision.
        /// In result, if unity vectors precision is not enough for current values, then orbit become unstable.
        /// To avoid this issue, you can disable comparison, and then orbit motion will be nice and stable, but you will no longer be able to change orbit by moving objects in editor.
        /// </remarks>
        [SerializeField]
        [Tooltip(
            "Disable continious editing orbit in update loop, if you don't need it, or you need to fix Kraken issue on large scale orbits.")]
        bool lockOrbitEditing;

        public bool LockOrbitEditing
        {
            get => lockOrbitEditing;
            set => lockOrbitEditing = value;
        }

        private Coroutine _updateRoutine;

        private bool IsReferencesAsigned
        {
            get { return attractorSettings.attractorObject != null; }
        }
        
        public void SetOrbitSettings(Transform attractorTransform, double attractorMass, double gravityConstant)
        {
            attractorSettings.attractorObject = attractorTransform;
            attractorSettings.attractorMass = attractorMass;
            attractorSettings.gravityConstant = gravityConstant;
        }

        public void SetOrbitData(KeplerOrbitData newOrbitData)
        {
            orbitData = newOrbitData;
        }

        private void OnEnable()
        {
            if (!lockOrbitEditing)
            {
                ForceUpdateOrbitData();
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }
#endif
            if (_updateRoutine != null)
            {
                StopCoroutine(_updateRoutine);
            }

            _updateRoutine = StartCoroutine(OrbitUpdateLoop());
        }

        private void OnDisable()
        {
            if (_updateRoutine != null)
            {
                StopCoroutine(_updateRoutine);
                _updateRoutine = null;
            }
        }

        public void SetGravityConstant(double gravityConstant)
        {
            orbitData.gravConst = gravityConstant;
        }

        /// <summary>
        /// Updates orbit internal data.
        /// </summary>
        /// <remarks>
        /// In this method orbit data is updating from view state:
        /// If you change body position, attractor mass or any other vital orbit parameter, 
        /// this change will be noticed and applyed to internal OrbitData state in this method.
        /// If you need to change orbitData state directly, by script, you need to change OrbitData state and then call ForceUpdateOrbitData
        /// </remarks>
        private void Update()
        {
            if (IsReferencesAsigned)
            {
                if (!lockOrbitEditing)
                {
                    var rescaledPosition = new DoubleVector3(transform.position) * scale;
                    var rescaledAttractorPosition =
                        new DoubleVector3(attractorSettings.attractorObject.position) * scale;

                    var position = (rescaledPosition * scale) - (rescaledAttractorPosition * scale);

                    bool velocityHandleChanged = false;
                    if (velocityHandle != null)
                    {
                        Vector3 velocity = GetVelocityHandleDisplayedVelocity();
                        if (velocity != new Vector3((float)orbitData.velocity.X, (float)orbitData.velocity.Y,
                                (float)orbitData.velocity.Z))
                        {
                            velocityHandleChanged = true;
                        }
                    }

                    if (position != orbitData.position ||
                        velocityHandleChanged ||
                        Math.Abs(orbitData.gravConst - attractorSettings.gravityConstant) > 0.00001f ||
                        Math.Abs(orbitData.attractorMass - attractorSettings.attractorMass) > 0.00001f)
                    {
                        ForceUpdateOrbitData();
                    }
                }
            }
            else
            {
                if (Application.isPlaying)
                {
                    Debug.LogError("KeplerMover: Attractor reference not asigned", context: gameObject);
                }
                else
                {
                    Debug.Log("KeplerMover: Attractor reference not asigned", context: gameObject);
                }
            }
        }


        /// <summary>
        /// Progress orbit path motion.
        /// Actual kepler orbiting is processed here.
        /// </summary>
        /// <remarks>
        /// Orbit motion progress calculations must be placed after Update, so orbit parameters changes can be applyed,
        /// but before LateUpdate, so orbit can be displayed in same frame.
        /// Coroutine loop is best candidate for achieving this.
        /// </remarks>
        private IEnumerator OrbitUpdateLoop()
        {
            while (true)
            {
                if (IsReferencesAsigned)
                {
                    if (!orbitData.IsValidOrbit)
                    {
                        //try to fix orbit if we can.
                        orbitData.CalculateOrbitStateFromOrbitalVectors();
                    }

                    if (orbitData.IsValidOrbit)
                    {
                        orbitData.UpdateOrbitDataByTime(Time.deltaTime * timeScale);
                        ForceUpdateViewFromInternalState();
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Updates OrbitData from new body position and velocity vectors.
        /// </summary>
        /// <param name="relativePosition">The relative position.</param>
        /// <param name="velocity">The relative velocity.</param>
        /// <remarks>
        /// This method can be useful to assign new position of body by script.
        /// Or you can directly change OrbitData state and then manually update view.
        /// </remarks>
        public void CreateNewOrbitFromPositionAndVelocity(Vector3 relativePosition, Vector3 velocity)
        {
            if (IsReferencesAsigned)
            {
                orbitData.position = new DoubleVector3(relativePosition.x, relativePosition.y, relativePosition.z);
                orbitData.velocity = new DoubleVector3(velocity.x, velocity.y, velocity.z);
                orbitData.CalculateOrbitStateFromOrbitalVectors();
                ForceUpdateViewFromInternalState();
            }
        }

        /// <summary>
        /// Forces the update of body position, and velocity handler from OrbitData.
        /// Call this method after any direct changing of OrbitData.
        /// </summary>
        [ContextMenu("Update transform from orbit state")]
        public void ForceUpdateViewFromInternalState()
        {
            var pos = new Vector3((float)(orbitData.position.X / scale), (float)(orbitData.position.Y / scale),
                (float)(orbitData.position.Z / scale));
            transform.position = attractorSettings.attractorObject.position + pos;
            ForceUpdateVelocityHandleFromInternalState();
        }

        /// <summary>
        /// Forces the refresh of position of velocity handle object from actual orbit state.
        /// </summary>
        public void ForceUpdateVelocityHandleFromInternalState()
        {
            if (velocityHandle != null)
            {
                Vector3 velocityRelativePosition = new Vector3((float)orbitData.velocity.X, (float)orbitData.velocity.Y,
                    (float)orbitData.velocity.Z);
                if (velocityHandleLengthScale > 0 && !double.IsNaN(velocityHandleLengthScale) &&
                    !double.IsInfinity(velocityHandleLengthScale))
                {
                    velocityRelativePosition *= (float)velocityHandleLengthScale;
                }

                velocityHandle.position = transform.position + velocityRelativePosition;
            }
        }

        /// <summary>
        /// Gets the displayed velocity vector from Velocity Handle object position if Handle reference is not null.
        /// NOTE: Displayed velocity may not be equal to actual orbit velocity.
        /// </summary>
        /// <returns>Displayed velocity vector if Handle is not null, otherwise zero vector.</returns>
        public Vector3 GetVelocityHandleDisplayedVelocity()
        {
            if (velocityHandle != null)
            {
                Vector3 velocity = velocityHandle.position - transform.position;
                if (velocityHandleLengthScale > 0 && !double.IsNaN(velocityHandleLengthScale) &&
                    !double.IsInfinity(velocityHandleLengthScale))
                {
                    velocity /= (float)velocityHandleLengthScale;
                }

                return velocity;
            }

            return new Vector3();
        }

        /// <summary>
        /// Forces the update of internal orbit data from current world positions of body, attractor settings and velocityHandle.
        /// </summary>
        /// <remarks>
        /// This method must be called after any manual changing of body position, velocity handler position or attractor settings.
        /// It will update internal OrbitData state from view state.
        /// </remarks>
        [ContextMenu("Update Orbit data from current vectors")]
        public void ForceUpdateOrbitData()
        {
            if (IsReferencesAsigned)
            {
                orbitData.attractorMass = attractorSettings.attractorMass;
                orbitData.gravConst = attractorSettings.gravityConstant;
                var position = new DoubleVector3(transform.position * scale);
                var attractorPosition = new DoubleVector3(attractorSettings.attractorObject.position * scale);
                // Possible loss of precision, may be a problem in some situations.
                var pos = position - attractorPosition;
                orbitData.position = new DoubleVector3(pos.X, pos.Y, pos.Z);
                if (velocityHandle != null)
                {
                    Vector3 velocity = GetVelocityHandleDisplayedVelocity();
                    orbitData.velocity = new DoubleVector3(velocity.x, velocity.y, velocity.z);
                }

                orbitData.CalculateOrbitStateFromOrbitalVectors();
            }
        }

        /// <summary>
        /// Change orbit velocity vector to match circular orbit.
        /// </summary>
        [ContextMenu("Circularize orbit")]
        public void SetAutoCircleOrbit()
        {
            if (IsReferencesAsigned)
            {
                orbitData.velocity = KeplerOrbitUtils.CalcCircleOrbitVelocity(DoubleVector3.zero, orbitData.position,
                    orbitData.attractorMass, orbitData.orbitNormal, orbitData.gravConst);
                orbitData.CalculateOrbitStateFromOrbitalVectors();
                ForceUpdateVelocityHandleFromInternalState();
            }
        }

        [ContextMenu("Inverse velocity")]
        public void InverseVelocity()
        {
            if (IsReferencesAsigned)
            {
                orbitData.velocity = -orbitData.velocity;
                orbitData.CalculateOrbitStateFromOrbitalVectors();
                ForceUpdateVelocityHandleFromInternalState();
            }
        }

        [ContextMenu("Inverse position")]
        public void InversePositionRelativeToAttractor()
        {
            if (IsReferencesAsigned)
            {
                orbitData.position = -orbitData.position;
                orbitData.CalculateOrbitStateFromOrbitalVectors();
                ForceUpdateVelocityHandleFromInternalState();
            }
        }

        [ContextMenu("Inverse velocity and position")]
        public void InverseOrbit()
        {
            if (IsReferencesAsigned)
            {
                orbitData.velocity = -orbitData.velocity;
                orbitData.position = -orbitData.position;
                orbitData.CalculateOrbitStateFromOrbitalVectors();
                ForceUpdateVelocityHandleFromInternalState();
            }
        }

        [ContextMenu("Reset orbit")]
        public void ResetOrbit()
        {
            orbitData = new KeplerOrbitData();
        }
    }
}