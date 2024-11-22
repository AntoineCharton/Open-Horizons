using BigWorld.Doubles;
using UnityEngine;

namespace BigWorld.Kepler
{
    /// <summary>
    /// Component for displaying current orbit curve in editor and in game.
    /// </summary>
    /// <seealso cref="UnityEngine.MonoBehaviour" />
    [RequireComponent(typeof(KeplerOrbitMover))]
    [ExecuteAlways]
    public class KeplerOrbitLineDisplay : MonoBehaviour
    {
        /// <summary>
        /// The orbit curve precision.
        /// </summary>
        public int orbitPointsCount = 50;

        /// <summary>
        /// The maximum orbit distance of orbit display in world units.
        /// </summary>
        public float maxOrbitWorldUnitsDistance = 1000f;

        /// <summary>
        /// The line renderer reference.
        /// </summary>
        public LineRenderer lineRendererReference;

#if UNITY_EDITOR
        [Header("Gizmo display options:")]
        public bool showOrbitGizmoInEditor = true;
        public bool showOrbitGizmoWhileInPlayMode = true;
        public bool showVelocityGizmoInEditor = true;
        public bool showPeriapsisApoapsisGizmosInEditor = true;
        public bool showAscendingNodeInEditor = true;
        public bool showAxisGizmosInEditor = false;
        [Range(0f, 1f)]
        public float gizmosAlphaMain = 1f;
        [Range(0f, 1f)]
        public float gizmosAlphaSecondary = 0.3f;
#endif

        private KeplerOrbitMover _moverReference;
        private DoubleVector3[] _orbitPoints;

        private void OnEnable()
        {
            if (_moverReference == null)
            {
                _moverReference = GetComponent<KeplerOrbitMover>();
            }
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }
#endif
            if (lineRendererReference != null && _moverReference.AttractorSettings.attractorObject != null)
            {
                var attractorPosHalf = _moverReference.AttractorSettings.attractorObject.position;

                _moverReference.OrbitData.GetOrbitPointsNoAlloc(
                    ref _orbitPoints,
                    orbitPointsCount,
                    new DoubleVector3(attractorPosHalf.x, attractorPosHalf.y, attractorPosHalf.z),
                    maxOrbitWorldUnitsDistance);
                lineRendererReference.positionCount = _orbitPoints.Length;
                for (int i = 0; i < _orbitPoints.Length; i++)
                {
                    var point = _orbitPoints[i];
                    lineRendererReference.SetPosition(i, new Vector3((float)point.X, (float)point.Y, (float)point.Z));
                }

                lineRendererReference.loop = _moverReference.OrbitData.eccentricity < 1.0;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (showOrbitGizmoInEditor && _moverReference != null)
            {
                if (!Application.isPlaying || showOrbitGizmoWhileInPlayMode)
                {
                    if (_moverReference.AttractorSettings != null &&
                        _moverReference.AttractorSettings.attractorObject != null)
                    {
                        if (showVelocityGizmoInEditor)
                        {
                            ShowVelocity();
                        }

                        ShowOrbit();
                        if (showPeriapsisApoapsisGizmosInEditor)
                        {
                            ShowNodes();
                        }

                        if (showAxisGizmosInEditor)
                        {
                            ShowAxis();
                        }

                        if (showAscendingNodeInEditor)
                        {
                            ShowAscNode();
                        }
                    }
                }
            }
        }

        private void ShowAxis()
        {
            if (gizmosAlphaSecondary <= 0) return;
            var origin = _moverReference.AttractorSettings.attractorObject.position + new Vector3(
                (float)_moverReference.OrbitData.centerPoint.X, (float)_moverReference.OrbitData.centerPoint.Y,
                (float)_moverReference.OrbitData.centerPoint.Z);
            Gizmos.color = new Color(0, 1, 0.5f, gizmosAlphaSecondary);
            var semiMajorAxis = new Vector3(
                (float)_moverReference.OrbitData.semiMajorAxisBasis.X,
                (float)_moverReference.OrbitData.semiMajorAxisBasis.Y,
                (float)_moverReference.OrbitData.semiMajorAxisBasis.Z);
            Gizmos.DrawLine(origin, origin + semiMajorAxis);
            Gizmos.color = new Color(1, 0.8f, 0.2f, gizmosAlphaSecondary);
            Gizmos.DrawLine(origin,
                origin + new Vector3((float)_moverReference.OrbitData.semiMinorAxisBasis.X,
                    (float)_moverReference.OrbitData.semiMinorAxisBasis.Y,
                    (float)_moverReference.OrbitData.semiMinorAxisBasis.Z));
            Gizmos.color = new Color(0.9f, 0.1f, 0.2f, gizmosAlphaSecondary);
            Gizmos.DrawLine(origin,
                origin + new Vector3((float)_moverReference.OrbitData.orbitNormal.X,
                    (float)_moverReference.OrbitData.orbitNormal.Y, (float)_moverReference.OrbitData.orbitNormal.Z));
        }

