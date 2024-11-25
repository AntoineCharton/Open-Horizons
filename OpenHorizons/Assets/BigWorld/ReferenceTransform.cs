using System;
using BigWorld.Doubles;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigWorld
{
    [ExecuteInEditMode]
    public class ReferenceTransform : MonoBehaviour
    {
        public RescalledPlanet relativePosition;
        public DoubleVector3 referencePosition;
        public DoubleVector3 localPosition;
        public DoubleVector3 simulationPosition;
        public Transform test;

        public DoubleVector3 UniversePosition
        {
            get => new DoubleVector3(referencePosition.X + simulationPosition.X, referencePosition.Y + simulationPosition.Y,
                referencePosition.Z + simulationPosition.Z);
        }

        private void Update()
        {
            simulationPosition = new DoubleVector3(transform.position);
            var parentMatrix = DoubleMatrix4X4.TRS(relativePosition.position, relativePosition.transform.rotation.eulerAngles, DoubleVector3.one);
            var localPos = parentMatrix.MultiplyPoint3X4(localPosition);
            referencePosition = localPos;
            if (Vector3.Distance(transform.position, Vector3.zero) > 1000)
            {
                var position = relativePosition.position;
                localPosition.X += simulationPosition.X;
                localPosition.Y += simulationPosition.Y;
                localPosition.Z += simulationPosition.Z;
                transform.position = Vector3.zero;
                simulationPosition = new DoubleVector3(Vector3.zero);
            }
        }
    }
}