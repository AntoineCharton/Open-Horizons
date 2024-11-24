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
        /// The line renderer reference.
        /// </summary>
        public LineRenderer lineRendererReference;

        private GameObject _apoapsisLine;
        private GameObject _periapsisLine;
        private GameObject _ascendingLine;
        private GameObject _semiMajorAxisLine;
        private GameObject _orbitNormalLine;
        
        private GameObject _semiMinorAxisLine;
        
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
                DoubleVector3 attractorPosHalf = new DoubleVector3(_moverReference.AttractorSettings.attractorObject.position * _moverReference.Scale);

                _moverReference.OrbitData.GetOrbitPointsNoAlloc(
                    ref _orbitPoints,
                    orbitPointsCount,
                    attractorPosHalf);
                lineRendererReference.positionCount = _orbitPoints.Length;
                for (int i = 0; i < _orbitPoints.Length; i++)
                {
                    var point = _orbitPoints[i];
                    lineRendererReference.SetPosition(i, new Vector3((float)point.X, (float)point.Y, (float)point.Z) / _moverReference.Scale);
                }

                lineRendererReference.loop = _moverReference.OrbitData.eccentricity < 1.0;
            }
            
            if (showPeriapsisApoapsisGizmosInEditor)
            {
                ShowNodes();
            }
            
            if (showAscendingNodeInEditor)
            {
                ShowAscNode();
            }
            
            if (showAxisGizmosInEditor)
            {
                //ShowAxis();
            }
        }

        private void ShowAxis()
        {
            if (gizmosAlphaSecondary <= 0) return;
            var origin = _moverReference.AttractorSettings.attractorObject.position + new Vector3(
                (float)(_moverReference.OrbitData.centerPoint.X / _moverReference.Scale), 
                (float)(_moverReference.OrbitData.centerPoint.Y/ _moverReference.Scale),
                (float)(_moverReference.OrbitData.centerPoint.Z / _moverReference.Scale));
            var semiMajorAxis = new Vector3(
                (float)(_moverReference.OrbitData.semiMajorAxisBasis.X / _moverReference.Scale),
                (float)(_moverReference.OrbitData.semiMajorAxisBasis.Y / _moverReference.Scale),
                (float)(_moverReference.OrbitData.semiMajorAxisBasis.Z / _moverReference.Scale));
            _semiMajorAxisLine = GizmosDrawLine(origin, (origin + semiMajorAxis), new Color(0, 1, 0.5f, gizmosAlphaSecondary), 0, _semiMajorAxisLine);
            _semiMinorAxisLine = GizmosDrawLine(origin ,
                origin + new Vector3((float)(_moverReference.OrbitData.semiMinorAxisBasis.X/ _moverReference.Scale),
                    (float)(_moverReference.OrbitData.semiMinorAxisBasis.Y/ _moverReference.Scale),
                    (float)(_moverReference.OrbitData.semiMinorAxisBasis.Z/ _moverReference.Scale)), new Color(1, 0.8f, 0.2f, gizmosAlphaSecondary), 0, _semiMinorAxisLine);
            _orbitNormalLine = GizmosDrawLine(origin,
                (origin + new Vector3((float)(_moverReference.OrbitData.orbitNormal.X / _moverReference.Scale),
                    (float)(_moverReference.OrbitData.orbitNormal.Y / _moverReference.Scale), 
                    (float)(_moverReference.OrbitData.orbitNormal.Z / _moverReference.Scale))), new Color(0.9f, 0.1f, 0.2f, gizmosAlphaSecondary), 0, _orbitNormalLine);
        }

        private void ShowAscNode()
        {
            if (gizmosAlphaSecondary <= 0) return;
            Vector3 origin = _moverReference.AttractorSettings.attractorObject.position;
            Gizmos.color = new Color(0.29f, 0.42f, 0.64f, gizmosAlphaSecondary);
            DoubleVector3 ascNode;
            if (_moverReference.OrbitData.GetAscendingNode(out ascNode))
            {
                _ascendingLine = GizmosDrawLine(origin, (origin + new Vector3((float)ascNode.X/ _moverReference.Scale, (float)ascNode.Y/ _moverReference.Scale, (float)ascNode.Z/ _moverReference.Scale)), Color.white, 0, _ascendingLine);
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
            Gizmos.DrawLine(pos / _moverReference.Scale, (pos + new Vector3((float)velocity.X, (float)velocity.Y, (float)velocity.Z)) / _moverReference.Scale);
        }

        private void ShowNodes()
        {
            if (gizmosAlphaSecondary <= 0) return;
            if (!_moverReference.OrbitData.IsValidOrbit) return;
            
            var periapsis = new Vector3((float)(_moverReference.OrbitData.periapsis.X / _moverReference.Scale), (float)(_moverReference.OrbitData.periapsis.Y / _moverReference.Scale), (float)(_moverReference.OrbitData.periapsis.Z / _moverReference.Scale));
            var attractorPos = _moverReference.AttractorSettings.attractorObject.position;
            Vector3 point = attractorPos + periapsis;
            _apoapsisLine = GizmosDrawLine(attractorPos, point, new Color(0.9f, 0.4f, 0.2f, gizmosAlphaSecondary) , 0, _apoapsisLine);

            if (_moverReference.OrbitData.eccentricity < 1)
            {
                var apoapsis = new Vector3((float)(_moverReference.OrbitData.apoapsis.X / _moverReference.Scale),
                    (float)(_moverReference.OrbitData.apoapsis.Y / _moverReference.Scale), (float)(_moverReference.OrbitData.apoapsis.Z / _moverReference.Scale));
                point = _moverReference.AttractorSettings.attractorObject.position + apoapsis;
                _periapsisLine = GizmosDrawLine(attractorPos, point, new Color(0.2f, 0.4f, 0.78f, gizmosAlphaSecondary) , 0, _periapsisLine);
            }
        }
        
        public static GameObject GizmosDrawLine(Vector3 start, Vector3 dir, Color color, float duration, GameObject cachedGameObject, float width = 0.05f)
        {
            if (!cachedGameObject)
            {
                cachedGameObject = new GameObject();
                cachedGameObject.transform.position = start;
                cachedGameObject.AddComponent<LineRenderer>();
            }
            LineRenderer lr = cachedGameObject.GetComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.SetPosition(0, start);
            lr.SetPosition(1, dir);
            if (duration > 0)
            {
                GameObject.Destroy(cachedGameObject, duration);
            }
            return cachedGameObject;
        }

        [ContextMenu("AutoFind LineRenderer")]
        private void AutoFindLineRenderer()
        {
            if (lineRendererReference == null)
            {
                lineRendererReference = GetComponent<LineRenderer>();
            }
        }
    }
}