        private void ShowAscNode()
        {
            if (gizmosAlphaSecondary <= 0) return;
            Vector3 origin = _moverReference.AttractorSettings.attractorObject.position;
            Gizmos.color = new Color(0.29f, 0.42f, 0.64f, gizmosAlphaSecondary);
            DoubleVector3 ascNode;
            if (_moverReference.OrbitData.GetAscendingNode(out ascNode))
            {
                Gizmos.DrawLine(origin, origin + new Vector3((float)ascNode.X, (float)ascNode.Y, (float)ascNode.Z));
            }
        }

        private void ShowVelocity()
        {
            if (gizmosAlphaSecondary <= 0) return;
            Gizmos.color = new Color(1, 1, 1, gizmosAlphaSecondary);
            var velocity =
                _moverReference.OrbitData.GetVelocityAtEccentricAnomaly(_moverReference.OrbitData.eccentricAnomaly);
            if (_moverReference.VelocityHandleLengthScale > 0)
            {
                velocity *= _moverReference.VelocityHandleLengthScale;
            }

            var pos = transform.position;
            Gizmos.DrawLine(pos, pos + new Vector3((float)velocity.X, (float)velocity.Y, (float)velocity.Z));
        }

        private void ShowOrbit()
        {
            var attractorPosHalf = _moverReference.AttractorSettings.attractorObject.position;
            var attractorPos = new DoubleVector3(attractorPosHalf.x, attractorPosHalf.y, attractorPosHalf.z);
            _moverReference.OrbitData.GetOrbitPointsNoAlloc(ref _orbitPoints, orbitPointsCount, attractorPos,
                maxOrbitWorldUnitsDistance);
            Gizmos.color = new Color(1, 1, 1, gizmosAlphaMain);
            for (int i = 0; i < _orbitPoints.Length - 1; i++)
            {
                var p1 = _orbitPoints[i];
                var p2 = _orbitPoints[i + 1];
                Gizmos.DrawLine(new Vector3((float)p1.X, (float)p1.Y, (float)p1.Z),
                    new Vector3((float)p2.X, (float)p2.Y, (float)p2.Z));
            }
        }

        private void ShowNodes()
        {
            if (gizmosAlphaSecondary <= 0) return;
            if (!_moverReference.OrbitData.IsValidOrbit) return;

            Gizmos.color = new Color(0.9f, 0.4f, 0.2f, gizmosAlphaSecondary);
            var periapsis = new Vector3((float)_moverReference.OrbitData.periapsis.X,
                (float)_moverReference.OrbitData.periapsis.Y, (float)_moverReference.OrbitData.periapsis.Z);
            var attractorPos = _moverReference.AttractorSettings.attractorObject.position;
            Vector3 point = attractorPos + periapsis;
            Gizmos.DrawLine(attractorPos, point);

            if (_moverReference.OrbitData.eccentricity < 1)
            {
                Gizmos.color = new Color(0.2f, 0.4f, 0.78f, gizmosAlphaSecondary);
                var apoapsis = new Vector3((float)_moverReference.OrbitData.apoapsis.X,
                    (float)_moverReference.OrbitData.apoapsis.Y, (float)_moverReference.OrbitData.apoapsis.Z);
                point = _moverReference.AttractorSettings.attractorObject.position + apoapsis;
                Gizmos.DrawLine(attractorPos, point);
            }
        }

        [ContextMenu("AutoFind LineRenderer")]
        private void AutoFindLineRenderer()
        {
            if (lineRendererReference == null)
            {
                lineRendererReference = GetComponent<LineRenderer>();
            }
        }
#endif
    }
}